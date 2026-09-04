using EkranCeviri.Ceviri;
using EkranCeviri.Ekran;
using static EkranCeviri.Testler.SafYardimci;

namespace EkranCeviri.Testler;

/// <summary>
/// Faz C (akış) saf testleri. Canlı döngünün kendisi WPF/GDI'ye bağlı;
/// burada döngünün KARAR katmanı sabitlenir: hareket kararının "kaydırma +
/// yeni mesaj" senaryosu ve boş-nöbet defterinin tur kapısı. Her iki parça
/// Yonetici.CanliTurAsync'in ÜRETİM fonksiyonlarıdır (kopya mantık yok).
/// </summary>
public static class SafTestlerC
{
    private const int Hn = 32 * 32;

    /// <summary>Düz gri sohbet zemini (32×32 iz).</summary>
    private static byte[] Kare()
    {
        var k = new byte[Hn];
        Array.Fill(k, (byte)120);
        return k;
    }

    /// <summary>İçerik <paramref name="satir"/> satır yukarı kaydı (sohbet
    /// aşağı doğru büyür: eski satırlar yukarı çıkar, alt boşalır).</summary>
    private static byte[] YukariKaydir(byte[] k, int satir)
    {
        var y = Kare();
        int n = satir * 32;
        for (int i = n; i < Hn; i++) y[i - n] = k[i];
        return y;
    }

    /// <summary>Alta 3 satırlık yeni balon (koyu zemin).</summary>
    private static byte[] YeniBalon(byte[] k, byte ton = 40)
    {
        var y = (byte[])k.Clone();
        for (int i = Hn - 96; i < Hn; i++) y[i] = ton;
        return y;
    }

    /// <summary>Belirli satırlara "mesaj" dokusu (satır başına farklı ton).</summary>
    private static byte[] Mesajlar(byte[] k, int ilkSatir, int adet)
    {
        var y = (byte[])k.Clone();
        for (int s = ilkSatir; s < ilkSatir + adet && s < 32; s++)
            for (int x = 0; x < 32; x++) y[s * 32 + x] = (byte)(60 + (s * 7) % 90);
        return y;
    }

    /// <summary>12 hücre her tur yanıp söner ("yazıyor…" / GIF).</summary>
    private static byte[] Animasyon(byte[] k, int tur)
    {
        var y = (byte[])k.Clone();
        for (int i = 300; i < 312; i++) y[i] = (byte)(tur % 2 == 0 ? 40 : 200);
        return y;
    }

    public static void Kos()
    {
        KaydirmaVeYeniMesaj();
        BosNobetTurKapisi();
        YasakSuresiDonusumu();
        KaliteBaglamKaynagi();
    }

    // ---------------- hareket kararı: kaydırma + yeni mesaj ----------------

