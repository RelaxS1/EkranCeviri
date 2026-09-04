using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using EkranCeviri.Cekirdek;

namespace EkranCeviri.Ceviri;

/// <summary>
/// Kalıcı çeviri hafızası. Kullanıcı aynı kişiyle her gün konuşuyor ve aynı
/// cümleler tekrar ediyor: hafıza önce sorulunca tekrarlar ANINDA geliyor,
/// bu da pahalı/kaliteli modeli varsayılan yapmayı mümkün kılıyor.
///
/// Hafızaya YALNIZ güvenilir kaynak (Grok) yazılır — makine motoru çıktısı
/// hafızayı zehirliyordu ve yanlış çeviri kalıcı hale geliyordu.
/// </summary>
public sealed class Hafiza
{
    /// <summary>Kayıt biçimi değişince eski dosya SİLİNİR. Uyumsuz kayıtlar
    /// yanlış çeviri gösteriyordu.</summary>
    private const int Surum = 2;
    private const int EnFazlaKayit = 3000;
    private const int EnFazlaUretilen = 8000;

    private static string Yol => Path.Combine(Ayarlar.DestekDizini, "hafiza.json");

    private readonly ConcurrentDictionary<string, string> _kayitlar = new(StringComparer.Ordinal);

    /// <summary>Kendi ürettiğimiz çevirilerin anahtarları — ZEHİR KALKANI.
    /// Katmanımız ekran yakalamasına sızarsa kendi çevirimizi okuyup
    /// tekrar çeviriyoruz; bunu buradan anlıyoruz.</summary>
    private readonly ConcurrentDictionary<string, byte> _uretilenler = new(StringComparer.Ordinal);

    private readonly object _diskKilidi = new();
    private bool _kirli;
    private DateTime _sonYazim = DateTime.MinValue;

    public Hafiza() => Yukle();

    public int Adet => _kayitlar.Count;

    private void Yukle()
    {
        try
        {
            if (!File.Exists(Yol)) return;
            using var akis = File.OpenRead(Yol);
            using var belge = JsonDocument.Parse(akis);
            var kok = belge.RootElement;

            if (!kok.TryGetProperty("surum", out var s) || s.GetInt32() != Surum)
            {
                Gunluk.Yaz("hafıza sürümü uyumsuz, sıfırlandı");
                akis.Dispose();
                File.Delete(Yol);
                return;
            }
            if (!kok.TryGetProperty("kayitlar", out var k)) return;
            foreach (var alan in k.EnumerateObject())
            {
                var deger = alan.Value.GetString();
                if (!string.IsNullOrEmpty(deger)) _kayitlar[alan.Name] = deger;
            }
            Gunluk.Yaz($"hafıza yüklendi: {_kayitlar.Count} kayıt");
        }
        catch (Exception e)
        {
            Gunluk.Hata("hafızaYükle", e);
            // Bozuk hafıza dosyası uygulamayı açılmaz hale getirmemeli
            try { File.Delete(Yol); } catch { /* yok sayılır */ }
        }
    }

    /// <summary>Önce birebir, yoksa OCR titremesine toleranslı bulanık arama.</summary>
    public string? Bul(string anahtar)
    {
        if (_kayitlar.TryGetValue(anahtar, out var tam) && tam.Length > 0)
            return tam;
        return BulanikBul(anahtar, _kayitlar);
    }

    /// <summary>
    /// Bulanık eşleşme. Kuralların HEPSİ bir hatanın sonucudur:
    /// • 20 karakterden kısa anahtarda YOK — kısa metinlerde yanlış çeviri riski
    /// • Rakam dizileri EŞİT olmalı — "30 dk 55.-" ile "60 dk 85.-" tek
    ///   karakter farkla eşleşip YANLIŞ fiyat gösteriyordu
    /// • İlk bulunan değil EN İYİ aday — sözlük yineleme sırası belirsiz
    ///   olduğu için aynı mesaja her açılışta FARKLI çeviri veriyordu
    /// TEK KOPYA: oturum önbelleği (Cevirmen) de aynı aramayı kullanır —
    /// macOS'ta kopya mantık test edilip üretimle çelişmişti; bu yüzden
    /// statik ve paylaşılır.
    /// </summary>
    internal static string? BulanikBul(string anahtar,
                                       IReadOnlyDictionary<string, string> sozluk)
    {
        if (anahtar.Length < 20) return null;
        var rakam = Kalite.Rakamlari(anahtar);
        const int tolerans = 2;

        int bakilan = 0;
        int enIyiMesafe = int.MaxValue;
        string? enIyi = null;

        foreach (var (k, v) in sozluk)
        {
            if (Math.Abs(k.Length - anahtar.Length) > tolerans) continue;
            // Tarama üst sınırı: hafıza büyüdükçe canlı modu yavaşlatmasın
            if (++bakilan > 400) break;
            if (v.Length == 0) continue;
            if (Kalite.Rakamlari(k) != rakam) continue;

            for (int mesafe = 0; mesafe <= tolerans; mesafe++)
            {
                if (!Kalite.MesafeAzMi(k, anahtar, mesafe)) continue;
                if (mesafe < enIyiMesafe) { enIyiMesafe = mesafe; enIyi = v; }
                break;
            }
            if (enIyiMesafe == 0) break;   // daha iyisi olamaz
        }
        return enIyi;
    }

