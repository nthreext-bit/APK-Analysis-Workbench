namespace ApkAssistant;

/// <summary>
/// Tek bir bilinen-desen kataloğu girdisi. <see cref="Pattern"/>, CodeAnalyzer'ın
/// beklediği biçimdedir: düz metin, ya da "re:" önekiyle regex.
/// </summary>
public record PatternCatalogEntry(string Category, string Label, string Pattern);

/// <summary>
/// Statik analiz için önceden tanımlanmış, bilinen desenlerin kataloğu.
///
/// ÖNEMLİ: Bu desenler yalnızca incelemeye değer olabilecek KONUMLARI
/// İŞARETLEMEK içindir. Hiçbir dosyayı değiştirmez, hiçbir kodu atlatmaz/
/// devre dışı bırakmaz — yalnızca "burada böyle bir şey olabilir, elle
/// bakmanız gerekir" der. Yanlış pozitif oranı yüksektir: örn. "VpnService"
/// adı her zaman "tespit" anlamına gelmez, uygulamanın kendi meşru VPN
/// özelliği de olabilir; "CertificatePinner" referansı da her zaman aktif
/// bir sabitleme anlamına gelmeyebilir. Her bulgu ilgili dosyada elle
/// doğrulanmalı ve karar (değiştirmek/değiştirmemek) kullanıcıya aittir.
/// </summary>
public static class KnownPatternCatalog
{
    public const string CategoryVpn = "VPN Tespiti";
    public const string CategoryPinning = "Sertifika Sabitleme (SSL Pinning)";

    public static IReadOnlyList<PatternCatalogEntry> VpnDetection { get; } = new List<PatternCatalogEntry>
    {
        new(CategoryVpn, "VpnService sınıfı",                "VpnService"),
        new(CategoryVpn, "VpnService (smali tip imzası)",    "Landroid/net/VpnService;"),
        new(CategoryVpn, "NetworkCapabilities.TRANSPORT_VPN", "TRANSPORT_VPN"),
        new(CategoryVpn, "ConnectivityManager.TYPE_VPN",      "TYPE_VPN"),
        new(CategoryVpn, "tun arayüz adı (tünel)",            "tun0"),
        new(CategoryVpn, "ppp arayüz adı (tünel)",            "ppp0"),
        new(CategoryVpn, "Ağ arayüzü enumerasyonu",           "getNetworkInterfaces"),
        new(CategoryVpn, "Olası VPN tespit metodu adı",       @"re:(?i)\b(is|check|has|detect)[a-z_]{0,12}vpn[a-z_]*\b"),
        new(CategoryVpn, "WireGuard paket adı",               "com.wireguard"),
        new(CategoryVpn, "OpenVPN (blinkt) paket adı",        "de.blinkt.openvpn"),
        new(CategoryVpn, "OpenVPN (resmi) paket adı",         "net.openvpn"),
        new(CategoryVpn, "Psiphon paket adı",                 "com.psiphon3"),
        new(CategoryVpn, "Tor Project paket adı",             "org.torproject"),
        new(CategoryVpn, "NordVPN paket adı",                 "com.nordvpn"),
        new(CategoryVpn, "ExpressVPN paket adı",              "com.expressvpn"),
        new(CategoryVpn, "TunnelBear paket adı",              "com.tunnelbear"),
    };

    public static IReadOnlyList<PatternCatalogEntry> SslPinning { get; } = new List<PatternCatalogEntry>
    {
        new(CategoryPinning, "OkHttp CertificatePinner",            "CertificatePinner"),
        new(CategoryPinning, "OkHttp CertificatePinner (smali)",    "Lokhttp3/CertificatePinner;"),
        new(CategoryPinning, "X509TrustManager arayüzü",            "X509TrustManager"),
        new(CategoryPinning, "checkServerTrusted metodu",           "checkServerTrusted"),
        new(CategoryPinning, "HostnameVerifier arayüzü",            "HostnameVerifier"),
        new(CategoryPinning, "TrustManagerFactory",                 "TrustManagerFactory"),
        new(CategoryPinning, "SSLContext kullanımı",                "SSLContext"),
        new(CategoryPinning, "Eski PinningTrustManager (Square)",   "PinningTrustManager"),
        new(CategoryPinning, "network_security_config referansı",   "network_security_config"),
        new(CategoryPinning, "SHA-256 pin öneki",                   "sha256/"),
        new(CategoryPinning, "SSLCertificateSocketFactory",         "SSLCertificateSocketFactory"),
        new(CategoryPinning, "X509TrustManagerExtensions",          "X509TrustManagerExtensions"),
    };
}
