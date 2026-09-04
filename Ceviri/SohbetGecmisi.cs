using System.IO;
using System.Text;
using System.Text.Json;

namespace EkranCeviri.Ceviri;

/// <summary>Sohbet geçmişinin bir satırı: zaman (Unix sn), kim ("ben"/"karsi"),
/// metin ve çevirisi.</summary>
public sealed record GecmisKaydi(long T, string Kim, string Metin, string Ceviri);

/// <summary>
/// YEREL SOHBET HAFIZASI (hafif RAG) — Giden.swift 1-106. Her yeni mesaj
/// JSONL dosyasına yazılır. Cevap önerisi istenince kullanıcının geçmiş
/// cevapları üslup örneği olur; son gelen mesajla kelime kesişimi en yüksek
/// eski konuşmalar bağlam olarak eklenir. Hafıza büyüdükçe öneriler
/// kişiselleşir.
///
/// Dosyaya TÜM erişim tek kilit altında: macOS'ta kırpma (oku-yaz) ile ekleme
/// üç kuyruktan çakışınca yeni kayıt kayboluyordu.
/// <see cref="UslupOrnekleri"/> ve <see cref="BenzerGecmis"/> SAF (liste
/// üzerinde) — saf koşucuda test edilir; dosya üyeleri orada çağrılmaz.
/// Ayarlar/Gunluk'a bağımlı DEĞİL (saf koşucu için); günlük satırı
/// <see cref="GunlukYaz"/> kancasıyla verilir.
/// </summary>
public static class SohbetGecmisi
{
    /// <summary>Varsayılan %APPDATA%\EkranCeviri\sohbet_gecmisi.jsonl; testler
    /// geçici dizine yönlendirir.</summary>
    public static string Yol { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EkranCeviri", "sohbet_gecmisi.jsonl");

    /// <summary>Arşiv sınırı: macOS'ta 8 günde 4,4 MB'a ulaşmıştı, sınırsız
    /// büyüyordu. Aşınca en eski yarısı atılır (üslup örnekleri için son
    /// kayıtlar yeterli).</summary>
    public const long TavanBayt = 5_000_000;

    /// <summary>Günlük kancası (uygulama Gunluk.Yaz bağlar; saf koşucuda boş).</summary>
    public static Action<string>? GunlukYaz { get; set; }

    private static readonly object Kilit = new();

    // Anahtarlar macOS dosyasıyla aynı (t/kim/metin/ceviri): iki sürüm
    // arasında taşınan geçmiş okunabilsin.
    private static readonly JsonSerializerOptions Secenek = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static void Yaz(string kim, string metin, string? ceviri)
    {
        var kayit = new GecmisKaydi(DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                    kim, metin, ceviri ?? "");
        string satir;
        try { satir = JsonSerializer.Serialize(kayit, Secenek) + "\n"; }
        catch (Exception) { return; }
        try
        {
            lock (Kilit)
            {
                var dizin = Path.GetDirectoryName(Yol);
                if (!string.IsNullOrEmpty(dizin)) Directory.CreateDirectory(dizin);
                File.AppendAllText(Yol, satir, Encoding.UTF8);
            }
        }
        catch (Exception e)
        {
            GunlukYaz?.Invoke("geçmiş yazılamadı: " + e.Message);
        }
    }

    /// <summary>Son <paramref name="son"/> kayıt (dosya sırasıyla, eski → yeni).
    /// Bozuk satır atlanır — tek bozuk satır tüm geçmişi düşürmesin.</summary>
    public static List<GecmisKaydi> Oku(int son = 500)
    {
        string[] satirlar;
        try
        {
            lock (Kilit)
            {
                if (!File.Exists(Yol)) return [];
                satirlar = File.ReadAllLines(Yol, Encoding.UTF8);
            }
        }
        catch (Exception e)
        {
            GunlukYaz?.Invoke("geçmiş okunamadı: " + e.Message);
            return [];
        }
        var sonuc = new List<GecmisKaydi>();
        foreach (var s in satirlar.Where(x => x.Length > 0).TakeLast(son))
        {
            try
            {
                var k = JsonSerializer.Deserialize<GecmisKaydi>(s, Secenek);
                if (k is not null) sonuc.Add(k);
            }
            catch (JsonException) { /* bozuk satır atlanır */ }
        }
        return sonuc;
    }

    /// <summary>5 MB'ı aşınca en eski yarı atılır; atomik yazım.</summary>
    public static void Kirp()
    {
        try
        {
            lock (Kilit)
            {
                if (!File.Exists(Yol)) return;
                if (new FileInfo(Yol).Length <= TavanBayt) return;
                var satirlar = File.ReadAllLines(Yol, Encoding.UTF8)
                                   .Where(x => x.Length > 0).ToList();
                var tut = satirlar.Skip(satirlar.Count - satirlar.Count / 2).ToList();
                var gecici = Yol + ".yeni";
                File.WriteAllText(gecici, string.Join("\n", tut) + "\n", Encoding.UTF8);
                File.Move(gecici, Yol, overwrite: true);
                GunlukYaz?.Invoke($"geçmiş: arşiv kırpıldı ({satirlar.Count} → {tut.Count} satır)");
            }
        }
        catch (Exception e)
        {
            GunlukYaz?.Invoke("geçmiş kırpılamadı: " + e.Message);
        }
    }

    public static void Sil()
    {
        try
        {
            lock (Kilit)
                if (File.Exists(Yol)) File.Delete(Yol);
        }
        catch (Exception e)
        {
            GunlukYaz?.Invoke("geçmiş silinemedi: " + e.Message);
        }
    }

    /// <summary>Kullanıcının kendi ("ben") mesajları, EN YENİ ÖNCE, aynı
    /// anahtarla tekrar edenler bir kez; en fazla <paramref name="adet"/>.</summary>
    public static List<string> UslupOrnekleri(IReadOnlyList<GecmisKaydi> kayitlar, int adet = 12)
    {
        var gorulen = new HashSet<string>(StringComparer.Ordinal);
        var ornekler = new List<string>();
        for (int i = kayitlar.Count - 1; i >= 0; i--)
        {
            var k = kayitlar[i];
            if (k.Kim != "ben" || string.IsNullOrEmpty(k.Metin)) continue;
            if (!gorulen.Add(Kalite.Anahtarla(k.Metin))) continue;
            ornekler.Add(k.Metin);
            if (ornekler.Count >= adet) break;
        }
        return ornekler;
    }

    /// <summary>Sorguyla (son gelen mesaj) kelime kesişimi olan eski "karsi"
    /// mesajları ve onları izleyen ilk "ben" cevabı: "KARŞI: …\nBEN: …"
    /// çiftleri, kesişim puanına göre; en fazla <paramref name="adet"/>.
    /// Yalnız 3 harften uzun kelimeler sayılır (bağlaçlar puan üretmesin).</summary>
    public static List<string> BenzerGecmis(IReadOnlyList<GecmisKaydi> kayitlar,
                                            string sorgu, int adet = 3)
    {
        var sorguKelimeleri = new HashSet<string>(
            Kelimeler(sorgu).Where(k => k.Length > 3), StringComparer.Ordinal);
        if (sorguKelimeleri.Count == 0) return [];

        var ciftler = new List<(int Skor, string Metin)>();
        for (int i = 0; i < kayitlar.Count; i++)
        {
            if (kayitlar[i].Kim != "karsi") continue;
            var gelen = kayitlar[i].Metin;
            var kelimeler = new HashSet<string>(Kelimeler(gelen), StringComparer.Ordinal);
            int skor = sorguKelimeleri.Count(kelimeler.Contains);
            if (skor <= 0) continue;
            // Bu gelen mesajı izleyen ilk "ben" cevabını bul (en fazla 3 ileri)
            for (int j = i + 1; j < Math.Min(i + 4, kayitlar.Count); j++)
            {
                if (kayitlar[j].Kim != "ben") continue;
                ciftler.Add((skor, $"KARŞI: {gelen}\nBEN: {kayitlar[j].Metin}"));
                break;
            }
        }
        return ciftler.OrderByDescending(c => c.Skor)
                      .Take(adet).Select(c => c.Metin).ToList();
    }

    /// <summary>Küçük harfli kelimeler; harf dışı her şey ayırıcı.</summary>
    private static IEnumerable<string> Kelimeler(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.ToLowerInvariant())
        {
            if (char.IsLetter(c)) sb.Append(c);
            else if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
        }
        if (sb.Length > 0) yield return sb.ToString();
    }
}
