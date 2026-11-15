using System.Diagnostics;
using System.Text;

namespace ApkAssistant;

public class ToolExecutionException : Exception
{
    public int ExitCode { get; }
    public string ToolPath { get; }

    public ToolExecutionException(string message, int exitCode, string toolPath, Exception? inner = null)
        : base(message, inner)
    {
        ExitCode = exitCode;
        ToolPath = toolPath;
    }
}

public class ApkManager
{
    public string ApktoolPath   { get; set; }
    public string JadxPath      { get; set; }
    public string ApksignerPath { get; set; }
    public string JavaPath      { get; set; }

    public ApkManager(
        string? apktoolPath = null,
        string? jadxPath = null,
        string? apksignerPath = null,
        string? javaPath = null)
    {
        ApktoolPath   = apktoolPath   ?? "";
        JadxPath      = jadxPath      ?? "";
        ApksignerPath = apksignerPath ?? "";
        JavaPath      = string.IsNullOrWhiteSpace(javaPath) ? "java" : javaPath;
    }

    // ================= Public API =================

    public Task<int> DecompileApkToolAsync(
        string apkPath, string outputDir, IProgress<string>? onOutput, CancellationToken ct)
    {
        ValidateFile(apkPath, "apkPath");
        ValidateTool(ApktoolPath, "apktool");
        var args = new[] { "d", "-f", "-o", outputDir, apkPath };
        return RunToolAsync(ApktoolPath, args, onOutput, ct);
    }

    public Task<int> DecompileJadxAsync(
        string apkPath, string outputDir, IProgress<string>? onOutput, CancellationToken ct)
    {
        ValidateFile(apkPath, "apkPath");
        ValidateTool(JadxPath, "jadx");
        var args = new[] { "-d", outputDir, apkPath };
        return RunToolAsync(JadxPath, args, onOutput, ct);
    }

    public Task<int> RecompileApkAsync(
        string sourceDir, string outputApkPath, IProgress<string>? onOutput, CancellationToken ct)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Kaynak klasör yok: {sourceDir}");
        ValidateTool(ApktoolPath, "apktool");
        var args = new[] { "b", sourceDir, "-o", outputApkPath };
        return RunToolAsync(ApktoolPath, args, onOutput, ct);
    }

    // ================= Internals (testable) =================

    private Task<int> RunToolAsync(
        string toolPath, IReadOnlyList<string> args, IProgress<string>? onOutput, CancellationToken ct)
    {
        var psi = BuildProcessStartInfo(toolPath, args);
        return RunProcessAsync(psi, onOutput, ct);
    }

    internal ProcessStartInfo BuildProcessStartInfo(string toolPath, IReadOnlyList<string> args)
    {
        var ext = Path.GetExtension(toolPath).ToLowerInvariant();

        var psi = new ProcessStartInfo
        {
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            RedirectStandardInput  = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };

        var toolDir = Path.GetDirectoryName(toolPath);
        if (!string.IsNullOrEmpty(toolDir) && Directory.Exists(toolDir))
            psi.WorkingDirectory = toolDir;

        if (ext == ".jar")
        {
            psi.FileName = string.IsNullOrWhiteSpace(JavaPath) ? "java" : JavaPath;
            psi.ArgumentList.Add("-jar");
            psi.ArgumentList.Add(toolPath);
            foreach (var a in args) psi.ArgumentList.Add(a);
        }
        else if (ext == ".bat" || ext == ".cmd")
        {
            // cmd.exe kendi parse kurallarını kullanır; tek bir komut satırı veriyoruz
            psi.FileName  = "cmd.exe";
            psi.Arguments = "/c \"" + BuildCmdCommandLine(toolPath, args) + "\"";
        }
        else
        {
            psi.FileName = toolPath;
            foreach (var a in args) psi.ArgumentList.Add(a);
        }

        return psi;
    }

    private static string BuildCmdCommandLine(string programPath, IReadOnlyList<string> args)
    {
        static string Q(string s)
        {
            if (s.Length == 0) return "\"\"";
            bool needsQuote = s.IndexOfAny(new[] { ' ', '\t', '&', '|', '^', '(', ')', '!', '<', '>', '"' }) >= 0;
            return needsQuote ? "\"" + s.Replace("\"", "\\\"") + "\"" : s;
        }

        var sb = new StringBuilder();
        sb.Append('"').Append(programPath).Append('"');
        foreach (var a in args) sb.Append(' ').Append(Q(a));
        return sb.ToString();
    }

    internal async Task<int> RunProcessAsync(
        ProcessStartInfo psi, IProgress<string>? onOutput, CancellationToken ct)
    {
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        if (onOutput != null)
        {
            process.OutputDataReceived += (_, e) => { if (e.Data != null) onOutput.Report(e.Data); };
            process.ErrorDataReceived  += (_, e) => { if (e.Data != null) onOutput.Report("[stderr] " + e.Data); };
        }

        if (!process.Start())
            throw new InvalidOperationException("Süreç başlatılamadı: " + psi.FileName);

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (psi.RedirectStandardInput)
        {
            try { process.StandardInput.Close(); } catch { /* ignore */ }
        }

        using var reg = ct.Register(() => KillSafe(process));

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            // DataReceived işleyicilerinin boşalması için
            try { process.WaitForExit(); } catch { /* ignore */ }
        }

        ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
            throw new ToolExecutionException(
                $"{Path.GetFileName(psi.FileName)} sıfır dışı kodla çıktı: {process.ExitCode}",
                process.ExitCode,
                psi.FileName);

        return process.ExitCode;
    }

    private static void KillSafe(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch { /* süreç zaten kapanmış olabilir */ }
    }

    private static void ValidateFile(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException($"{name} için dosya bulunamadı: {path}", path);
    }

    private static void ValidateTool(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"{name} yolu ayarlanmamış.");
        if (!File.Exists(path))
            throw new FileNotFoundException($"{name} bulunamadı: {path}", path);
    }
}