    private static void KaydirmaVeYeniMesaj()
    {
        Baslik("Canlı tur — kaydırma + yeni mesaj senaryosu (Hareket.Karar)");
        var hd = new HareketDurumu();
        double t = 0;

        // İlk çeviri tohumu: 10 satırlık sohbet OCR'landı (Yonetici.IlkCeviriAsync
        // _hareket.SonOcrIzi'yi bu izle tohumlar).
        var k0 = Mesajlar(Kare(), 20, 10);
        hd.SonOcrIzi = k0; hd.SonOcrZamani = t;
        var onceki = k0;

        // 1) Sabit turlar: OCR yok (tohum sayesinde ilk tur da tam OCR yapmaz)
        t += 0.8;
        var kr = Hareket.Karar(k0, onceki, hd, t);
        Dogru(!kr.OcrYap && kr.Sabit, "tohumlu ilk canlı tur sabit ekranda OCR yapmaz");

        // 2) Yeni mesaj gelir → sohbet 3 satır yukarı kayar VE altta yeni balon
        t += 0.8;
        var k1 = YeniBalon(YukariKaydir(k0, 3));
        kr = Hareket.Karar(k1, onceki, hd, t); onceki = k1;
        Dogru(kr.Fark > 0.012 && !kr.Sabit && !kr.OcrYap,
              "kaydırma anında kare hareketli: OCR beklenir (kaydırma telafisi çalışır)");
        Dogru(kr.IcerikDegisti, "kaydırma+yeni balon son OCR'a göre içerik değişimidir");

        // 3) Ekran durur → OCR hemen (tek turda)
        t += 0.8;
        kr = Hareket.Karar(k1, onceki, hd, t);
        Dogru(kr.OcrYap && kr.Sabit, "kaydırma durunca ilk sabit turda OCR");
        hd.SonOcrIzi = k1; hd.SonOcrZamani = t;

        // 4) Aynı görünüm sürerse tekrar OCR yok (titreme + CPU + para)
        t += 0.8;
        kr = Hareket.Karar(k1, onceki, hd, t);
        Dogru(!kr.OcrYap, "OCR sonrası sabit ekranda yeniden OCR yok");

        // 5) Sadece kaydırma (yeni mesaj yok, kullanıcı yukarı baktı): içerik
        //    son OCR'a göre DEĞİŞMİŞ sayılır (hücreler yer değiştirdi) → durunca
        //    OCR yapılır; eşleştirme eski çevirileri yeni konuma taşır.
        t += 0.8;
        var k2 = YukariKaydir(k1, 5);
        kr = Hareket.Karar(k2, onceki, hd, t); onceki = k2;
        Dogru(!kr.OcrYap, "kaydırma sürerken OCR yok");
        t += 0.8;
        kr = Hareket.Karar(k2, onceki, hd, t);
        Dogru(kr.OcrYap, "kaydırma bitince yeni konumlar için OCR");
        hd.SonOcrIzi = k2; hd.SonOcrZamani = t;

        // 6) "yazıyor…" animasyonu başlar; 3 turdan sonra maskelenir, OCR
        //    tetiklemez
        int ocrSayisi = 0;
        for (int tur = 0; tur < 8; tur++)
        {
            t += 0.8;
            var k = Animasyon(k2, tur);
            kr = Hareket.Karar(k, onceki, hd, t); onceki = k;
            if (kr.OcrYap) { ocrSayisi++; hd.SonOcrIzi = k; hd.SonOcrZamani = t; }
        }
        Dogru(ocrSayisi <= 1, "'yazıyor…' animasyonu sürerken OCR fırtınası yok (≤1)");
        Dogru(kr.AnimasyonOrani > 0, "animasyon hücreleri maskeye alındı");

        // 7) Animasyon sürerken YENİ MESAJ gelir: maske dışı değişim → en geç
        //    8 sn içinde OCR (mesaj kaybolmaz)
        bool yakalandi = false;
        int turSayisi = 0;
        for (int tur = 8; tur < 24; tur++)
        {
            t += 0.8; turSayisi++;
            var k = YeniBalon(Animasyon(k2, tur), 220);
            kr = Hareket.Karar(k, onceki, hd, t); onceki = k;
            if (kr.OcrYap) { yakalandi = true; hd.SonOcrIzi = k; hd.SonOcrZamani = t; break; }
        }
        Dogru(yakalandi, "animasyon altında gelen yeni mesaj OCR'lanır");
        Dogru(turSayisi * 0.8 <= 8.1, "…ve en geç 8 sn (zorla aralığı) içinde");

        // 8) Ağ geri çekilmesi: yasak süresinde yeni mesaj gelse bile OCR yok,
        //    yasak bitince ilk sabit turda OCR (Yonetici.GeriCekilmeBitis →
        //    HareketDurumu.OcrYasakBitis köprüsü)
        hd.OcrYasakBitis = t + 60;
        var k3 = YeniBalon(hd.SonOcrIzi!, 10);
        t += 0.8; kr = Hareket.Karar(k3, onceki, hd, t); onceki = k3;
        t += 0.8; kr = Hareket.Karar(k3, onceki, hd, t);
        Dogru(kr.IcerikDegisti && !kr.OcrYap, "geri çekilmede içerik değişse de OCR yok (pil/para)");
        t += 60; kr = Hareket.Karar(k3, onceki, hd, t);
        Dogru(kr.OcrYap, "geri çekilme bitince bekleyen mesaj OCR'lanır");
    }

