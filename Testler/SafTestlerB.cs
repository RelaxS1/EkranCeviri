using System.IO;
using EkranCeviri.Ceviri;
using static EkranCeviri.Testler.SafYardimci;

namespace EkranCeviri.Testler;

/// <summary>
/// FAZ B (motorlar) saf testleri — macOS Testler/main.swift 37, 84-99,
/// 682-723, 811-818'den taşındı; kesik yanıt kurtarma ve sohbet geçmişi
/// (üslup örnekleri, benzer geçmiş) burada eklendi. Ağ YOK: yalnız yanıt
/// çözücü, oturum önbelleği, giden motor seçimi ve liste üzerinde çalışan
/// geçmiş fonksiyonları. Her kontrol ÜRETİM fonksiyonunu çağırır (kopya
/// mantık test edilip üretimle çelişmişti — macOS denetim bulgusu).
/// </summary>
public static class SafTestlerB
{
    public static void Kos()
    {
        CitTemizleme();
        HizaKorumasi();
        KesikYanitKurtarma();
        YanitCozme();
        OturumOnbellegiNesil();
        OturumOnbellegiEszamanli();
        OturumOnbellegiTavan();
        GidenMotorSecimi();
        UslupOrnekleriTestleri();
        BenzerGecmisTestleri();
        SohbetGecmisiDosya();
    }

    /// <summary>Dizi kıyası tek dizgeyle: boş yuva (null) "" sayılır —
    /// Mac dizisi boş yuvayı "" tutuyordu, .NET null; anlam aynı.</summary>
    private static string Dizi(IEnumerable<string?>? d) =>
        d is null ? "<null>" : string.Join("|", d.Select(x => x ?? ""));

    // ---------------- çit temizleme (Mac 37) ----------------

    private static void CitTemizleme()
    {
        Baslik("Grok yanıtı çit temizleme");
        Esit("[\"a\"]", YanitCozucu.CitleriAt("```json\n[\"a\"]\n```"), "çit temizleme");
        Esit("{\"x\":1}", YanitCozucu.CitleriAt("  {\"x\":1}  "),
             "çitsiz yanıt yalnız kırpılır");
    }

    // ---------------- hiza koruması (Mac 84-99) ----------------

    private static void HizaKorumasi()
    {
        Baslik("Hiza koruması — ÜRETİM fonksiyonu (kopya değil)");
        Esit("bir|iki",
             Dizi(YanitCozucu.CevirileriYerlestir([(1, "bir"), (2, "iki")], 2)),
             "1-tabanlı indeks kaymaz");
        Esit("bir|iki|üç",
             Dizi(YanitCozucu.CevirileriYerlestir([(2, "üç"), (0, "bir"), (1, "iki")], 3)),
             "karışık sıra indeksle düzelir");
        Esit("||iki",
             Dizi(YanitCozucu.CevirileriYerlestir([(2, "iki")], 3)),
             "eksik öğe doğru yere");
        Esit("a|b",
             Dizi(YanitCozucu.CevirileriYerlestir([(null, "a"), (null, "b")], 2)),
             "indeks yoksa dizi sırası");
        // Model adet'ten FAZLA kayıt döndürünce (denetim bulgusu): 0 indeksi
        // varsa dizi 0-tabanlıdır, kaydırılmaz — Mac'te [iki|üç] çıkıyordu.
        Esit("bir|iki",
             Dizi(YanitCozucu.CevirileriYerlestir([(0, "bir"), (1, "iki"), (2, "üç")], 2)),
             "fazla 0-tabanlı kayıt kaydırılmaz");
        Esit("bir|iki",
             Dizi(YanitCozucu.CevirileriYerlestir([(1, "bir"), (2, "iki"), (3, "üç")], 2)),
             "fazla 1-tabanlı kayıt bir kaydırılır");
        Esit("bir||",
             Dizi(YanitCozucu.CevirileriYerlestir([(0, "bir"), (3, "dört")], 3)),
             "0 indeksi varken aralık dışı kayıt düşer");
    }

    // ---------------- kesik yanıt kurtarma ----------------

