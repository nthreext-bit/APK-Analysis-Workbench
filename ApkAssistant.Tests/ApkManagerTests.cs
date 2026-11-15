using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApkAssistant.Tests;

public class ApkManagerTests
{
    // 1. .exe → doğrudan çalıştırma
    [Fact]
    public void BuildPsi_Exe_UsesExeDirectly()
    {
        var mgr = new ApkManager();
        var psi = mgr.BuildProcessStartInfo(@"C:\tools\apktool.exe", new[] { "d", "in.apk" });
        Assert.Equal(@"C:\tools\apktool.exe", psi.FileName);
        Assert.Equal(new[] { "d", "in.apk" }, psi.ArgumentList);
        Assert.False(psi.UseShellExecute);
        Assert.True(psi.RedirectStandardOutput);
    }

    // 2. .jar → java -jar
    [Fact]
    public void BuildPsi_Jar_UsesJavaMinusJar()
    {
        var mgr = new ApkManager(javaPath: "java");
        var psi = mgr.BuildProcessStartInfo(@"C:\tools\apktool.jar", new[] { "d", "in.apk" });
        Assert.Equal("java", psi.FileName);
        Assert.Equal(new[] { "-jar", @"C:\tools\apktool.jar", "d", "in.apk" }, psi.ArgumentList);
    }

    // 3. .bat → cmd /c "..."
    [Fact]
    public void BuildPsi_Bat_UsesCmdWrapper()
    {
        var mgr = new ApkManager();
        var psi = mgr.BuildProcessStartInfo(@"C:\tools\jadx-cli.bat", new[] { "-d", "out", "in.apk" });
        Assert.Equal("cmd.exe", psi.FileName);
        Assert.Contains("jadx-cli.bat", psi.Arguments);
    }

    // 4. Run async - success
    [Fact]
    public async Task RunProcessAsync_Echo_ReturnsZeroAndCapturesOutput()
    {
        var mgr = new ApkManager();
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("echo hello_from_test");

        var lines = new List<string>();
        var tcs = new TaskCompletionSource();
        var progress = new Progress<string>(l =>
        {
            lock (lines) lines.Add(l);
            if (l.Contains("hello_from_test")) tcs.TrySetResult();
        });

        var code = await mgr.RunProcessAsync(psi, progress, CancellationToken.None);
        Assert.Equal(0, code);

        // Progress is posted async; wait for the expected line
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        lock (lines)
            Assert.Contains(lines, l => l.Contains("hello_from_test"));
    }

    // 5. Non-zero exit → exception
    [Fact]
    public async Task RunProcessAsync_NonZeroExit_ThrowsToolExecutionException()
    {
        var mgr = new ApkManager();
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("exit 3");

        var ex = await Assert.ThrowsAsync<ToolExecutionException>(
            () => mgr.RunProcessAsync(psi, null, CancellationToken.None));
        Assert.Equal(3, ex.ExitCode);
    }

    // 6. Cancellation kills process
    [Fact]
    public async Task RunProcessAsync_Cancellation_KillsProcess()
    {
        var mgr = new ApkManager();
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("ping -n 30 127.0.0.1 >nul"); // ~30 sec wait

        using var cts = new CancellationTokenSource(200);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mgr.RunProcessAsync(psi, null, cts.Token));
    }
}
