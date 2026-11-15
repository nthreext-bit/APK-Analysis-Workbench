using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApkAssistant.Tests;

public class CodeAnalyzerTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ApkAnalyzerTest_" + Guid.NewGuid().ToString("N"));
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
    public async Task SimpleMatch_ReturnsOneResult()
    {
        using var tmp = new TempDir();
        tmp.Write("a.smali", "line1\ninvoke-static {v0}, LNetworkInterface;->x()\n");
        var analyzer = new CodeAnalyzer();
        var results = await analyzer.SearchAsync(tmp.Path, new[] { "NetworkInterface" }, null, default);
        Assert.Single(results);
        Assert.Equal(2, results[0].LineNumber);
        Assert.Contains("NetworkInterface", results[0].LineContent);
    }

    [Fact]
    public async Task EmptyDirectory_ReturnsEmpty()
    {
        using var tmp = new TempDir();
        var analyzer = new CodeAnalyzer();
        var results = await analyzer.SearchAsync(tmp.Path, new[] { "anything" }, null, default);
        Assert.Empty(results);
    }

    [Fact]
    public async Task CaseInsensitive_ByDefault()
    {
        using var tmp = new TempDir();
        tmp.Write("a.java", "CheckSignature();\n");
        var analyzer = new CodeAnalyzer();
        var results = await analyzer.SearchAsync(tmp.Path, new[] { "checksignature" }, null, default);
        Assert.Single(results);
    }

    [Fact]
    public async Task LiteralPattern_DoesNotTreatDotAsWildcard()
    {
        // com.openvpn  →  yalnızca 2. satır eşleşmeli (regex olsa 3 satır da eşleşirdi)
        using var tmp = new TempDir();
        tmp.Write("a.smali", "com_openvpn\ncom.openvpn\ncomXopenvpn\n");
        var analyzer = new CodeAnalyzer();
        var results = await analyzer.SearchAsync(tmp.Path, new[] { "com.openvpn" }, null, default);
        Assert.Single(results);
        Assert.Equal(2, results[0].LineNumber);
    }

    [Fact]
    public async Task RegexPrefix_EnablesRegexMatching()
    {
        using var tmp = new TempDir();
        tmp.Write("a.smali", "NetworkInterface\nNetworkSecurity\nSomeNetwork\n");
        var analyzer = new CodeAnalyzer();
        var results = await analyzer.SearchAsync(
            tmp.Path, new[] { @"re:^Network[A-Z]\w+" }, null, default);
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Match == "NetworkInterface");
        Assert.Contains(results, r => r.Match == "NetworkSecurity");
    }

    [Fact]
    public async Task BinaryExtension_IsIgnored()
    {
        using var tmp = new TempDir();
        tmp.Write("image.png", "NetworkInterface");
        tmp.Write("classes.dex", "NetworkInterface");
        var analyzer = new CodeAnalyzer();
        var results = await analyzer.SearchAsync(tmp.Path, new[] { "NetworkInterface" }, null, default);
        Assert.Empty(results);
    }

    [Fact]
    public async Task RecursiveScan_FindsNestedFile()
    {
        using var tmp = new TempDir();
        tmp.Write("sub/deep/nested.smali", "checkSignature\n");
        var analyzer = new CodeAnalyzer();
        var results = await analyzer.SearchAsync(tmp.Path, new[] { "checkSignature" }, null, default);
        Assert.Single(results);
        Assert.EndsWith("nested.smali", results[0].FilePath);
    }

    [Fact]
    public async Task MultiplePatterns_ReturnsAllMatches()
    {
        using var tmp = new TempDir();
        tmp.Write("a.smali", "TRANSPORT_VPN\ncheckSignature\n");
        var analyzer = new CodeAnalyzer();
        var results = await analyzer.SearchAsync(
            tmp.Path, new[] { "TRANSPORT_VPN", "checkSignature" }, null, default);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task NonExistentDirectory_Throws()
    {
        var analyzer = new CodeAnalyzer();
        var bogus = @"C:\does\not\exist\hopefully_" + Guid.NewGuid().ToString("N");
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => analyzer.SearchAsync(bogus, new[] { "x" }, null, default));
    }

    [Fact]
    public async Task EmptyPatterns_Throws()
    {
        using var tmp = new TempDir();
        var analyzer = new CodeAnalyzer();
        await Assert.ThrowsAsync<ArgumentException>(
            () => analyzer.SearchAsync(tmp.Path, Array.Empty<string>(), null, default));
    }

    [Fact]
    public async Task PreCancelledToken_Throws()
    {
        using var tmp = new TempDir();
        tmp.Write("a.smali", "test\n");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var analyzer = new CodeAnalyzer();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => analyzer.SearchAsync(tmp.Path, new[] { "test" }, null, cts.Token));
    }

    [Fact]
    public async Task MaxResults_Respected()
    {
        using var tmp = new TempDir();
        for (int i = 0; i < 20; i++)
            tmp.Write($"f{i:D2}.smali", "NetworkInterface\n");
        var analyzer = new CodeAnalyzer { MaxResults = 5 };
        var results = await analyzer.SearchAsync(tmp.Path, new[] { "NetworkInterface" }, null, default);
        Assert.Equal(5, results.Count);
    }
}