    private static void KesikYanitKurtarma()
    {
        Baslik("Kesik JSON kurtarma (max_tokens'a takılan yanıt)");
        // Üçüncü nesne yarım kaldı: ilk ikisi kurtarılır, üçüncü boş kalır ve
        // tamamlama turu onu alır. Eskiden TÜM parti sessizce düşüyordu.
        const string yarim = "{\"ceviriler\":[{\"indeks\":0,\"standart\":\"a\",\"ceviri\":\"bir\"},"
                           + "{\"indeks\":1,\"standart\":\"b\",\"ceviri\":\"iki\"},"
                           + "{\"indeks\":2,\"standart\":\"c\",\"cev";
        Esit("bir|iki|", Dizi(YanitCozucu.KesikJsondanKurtar(yarim, 3)),
             "yarım JSON'dan 2/3 satır kurtarılır");
        const string tam = "{\"ceviriler\":[{\"indeks\":0,\"ceviri\":\"bir\"},{\"indeks\":1,\"ceviri\":\"iki\"}]}";
        Esit("bir|iki", Dizi(YanitCozucu.KesikJsondanKurtar(tam, 2)),
             "tam JSON'da dış zarf kayıt sayılmaz, iç nesneler yerleşir");
        Dogru(YanitCozucu.KesikJsondanKurtar("bu bir açıklama metni {\"x\": 1}", 2) is null,
              "hiç 'ceviri' nesnesi yoksa null");
        // Dizge içindeki süslü parantez nesne sınırı sanılmaz.
        Esit("a } b|",
             Dizi(YanitCozucu.KesikJsondanKurtar("[{\"indeks\":0,\"ceviri\":\"a } b\"},{\"ind", 2)),
             "dizge içindeki '}' nesne sınırı sayılmaz");
    }

    // ---------------- tam çözümleme ----------------

    private static void YanitCozme()
    {
        Baslik("Grok yanıtı çözümleme (ana yol + yedek yollar)");
        Esit("bir|iki",
             Dizi(YanitCozucu.Coz("```json\n{\"ceviriler\":[{\"indeks\":1,\"ceviri\":\"bir\"},{\"indeks\":2,\"ceviri\":\"iki\"}]}\n```", 2)),
             "çitli + 1-tabanlı nesne dizisi doğru yerleşir");
        Esit("a|b", Dizi(YanitCozucu.Coz("[\"a\",\"b\"]", 2)),
             "düz dize dizisi yedek yolu");
        Esit("bir|", Dizi(YanitCozucu.Coz("{\"ceviriler\":[{\"indeks\":0,\"ceviri\":\"bir\"},{\"indeks\":1,\"cev", 2)),
             "çözülemeyen yanıt kesik kurtarmaya düşer");
        Dogru(YanitCozucu.Coz("Üzgünüm, çeviremiyorum.", 2) is null,
              "JSON olmayan yanıt null (çağıran bos-yanit sayar)");
        Esit(1, YanitCozucu.DoluSayisi(["bir", null, ""]), "dolu satır sayımı");
    }

    // ---------------- oturum önbelleği (Mac 682-723) ----------------

    private static void OturumOnbellegiNesil()
    {
        Baslik("Çeviri önbelleği (OturumOnbellegi) — nesil kapısı");
        // Arka planda biten iş, başlangıcındaki nesille birleşmeye gelir; arada
        // Temizle olduysa (kullanıcı yeni bölge seçti) YAZAMAMALI. Eskiden
        // "kopya al / toptan yaz" deseni silinen çevirileri geri getiriyordu.
        var o = new OturumOnbellegi();
        o["a"] = "bir";
        var (kopya, nesil) = o.Kopyala();
        Esit("bir", kopya.GetValueOrDefault("a") ?? "", "kopya alındı");
        Dogru(o.Birlestir(new Dictionary<string, string> { ["b"] = "iki" }, nesil),
              "aynı nesille birleştirme yazar");
        Esit("iki", o["b"] ?? "", "yeni anahtar girdi");
        o.Temizle();
        Esit(0, o.Sayi, "temizle sözlüğü boşaltır");
        Dogru(!o.Birlestir(new Dictionary<string, string> { ["c"] = "üç" }, nesil),
              "ESKİ nesille birleştirme REDDEDİLİR");
        Esit(0, o.Sayi, "reddedilen birleştirme hiçbir şey yazmadı");
        var (_, yeniNesil) = o.Kopyala();
        Dogru(yeniNesil != nesil, "temizle nesli ilerletti");
        Dogru(o.Birlestir(new Dictionary<string, string> { ["d"] = "dört" }, yeniNesil),
              "yeni nesille birleştirme yazar");
        Esit("dört", o["d"] ?? "", "yazıldı");
        o.Sil("d");
        Dogru(o["d"] is null, "sil anahtarı düşürür");
    }

