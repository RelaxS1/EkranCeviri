using System.Windows;
using EkranCeviri.Cekirdek;
using EkranCeviri.Ceviri;
using EkranCeviri.Ekran;

namespace EkranCeviri.Testler;

/// <summary>
/// Ağsız birim testleri. Her testin karşılığı macOS sürümünde bir hata
/// raporudur — buradaki bir kırmızı, kullanıcının gerçekten yaşadığı bir
/// sorunun geri gelmesi demektir.
/// </summary>
public static class Program
{
    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Saf testler (macOS'ta da koşan küme) burada da koşar; sayaçlar
        // SafYardimci üzerinden tek yerde birleşir.
        SafYardimci.Sifirla();
        SafTestlerA.Kos();
        SafTestlerB.Kos();
        SafTestlerC.Kos();
        SafTestlerD.Kos();

        AyarDogrulamaTestleri();

        Baslik("Giden mesaj biçimi — RAKAMLAR KUTSAL");
        // Bu metin doğrudan müşteriye gidiyor: saat/fiyat bozulursa zarar.
        Esit("17:30", Kalite.GidenFormatla("17:30"), "saat korunur");
        Esit("Yarın 17:30da buluşuruz",
             Kalite.GidenFormatla("Yarın 17:30'da buluşuruz."),
             "cümle içinde saat korunur");
        Dogru(Kalite.GidenFormatla("1,5 saat sürer.").Contains("1,5"),
              "ondalık korunur");
        Dogru(Kalite.GidenFormatla("30 dk 55.- / 60 dk 85.-").Contains("55.-"),
              "İsviçre fiyat biçimi (55.-) korunur");
        Dogru(Kalite.GidenFormatla("30 dk 55.- / 60 dk 85.-").Contains("85.-"),
              "ikinci fiyat da korunur");
        Esit("Tamam görüşürüz", Kalite.GidenFormatla("Tamam, görüşürüz!"),
             "noktalama silinir");
        Esit("Nasılsın", Kalite.GidenFormatla("nasılsın?"),
             "yalnız ilk harf büyük");
        Esit("12.05 tarihinde", Kalite.GidenFormatla("12.05 tarihinde"),
             "tarih silinmez");

        Baslik("Rakam koruma kapısı");
        Dogru(Kalite.RakamlarKorundu("saat 17:30 da 150 frank",
                                     "am 17:30 für 150 franken"),
              "rakamlar aynen geçmiş");
        Dogru(!Kalite.RakamlarKorundu("150 frank", "hundertfüfzg franken"),
              "model sayıyı harfle yazdıysa KAPIYA TAKILIR");
        Dogru(Kalite.RakamlarKorundu("selam", "sali"),
              "rakam yoksa kapı serbest");
        Dogru(!Kalite.RakamlarKorundu("30 dk 55 franken", "30 min"),
              "eksik rakam yakalanır");

        Baslik("Anahtarlama");
        Esit("chunnschmorn", Kalite.Anahtarla("chunnsch morn"), "sadeleştirme");
        Dogru(Kalite.Anahtarla("chunnsch morn?") != Kalite.Anahtarla("chunnsch morn"),
              "SORU İŞARETİ anlamı değiştirir, anahtar da değişmeli");

        Baslik("Düzenleme mesafesi");
        Dogru(Kalite.MesafeAzMi("trffe", "träffe", 2), "OCR titremesi tolere");
        Dogru(!Kalite.MesafeAzMi("merhaba", "hoşçakal", 2), "farklı kelime");
        Dogru(Kalite.MesafeAzMi("abc", "abc", 0), "birebir");
        Dogru(!Kalite.MesafeAzMi("kisa", "cok cok uzun bir metin", 2),
              "uzunluk farkı erken eler");

        Baslik("Emoji koruma (grafem kümesi)");
        Dogru(Kalite.EmojileriKoru("seni seviyorum ❤️", "ich liebe dich")
                    .Contains("❤️"), "düşen emoji geri eklenir");
        Esit("", Kalite.EmojileriKoru("😂", ""),
             "BOŞ çeviriye emoji eklenmez (sahte çeviri üretiyordu)");
        Esit("ich liebe dich ❤️",
             Kalite.EmojileriKoru("seni seviyorum ❤️", "ich liebe dich ❤️"),
             "zaten varsa tekrar eklenmez");
        Dogru(!Kalite.EmojileriKoru("kalp ❤️", "herz").Contains("❤ "),
              "ZWJ/varyasyon dizisi PARÇALANMAZ (bozuk kopya çıkıyordu)");

        Baslik("Karışık çeviri yakalama");
        Dogru(Kalite.KarisikMi("isch das ok fuer dich mach der kei sorge",
                               "isch das ok fuer dich mach der kei sorge"),
              "hiç çevrilmemiş metin karışık sayılır");
        Dogru(!Kalite.KarisikMi("wie geht es dir heute",
                                "bugün nasılsın canım"),
              "gerçek çeviri karışık değil");

        Baslik("Kalıntı kapıları");
        Dogru(Kalite.TurkceKalintiVar("Tamam canim bis spöter"),
              "Türkçe kelime yakalanır");
        Dogru(Kalite.TurkceKalintiVar("bis spöter aşkım"),
              "Türkçeye özgü harf yakalanır");
        Dogru(!Kalite.TurkceKalintiVar("ich mues no chli schaffe"),
              "temiz lehçe metni geçer");
        Dogru(Kalite.AlmancaKalintiVar("bugün ich habe çok yorgunum"),
              "2+ Almanca kelime yakalanır");
        Dogru(!Kalite.AlmancaKalintiVar("bugün çok yorgunum"),
              "temiz Türkçe geçer");
        Dogru(!Kalite.AlmancaKalintiVar("Berlin çok güzeldi"),
              "tek yabancı ad yanlış alarm vermez");

        Baslik("Lehçe algılama");
        LehceEsit("Züridütsch", ["chunnsch morn i d stadt", "nöd so schlimm"]);
        LehceEsit("Bärndütsch", ["i ga gäng no chli wärche", "itz isch guet"]);
        LehceEsit("Baseldytsch", ["y ha kei zyt fir s drämmli"]);
        LehceEsit("Bayrisch", ["servus wia gehts da heid", "passt scho"]);
        LehceEsit("Norddeutsch", ["moin moin", "lass uns schnacken"]);
        LehceEsit("Hochdeutsch", ["ich komme morgen in die stadt"]);
        LehceEsit("İsviçre Almancası", ["hesch guet gschlafe"]);
        // Tek gündelik kelime yanlış lehçeye kaydırıyordu
        Dogru(Lehce.Algila(["das ist nicht gut"]).Kisa == "Hochdeutsch",
              "tek zayıf işaret lehçe kararı VERMEZ");

        Baslik("Lehçe standartlaştırma (makine motorları için)");
        Dogru(Lehce.Standartlastir("ich bi müde").Contains("ich bin"),
              "kalıp uygulanır");
        Dogru(!Lehce.Standartlastir("ich bin müde").Contains("binn"),
              "KELİME SINIRI: 'ich bi' kalıbı 'ich bin' içini yemez");
        Dogru(Lehce.Standartlastir("ich chan nöd cho").Contains("kann"),
              "sözlük uygulanır");
        Dogru(Lehce.Standartlastir("ich chan nöd cho").Contains("nicht"),
              "olumsuzluk çevrilir");
        Dogru(Lehce.Standartlastir("halb sechs").Contains("fünf uhr dreissig"),
              "saat kalıbı AÇIK yazılır (Bing yanlış çeviriyordu)");
        Dogru(Lehce.Standartlastir("Hoi zäme").StartsWith("Hallo"),
              "büyük harf korunur");

        Baslik("Blok gruplama (regresyon)");
        BlokTestleri();

        Baslik("Blok eşleştirme");
        var a = new Blok("chunnsch morn", new Rect(10, 10, 100, 20), false);
        var b = new Blok("chunnsch morn", new Rect(10, 12, 100, 20), false);
        var c = new Blok("bis spöter", new Rect(10, 11, 100, 20), false);
        Dogru(Bloklayici.Eslesirler(a, b), "aynı metin eşleşir");
        Dogru(!Bloklayici.Eslesirler(a, c),
              "KONUM ÖRTÜŞSE BİLE farklı metin eşleşmez "
              + "(eski çeviri başka mesaja yapışıyordu)");

        return SafYardimci.Bitir("");
    }

