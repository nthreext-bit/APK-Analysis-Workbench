using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ApkAssistant;

public record PatchResult(
    string FilePath,
    string BackupPath,
    int ChangesMade,
    string Summary);

public class PatchManager
{
    public string BackupExtension { get; set; } = ".bak";
    public TimeSpan RegexTimeout { get; set; } = TimeSpan.FromSeconds(2);

    // ========================= Yedekleme =========================
    public string CreateBackup(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Yedeklenecek dosya bulunamadı: " + filePath, filePath);

        var primary = filePath + BackupExtension;
        if (!File.Exists(primary))
        {
            File.Copy(filePath, primary);
            return primary;
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var candidate = $"{filePath}{BackupExtension}.{stamp}";
        int n = 1;
        while (File.Exists(candidate))
            candidate = $"{filePath}{BackupExtension}.{stamp}.{n++}";
        File.Copy(filePath, candidate);
        return candidate;
    }

    public bool TryRestoreFromBackup(string filePath, out string backupUsed)
    {
        backupUsed = "";
        if (!File.Exists(filePath)) return false;

        var primary = filePath + BackupExtension;
        if (File.Exists(primary))
        {
            File.Copy(primary, filePath, overwrite: true);
            backupUsed = primary;
            return true;
        }

        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;

        var pattern = Path.GetFileName(filePath) + BackupExtension + ".*";
        var candidates = Directory.GetFiles(dir, pattern)
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .ToList();
        if (candidates.Count == 0) return false;

        File.Copy(candidates[0], filePath, overwrite: true);
        backupUsed = candidates[0];
        return true;
    }

    // ============== network_security_config.xml yaması ==============
    public async Task<PatchResult> PatchNetworkSecurityConfigAsync(string xmlPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(xmlPath))
            throw new ArgumentException("xmlPath boş olamaz.", nameof(xmlPath));
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException("XML bulunamadı: " + xmlPath, xmlPath);

        ct.ThrowIfCancellationRequested();

        var backup = CreateBackup(xmlPath);
        int changes = 0;
        var notes = new List<string>();

        await Task.Run(() =>
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(xmlPath, LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex)
            {
                TryRestoreQuiet(xmlPath, backup);
                throw new InvalidOperationException(
                    "XML ayrıştırılamadı, dosya değiştirilmedi: " + ex.Message, ex);
            }

            if (doc.Root is null)
            {
                TryRestoreQuiet(xmlPath, backup);
                throw new InvalidOperationException("XML kökü yok — geçersiz dosya.");
            }

            var pins = doc.Descendants("pin-set").ToList();
            foreach (var el in pins) { el.Remove(); changes++; }
            if (pins.Count > 0) notes.Add($"{pins.Count} <pin-set> silindi");

            var anchors = doc.Descendants("trust-anchors").ToList();
            foreach (var el in anchors) { el.Remove(); changes++; }
            if (anchors.Count > 0) notes.Add($"{anchors.Count} <trust-anchors> silindi");

            var userCerts = doc.Descendants("certificates")
                .Where(el => string.Equals((string?)el.Attribute("src"), "user", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var el in userCerts) { el.Remove(); changes++; }
            if (userCerts.Count > 0) notes.Add($"{userCerts.Count} <certificates src=user> silindi");

            var baseConfigs = doc.Descendants("base-config").ToList();
            if (baseConfigs.Count == 0)
            {
                doc.Root.AddFirst(new XElement("base-config",
                    new XAttribute("cleartextTrafficPermitted", "true")));
                changes++;
                notes.Add("<base-config cleartext=true> oluşturuldu");
            }
            else
            {
                foreach (var el in baseConfigs)
                {
                    var attr = el.Attribute("cleartextTrafficPermitted");
                    if (attr is null) { el.SetAttributeValue("cleartextTrafficPermitted", "true"); changes++; }
                    else if (!string.Equals(attr.Value, "true", StringComparison.OrdinalIgnoreCase))
                    { attr.Value = "true"; changes++; }
                }
                notes.Add("base-config cleartext=true");
            }

            var domainConfigs = doc.Descendants("domain-config").ToList();
            foreach (var el in domainConfigs)
            {
                var attr = el.Attribute("cleartextTrafficPermitted");
                if (attr is null) { el.SetAttributeValue("cleartextTrafficPermitted", "true"); changes++; }
                else if (!string.Equals(attr.Value, "true", StringComparison.OrdinalIgnoreCase))
                { attr.Value = "true"; changes++; }
            }
            if (domainConfigs.Count > 0) notes.Add($"{domainConfigs.Count} domain-config cleartext=true");

            ct.ThrowIfCancellationRequested();

            var tmp = xmlPath + ".tmp_" + Guid.NewGuid().ToString("N");
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    doc.Save(sw);
                }
                File.Move(tmp, xmlPath, overwrite: true);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                TryRestoreQuiet(xmlPath, backup);
                throw;
            }
        }, ct);

        return new PatchResult(xmlPath, backup, changes,
            changes == 0 ? "Değişiklik yok (zaten temiz)" : string.Join("; ", notes));
    }

    // ================= Smali / metin değiştirme =================
    public async Task<PatchResult> ReplaceInFileAsync(
        string filePath, string search, string replace,
        bool isRegex, bool caseSensitive, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath boş olamaz.", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Dosya bulunamadı: " + filePath, filePath);
        if (string.IsNullOrEmpty(search))
            throw new ArgumentException("Aranacak metin boş olamaz.", nameof(search));

        ct.ThrowIfCancellationRequested();

        var backup = CreateBackup(filePath);

        string original;
        try { original = await File.ReadAllTextAsync(filePath, ct); }
        catch { TryRestoreQuiet(filePath, backup); throw; }

        Regex regex;
        try
        {
            var opts = RegexOptions.CultureInvariant | RegexOptions.Multiline;
            if (!caseSensitive) opts |= RegexOptions.IgnoreCase;
            var source = isRegex ? search : Regex.Escape(search);
            regex = new Regex(source, opts, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            TryRestoreQuiet(filePath, backup);
            throw new ArgumentException("Geçersiz regex: " + ex.Message, nameof(search), ex);
        }

        int count;
        string updated;
        try
        {
            count = regex.Matches(original).Count;
            if (count == 0)
                return new PatchResult(filePath, backup, 0, "Eşleşme yok (dosya değişmedi)");
            updated = regex.Replace(original, replace ?? string.Empty);
        }
        catch (RegexMatchTimeoutException ex)
        {
            TryRestoreQuiet(filePath, backup);
            throw new InvalidOperationException("Regex zaman aşımı: " + ex.Message, ex);
        }

        try
        {
            await File.WriteAllTextAsync(filePath, updated, new UTF8Encoding(false), ct);
        }
        catch
        {
            TryRestoreQuiet(filePath, backup);
            throw;
        }

        return new PatchResult(filePath, backup, count,
            $"{count} eşleşme değiştirildi ({(isRegex ? "regex" : "literal")})");
    }

    private static void TryRestoreQuiet(string filePath, string backup)
    {
        try { if (File.Exists(backup)) File.Copy(backup, filePath, overwrite: true); }
        catch { /* ignore */ }
    }
}
