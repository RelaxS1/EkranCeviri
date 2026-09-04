using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EkranCeviri.Cekirdek;

/// <summary>
/// Kullanıcı ayarları. %APPDATA%\EkranCeviri\config.json içinde tutulur.
/// API ANAHTARI BU DOSYAYA ASLA YAZILMAZ — <see cref="AnahtarKasasi"/>'na
/// bak. macOS sürümünde bu ayrım denetimde zorunlu tutuldu.
/// </summary>
public sealed partial class Ayarlar
{
    public static string DestekDizini { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EkranCeviri");

    private static string ConfigYolu => Path.Combine(DestekDizini, "config.json");

    /// <summary>QA/sınama modlarından biri açıksa true. Ayarlar bu hâlde
    /// bellekte değiştirildiği için DİSKE YAZILMAZ (bkz. <see cref="Kaydet"/>).
    /// Komut satırı süreç ömrü boyunca sabit olduğundan bir kez okunur.</summary>
    public static bool SinamaModuAktif { get; } =
        Environment.GetCommandLineArgs().Any(a => a is "--sinama" or "--gizli-sinama");

    // ---- çeviri
    public string HedefDil { get; set; } = "tr";

    /// <summary>"ai" = yalnız Grok · "hizli" = ücretsiz motorlar</summary>
    public string Motor { get; set; } = "ai";

    public string GrokModel { get; set; } = "grok-4.20-0309-non-reasoning";
    public string GrokModelKalite { get; set; } = "grok-4.3";
    public bool HizOnceligi { get; set; }

    /// <summary>HIZLI-ÖNCE / KALİTE-SONRA: canlıda ve ilk çeviride önce hızlı
    /// modelle göster, sonra kalite modeliyle arka planda düzelt (farklıysa
    /// tek yeniden çizim; kalıcı hafızaya yalnız kalite sonucu). Ek ücret
    /// yalnız ucuz hızlı çağrı — kullanıcı "para yakma modu" dedi, gecikme
    /// şikâyet etti.</summary>
    public bool KaliteSonra { get; set; } = true;

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
    /// <summary>"grok" | "bing". Eski dosyalardan gelen "hizli" yüklemede
    /// "bing"e çevrilir.</summary>
    public string GidenMotor { get; set; } = "grok";

    /// <summary>Giden mesaj için AYRI hız anahtarı: müşteriye giden metin
    /// varsayılan olarak KALİTE modeliyle (lehçe doğruluğu > hız).</summary>
    public bool GidenHizOnceligi { get; set; }

    /// <summary>Giden mesaj için ayrı üslup metni; boşsa <see cref="Kisilik"/>
    /// kullanılır. (Mac `gidenKarakter` dizesi — Windows'ta ad çakışmasın diye
    /// ayrı ad; JSON anahtarı yeni.)</summary>
    public string GidenKarakterMetni { get; set; } = "";

    /// <summary>Alfred kuralı: yalnız ilk harf büyük, noktalama yok.
    /// JSON anahtarı DEĞİŞMEZ (mevcut kullanıcı ayarları).</summary>
    public bool GidenKarakter { get; set; } = true;

    /// <summary>Giden çevirinin ETKİN motoru. ÜCRETSİZ GERÇEKTEN ÜCRETSİZ
    /// (sahip kararı #7): ücretsiz motor seçen kullanıcı kısayolla xAI'ye
    /// ÇIKMAZ.</summary>
    [JsonIgnore]
    public string EtkinGidenMotor => Motor == "ai" ? GidenMotor : "bing";

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
        a.Dogrula();
        return a;
    }

    [GeneratedRegex("^[a-z]{2}(-[A-Z]{2})?$")]
    private static partial Regex DilKoduDeseni();

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex ModelAdiDeseni();

    /// <summary>
    /// Dosyadan gelen değerleri izin listesi/aralıkla sınırlar: bozuk bir
    /// config.json uygulamayı açılamaz hâle getirmesin ve model adı gibi API
    /// gövdesine giden alanlar yalnız güvenli karakter taşısın. Diske DOKUNMAZ.
    /// </summary>
    public void Dogrula()
    {
        var varsayilan = new Ayarlar();
        // JSON'da açıkça null yazılmış dize alanı NRE ile açılışı düşürmesin
        HedefDil ??= ""; Motor ??= ""; GidenMotor ??= ""; DilModu ??= "";
        BenCinsiyet ??= ""; KarsiCinsiyet ??= ""; KaynakDilKodu ??= "";
        OcrDili ??= ""; GrokModel ??= ""; GrokModelKalite ??= "";
        Kisilik ??= ""; GidenKarakterMetni ??= ""; KaynakDilAdi ??= "";
        if (!Diller.Gecerli(HedefDil)) HedefDil = "tr";
        if (Motor is not ("ai" or "hizli")) Motor = "ai";
        // Eski dosyalar giden motoru "hizli" yazıyordu; değer kümesi artık grok|bing
        if (GidenMotor == "hizli") GidenMotor = "bing";
        if (GidenMotor is not ("grok" or "bing")) GidenMotor = "grok";
        if (DilModu is not ("alman" or "isvicre" or "otomatik")) DilModu = "alman";
        if (BenCinsiyet is not ("kadin" or "erkek" or "yok")) BenCinsiyet = "yok";
        if (KarsiCinsiyet is not ("kadin" or "erkek" or "yok")) KarsiCinsiyet = "yok";
        if (!DilKoduDeseni().IsMatch(KaynakDilKodu)) KaynakDilKodu = "de";
        if (!DilKoduDeseni().IsMatch(OcrDili)) OcrDili = "de";
        // Sanal tuş kodu 1..255; dışı kısayolu hiç kaydettirmiyordu
        if (KisayolTus is < 1 or > 255) KisayolTus = 0x43;
        KisayolMod &= 0x000F;
        if (KisayolMod == 0) KisayolMod = 0x0003;
        if (!ModelAdiDeseni().IsMatch(GrokModel)) GrokModel = varsayilan.GrokModel;
        if (!ModelAdiDeseni().IsMatch(GrokModelKalite)) GrokModelKalite = varsayilan.GrokModelKalite;
        if (Kisilik.Length > 2000) Kisilik = Kisilik[..2000];
        if (GidenKarakterMetni.Length > 2000) GidenKarakterMetni = GidenKarakterMetni[..2000];
        if (KaynakDilAdi.Length > 120) KaynakDilAdi = KaynakDilAdi[..120];
    }

    public void Kaydet()
    {
        // SINAMA KİLİDİ: QA modları ayarları bellekte DEĞİŞTİRİYOR (motor,
        // giden motor, anahtar). Bu hâlde herhangi bir menü tıklaması
        // kullanıcının GERÇEK ayarını bozuyordu: Grok → ücretsiz motora
        // düşüyordu ve kullanıcı bunu hiç fark etmiyordu (Mac ekran testinde
        // yakalandı).
        if (SinamaModuAktif)
        {
            Gunluk.Yaz("ayar: sınama modu — diske yazılmadı");
            return;
        }
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