    /// <summary>Ayarlar.Dogrula (Mac 598-656). Windows'a özel: Ayarlar
    /// ProtectedData'ya bağlı olduğundan saf koşucuda derlenmez. Ayar ELLE
    /// kurulur — Ayarlar.Yukle() gerçek kullanıcı dizinini okur.</summary>
    private static void AyarDogrulamaTestleri()
    {
        Baslik("Ayarlar (elle kurulur — gerçek ayar tekili ÇAĞRILMAZ)");
        Dogru(!Ayarlar.SinamaModuAktif,
              "test ikilisi sınama bayrağı taşımıyor (kaydet kilidi burada koruyucu DEĞİL)");
        {
            var a = new Ayarlar { Motor = "ai" };
            Dogru(a.GrokModel.Length > 0, "varsayılan model boş değil");
            Esit("ai", a.Motor, "elle kurulan motor korunur");
            Esit("grok", a.EtkinGidenMotor, "ai modunda giden motor Grok");
            a.Motor = "hizli";
            Esit("bing", a.EtkinGidenMotor,
                 "ücretsiz motor seçen kullanıcı kısayolla xAI'ye ÇIKMAZ (sahip kararı #7)");
        }
        // Bozuk config.json uygulamayı açılamaz hâle getirmemeli: Dogrula()
        // dosyadan gelen her alanı izin listesine/aralığa çeker. Diske DOKUNMAZ.
        {
            var a = new Ayarlar
            {
                HedefDil = "zz", Motor = "sihirli", GidenMotor = "deepl",
                DilModu = "klingon", BenCinsiyet = "?", KarsiCinsiyet = "?",
                KaynakDilKodu = "DE-de-de", OcrDili = "de_DE",
                KisayolTus = 999, KisayolMod = 0,
                GrokModel = "kötü model!", GrokModelKalite = "",
                Kisilik = new string('k', 5000),
                GidenKarakterMetni = new string('g', 5000),
                KaynakDilAdi = new string('a', 500),
            };
            a.Dogrula();
            Esit("tr", a.HedefDil, "bilinmeyen hedef dil → tr");
            Esit("ai", a.Motor, "bilinmeyen motor → ai");
            Esit("grok", a.GidenMotor, "bilinmeyen giden motor → grok");
            Esit("alman", a.DilModu, "bilinmeyen dil modu → alman");
            Esit("yok", a.BenCinsiyet, "bilinmeyen cinsiyet → yok");
            Esit("yok", a.KarsiCinsiyet, "bilinmeyen karşı cinsiyet → yok");
            Esit("de", a.KaynakDilKodu, "geçersiz kaynak dil kodu → de");
            Esit("de", a.OcrDili, "geçersiz OCR dili → de");
            Esit(0x43u, a.KisayolTus, "aralık dışı kısayol tuşu → C");
            Esit(0x0003u, a.KisayolMod, "boş kısayol modu → Ctrl+Alt");
            Esit(new Ayarlar().GrokModel, a.GrokModel, "API gövdesine giden model adı temizlenir");
            Esit(new Ayarlar().GrokModelKalite, a.GrokModelKalite, "boş kalite modeli → varsayılan");
            Esit(2000, a.Kisilik.Length, "kişilik metni 2000 karakterle sınırlanır");
            Esit(2000, a.GidenKarakterMetni.Length, "giden üslup metni 2000 karakterle sınırlanır");
            Esit(120, a.KaynakDilAdi.Length, "kaynak dil adı 120 karakterle sınırlanır");
        }
        {
            // Geçerli değerler Dogrula()'dan SAĞ ÇIKMALI: kapı "her şeyi
            // varsayılana çek" olsaydı yukarıdaki testler yine geçerdi ve
            // kullanıcı ayarı sessizce sıfırlanırdı.
            var a = new Ayarlar
            {
                HedefDil = "en", Motor = "hizli", GidenMotor = "bing",
                DilModu = "isvicre", KaynakDilKodu = "de-DE", OcrDili = "de-DE",
                KisayolTus = 0x54, KisayolMod = 0x0006, BenCinsiyet = "erkek",
            };
            a.Dogrula();
            Esit("en", a.HedefDil, "geçerli hedef dil korunur");
            Esit("hizli", a.Motor, "geçerli motor korunur");
            Esit("bing", a.GidenMotor, "geçerli giden motor korunur");
            Esit("isvicre", a.DilModu, "geçerli dil modu korunur");
            Esit("de-DE", a.KaynakDilKodu, "geçerli kaynak dil kodu korunur");
            Esit("de-DE", a.OcrDili, "geçerli OCR dili korunur");
            Esit(0x54u, a.KisayolTus, "geçerli kısayol korunur");
            Esit(0x0006u, a.KisayolMod, "geçerli kısayol modu korunur");
            Esit("erkek", a.BenCinsiyet, "geçerli cinsiyet korunur");
        }
        {
            // Eski dosyalar giden motoru "hizli" yazıyordu
            var a = new Ayarlar { GidenMotor = "hizli" };
            a.Dogrula();
            Esit("bing", a.GidenMotor, "eski 'hizli' giden motoru → bing");
        }
    }