    // ---------------- boş-nöbet defteri: tur kapısı ----------------

    private static void BosNobetTurKapisi()
    {
        Baslik("BosNobetDefteri.TurAcikMi — canlı turun 'boşları yeniden dene' kapısı");
        var d = new BosNobetDefteri();
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        Dogru(d.TurAcikMi(0, t0), "taze defterde ilk tur açık (hiç tur yapılmadı)");
        d.TurBasladi(t0);
        Dogru(!d.TurAcikMi(0, t0.AddSeconds(10)), "tur başladıktan 10 sn sonra kapalı (TTL 90)");
        Dogru(d.TurAcikMi(3, t0.AddSeconds(10)), "pesPeseHata ≥ 3 ise TTL beklenmez (geri çekilme sonrası)");
        Dogru(!d.TurAcikMi(2, t0.AddSeconds(10)), "pesPeseHata 2 tek başına açmaz");
        Dogru(d.TurAcikMi(0, t0.AddSeconds(BosNobetDefteri.TurTtl.TotalSeconds)),
              "TTL dolunca sağlıklı ağda da tur açılır (tek besleyici pesPeseHata değil)");

        // Anahtar başına hak: 4 deneme, sonra kapalı; 900 sn sessizlikten sonra yenilenir
        const string a = "guten morgen wie gehts";
        for (int i = 0; i < BosNobetDefteri.EnFazlaDeneme; i++)
        {
            Dogru(d.HakVarMi(a, t0.AddSeconds(i)), $"deneme {i + 1}: hak var");
            d.Denendi(a, t0.AddSeconds(i));
        }
        Dogru(!d.HakVarMi(a, t0.AddSeconds(10)), "4 denemeden sonra hak yok (para yakma durur)");
        Dogru(d.HakVarMi(a, t0.AddSeconds(10 + BosNobetDefteri.HakYenileme.TotalSeconds)),
              "900 sn sessizlikten sonra hak yenilenir (ağ saatlerce kapalı kalabilir)");
        d.Basarili(a);
        Dogru(d.HakVarMi(a, t0.AddSeconds(11)), "gerçek çeviri gelince kayıt silinir, hak geri gelir");
        Esit(0, d.Izlenen, "başarılı anahtar defterde izlenmez");

        d.Denendi(a, t0);
        d.Sifirla();
        Dogru(d.TurAcikMi(0, t0.AddSeconds(1)) && d.Izlenen == 0,
              "CanliDurdur → Sifirla: tur açılır, kayıtlar boşalır");

        // Canlı tur senaryosu: tur açıkken '' işaretli anahtar silinir (eksik
        // olur), kapalıyken '' kalır — Yonetici.CanliGuncelleAsync kuralı
        var onb = new OturumOnbellegi();
        onb[a] = "";
        bool nobetciDene = d.TurAcikMi(0, t0.AddSeconds(1));
        string? ceviri = null;
        if (onb[a] is { } hazir)
        {
            if (hazir.Length > 0) ceviri = hazir;
            else if (nobetciDene && d.HakVarMi(a, t0.AddSeconds(1))) onb.Sil(a);
            else ceviri = "";
        }
        Dogru(nobetciDene && ceviri is null && onb[a] is null,
              "tur açık + hak var: '' anahtar önbellekten silinir → motora gider");
        onb[a] = "";
        d.TurBasladi(t0.AddSeconds(1));
        nobetciDene = d.TurAcikMi(0, t0.AddSeconds(2));
        ceviri = null;
        if (onb[a] is { } hazir2)
        {
            if (hazir2.Length > 0) ceviri = hazir2;
            else if (nobetciDene && d.HakVarMi(a, t0.AddSeconds(2))) onb.Sil(a);
            else ceviri = "";
        }
        Dogru(!nobetciDene && ceviri == "" && onb[a] == "",
              "tur kapalı: '' bloğa taşınır, motora gitmez (boşuna çağrı yok)");
    }

