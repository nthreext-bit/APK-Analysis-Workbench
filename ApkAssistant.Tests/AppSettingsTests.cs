using System;
using System.IO;
using Xunit;

namespace ApkAssistant.Tests;

public class AppSettingsTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "ApkSettingsTest_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var path = TempPath();
        var settings = SettingsManager.Load(path);
        Assert.Equal("adb", settings.AdbPath);
        Assert.Equal("", settings.ApktoolPath);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var path = TempPath();
        try
        {
            var original = new AppSettings
            {
                ApktoolPath = @"C:\tools\apktool.bat",
                JadxPath = @"C:\tools\jadx.bat",
                ApksignerPath = @"C:\tools\apksigner.bat",
                ZipalignPath = @"C:\tools\zipalign.exe",
                KeytoolPath = @"C:\tools\keytool.exe",
                AdbPath = @"C:\platform-tools\adb.exe",
                LastApkPath = @"C:\apks\sample.apk",
                LastOutputDir = @"C:\out\sample"
            };

            SettingsManager.Save(original, path);
            var loaded = SettingsManager.Load(path);

            Assert.Equal(original.ApktoolPath, loaded.ApktoolPath);
            Assert.Equal(original.JadxPath, loaded.JadxPath);
            Assert.Equal(original.ApksignerPath, loaded.ApksignerPath);
            Assert.Equal(original.ZipalignPath, loaded.ZipalignPath);
            Assert.Equal(original.KeytoolPath, loaded.KeytoolPath);
            Assert.Equal(original.AdbPath, loaded.AdbPath);
            Assert.Equal(original.LastApkPath, loaded.LastApkPath);
            Assert.Equal(original.LastOutputDir, loaded.LastOutputDir);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaultsWithoutThrowing()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "{ bu gecerli json degil ]]]");
            var settings = SettingsManager.Load(path);
            Assert.Equal("adb", settings.AdbPath);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
