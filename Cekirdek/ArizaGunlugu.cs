using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EkranCeviri.Ceviri;

namespace EkranCeviri.Cekirdek;

/// <summary>
/// Zaman aşımı ile kullanıcı iptalini AYIRAN istisna. Motorlar (Faz B)
/// HttpClient zaman aşımını buna çevirir: ikisi de OperationCanceledException
/// olduğu için arıza günlüğünde "iptal" görünüyor, gerçek ağ sorunu
/// sayılmıyordu.
/// </summary>
public sealed class ZamanAsimiHatasi : OperationCanceledException
{
    public ZamanAsimiHatasi(string mesaj) : base(mesaj) { }
}

/// <summary>
/// ARIZA GÜNLÜĞÜ: bir çeviri neden başarısız oldu? Sahip isteği: "Uygulama
/// çeviremediğinde gidip kontrol edebilmen için bir alan olsun, neden
/// çöktüğünü bilelim." Başarısız HER çeviri tek satır olarak ariza.log'a düşer.
/// GİZLİLİK (pazarlık dışı, SECURITY.md): mesaj METNİ yazılmaz — yalnız
/// uzunluk ve sha256'nın ilk 8 hanesi. Metin ANCAK --tani modunda eklenir.
/// Saf: WPF yok, Ayarlar/Gunluk'a bağımlı değil (saf koşucu için).
/// </summary>
public static class ArizaGunlugu
{
    /// <summary>Varsayılan %APPDATA%\EkranCeviri; testler geçici dizine alır.</summary>
    public static string Dizin { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EkranCeviri");

    public const string DosyaAdi = "ariza.log";

    /// <summary>2 MB'ı geçince tek nesil (.1) döndürülür — teşhis günlüğüyle aynı desen.</summary>
    public const long TavanBayt = 2_000_000;

    public static string DosyaYolu => Path.Combine(Dizin, DosyaAdi);

    /// <summary>Program.cs "--tani" ile açar; yalnız o zaman metin yazılır.</summary>
    public static bool TaniModu { get; set; }

    private static readonly object Kilit = new();

    /// <summary>Metnin KİMLİĞİ, metni açığa vurmadan: sha256'nın ilk 8
    /// onaltılık hanesi (küçük harf). "Aynı mesaj yine mi düştü?" sorusu
    /// buradan cevaplanır.</summary>
    public static string MetinKarmasi(string metin)
    {
        var ozet = SHA256.HashData(Encoding.UTF8.GetBytes(metin));
        return Convert.ToHexString(ozet, 0, 4).ToLowerInvariant();
    }

    /// <summary>Tek satır: zaman | aşama | motor | sebep | süre | uzunluk | karma.
    /// <paramref name="taniModu"/> YALNIZ --tani ile true olur; yalnız o
    /// zaman metin eklenir.</summary>
    public static string Satir(DateTime zaman, string asama, string motor, string sebep,
                               int sureMs, string metin, bool taniModu)
    {
        var alanlar = new List<string>
        {
            zaman.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            asama, motor, sebep, $"{sureMs}ms", $"uzunluk={metin.Length}",
            $"karma={MetinKarmasi(metin)}",
        };
        if (taniModu) alanlar.Add("metin=" + metin);
        return string.Join(" | ", alanlar);
    }

    public static void Yaz(string asama, string motor, string sebep, int sureMs, string metin)
    {
        // BOŞ KOVAYA SATIR YOK: aşamasız/sebepsiz kayıt gürültüdür.
        if (string.IsNullOrEmpty(asama) || string.IsNullOrEmpty(sebep)) return;
        // 60 sn'lik teşhis özeti sayaç sözlüğünün TÜM anahtarlarını basar:
        // sebepler orada da görünür.
        Teshis.Paylasilan.Say("ariza-" + sebep);
        var satir = Satir(DateTime.Now, asama, motor, sebep, sureMs, metin, TaniModu)
                    + Environment.NewLine;
        try
        {
            lock (Kilit)
            {
                Directory.CreateDirectory(Dizin);
                var yol = DosyaYolu;
                if (File.Exists(yol) && new FileInfo(yol).Length > TavanBayt)
                    File.Move(yol, yol + ".1", overwrite: true);
                File.AppendAllText(yol, satir, Encoding.UTF8);
            }
        }
        catch
        {
            // Arıza günlüğü yazamamak uygulamayı durdurmaz.
        }
    }

    /// <summary>Hatayı sayılabilir, küçük bir sözlükten gelen sebep etiketine
    /// indirger. Etiket kümesi KÜÇÜK kalmalı: aynı etiket Teşhis sayacı adı oluyor.</summary>
    public static string Sebep(Exception e) => e switch
    {
        GidenRet => "kalite-kapisi-reddetti",
        ZamanAsimiHatasi => "zaman-asimi",
        TimeoutException => "zaman-asimi",
        // HttpClient zaman aşımı .NET'te TaskCanceledException + iç TimeoutException
        OperationCanceledException { InnerException: TimeoutException } => "zaman-asimi",
        OperationCanceledException => "iptal",
        HttpRequestException => "ag-hatasi",
        JsonException => "bos-yanit",
        FormatException => "bos-yanit",
        _ => "bilinmeyen",
    };

    /// <summary>Yazım senkron; macOS'taki kuyruk bekleme imzasıyla uyum için.</summary>
    public static void Bekle() { }
}