    private static void BlokTestleri()
    {
        var boyut = new Size(1000, 800);
        var satirlar = new List<OcrSatir>
        {
            // Sol balon, iki satır (aynı balonda)
            new() { Metin = "chunnsch morn", Kutu = new Rect(40, 100, 300, 22) },
            new() { Metin = "i d stadt", Kutu = new Rect(40, 124, 200, 22) },
            // AYRI sol balon (dikey boşluk büyük)
            new() { Metin = "hesch zit", Kutu = new Rect(40, 220, 250, 22) },
            // Sağ balon = benim
            new() { Metin = "ja klar", Kutu = new Rect(700, 300, 250, 22) },
            // Ortalanmış DAR blok = sistem öğesi
            new() { Metin = "Bugün", Kutu = new Rect(460, 60, 90, 20) },
            // Harfsiz kutu = sessiz
            new() { Metin = "14:32", Kutu = new Rect(300, 150, 50, 16) },
        };

        var (bloklar, sessizler) = Bloklayici.Ayir(satirlar, boyut);

        Dogru(sessizler.Count >= 1, "sessiz kutu ayrıldı");
        var ikiSatir = bloklar.FirstOrDefault(x => x.Metin.Contains("chunnsch"));
        Dogru(ikiSatir is not null && ikiSatir.Metin.Contains("i d stadt"),
              "iki satır tek balonda birleşti");
        Dogru(bloklar.Any(x => x.Metin.Contains("hesch zit")
                            && !x.Metin.Contains("chunnsch")),
              "ayrı balonlar BİRLEŞMEDİ");
        var benimki = bloklar.FirstOrDefault(x => x.Metin.Contains("ja klar"));
        // Kendi mesajlarımız da HEDEFTİR (Mac davranışı): kullanıcı kendi
        // yazdığı Almancayı da Türkçe görmek istiyor; Türkçe yazdıkları
        // Kalite.CevrilecekSeyYokMu ile motora gitmez.
        Dogru(benimki is { Benim: true, Hedef: true }, "sağ balon benim ve yine hedef");
        var tarih = bloklar.FirstOrDefault(x => x.Metin.Contains("Bugün"));
        Dogru(tarih is { Hedef: false }, "ortalanmış dar blok atlandı");
        Dogru(ikiSatir is { Hedef: true }, "karşı tarafın balonu hedef");

        // Saat damgası METİNDEN silinir ama fiyat SİLİNMEZ
        var fiyat = Bloklayici.Ayir([
            new OcrSatir { Metin = "das kostet 12.50 franken 14:32",
                           Kutu = new Rect(40, 100, 400, 22) },
        ], boyut).Bloklar.First();
        Dogru(fiyat.Metin.Contains("12.50"),
              "FİYAT silinmez (eski desen fiyatı saat sanıyordu)");
        Dogru(!fiyat.Metin.Contains("14:32"), "saat damgası silinir");
    }

    // ---- yardımcılar: sayaç SafYardimci'de (saf testlerle birleşik)

    private static void Baslik(string s) => SafYardimci.Baslik(s);
    private static void Dogru(bool kosul, string ad) => SafYardimci.Dogru(kosul, ad);
    private static void Esit<T>(T beklenen, T gelen, string ad) => SafYardimci.Esit(beklenen, gelen, ad);

    private static void LehceEsit(string beklenen, string[] metinler)
    {
        var (_, kisa) = Lehce.Algila(metinler);
        SafYardimci.Esit(beklenen, kisa, beklenen);
    }
}