    private static void OturumOnbellegiEszamanli()
    {
        // Eşzamanlı yazım: kilitsiz sözlük burada ÇÖKÜYORDU. Toplam
        // 2400 < tavan (3000), yani yaşlandırma devreye girmez; eksik sayı
        // doğrudan kayıp yazım demektir.
        var o = new OturumOnbellegi();
        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, p =>
        {
            for (int i = 0; i < 300; i++) o[$"{p}-{i}"] = "v";
        });
        Esit(2400, o.Sayi, "8 iş parçacığından 2400 yazım eksiksiz");
    }

    private static void OturumOnbellegiTavan()
    {
        // Tavan aşılınca EN ESKİ kayıtlar atılır, taze olanlar kalır (sırasız
        // sözlükte rastgele silme "az önce çevrilen mesaj kayboldu" demekti).
        var o = new OturumOnbellegi();
        for (int i = 0; i <= 3000; i++) o[$"k{i}"] = $"v{i}";
        Dogru(o.Sayi <= 3000, "tavan aşılınca kırpılır");
        Dogru(o["k0"] is null, "en eski kayıt atıldı");
        Esit("v3000", o["k3000"] ?? "", "en taze kayıt duruyor");
        Esit(2000, o.Sayi, "kırpma hedefi 2000 (en eski 1000 atılır)");
    }

    // ---------------- giden motor seçimi (Mac 811-818) ----------------

    private static void GidenMotorSecimi()
    {
        Baslik("Yazdığımı Çevir motor seçimi ana motoru izler");
        // Giden motorun menüsü YOK ve varsayılanı "grok"; ücretsiz seçen
        // kullanıcı bunu göremeden ücret ödüyordu.
        Esit("grok", GidenKapisi.MotorSecimi("ai", "grok"),
             "ai motorunda giden çeviri Grok'ta kalır");
        Esit("bing", GidenKapisi.MotorSecimi("ai", "bing"),
             "ai motorunda açık bing tercihi korunur");
        Esit("bing", GidenKapisi.MotorSecimi("hizli", "grok"),
             "ücretsiz motorda giden çeviri de ücretsiz olur");
    }

    // ---------------- sohbet geçmişi: üslup örnekleri ----------------

    private static GecmisKaydi K(long t, string kim, string metin) => new(t, kim, metin, "");

    private static void UslupOrnekleriTestleri()
    {
        Baslik("Sohbet geçmişi — üslup örnekleri");
        var kayitlar = new List<GecmisKaydi>
        {
            K(1, "karsi", "Hoi, wie gahts?"),
            K(2, "ben", "Guet und dir"),
            K(3, "karsi", "Au guet"),
            K(4, "ben", "guet und dir!"),        // aynı anahtar: bir kez
            K(5, "ben", ""),                     // boş: atlanır
            K(6, "ben", "Chunnsch morn?"),
        };
        var u = SohbetGecmisi.UslupOrnekleri(kayitlar);
        Esit("Chunnsch morn?|guet und dir!", string.Join("|", u),
             "en yeni önce, tekrar eden 'ben' mesajı bir kez, boş ve 'karsi' hariç");
        Esit(2, u.Count, "tekrar sayılmaz");

        var cok = Enumerable.Range(0, 20).Select(i => K(i, "ben", $"mesaj {i}")).ToList();
        var sinir = SohbetGecmisi.UslupOrnekleri(cok);
        Esit(12, sinir.Count, "12 sınırı");
        Esit("mesaj 19", sinir[0], "sınırlı listede de en yeni önce");
        Esit(3, SohbetGecmisi.UslupOrnekleri(cok, 3).Count, "adet parametresi uygulanır");
        Esit(0, SohbetGecmisi.UslupOrnekleri([]).Count, "boş geçmiş boş liste");
    }

    // ---------------- sohbet geçmişi: benzer geçmiş ----------------

    private static void BenzerGecmisTestleri()
    {
        Baslik("Sohbet geçmişi — benzer konuşma (kelime kesişimi)");
        var kayitlar = new List<GecmisKaydi>
        {
            K(1, "karsi", "chunnsch du morn is büro"),      // 2 kesişim: chunnsch, morn
            K(2, "ben", "ja i chume morn"),
            K(3, "karsi", "hesch hunger"),                  // kesişim yok
            K(4, "ben", "nei"),
            K(5, "karsi", "wo bisch morn"),                 // 1 kesişim: morn
            K(6, "karsi", "hallo?"),                        // araya giren karsi
            K(7, "ben", "dihei"),
            K(8, "karsi", "chunnsch hüt"),                  // 1 kesişim: chunnsch
            K(9, "karsi", "x"), K(10, "karsi", "y"), K(11, "karsi", "z"),
            K(12, "ben", "geç cevap"),                      // 3 ileriden sonra: eşleşmez
        };
        var b = SohbetGecmisi.BenzerGecmis(kayitlar, "KARŞI: chunnsch au morn?");
        Esit(2, b.Count, "cevabı olan kesişimli konuşmalar (izleyen 3 kayıt içinde 'ben' yoksa düşer)");
        Esit("KARŞI: chunnsch du morn is büro\nBEN: ja i chume morn", b[0],
             "en yüksek kesişim puanı önce; biçim KARŞI/BEN");
        Esit("KARŞI: wo bisch morn\nBEN: dihei", b[1],
             "izleyen İLK 'ben' cevabı (araya giren 'karsi' atlanır)");
        Esit(0, SohbetGecmisi.BenzerGecmis(kayitlar, "wo au").Count,
             "3 harf ve kısa kelimeler puan üretmez (bağlaç filtresi)");

        // Tavan: 3'ten fazla aday olsa da en fazla 3 döner.
        var cok = new List<GecmisKaydi>();
        for (int i = 0; i < 6; i++)
        {
            cok.Add(K(i * 2, "karsi", $"morn träffe {i}"));
            cok.Add(K(i * 2 + 1, "ben", $"okey {i}"));
        }
        Esit(3, SohbetGecmisi.BenzerGecmis(cok, "morn träffe").Count, "en fazla 3 çift");
        Esit(6, SohbetGecmisi.BenzerGecmis(cok, "morn träffe", 6).Count, "adet parametresi uygulanır");
    }

    /// <summary>Dosya yolu: Yaz seri arka plan zincirine bırakır (Mac
    /// gecmisKuyrugu), Oku/Sil zinciri boşaltır — yaz→oku sırası tutarlı,
    /// silinen dosya bekleyen eklemeyle geri gelmez. Geçici dizine yönlendirilir.</summary>
    private static void SohbetGecmisiDosya()
    {
        Baslik("Sohbet geçmişi — dosya (seri kuyruk, yaz→oku, sil)");
        var eskiYol = SohbetGecmisi.Yol;
        var dizin = Path.Combine(Path.GetTempPath(), "ec_saf_gecmis_" + Environment.ProcessId);
        SohbetGecmisi.Yol = Path.Combine(dizin, "sohbet_gecmisi.jsonl");
        try
        {
            SohbetGecmisi.Sil();
            for (int i = 0; i < 50; i++)
                SohbetGecmisi.Yaz(i % 2 == 0 ? "karsi" : "ben", $"mesaj {i}", i % 2 == 0 ? $"çeviri {i}" : null);
            var okunan = SohbetGecmisi.Oku();
            Esit(50, okunan.Count, "50 yazım → 50 kayıt (Oku kuyruğu boşaltır, kayıp yok)");
            Dogru(okunan.Select(k => k.Metin).SequenceEqual(
                      Enumerable.Range(0, 50).Select(i => $"mesaj {i}")),
                  "ekleme sırası korunur (seri zincir)");
            Esit("çeviri 0", okunan[0].Ceviri, "çeviri alanı yazılır");
            Esit("", okunan[1].Ceviri, "null çeviri boş dizge olur (Mac ceviri ?? \"\")");
            Esit("ben", okunan[1].Kim, "kim alanı");
            Esit(5, SohbetGecmisi.Oku(5).Count, "son N kaydı verir");
            Esit("mesaj 49", SohbetGecmisi.Oku(1)[0].Metin, "son kayıt en yeni");
            Dogru(File.ReadAllText(SohbetGecmisi.Yol).Contains("\"kim\":\"ben\""),
                  "JSON anahtarları Mac dosyasıyla aynı (kim)");

            SohbetGecmisi.Kirp();
            Esit(50, SohbetGecmisi.Oku().Count, "5 MB altında kırpma dokunmaz");

            SohbetGecmisi.Yaz("ben", "son", null);
            SohbetGecmisi.Sil();
            Dogru(!File.Exists(SohbetGecmisi.Yol), "Sil bekleyen eklemeyi boşaltır, dosya geri gelmez");
            Esit(0, SohbetGecmisi.Oku().Count, "silinmiş geçmiş boş liste");
        }
        finally
        {
            SohbetGecmisi.Yol = eskiYol;
            try { Directory.Delete(dizin, true); } catch (Exception) { }
        }
    }
}
