using System.Diagnostics;
using System.Text;

namespace ApkAssistant;

public record SignerVerifyResult(
    bool IsVerified,
    bool V1Signed,
    bool V2Signed,
    bool V3Signed,
    int SignerCount,
    IReadOnlyList<string> CertificateDNs,
    string RawOutput)
{
    public static SignerVerifyResult NotVerified(string rawOutput) =>
        new(false, false, false, false, 0, Array.Empty<string>(), rawOutput ?? "");
}

public class Signer
{
    public string ZipalignPath  { get; set; }
    public string ApksignerPath { get; set; }
    public string KeytoolPath   { get; set; }
    public string JavaPath      { get; set; }

    private readonly ApkManager _runner;

    public const string DefaultDebugAlias    = "androiddebugkey";
    public const string DefaultDebugPass     = "android";
    public const string DefaultDebugDname    = "CN=Android Debug,O=Android,C=US";
    public const int    DefaultDebugValidity = 10000;

    public Signer(
        string? zipalignPath = null,
        string? apksignerPath = null,
        string? keytoolPath = null,
        string? javaPath = null)
    {
        ZipalignPath  = zipalignPath  ?? "";
        ApksignerPath = apksignerPath ?? "";
        KeytoolPath   = string.IsNullOrWhiteSpace(keytoolPath) ? "keytool" : keytoolPath;
        JavaPath      = string.IsNullOrWhiteSpace(javaPath)    ? "java"    : javaPath;
        _runner = new ApkManager(javaPath: JavaPath);
    }

    public static string GetDefaultDebugKeystorePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".android", "debug.keystore");
    }

    // ========================= Zipalign =========================
    public async Task<int> ZipalignAsync(
        string inputApk, string outputApk, IProgress<string>? onOutput, CancellationToken ct)
    {
        ValidateTool(ZipalignPath, "zipalign");
        ValidateFile(inputApk, "inputApk");
        if (string.IsNullOrWhiteSpace(outputApk))
            throw new ArgumentException("outputApk boş olamaz.", nameof(outputApk));

        return await _runner.RunProcessAsync(BuildZipalignPsi(inputApk, outputApk), onOutput, ct);
    }

    internal ProcessStartInfo BuildZipalignPsi(string inputApk, string outputApk) =>
        _runner.BuildProcessStartInfo(ZipalignPath, new[] { "-f", "-p", "4", inputApk, outputApk });

    // ========================= Sign =========================
    public async Task<int> SignAsync(
        string apkPath, string keystorePath, string alias,
        string storePass, string keyPass, IProgress<string>? onOutput, CancellationToken ct)
    {
        ValidateTool(ApksignerPath, "apksigner");
        ValidateFile(apkPath, "apkPath");
        ValidateFile(keystorePath, "keystorePath");
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Keystore alias boş olamaz.", nameof(alias));

        return await _runner.RunProcessAsync(
            BuildSignPsi(apkPath, keystorePath, alias, storePass, keyPass), onOutput, ct);
    }

    internal ProcessStartInfo BuildSignPsi(
        string apkPath, string keystorePath, string alias, string storePass, string keyPass)
    {
        var args = new List<string>
        {
            "sign",
            "--ks", keystorePath,
            "--ks-key-alias", alias,
            "--ks-pass", FormatPasswordArg(storePass),
        };
        if (!string.IsNullOrEmpty(keyPass))
        {
            args.Add("--key-pass");
            args.Add(FormatPasswordArg(keyPass));
        }
        args.Add(apkPath);

        return _runner.BuildProcessStartInfo(ApksignerPath, args);
    }

    private static string FormatPasswordArg(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "pass:";
        var t = raw.TrimStart();
        if (t.StartsWith("pass:", StringComparison.Ordinal) ||
            t.StartsWith("env:",  StringComparison.Ordinal) ||
            t.StartsWith("file:", StringComparison.Ordinal) ||
            t == "stdin")
            return t;
        return "pass:" + raw;
    }

    // ========================= Verify =========================
    public async Task<SignerVerifyResult> VerifyAsync(
        string apkPath, IProgress<string>? onOutput, CancellationToken ct)
    {
        ValidateTool(ApksignerPath, "apksigner");
        ValidateFile(apkPath, "apkPath");

        var collector = new StringCollector();
        var chained = onOutput is null
            ? (IProgress<string>)collector
            : new ChainedProgress(onOutput, collector);

        int exit;
        try
        {
            exit = await _runner.RunProcessAsync(BuildVerifyPsi(apkPath), chained, ct);
        }
        catch (ToolExecutionException ex)
        {
            var rawFail = collector.ToString();
            if (string.IsNullOrWhiteSpace(rawFail)) throw;
            return ParseVerifyOutput(rawFail, ex.ExitCode);
        }

        return ParseVerifyOutput(collector.ToString(), exit);
    }

    internal ProcessStartInfo BuildVerifyPsi(string apkPath) =>
        _runner.BuildProcessStartInfo(ApksignerPath, new[] { "verify", "--verbose", "--print-certs", apkPath });

    internal static SignerVerifyResult ParseVerifyOutput(string rawOutput, int exitCode)
    {
        var dns = new List<string>();
        bool v1 = false, v2 = false, v3 = false;
        int signerCount = 0;

        if (!string.IsNullOrEmpty(rawOutput))
        {
            foreach (var lineRaw in rawOutput.Split('\n'))
            {
                var line = lineRaw.Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("Verified using v1 scheme", StringComparison.OrdinalIgnoreCase))
                    v1 = line.EndsWith("true", StringComparison.OrdinalIgnoreCase);
                else if (line.StartsWith("Verified using v2 scheme", StringComparison.OrdinalIgnoreCase))
                    v2 = line.EndsWith("true", StringComparison.OrdinalIgnoreCase);
                else if (line.StartsWith("Verified using v3 scheme", StringComparison.OrdinalIgnoreCase))
                    v3 = line.EndsWith("true", StringComparison.OrdinalIgnoreCase);
                else if (line.StartsWith("Number of signers:", StringComparison.OrdinalIgnoreCase))
                {
                    var part = line["Number of signers:".Length..].Trim();
                    if (int.TryParse(part, out var n)) signerCount = n;
                }
                else if (line.Contains("certificate DN:", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = line.IndexOf("certificate DN:", StringComparison.OrdinalIgnoreCase);
                    var dn  = line[(idx + "certificate DN:".Length)..].Trim();
                    if (dn.Length > 0) dns.Add(dn);
                }
            }
        }

        bool verified = exitCode == 0 && (v1 || v2 || v3 || signerCount > 0 || dns.Count > 0);
        return new SignerVerifyResult(verified, v1, v2, v3, signerCount, dns, rawOutput ?? "");
    }

    // ========================= Debug keystore =========================
    public async Task<string> CreateDebugKeystoreAsync(
        string keystorePath,
        string alias = DefaultDebugAlias,
        string storePass = DefaultDebugPass,
        string keyPass = DefaultDebugPass,
        string dname = DefaultDebugDname,
        int validityDays = DefaultDebugValidity,
        CancellationToken ct = default)
    {
        ValidateTool(KeytoolPath, "keytool");
        if (string.IsNullOrWhiteSpace(keystorePath))
            throw new ArgumentException("keystorePath boş olamaz.", nameof(keystorePath));

        var dir = Path.GetDirectoryName(keystorePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (File.Exists(keystorePath))
            throw new InvalidOperationException("Keystore zaten var, üzerine yazılmadı: " + keystorePath);

        var psi = BuildKeytoolPsi(keystorePath, alias, storePass, keyPass, dname, validityDays);
        var collector = new StringCollector();
        await _runner.RunProcessAsync(psi, collector, ct);

        if (!File.Exists(keystorePath) || new FileInfo(keystorePath).Length == 0)
            throw new InvalidOperationException("keytool çalıştı ama keystore oluşmadı: " + keystorePath);

        return keystorePath;
    }

    internal ProcessStartInfo BuildKeytoolPsi(
        string keystorePath, string alias, string storePass, string keyPass,
        string dname, int validityDays) =>
        _runner.BuildProcessStartInfo(KeytoolPath, new[]
        {
            "-genkeypair", "-v",
            "-keystore", keystorePath,
            "-alias", alias,
            "-keyalg", "RSA",
            "-keysize", "2048",
            "-validity", validityDays.ToString(),
            "-storepass", storePass,
            "-keypass",   keyPass,
            "-dname",     dname
        });

    // ========================= Yardımcı =========================
    private static void ValidateTool(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"{name} yolu ayarlanmamış.");
        if (Path.IsPathRooted(path) && !File.Exists(path))
            throw new FileNotFoundException($"{name} bulunamadı: {path}", path);
    }

    private static void ValidateFile(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException($"{name} için dosya bulunamadı: {path}", path);
    }

    private sealed class StringCollector : IProgress<string>
    {
        private readonly StringBuilder _sb = new();
        private readonly object _lock = new();
        public void Report(string value) { lock (_lock) _sb.AppendLine(value); }
        public override string ToString() { lock (_lock) return _sb.ToString(); }
    }

    private sealed class ChainedProgress : IProgress<string>
    {
        private readonly IProgress<string> _a, _b;
        public ChainedProgress(IProgress<string> a, IProgress<string> b) { _a = a; _b = b; }
        public void Report(string v) { _a.Report(v); _b.Report(v); }
    }
}
