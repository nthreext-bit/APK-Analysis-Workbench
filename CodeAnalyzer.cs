using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ApkAssistant;

public record SearchResult(
    string FilePath,
    int LineNumber,
    int ColumnNumber,
    string Match,
    string LineContent,
    string Pattern);

public class CodeAnalyzer
{
    /// <summary>Varsayılan olarak taranacak kaynak/metin uzantıları.</summary>
    public static readonly HashSet<string> DefaultExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".smali", ".java", ".kt", ".xml", ".txt", ".json", ".properties",
        ".gradle", ".pro", ".yml", ".yaml", ".conf", ".cfg", ".ini"
    };

    /// <summary>Uzantı beyaz listesi boşaltılsa bile asla açılmayacak binary uzantılar.</summary>
    private static readonly HashSet<string> KnownBinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico",
        ".zip", ".apk", ".jar", ".so", ".dll", ".exe", ".bin",
        ".dex", ".odex", ".vdex", ".arsc", ".mp3", ".mp4", ".webm",
        ".pdf", ".ttf", ".otf", ".woff", ".woff2"
    };

    /// <summary>Null veya boş bırakılırsa tüm (binary olmayan) dosyalar taranır.</summary>
    public HashSet<string>? FileExtensions { get; set; } =
        new(DefaultExtensions, StringComparer.OrdinalIgnoreCase);

    public long MaxFileSizeBytes { get; set; } = 10L * 1024 * 1024;   // 10 MB
    public int MaxResults { get; set; } = 5000;
    public bool CaseSensitive { get; set; } = false;
    public string RegexPrefix { get; set; } = "re:";
    public TimeSpan RegexTimeout { get; set; } = TimeSpan.FromSeconds(2);

    // ==========================================================
    //  Genel API
    // ==========================================================
    public Task<List<SearchResult>> SearchAsync(
        string rootDir,
        IEnumerable<string> patterns,
        IProgress<string>? onStatus,
        CancellationToken ct)
    {
        return Task.Run(() => SearchInternal(rootDir, patterns, onStatus, ct), ct);
    }

    // ==========================================================
    //  İç uygulama (Task.Run içinde çalışır)
    // ==========================================================
    private List<SearchResult> SearchInternal(
        string rootDir,
        IEnumerable<string> patterns,
        IProgress<string>? onStatus,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rootDir))
            throw new ArgumentException("rootDir boş olamaz.", nameof(rootDir));
        if (!Directory.Exists(rootDir))
            throw new DirectoryNotFoundException("Taranacak klasör bulunamadı: " + rootDir);

        var compiled = CompilePatterns(patterns);
        if (compiled.Count == 0)
            throw new ArgumentException("En az bir geçerli desen gerekli.", nameof(patterns));

        ct.ThrowIfCancellationRequested();

        var results = new List<SearchResult>();
        var enumOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint  // symlink döngüsü yok
        };

        long scanned = 0;
        foreach (var file in Directory.EnumerateFiles(rootDir, "*.*", enumOptions))
        {
            ct.ThrowIfCancellationRequested();
            if (!ShouldScan(file)) continue;
            long size;
            try { size = new FileInfo(file).Length; }
            catch { continue; }
            if (size > MaxFileSizeBytes) continue;

            scanned++;
            if (scanned % 200 == 0)
                onStatus?.Report($"Taranan: {scanned} dosya, eşleşme: {results.Count}");

            try
            {
                ScanFile(file, compiled, results, ct, onStatus);
            }
            catch (IOException ex)
            {
                onStatus?.Report($"[atlandı] {file} → {ex.Message}");
            }
            if (results.Count >= MaxResults)
            {
                onStatus?.Report($"Maksimum eşleşme sayısına ulaşıldı ({MaxResults}). Tarama durduruldu.");
                break;
            }
        }
        onStatus?.Report($"Tarama bitti. Toplam {scanned} dosya, {results.Count} eşleşme.");
        return results;
    }

    private void ScanFile(
        string file,
        IReadOnlyList<(string Pattern, Regex Regex)> compiled,
        List<SearchResult> results,
        CancellationToken ct,
        IProgress<string>? onStatus)
    {
        using var reader = new StreamReader(
            file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        int lineNum = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNum++;
            if ((lineNum & 0x3FF) == 0) ct.ThrowIfCancellationRequested(); // her 1024 satırda
            foreach (var (pattern, regex) in compiled)
            {
                if (results.Count >= MaxResults) return;
                try
                {
                    var m = regex.Match(line);
                    if (m.Success)
                    {
                        var content = line.Length > 500 ? line[..500] + "…" : line.Trim();
                        results.Add(new SearchResult(
                            file, lineNum, m.Index + 1, m.Value, content, pattern));
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    onStatus?.Report($"[regex timeout] {pattern} @ {file}:{lineNum}");
                }
            }
        }
    }

    // ==========================================================
    //  Desen derleme
    // ==========================================================
    private List<(string Pattern, Regex Regex)> CompilePatterns(IEnumerable<string> patterns)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<(string, Regex)>();
        var options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        if (!CaseSensitive) options |= RegexOptions.IgnoreCase;

        foreach (var raw in patterns)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var trimmed = raw.Trim();
            if (!seen.Add(trimmed)) continue;

            bool isRegex = trimmed.StartsWith(RegexPrefix, StringComparison.Ordinal);
            var body = isRegex ? trimmed[RegexPrefix.Length..] : trimmed;
            if (string.IsNullOrEmpty(body)) continue;

            try
            {
                var source = isRegex ? body : Regex.Escape(body);
                var rx = new Regex(source, options, RegexTimeout);
                list.Add((trimmed, rx));
            }
            catch (ArgumentException)
            {
                // Kullanıcı re: eklemeden bozuk regex yazdıysa → literal olarak dene
                try
                {
                    var rx = new Regex(Regex.Escape(trimmed), options, RegexTimeout);
                    list.Add((trimmed, rx));
                }
                catch { /* yok say */ }
            }
        }
        return list;
    }

    // ==========================================================
    //  Dosya filtresi
    // ==========================================================
    private bool ShouldScan(string file)
    {
        var ext = Path.GetExtension(file);
        if (string.IsNullOrEmpty(ext)) return false;
        if (KnownBinaryExtensions.Contains(ext)) return false;
        // Boş/null FileExtensions → "tüm metin dosyaları" modu
        if (FileExtensions is null || FileExtensions.Count == 0) return true;
        return FileExtensions.Contains(ext);
    }
}
