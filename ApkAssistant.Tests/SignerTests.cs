using Xunit;

namespace ApkAssistant.Tests;

public class SignerTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SignerTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public string Write(string name, string content)
        {
            var full = System.IO.Path.Combine(Path, name);
            File.WriteAllText(full, content);
            return full;
        }
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }

    // ---------- Arg builders ----------

    [Fact]
    public void BuildZipalignPsi_ProducesExpectedArgs()
    {
        var s = new Signer(zipalignPath: @"C:\sdk\zipalign.exe");
        var psi = s.BuildZipalignPsi(@"in.apk", @"out.apk");

        Assert.Equal(@"C:\sdk\zipalign.exe", psi.FileName);
        Assert.Equal(new[] { "-f", "-p", "4", "in.apk", "out.apk" }, psi.ArgumentList);
    }

    [Fact]
    public void BuildSignPsi_BarePasswordGetsPassPrefix()
    {
        var s = new Signer(apksignerPath: @"C:\sdk\apksigner.bat");
        var psi = s.BuildSignPsi("app.apk", "ks.jks", "key0", "secret123", "keysec");

        Assert.Equal("cmd.exe", psi.FileName);
        Assert.Contains("--ks-pass pass:secret123", psi.Arguments);
        Assert.Contains("--key-pass pass:keysec", psi.Arguments);
        Assert.Contains("--ks-key-alias key0", psi.Arguments);
        Assert.Contains("app.apk", psi.Arguments);
    }

    [Fact]
    public void BuildSignPsi_KeepsEnvAndFilePrefixes()
    {
        var s = new Signer(apksignerPath: @"C:\sdk\apksigner.exe");
        var psi = s.BuildSignPsi("a.apk", "k.jks", "al", "env:MY_STORE_PASS", "file:C:\\pw.txt");

        Assert.Contains("env:MY_STORE_PASS", psi.ArgumentList);
        Assert.Contains("file:C:\\pw.txt", psi.ArgumentList);
    }

    [Fact]
    public void BuildSignPsi_OmitsKeyPassWhenEmpty()
    {
        var s = new Signer(apksignerPath: @"C:\sdk\apksigner.exe");
        var psi = s.BuildSignPsi("a.apk", "k.jks", "al", "s", "");

        Assert.DoesNotContain("--key-pass", psi.ArgumentList);
    }

    [Fact]
    public void BuildVerifyPsi_UsesVerboseAndPrintCerts()
    {
        var s = new Signer(apksignerPath: @"C:\sdk\apksigner.exe");
        var psi = s.BuildVerifyPsi("a.apk");

        Assert.Equal(new[] { "verify", "--verbose", "--print-certs", "a.apk" }, psi.ArgumentList);
    }

    [Fact]
    public void BuildKeytoolPsi_ContainsAllFields()
    {
        var s = new Signer(keytoolPath: @"C:\jdk\keytool.exe");
        var psi = s.BuildKeytoolPsi(@"C:\ks\debug.keystore", "androiddebugkey", "android", "android", "CN=Test", 10000);

        Assert.Contains("-genkeypair", psi.ArgumentList);
        Assert.Contains("-keystore", psi.ArgumentList);
        Assert.Contains(@"C:\ks\debug.keystore", psi.ArgumentList);
        Assert.Contains("-alias", psi.ArgumentList);
        Assert.Contains("androiddebugkey", psi.ArgumentList);
        Assert.Contains("-validity", psi.ArgumentList);
        Assert.Contains("10000", psi.ArgumentList);
        Assert.Contains("-dname", psi.ArgumentList);
        Assert.Contains("CN=Test", psi.ArgumentList);
    }

    // ---------- Verify parser ----------

    [Fact]
    public void ParseVerifyOutput_SuccessWithV1V2V3()
    {
        const string raw = @"
Verifies
Verified using v1 scheme (JAR signing): true
Verified using v2 scheme (APK Signature Scheme v2): true
Verified using v3 scheme (APK Signature Scheme v3): true
Number of signers: 1
Signer #1 certificate DN: CN=Android Debug, O=Android, C=US
";
        var r = Signer.ParseVerifyOutput(raw, 0);

        Assert.True(r.IsVerified);
        Assert.True(r.V1Signed);
        Assert.True(r.V2Signed);
        Assert.True(r.V3Signed);
        Assert.Equal(1, r.SignerCount);
        Assert.Single(r.CertificateDNs);
        Assert.Contains("CN=Android Debug", r.CertificateDNs[0]);
    }

    [Fact]
    public void ParseVerifyOutput_NotSigned_ReturnsNotVerified()
    {
        const string raw = "DOES NOT VERIFY\nERROR: Missing META-INF/MANIFEST.MF\n";
        var r = Signer.ParseVerifyOutput(raw, 1);

        Assert.False(r.IsVerified);
        Assert.False(r.V1Signed);
        Assert.Equal(0, r.SignerCount);
        Assert.Empty(r.CertificateDNs);
    }

    [Fact]
    public void ParseVerifyOutput_MultipleSigners()
    {
        const string raw = @"
Verified using v2 scheme (APK Signature Scheme v2): true
Number of signers: 2
Signer #1 certificate DN: CN=First, O=A
Signer #2 certificate DN: CN=Second, O=B
";
        var r = Signer.ParseVerifyOutput(raw, 0);

        Assert.True(r.IsVerified);
        Assert.Equal(2, r.SignerCount);
        Assert.Equal(2, r.CertificateDNs.Count);
    }

    // ---------- Error / validation ----------

    [Fact]
    public async Task VerifyAsync_MissingApk_Throws()
    {
        using var tmp = new TempDir();
        var s = new Signer(apksignerPath: "apksigner");
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => s.VerifyAsync(Path.Combine(tmp.Path, "missing.apk"), null, default));
    }

    [Fact]
    public async Task SignAsync_MissingKeystore_Throws()
    {
        using var tmp = new TempDir();
        var apk = tmp.Write("a.apk", "fake");
        var s = new Signer(apksignerPath: "apksigner");
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => s.SignAsync(apk, Path.Combine(tmp.Path, "no.jks"), "al", "s", "s", null, default));
    }

    [Fact]
    public async Task CreateDebugKeystore_EmptyPath_Throws()
    {
        var s = new Signer(keytoolPath: "keytool");
        await Assert.ThrowsAsync<ArgumentException>(
            () => s.CreateDebugKeystoreAsync("", ct: default));
    }

    [Fact]
    public async Task CreateDebugKeystore_ExistingFile_Throws()
    {
        using var tmp = new TempDir();
        var existing = tmp.Write("debug.keystore", "fakebytes");
        var s = new Signer(keytoolPath: "keytool");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.CreateDebugKeystoreAsync(existing));
    }

    // ---------- Delegation to ApkManager process runner ----------

    [Fact]
    public async Task VerifyAsync_MissingToolPath_Throws()
    {
        var s = new Signer(apksignerPath: "");
        using var tmp = new TempDir();
        var apk = tmp.Write("a.apk", "fake");

        await Assert.ThrowsAnyAsync<Exception>(
            () => s.VerifyAsync(apk, null, CancellationToken.None));
    }

    // ---------- Default keystore path ----------

    [Fact]
    public void DefaultDebugKeystorePath_IsUnderUserProfile()
    {
        var path = Signer.GetDefaultDebugKeystorePath();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.StartsWith(home, path);
        Assert.EndsWith(Path.Combine(".android", "debug.keystore"), path);
    }
}
