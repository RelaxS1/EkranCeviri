using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EkranCeviri.Cekirdek;

/// <summary>
/// Kullanıcı ayarları. %APPDATA%\EkranCeviri\config.json içinde tutulur.
/// API ANAHTARI BU DOSYAYA ASLA YAZILMAZ — <see cref="AnahtarKasasi"/>'na
/// bak. macOS sürümünde bu ayrım denetimde zorunlu tutuldu.
/// </summary>
public sealed class Ayarlar
{
    public static string DestekDizini { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EkranCeviri");

    private static string ConfigYolu => Path.Combine(DestekDizini, "config.json");

    // ---- çeviri
    public string HedefDil { get; set; } = "tr";

    /// <summary>"ai" = yalnız Grok · "hizli" = ücretsiz motorlar</summary>
    public string Motor { get; set; } = "ai";

    public string GrokModel { get; set; } = "grok-4.20-0309-non-reasoning";
    public string GrokModelKalite { get; set; } = "grok-4.3";
    public bool HizOnceligi { get; set; }

    // ---- kaynak dil
    /// <summary>alman | isvicre | otomatik</summary>
    public string DilModu { get; set; } = "alman";
    public string KaynakDilKodu { get; set; } = "de";
    public string KaynakDilAdi { get; set; } = "Almanca (lehçe otomatik algılanır)";

    /// <summary>Windows OCR dil etiketi. Almanca yüklü değilse
    /// <see cref="OcrDiliYuklu"/> false döner ve kullanıcı uyarılır.</summary>
    public string OcrDili { get; set; } = "de";

    // ---- kimlik ve üslup (istem kişiselleştirmesi)
    public string BenCinsiyet { get; set; } = "kadin";
    public string KarsiCinsiyet { get; set; } = "erkek";
    public bool Yetiskin { get; set; } = true;
    public bool EmojiSerbest { get; set; } = true;
    public string Kisilik { get; set; } = "";

    // ---- giden mesaj (kısayol)
    /// <summary>"grok" | "hizli"</summary>
    public string GidenMotor { get; set; } = "grok";

    /// <summary>Alfred kuralı: yalnız ilk harf büyük, noktalama yok.</summary>
    public bool GidenKarakter { get; set; } = true;

    /// <summary>Virtual-key kodu. Varsayılan 'C' (0x43).</summary>
    public uint KisayolTus { get; set; } = 0x43;

    /// <summary>MOD_CONTROL|MOD_ALT = 0x0002|0x0001 = 3</summary>
    public uint KisayolMod { get; set; } = 0x0003;

    // ---- onay ve durum
    public bool BulutOnay { get; set; }
    public bool GecmisAcik { get; set; } = true;
    public bool GizlilikGosterildi { get; set; }
    public bool AnahtarSoruldu { get; set; }
    public bool CanliAcik { get; set; } = true;

    // ---- anahtar: JSON'a ASLA yazılmaz
    [JsonIgnore]
    public string GrokApiKey { get; set; } = "";

    private static readonly JsonSerializerOptions Secenek = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder
            .UnsafeRelaxedJsonEscaping,
    };

    public static Ayarlar Yukle()
    {
        Ayarlar a;
        try
        {
            a = File.Exists(ConfigYolu)
                ? JsonSerializer.Deserialize<Ayarlar>(
                      File.ReadAllText(ConfigYolu, Encoding.UTF8), Secenek)
                  ?? new Ayarlar()
                : new Ayarlar();
        }
        catch (Exception e)
        {
            // Bozuk ayar dosyası uygulamayı AÇILMAZ hale getirmemeli.
            Gunluk.Yaz($"ayar okunamadı, varsayılana dönüldü: {e.Message}");
            a = new Ayarlar();
        }
        a.GrokApiKey = AnahtarKasasi.Oku() ?? "";
        return a;
    }

    public void Kaydet()
    {
        try
        {
            Directory.CreateDirectory(DestekDizini);
            // Atomik yazım: yarıda kalan yazma ayarları çöpe çeviriyordu.
            var gecici = ConfigYolu + ".yeni";
            File.WriteAllText(gecici, JsonSerializer.Serialize(this, Secenek),
                              Encoding.UTF8);
            File.Move(gecici, ConfigYolu, overwrite: true);
        }
        catch (Exception e)
        {
            Gunluk.Yaz($"ayar yazılamadı: {e.Message}");
        }
    }
}

/// <summary>
/// API anahtarı kasası. Anahtar DPAPI ile ŞİFRELENİR
/// (<see cref="DataProtectionScope.CurrentUser"/>): dosya kopyalansa bile
/// başka kullanıcı/başka makine çözemez. Düz metin hiçbir yere yazılmaz.
/// </summary>
public static class AnahtarKasasi
{
    private static string Yol => Path.Combine(Ayarlar.DestekDizini, "anahtar.bin");

    /// <summary>Şifrelemeye karışan ek entropi — dosyanın tek başına
    /// başka bir uygulamada çözülmesini zorlaştırır.</summary>
    private static readonly byte[] Tuz =
        Encoding.UTF8.GetBytes("EkranCeviri.anahtar.v1");

    public static string? Oku()
    {
        try
        {
            if (!File.Exists(Yol)) return null;
            var sifreli = File.ReadAllBytes(Yol);
            if (sifreli.Length == 0) return null;
            var acik = ProtectedData.Unprotect(
                sifreli, Tuz, DataProtectionScope.CurrentUser);
            var s = Encoding.UTF8.GetString(acik).Trim();
            return s.Length == 0 ? null : s;
        }
        catch (Exception e)
        {
            Gunluk.Yaz($"anahtar okunamadı: {e.Message}");
            return null;
        }
    }

    public static void Kaydet(string deger)
    {
        try
        {
            Directory.CreateDirectory(Ayarlar.DestekDizini);
            var sifreli = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(deger), Tuz,
                DataProtectionScope.CurrentUser);
            File.WriteAllBytes(Yol, sifreli);
        }
        catch (Exception e)
        {
            Gunluk.Yaz($"anahtar yazılamadı: {e.Message}");
        }
    }

    public static void Sil()
    {
        try { if (File.Exists(Yol)) File.Delete(Yol); }
        catch (Exception e) { Gunluk.Yaz($"anahtar silinemedi: {e.Message}"); }
    }

    /// <summary>Anahtarı ASLA tam gösterme — yalnız hangisi olduğunu
    /// ayırt etmeye yetecek kadarı.</summary>
    public static string Maske(string anahtar) =>
        anahtar.Length <= 12 ? "xai-••••"
                             : anahtar[..8] + "…" + anahtar[^4..];

    /// <summary>xAI anahtar biçimi. Yanlış yapıştırılan anahtar sessizce
    /// her çeviriyi bozuyordu; biçimi girişte tut.</summary>
    public static bool BicimGecerli(string anahtar) =>
        anahtar.StartsWith("xai-", StringComparison.Ordinal) && anahtar.Length > 20;
}
