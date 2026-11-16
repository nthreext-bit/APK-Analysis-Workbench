using System.Text.Json;

namespace ApkAssistant;

/// <summary>
/// Kalıcı uygulama ayarları: araç yolları + son kullanılan APK/çıktı klasörü.
/// </summary>
public class AppSettings
{
    public string ApktoolPath { get; set; } = "";
    public string JadxPath { get; set; } = "";
    public string ApksignerPath { get; set; } = "";
    public string ZipalignPath { get; set; } = "";
    public string KeytoolPath { get; set; } = "";
    public string AdbPath { get; set; } = "adb";

    public string LastApkPath { get; set; } = "";
    public string LastOutputDir { get; set; } = "";
}

/// <summary>
/// AppSettings'i diskte bir JSON dosyası olarak okuyup yazan yardımcı.
/// Bozuk/okunamayan bir dosya sessizce yok sayılır (varsayılan ayarlarla devam edilir) —
/// uygulamanın ayar dosyasındaki bir sorun yüzünden açılamaması istenmez.
/// </summary>
public static class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string GetDefaultPath(string? baseDirectory = null) =>
        Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "appsettings.user.json");

    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new AppSettings();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings, string path)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp_" + Guid.NewGuid().ToString("N");
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }
}
