namespace ApkAssistant;

/// <summary>Bilinen-desen taramasından dönen tek bir bulgu (salt-okunur).</summary>
public record KnownPatternHit(
    string Category,
    string Label,
    string FilePath,
    int LineNumber,
    int ColumnNumber,
    string Match,
    string LineContent);

/// <summary>
/// CodeAnalyzer üzerine kurulu, KnownPatternCatalog'daki bilinen VPN tespiti /
/// sertifika sabitleme kalıplarını tarayan salt-okunur yardımcı.
///
/// Bu sınıf HİÇBİR dosyayı değiştirmez ve hiçbir kodu atlatmaz; yalnızca
/// elle incelenmek üzere aday konumları listeler. Bulunan satırlarda ne
/// yapılacağına (değiştirip değiştirmemeye) kullanıcı kendisi karar verir —
/// gerekirse mevcut "Smali/metin değiştirme" (PatchManager.ReplaceInFileAsync)
/// özelliğiyle elle, tek tek uygulanır.
/// </summary>
public class KnownPatternScanner
{
    public CodeAnalyzer Analyzer { get; } = new();

    public async Task<List<KnownPatternHit>> ScanAsync(
        string rootDir,
        bool includeVpn,
        bool includePinning,
        IProgress<string>? onStatus,
        CancellationToken ct)
    {
        if (!includeVpn && !includePinning)
            throw new ArgumentException("En az bir kategori seçilmeli (VPN Tespiti veya SSL Pinning).");

        var entries = new List<PatternCatalogEntry>();
        if (includeVpn) entries.AddRange(KnownPatternCatalog.VpnDetection);
        if (includePinning) entries.AddRange(KnownPatternCatalog.SslPinning);

        // Aynı desen birden fazla girdide tekrar edebilir — CodeAnalyzer'a
        // tekilleştirilmiş gönderip sonucu katalog girdisine geri eşliyoruz.
        var byPattern = new Dictionary<string, PatternCatalogEntry>(StringComparer.Ordinal);
        foreach (var e in entries)
            byPattern.TryAdd(e.Pattern, e);

        var results = await Analyzer.SearchAsync(rootDir, byPattern.Keys, onStatus, ct);

        var hits = new List<KnownPatternHit>(results.Count);
        foreach (var r in results)
        {
            if (!byPattern.TryGetValue(r.Pattern, out var entry))
                continue; // normal şartlarda olmaz; eşleşmeyen desen atlanır

            hits.Add(new KnownPatternHit(
                entry.Category, entry.Label,
                r.FilePath, r.LineNumber, r.ColumnNumber, r.Match, r.LineContent));
        }
        return hits;
    }
}