    // ---------------- geri çekilme → yasak süresi köprüsü ----------------

    /// <summary>Yonetici duvar saati (UTC) tutar, Hareket.Karar Stopwatch
    /// saniyesi ister. Köprü: kalan süre saniyeye çevrilip 'şimdi'ye eklenir;
    /// geçmişte kalan bitiş 0 olur (yasak yok).</summary>
    private static void YasakSuresiDonusumu()
    {
        Baslik("Geri çekilme bitişi → OcrYasakBitis köprüsü");
        var an = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        double simdi = 1000;
        static double Kopru(DateTime geriBitis, DateTime an, double simdi) =>
            geriBitis > an ? simdi + (geriBitis - an).TotalSeconds : 0;

        Esit(0.0, Kopru(DateTime.MinValue, an, simdi), "hiç geri çekilme yok → yasak 0");
        Esit(0.0, Kopru(an.AddSeconds(-5), an, simdi), "bitmiş geri çekilme → yasak 0");
        Esit(1060.0, Kopru(an.AddSeconds(60), an, simdi), "60 sn kalan → şimdi+60");
        var hd = new HareketDurumu { SonOcrIzi = Kare(), OcrYasakBitis = Kopru(an.AddSeconds(60), an, simdi) };
        var k = YeniBalon(Kare());
        var kr = Hareket.Karar(k, k, hd, simdi + 1);
        Dogru(!kr.OcrYap, "köprülenen yasak Hareket.Karar'ı gerçekten durdurur");
        kr = Hareket.Karar(k, k, hd, simdi + 61);
        Dogru(kr.OcrYap, "yasak bitince karar OCR'a döner");
    }

    // ---------------- kalite partisi bağlam kaynağı ----------------

    /// <summary>Denetim tur 2 (C5 sapması): kalite işçisi `Task.Run` ile
    /// hemen başlar, `_mevcutBloklar` ise ilk çeviride ancak ekrana basılırken
    /// atanır → oradan kurulan bağlam BOŞ sohbetle varsayılan lehçeye düşüyordu
    /// (yarışa bağlı). Onarım: bağlam çağıranın verdiği sohbetten kurulur
    /// (Mac Motorlar.swift 846), sohbet boşsa partinin kopyaları kullanılır.
    /// Bu test seçim kuralını ve boş listenin neden yanlış olduğunu sabitler.</summary>
    private static void KaliteBaglamKaynagi()
    {
        Baslik("Kalite partisi bağlamı — sohbet kaynağı seçimi");
        var bosMevcut = new List<string>();
        var sohbet = new List<string> { "chunnsch morn i d stadt", "nöd so schlimm", "hesch guet gschlafe" };
        var kopyalar = new List<string> { "hesch guet gschlafe" };
        static List<string> Kaynak(List<string> sohbet, List<string> kopyalar) =>
            sohbet.Count > 0 ? sohbet : kopyalar;

        var (_, bosKisa) = Lehce.Algila(bosMevcut);
        var (_, doluKisa) = Lehce.Algila(sohbet);
        Dogru(bosKisa != doluKisa,
              "boş `_mevcutBloklar` ile kurulan bağlam lehçeyi KAÇIRIR (yarışın zararı)");
        Esit(sohbet.Count, Kaynak(sohbet, kopyalar).Count,
             "sohbet doluysa bağlam çağıranın verdiği sohbetten");
        Esit(doluKisa, Lehce.Algila(Kaynak(sohbet, kopyalar)).Kisa,
             "geçirilen sohbetten algılanan lehçe = ekrandaki sohbetin lehçesi");
        Dogru(ReferenceEquals(kopyalar, Kaynak(bosMevcut, kopyalar)),
              "sohbet boşsa (savunmacı) partinin kendi kopyaları bağlam olur");
        Dogru(Lehce.Algila(Kaynak(bosMevcut, kopyalar)).Kisa != bosKisa,
              "kopyalardan kurulan bağlam bile varsayılana düşmez");
    }
}
