using System.Security.Cryptography;
using System.Text;

namespace ApkAssistant;

public enum DiffStatus { Added, Removed, Modified }

public record DiffEntry(string RelativePath, DiffStatus Status);

/// <summary>
/// İki (genelde decompile edilmiş) klasör ağacını karşılaştırır: eklenen/silinen/
/// değişen dosyaları listeler ve değişen metin dosyaları için basit, LCS tabanlı
/// satır bazlı bir fark (diff) çıktısı üretir. Salt okunurdur — hiçbir dosyayı
/// değiştirmez.
/// </summary>
public class DiffManager
{
    /// <summary>Satır bazlı diff için üst sınır — çok büyük dosyalarda O(n*m) belleği patlatmasın diye.</summary>
    public long MaxCellsForLineDiff { get; set; } = 4_000_000;

    public Task<List<DiffEntry>> CompareDirectoriesAsync(
        string leftRoot, string rightRoot, CancellationToken ct) =>
        Task.Run(() => CompareDirectories(leftRoot, rightRoot, ct), ct);

    private List<DiffEntry> CompareDirectories(string leftRoot, string rightRoot, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(leftRoot) || !Directory.Exists(leftRoot))
            throw new DirectoryNotFoundException("Sol klasör bulunamadı: " + leftRoot);
        if (string.IsNullOrWhiteSpace(rightRoot) || !Directory.Exists(rightRoot))
            throw new DirectoryNotFoundException("Sağ klasör bulunamadı: " + rightRoot);

        var leftFiles = EnumerateRelative(leftRoot);
        var rightFiles = EnumerateRelative(rightRoot);

        var all = new SortedSet<string>(leftFiles.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in rightFiles.Keys) all.Add(k);

        var results = new List<DiffEntry>();
        foreach (var rel in all)
        {
            ct.ThrowIfCancellationRequested();

            bool inLeft = leftFiles.TryGetValue(rel, out var leftFull);
            bool inRight = rightFiles.TryGetValue(rel, out var rightFull);

            if (inLeft && !inRight) { results.Add(new DiffEntry(rel, DiffStatus.Removed)); continue; }
            if (!inLeft && inRight) { results.Add(new DiffEntry(rel, DiffStatus.Added)); continue; }

            if (!FilesEqual(leftFull!, rightFull!))
                results.Add(new DiffEntry(rel, DiffStatus.Modified));
        }
        return results;
    }

    private static Dictionary<string, string> EnumerateRelative(string root)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var opts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        foreach (var file in Directory.EnumerateFiles(root, "*", opts))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            dict[rel] = file;
        }
        return dict;
    }

    private static bool FilesEqual(string a, string b)
    {
        var infoA = new FileInfo(a);
        var infoB = new FileInfo(b);
        if (infoA.Length != infoB.Length) return false;

        using var sha = SHA256.Create();
        byte[] hashA, hashB;
        using (var fsA = File.OpenRead(a)) hashA = sha.ComputeHash(fsA);
        using (var fsB = File.OpenRead(b)) hashB = sha.ComputeHash(fsB);
        return hashA.AsSpan().SequenceEqual(hashB);
    }

    /// <summary>
    /// İki dosya arasında basit, satır bazlı bir fark metni üretir. Satırlar
    /// '+' (sağda var, solda yok), '-' (solda var, sağda yok) veya ' ' (aynı)
    /// öneki ile listelenir. Dosyalar diff için çok büyükse açıklayıcı bir
    /// mesaj döner (istisna fırlatmaz).
    /// </summary>
    public string BuildLineDiff(string leftFile, string rightFile)
    {
        string[] leftLines, rightLines;
        try
        {
            leftLines = File.ReadAllLines(leftFile);
            rightLines = File.ReadAllLines(rightFile);
        }
        catch (Exception ex)
        {
            return $"(Satır bazlı fark okunamadı: {ex.Message})";
        }

        long cells = (long)(leftLines.Length + 1) * (rightLines.Length + 1);
        if (cells > MaxCellsForLineDiff)
            return $"(Dosyalar satır bazlı karşılaştırma için çok büyük: {leftLines.Length} x {rightLines.Length} satır. " +
                   "Yalnızca 'Değişti' olarak işaretlendi.)";

        var ops = LineDiffAlgorithm.Diff(leftLines, rightLines);
        var sb = new StringBuilder();
        foreach (var (op, line) in ops)
            sb.Append(op).Append(' ').Append(line).Append('\n');
        return sb.ToString();
    }

    /// <summary>
    /// Basit ikili-dosya sezgisi: ilk <paramref name="sampleSize"/> bayt içinde
    /// NUL (0x00) bayt varsa dosya ikili kabul edilir ve satır bazlı fark
    /// denenmez (ör. .png, .dex, .so, .apk gibi kaynaklar).
    /// </summary>
    public static bool LooksLikeBinary(string path, int sampleSize = 8000)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buffer = new byte[Math.Min(sampleSize, (int)Math.Min(fs.Length, int.MaxValue))];
            int read = fs.Read(buffer, 0, buffer.Length);
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == 0) return true;
            }
            return false;
        }
        catch
        {
            return true; // okunamayan dosya güvenli tarafta kalınarak ikili sayılır
        }
    }
}

/// <summary>
/// En-uzun-ortak-altdizi (LCS) tabanlı klasik satır diff algoritması.
/// O(n*m) zaman/bellek kullanır — büyük dosyalar için DiffManager.MaxCellsForLineDiff
/// ile sınırlandırılır.
/// </summary>
internal static class LineDiffAlgorithm
{
    public static List<(char Op, string Line)> Diff(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        int n = a.Count, m = b.Count;
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = m - 1; j >= 0; j--)
            {
                dp[i, j] = a[i] == b[j]
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var result = new List<(char, string)>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y]) { result.Add((' ', a[x])); x++; y++; }
            else if (dp[x + 1, y] >= dp[x, y + 1]) { result.Add(('-', a[x])); x++; }
            else { result.Add(('+', b[y])); y++; }
        }
        while (x < n) { result.Add(('-', a[x])); x++; }
        while (y < m) { result.Add(('+', b[y])); y++; }
        return result;
    }
}