    /// <summary>
    /// Kalıcı hafızaya yazar. ÜÇ KORUMA (hepsi hafıza zehirlenmesi
    /// yaşandıktan sonra konuldu — kirli kayıtlar sonsuza kadar servis
    /// edilip "çeviri hatalı" şikayetine yol açıyordu).
    /// <paramref name="kaynakMetin"/> verilirse Türkçe denetimi ONUN üzerinde
    /// yapılır (Mac 466-467): anahtar noktalama/boşluk arındırılmış olduğu
    /// için kelime kümesi denetimi anahtarda ıskalıyor, harf denetimi ise
    /// ham metinde daha güvenilir.
    /// </summary>
    public void Ata(string anahtar, string ceviri, string kaynakMetin = "")
    {
        if (string.IsNullOrWhiteSpace(anahtar) || string.IsNullOrWhiteSpace(ceviri))
            return;

        // 1) Çok kısa anahtar kaydetme: "k → tamam" gibi kayıtlar bulanık
        //    eşleşmeyle HER kısa mesaja yanlış çeviri döndürüyordu.
        if (anahtar.Length < 4) return;

        // 2) Kaynak zaten Türkçeyse uygulamanın KENDİ çıktısını okumuşuz
        //    demektir (katman yakalamaya sızmış). Kaydetmek hafızayı
        //    kirletir ve kendi çevirimizi "kaynak metin" yapar.
        if (Kalite.TurkceKalintiVar(kaynakMetin.Length == 0 ? anahtar : kaynakMetin))
            return;

        // 3) Çeviri kaynağın aynısıysa değersiz — yer kaplar, bulanık
        //    eşleşmeyi bozar.
        if (Kalite.Anahtarla(ceviri) == anahtar) return;

        // Aynı değer zaten kayıtlıysa diski boşuna kirletme.
        if (_kayitlar.TryGetValue(anahtar, out var eski) && eski == ceviri) return;

        _kayitlar[anahtar] = ceviri;
        UretilenIsaretle(Kalite.Anahtarla(ceviri));
        _kirli = true;

        if (_kayitlar.Count > EnFazlaKayit) Kirp();
    }

    /// <summary>✨ ile yeniden çevrilen metin hafızayı GÜNCELLEMELİ — eski
    /// kaydı da düzeltir. TEK YAZMA KAPISI: macOS'ta ayrı bir yol `yaz`ın üç
    /// hijyen kapısını atlıyordu; gerçek dosyada 16 öksüz / 11 yasak kısa
    /// anahtar ölçüldü.</summary>
    public void Guncelle(string anahtar, string ceviri, string kaynakMetin = "") =>
        Ata(anahtar, ceviri, kaynakMetin);

    /// <summary>Bu metni BİZ mi ürettik? (zehir kalkanı)</summary>
    public bool UretilenMi(string anahtar) => _uretilenler.ContainsKey(anahtar);

    /// <summary>Ürettiğimiz çevirinin normalize anahtarını zehir kalkanı
    /// kümesine ekler. Küme HİÇ KIRPILMIYORDU (kullanıcı uygulamayı günlerce
    /// kapatmıyor): 8000'i aşınca en eski dörtte biri deterministik (anahtar
    /// sırası) atılır.</summary>
    public void UretilenIsaretle(string ceviriAnahtari)
    {
        if (string.IsNullOrEmpty(ceviriAnahtari)) return;
        _uretilenler[ceviriAnahtari] = 1;
        if (_uretilenler.Count <= EnFazlaUretilen) return;
        foreach (var k in _uretilenler.Keys.Order(StringComparer.Ordinal)
                                      .Take(EnFazlaUretilen / 4).ToList())
            _uretilenler.TryRemove(k, out _);
    }

    private void Kirp()
    {
        // Kullanım sıklığı tutmuyoruz (her erişimde yazmak canlı modda
        // pahalı); rastgele değil DETERMİNİSTİK olarak anahtar sırasına
        // göre en eski çeyreği atıyoruz. Basit ama öngörülebilir.
        var atilacak = _kayitlar.Keys.Order(StringComparer.Ordinal)
                                     .Take(EnFazlaKayit / 4).ToList();
        foreach (var k in atilacak) _kayitlar.TryRemove(k, out _);
        Gunluk.Yaz($"hafıza kırpıldı: {atilacak.Count} kayıt atıldı");
    }

    /// <summary>
    /// Diske yazar. Canlı modda saniyede bir çağrılabildiği için hem
    /// "kirli" bayrağı hem de en az 5 saniyelik aralık uygulanır — her
    /// turda diske yazmak SSD'yi ve pili boşuna yoruyordu.
    /// </summary>
    public void Kaydet(bool zorla = false)
    {
        lock (_diskKilidi)
        {
            if (!_kirli && !zorla) return;
            if (!zorla && DateTime.UtcNow - _sonYazim < TimeSpan.FromSeconds(5))
                return;
            try
            {
                Directory.CreateDirectory(Ayarlar.DestekDizini);
                var veri = new Dictionary<string, object>
                {
                    ["surum"] = Surum,
                    ["kayitlar"] = _kayitlar.ToDictionary(x => x.Key, x => x.Value),
                };
                var gecici = Yol + ".yeni";
                File.WriteAllText(gecici,
                    JsonSerializer.Serialize(veri, new JsonSerializerOptions
                    {
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder
                            .UnsafeRelaxedJsonEscaping,
                    }), Encoding.UTF8);
                // Atomik: yarıda kalan yazım hafızayı çöpe çeviriyordu
                File.Move(gecici, Yol, overwrite: true);
                _kirli = false;
                _sonYazim = DateTime.UtcNow;
            }
            catch (Exception e)
            {
                Gunluk.Hata("hafızaKaydet", e);
            }
        }
    }

    public void Temizle()
    {
        _kayitlar.Clear();
        _uretilenler.Clear();
        _kirli = true;
        Kaydet(zorla: true);
    }
}
