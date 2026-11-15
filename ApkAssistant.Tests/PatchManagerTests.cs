using System.Xml.Linq;
using Xunit;

namespace ApkAssistant.Tests;

public class PatchManagerTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PatchTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public string Write(string name, string content)
        {
            var full = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            return full;
        }
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }

    private const string SampleNsc = @"<?xml version=""1.0"" encoding=""utf-8""?>
<network-security-config>
    <base-config cleartextTrafficPermitted=""false"">
        <trust-anchors>
            <certificates src=""system""/>
            <certificates src=""user""/>
        </trust-anchors>
    </base-config>
    <domain-config>
        <domain includeSubdomains=""true"">example.com</domain>
        <pin-set expiration=""2030-01-01"">
            <pin digest=""SHA-256"">AAAA</pin>
        </pin-set>
    </domain-config>
</network-security-config>";

    [Fact]
    public void CreateBackup_FirstCall_CreatesBak()
    {
        using var tmp = new TempDir();
        var file = tmp.Write("a.smali", "original");
        var mgr = new PatchManager();
        var backup = mgr.CreateBackup(file);

        Assert.True(File.Exists(backup));
        Assert.EndsWith(".bak", backup);
        Assert.Equal("original", File.ReadAllText(backup));
    }

    [Fact]
    public void CreateBackup_SecondCall_UsesTimestamp()
    {
        using var tmp = new TempDir();
        var file = tmp.Write("a.smali", "x");
        var mgr = new PatchManager();
        var b1 = mgr.CreateBackup(file);
        var b2 = mgr.CreateBackup(file);

        Assert.NotEqual(b1, b2);
        Assert.Contains(".bak.", b2);
    }

    [Fact]
    public async Task PatchNsc_RemovesPinSetAndTrustAnchors()
    {
        using var tmp = new TempDir();
        var xml = tmp.Write("network_security_config.xml", SampleNsc);
        var mgr = new PatchManager();

        var r = await mgr.PatchNetworkSecurityConfigAsync(xml, default);

        var doc = XDocument.Load(xml);
        Assert.Empty(doc.Descendants("pin-set"));
        Assert.Empty(doc.Descendants("trust-anchors"));
        Assert.Empty(doc.Descendants("certificates"));
        Assert.True(r.ChangesMade >= 3);
        Assert.True(File.Exists(r.BackupPath));
    }

    [Fact]
    public async Task PatchNsc_SetsCleartextTrue()
    {
        using var tmp = new TempDir();
        var xml = tmp.Write("network_security_config.xml", SampleNsc);
        var mgr = new PatchManager();

        await mgr.PatchNetworkSecurityConfigAsync(xml, default);

        var doc = XDocument.Load(xml);
        var baseCfg = doc.Descendants("base-config").First();
        Assert.Equal("true", (string?)baseCfg.Attribute("cleartextTrafficPermitted"));
    }

    [Fact]
    public async Task PatchNsc_AddsBaseConfigWhenMissing()
    {
        using var tmp = new TempDir();
        var xml = tmp.Write("nsc.xml",
            "<network-security-config><domain-config><domain>x.com</domain></domain-config></network-security-config>");
        var mgr = new PatchManager();

        await mgr.PatchNetworkSecurityConfigAsync(xml, default);

        var doc = XDocument.Load(xml);
        Assert.NotEmpty(doc.Descendants("base-config"));
    }

    [Fact]
    public async Task PatchNsc_InvalidXml_ThrowsAndRestores()
    {
        using var tmp = new TempDir();
        var original = "<network-security-config><unclosed>";
        var xml = tmp.Write("bad.xml", original);
        var mgr = new PatchManager();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mgr.PatchNetworkSecurityConfigAsync(xml, default));

        Assert.Equal(original, File.ReadAllText(xml));
    }

    [Fact]
    public async Task ReplaceInFile_Literal_ReplacesAll()
    {
        using var tmp = new TempDir();
        var f = tmp.Write("a.smali", "checkSignature\nfoo checkSignature bar\n");
        var mgr = new PatchManager();

        var r = await mgr.ReplaceInFileAsync(f, "checkSignature", "allowAll", false, true, default);

        Assert.Equal(2, r.ChangesMade);
        Assert.DoesNotContain("checkSignature", File.ReadAllText(f));
        Assert.Contains("allowAll", File.ReadAllText(f));
    }

    [Fact]
    public async Task ReplaceInFile_CaseInsensitiveByDefault()
    {
        using var tmp = new TempDir();
        var f = tmp.Write("a.smali", "CheckSignature CheckSignature");
        var mgr = new PatchManager();

        var r = await mgr.ReplaceInFileAsync(f, "checksignature", "x", false, false, default);

        Assert.Equal(2, r.ChangesMade);
        Assert.Equal("x x", File.ReadAllText(f));
    }

    [Fact]
    public async Task ReplaceInFile_Regex_Works()
    {
        using var tmp = new TempDir();
        var f = tmp.Write("a.smali", "const/4 v0, 0x0\nconst/4 v1, 0x1\n");
        var mgr = new PatchManager();

        var r = await mgr.ReplaceInFileAsync(
            f, @"const/4 (\w+), 0x[01]", "const/4 $1, 0x1", true, true, default);

        Assert.Equal(2, r.ChangesMade);
        Assert.DoesNotContain("0x0", File.ReadAllText(f));
    }

    [Fact]
    public async Task ReplaceInFile_NoMatch_DoesNotRewrite()
    {
        using var tmp = new TempDir();
        var f = tmp.Write("a.smali", "abc");
        var mtime = File.GetLastWriteTimeUtc(f);
        var mgr = new PatchManager();

        var r = await mgr.ReplaceInFileAsync(f, "zzz", "y", false, true, default);

        Assert.Equal(0, r.ChangesMade);
        Assert.Equal("abc", File.ReadAllText(f));
        Assert.Equal(mtime, File.GetLastWriteTimeUtc(f));
    }

    [Fact]
    public async Task TryRestoreFromBackup_RestoresOriginal()
    {
        using var tmp = new TempDir();
        var f = tmp.Write("a.smali", "original");
        var mgr = new PatchManager();
        await mgr.ReplaceInFileAsync(f, "original", "patched", false, true, default);
        Assert.Equal("patched", File.ReadAllText(f));

        var ok = mgr.TryRestoreFromBackup(f, out var used);

        Assert.True(ok);
        Assert.True(File.Exists(used));
        Assert.Equal("original", File.ReadAllText(f));
    }

    [Fact]
    public void TryRestoreFromBackup_NoBackup_ReturnsFalse()
    {
        using var tmp = new TempDir();
        var f = tmp.Write("a.smali", "x");
        var mgr = new PatchManager();

        Assert.False(mgr.TryRestoreFromBackup(f, out var used));
        Assert.Equal("", used);
    }
}
