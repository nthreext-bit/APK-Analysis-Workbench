using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApkAssistant.Tests;

public class KnownPatternScannerTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ApkKnownPatternTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public string Write(string relativeName, string content)
        {
            var full = System.IO.Path.Combine(Path, relativeName);
            var dir = System.IO.Path.GetDirectoryName(full)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(full, content);
            return full;
        }
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }

    [Fact]
    public async Task ScanAsync_FindsVpnAndPinningIndicators()
    {
        using var dir = new TempDir();
        dir.Write("NetUtil.java",
            "import android.net.VpnService;\n" +
            "public boolean detectVpnUsage() { return false; }\n");
        dir.Write("MainActivity.smali",
            ".method private isVpnActive()Z\n" +
            "    const-string v0, \"tun0\"\n" +
            "    new-instance v1, Lokhttp3/CertificatePinner;\n" +
            ".end method\n");

        var scanner = new KnownPatternScanner();
        var hits = await scanner.ScanAsync(dir.Path, includeVpn: true, includePinning: true, onStatus: null, CancellationToken.None);

        Assert.Contains(hits, h => h.Match.Equals("tun0", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hits, h => h.Match.Equals("VpnService", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hits, h => h.Match.Equals("isVpnActive", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hits, h => h.Match.Equals("detectVpnUsage", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hits, h => h.Category == KnownPatternCatalog.CategoryPinning);
    }

    [Fact]
    public async Task ScanAsync_OnlyVpnCategory_ExcludesPinningHits()
    {
        using var dir = new TempDir();
        dir.Write("a.java", "VpnService x; CertificatePinner y;\n");

        var scanner = new KnownPatternScanner();
        var hits = await scanner.ScanAsync(dir.Path, includeVpn: true, includePinning: false, onStatus: null, CancellationToken.None);

        Assert.All(hits, h => Assert.Equal(KnownPatternCatalog.CategoryVpn, h.Category));
    }

    [Fact]
    public async Task ScanAsync_NoCategorySelected_Throws()
    {
        using var dir = new TempDir();
        var scanner = new KnownPatternScanner();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            scanner.ScanAsync(dir.Path, includeVpn: false, includePinning: false, onStatus: null, CancellationToken.None));
    }

    [Fact]
    public async Task ScanAsync_NoMatches_ReturnsEmpty()
    {
        using var dir = new TempDir();
        dir.Write("clean.txt", "nothing suspicious here, just plain text.\n");

        var scanner = new KnownPatternScanner();
        var hits = await scanner.ScanAsync(dir.Path, includeVpn: true, includePinning: true, onStatus: null, CancellationToken.None);

        Assert.Empty(hits);
    }
}
