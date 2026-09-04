using System.Diagnostics;
using EkranCeviri.Cekirdek;

namespace EkranCeviri.Ceviri;

/// <summary>
/// Bir çeviri turunun seçenekleri (Motorlar.swift bloklariCevir parametreleri).
/// <para><see cref="Zorla"/>: önbellek/hafıza atlanır, hedefler motora gider (✨, kalite turu).</para>
/// <para><see cref="KaliciYaz"/>: false ise (hareketli kare, hızlı-önce) çeviriler
/// kalıcı hafızaya YAZILMAZ — kaydırma ortasındaki karenin kesik satırları
/// sonsuza dek servis ediliyordu.</para>
/// <para><see cref="Canli"/>: canlı turdan geliyorsa ağda tek deneme, tekil
/// onarım turu yok, Grok tamamlama yok.</para>
/// <para><see cref="Hizli"/>: hızlı-önce yolu — yalnız Grok modelini değiştirir;
/// kalıcı hafızaya yazım çağıranın kararıdır (KaliciYaz:false verir).</para>
/// <para><see cref="ZamanAsimi"/>: arka plan (kalite) işi kendi tavanını verir.</para>
/// <para><see cref="Asama"/>: arıza günlüğü aşama etiketi.</para>
/// </summary>
public sealed record CeviriSecenekleri(bool Zorla = false,
                                       bool KaliciYaz = true,
                                       bool Canli = false,
                                       bool Hizli = false,
                                       TimeSpan? ZamanAsimi = null,
                                       string Asama = "ceviri");

/// <summary>
/// Çeviri düzenleyicisi — Motorlar.swift <c>bloklariCevir</c> BİREBİR + Windows
/// uyarlaması. Oturum önbelleği → kalıcı hafıza → motor sırasını yönetir,
/// motor seçer, sonucu kalite kapılarından geçirir ve yalnız GÜVENİLİR
/// (Grok) çıktıyı kalıcı hafızaya yazar.
///
/// Sıra neden böyle: kullanıcı aynı kişiyle her gün konuşuyor; aynı cümleler
/// tekrar ediyor. Hafıza önce sorulunca tekrarlar ANINDA geliyor ve kaliteli
/// (yavaş) model kullanmak ücretsiz hale geliyor.
///
/// ÜCRETSİZ GERÇEKTEN ÜCRETSİZ (sahip kararı #7): motor "hizli" iken bu
/// sınıftan xAI'ye HİÇBİR metin çıkmaz. Aynanın diğer yüzü: motor "ai" iken
/// makine motoruna DÜŞÜŞ YOK — sessiz düşüş "Grok kötü çeviriyor" yanılgısı
/// yaratıyor ve düşük kaliteli çıktı hafızayı zehirliyordu.
/// </summary>
public sealed class Cevirmen
{
    /// <summary>Hangi motorun sonucu? Karar noktaları (ikinci ücretsiz geçiş,
    /// "gerçek çeviri" sayımı, kalıcı hafızaya yazım) etiket METNİNE değil
    /// buna bakar: "⚠︎ Grok anahtarı yok → Bing" etiketi "Grok" içerdiği için
    /// Bing çıktısı Grok sanılıp kalıcı hafızaya yazılıyordu.</summary>
    private enum MotorTuru { Yok, Bing, Google, Grok }

    private readonly Hafiza _hafiza;
    private readonly GrokMotor _grok;
    private readonly BingMotor _bing;
    private readonly GoogleMotor _google;
    private readonly Func<Ayarlar> _ayar;
    private volatile bool _anahtarUyarisi;

    public Cevirmen(Func<Ayarlar> ayarSaglayici, Hafiza hafiza)
    {
        _ayar = ayarSaglayici;
        _hafiza = hafiza;
        _grok = new GrokMotor(Ag.Istemci, ayarSaglayici);
        _bing = new BingMotor(Ag.Istemci);
        _google = new GoogleMotor(Ag.Istemci);
        // Sohbet geçmişi saf katmanda (Gunluk'u tanımaz); günlük kancası burada bağlanır.
        SohbetGecmisi.GunlukYaz ??= Gunluk.Yaz;
    }

    public Hafiza Hafiza => _hafiza;

    /// <summary>Oturum önbelleği (anahtar → çeviri). Canlı eşleştirme
    /// buradan okur/siler; her tur sonunda nesil kapısıyla birleştirilir.</summary>
    public OturumOnbellegi Onbellek { get; } = new();

    /// <summary>Grok kullanılabilir mi? (arayüz uyarısı için)</summary>
    public bool GrokHazir => AnahtarKasasi.BicimGecerli(_ayar().GrokApiKey);

    /// <summary>Grok seçiliyken anahtar bulunmadığında true olur (bar uyarır).</summary>
    public bool AnahtarUyarisi => _anahtarUyarisi;

    /// <summary>Yeni bölge: oturum önbelleği boşalır (nesil ilerler, arka
    /// planda biten eski iş yazamaz) ve tekrar tavanı sıfırlanır.</summary>
    public void Sifirla()
    {
        Onbellek.Temizle();
        TekrarDefteri.Paylasilan.Sifirla();
    }

    /// <summary>
    /// Bloklardan çevirisi eksik olanları doldurur. Blokların
    /// <see cref="Blok.Ceviri"/> alanı YERİNDE güncellenir; oturum önbelleği
    /// tur sonunda birleştirilir.
    /// </summary>
    /// <returns>Panelde gösterilecek motor adı.</returns>
    public Task<string> BloklariCevirAsync(IReadOnlyList<Blok> bloklar,
                                           CeviriBaglam baglam,
                                           CancellationToken iptal,
                                           CeviriSecenekleri? secenek = null)
    {
        var s = secenek ?? new CeviriSecenekleri();
        return Teshis.Paylasilan.OlcAsync(s.Hizli ? "grok-hizli" : "ceviri",
                                          () => CevirIcAsync(bloklar, baglam, iptal, s));
    }

    private async Task<string> CevirIcAsync(IReadOnlyList<Blok> bloklar,
                                            CeviriBaglam baglam,
                                            CancellationToken iptal,
                                            CeviriSecenekleri s)
    {
        var t0 = Stopwatch.GetTimestamp();
        int GecenMs() => (int)Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        var ayar = baglam.Ayar;
        var asama = s.Asama;
        var defter = TekrarDefteri.Paylasilan;

        var hedefler = bloklar.Where(b => b.Hedef).ToList();
        if (hedefler.Count == 0) return "çevrilecek yazı yok";

        // Nesil BAŞTA alınır: tur sürerken kullanıcı yeni bölge seçtiyse
        // (Temizle) sonuç eski nesille gelir ve yazılmaz.
        var (bellek, nesil) = Onbellek.Kopyala();
        // Windows uyarlaması: eşleştirmeden gelen (ekranda zaten duran) çeviri
        // önbelleğe tohumlanır — çağıran blok üzerinde taşıyor, Mac ise
        // sözlükte. Boş çeviri tohumlanmaz ("" çevrilemedi protokolüdür).
        foreach (var b in hedefler)
            if (b.Ceviri is { Length: > 0 } c && !bellek.ContainsKey(b.Anahtar))
                bellek[b.Anahtar] = c;

        // ÖN ELEME — İLK ÜCRETLİ ÇAĞRIYI DA KES. (b) sınıfı metni motora
        // sormaya gerek yok: yanıt her zaman metnin KENDİSİ oluyor (ölçüm:
        // aynı metin iki farklı model yapılandırmasında 10-14 kez aynen
        // döndü). Arıza günlüğüne YALNIZ ilk işaretlemede satır yazılır —
        // her sabit karede tekrar yazmak günlüğü şişirirdi.
        foreach (var b in hedefler)
        {
            if (_hafiza.UretilenMi(b.Anahtar) || defter.DegismezMi(b.Anahtar)) continue;
            if (!Kalite.CevrilecekSeyYokMu(b.Metin, ayar.HedefDil)) continue;
            defter.DegismezIsaretle(b.Anahtar);
            ArizaGunlugu.Yaz(asama, "on-eleme", "cevrilecek-sey-yok", GecenMs(), b.Metin);
        }
        // Vazgeçilen blok EKRANDA ORİJİNALİYLE durur: değeri boş bırakmak
        // canlı turda "başarısız" sayılıp geri çekilme üretiyordu, "" bırakmak
        // ise "çevrilemedi" protokolüne düşüp yeniden denemeye sokuyordu.
        foreach (var b in hedefler)
            if (!bellek.ContainsKey(b.Anahtar) && defter.DegismezMi(b.Anahtar))
                bellek[b.Anahtar] = b.Metin;

        // Ücretli çağrı ELEMESİ: kendi çıktımız (zehir kalkanı — katman
        // yakalamaya sızdıysa ürettiğimiz çeviriyi yeniden çevirmeye kalkma)
        // + vazgeçilmiş anahtarlar ((b) işareti ya da tekrar tavanı).
        // `Zorla` ile gelen KALİTE turu da bu listeyi kullanır — ölçülen
        // israfın 76/93'ü tam olarak orada oluyordu.
        var hedefler2 = hedefler.Where(b => !_hafiza.UretilenMi(b.Anahtar)
                                            && !defter.VazgecildiMi(b.Anahtar)).ToList();
        if (!s.Zorla)
        {
            // 1) oturum önbelleğinde bulanık arama (OCR titremesi)
            foreach (var b in hedefler2)
            {
                if (bellek.ContainsKey(b.Anahtar)) continue;
                if (Hafiza.BulanikBul(b.Anahtar, bellek) is { } benzer)
                    bellek[b.Anahtar] = benzer;
            }
            // 2) KALICI YEREL HAFIZA: motora gitmeden önce diskteki çevirilere bak
            foreach (var b in hedefler2)
            {
                if (bellek.ContainsKey(b.Anahtar)) continue;
                if (_hafiza.Bul(b.Anahtar, ayar.HedefDil) is { } yerel)
                    bellek[b.Anahtar] = yerel;
            }
        }
        var eksikler = s.Zorla ? hedefler2
                               : hedefler2.Where(b => !bellek.ContainsKey(b.Anahtar)).ToList();

        // Kullanıcıya görünen etiket: "güncel" hiçbir şey anlatmıyordu. Hepsi
        // hafızadan geldiyse çeviri ANINDA ve ücretsiz olmuştur — bunu söyle.
        var motorAdi = "Hafızadan ✓";
        var motorTuru = MotorTuru.Yok;
        // Bu çağrıda değeri GROK'tan gelen anahtarlar: kalıcı hafızaya yalnız
        // bunlar yazılır (ücretsiz motorun aynı partideki çıktısı yazılmaz).
        var grokAnahtarlari = new HashSet<string>(StringComparer.Ordinal);
        // Karar TEK yerde verilir; ücretli yolların hepsi buna bakar.
        bool grokIzinli = ayar.Motor == "ai" && GrokHazir;
        var grokEtiketi = ayar.HizOnceligi || s.Hizli ? "grok-hizli" : "grok-kalite";
        // Arıza günlüğü SEBEBİ istiyor: hata yutulmaz, saklanır.
        Exception? sonHata = null;

        // Grok'a giden bağlam: ekrandaki ZATEN ÇEVRİLMİŞ mesajlar (rol, kaynak,
        // çeviri) — eksik olanlar hariç. Lehçe çağıranın tüm sohbetten
        // algıladığı değerdir.
        CeviriBaglam GrokBaglami(IReadOnlyList<KonusmaSatiri> onceki) => new()
        {
            Ayar = ayar,
            LehceAd = baglam.LehceAd,
            LehceKisa = baglam.LehceKisa,
            TarzOrnekleri = baglam.TarzOrnekleri,
            OncekiKonusmaYapili = onceki,
        };

        if (eksikler.Count > 0)
        {
            var metinler = eksikler.Select(b => b.Metin).ToList();
            var m = ayar.Motor;
            if (m == "ai" && !GrokHazir)
            {
                // Kullanıcı Grok seçti ama anahtar yok: sessizce ücretsiz
                // motora düşmek "Grok kötü çeviriyor" yanılgısı yaratıyordu;
                // bar uyarır, arıza günlüğü nedeni tutar.
                m = "hizli";
                _anahtarUyarisi = true;
                ArizaGunlugu.Yaz(asama, "grok", "anahtar-yok", GecenMs(),
                                 string.Join(" ", metinler));
            }
            // Makine motorlarına lehçe DEĞİL, standartlaştırılmış metin gider;
            // yankı denetimi de bu metinle yapılır.
            var makineMetinleri = metinler.Select(Lehce.Standartlastir).ToList();
            string?[]? ceviriler = null;

            if (m == "ai")
            {
                // SADECE GROK: kullanıcı açıkça Grok seçtiyse başka motor yok.
                var roller = eksikler.Select(b => b.Benim).ToList();
                var eksikAnahtarlar = new HashSet<string>(eksikler.Select(b => b.Anahtar),
                                                          StringComparer.Ordinal);
                var onceki = new List<KonusmaSatiri>();
                foreach (var b in hedefler)
                {
                    if (eksikAnahtarlar.Contains(b.Anahtar)) continue;
                    if (bellek.TryGetValue(b.Anahtar, out var c) && c.Length > 0)
                        onceki.Add(new KonusmaSatiri(b.Benim, b.Metin, c));
                }
                try
                {
                    var sonuc = await _grok.CevirAsync(metinler, GrokBaglami(onceki), iptal,
                                                       roller, s.Canli, s.Hizli,
                                                       s.ZamanAsimi, asama)
                                           .ConfigureAwait(false);
                    ceviriler = sonuc.Ceviriler.ToArray();
                    motorTuru = MotorTuru.Grok;
                    motorAdi = "Grok · " + baglam.LehceKisa;
                }
                catch (Exception e) { sonHata = e; }
            }
            else
            {
                // Ücretsiz zincir: Bing (de→tr'de Google'dan tutarlı) → Google.
                // ÜCRETLİ SON ÇARE YOK (sahip kararı #7): eskiden "ikisi de
                // düştüyse Grok'a sor" dalı vardı ve "Ücretsiz çeviri" seçmiş
                // kullanıcının HAM metnini xAI'ye gönderiyordu. İkisi de
                // düşerse çeviri yapılmaz, sebep arıza günlüğüne yazılır.
                // Canlı turda ağ TEK deneme (tur zaten yeniden dener).
                int agDeneme = s.Canli ? 1 : 3;
                try
                {
                    ceviriler = await _bing.CevirHamAsync(metinler, baglam, iptal, agDeneme)
                                           .ConfigureAwait(false);
                    motorTuru = MotorTuru.Bing;
                    motorAdi = _anahtarUyarisi ? "⚠︎ Grok anahtarı yok → Bing" : "Bing (ücretsiz)";
                }
                catch (Exception e) { sonHata = e; }
                if (ceviriler is null)
                {
                    try
                    {
                        ceviriler = await _google.CevirHamAsync(metinler, baglam, iptal, agDeneme)
                                                 .ConfigureAwait(false);
                        motorTuru = MotorTuru.Google;
                        motorAdi = _anahtarUyarisi ? "⚠︎ Grok anahtarı yok → Google" : "Google (ücretsiz)";
                    }
                    catch (Exception e) { sonHata = e; }
                }
            }

            if (ceviriler is null)
            {
                // Önbellekte olmayan ama ekranda TAŞINMIŞ çeviriyi (OCR
                // titremesiyle anahtarı değişen balon) boşla EZME: yalnız
                // değeri olanı ata. ARIZA GÜNLÜĞÜ: kullanıcı "çeviri hatası"
                // gördüğünde NEDENİ burada kalıcı olarak durur.
                foreach (var b in hedefler)
                    if (bellek.TryGetValue(b.Anahtar, out var c)) b.Ceviri = c;
                ArizaGunlugu.Yaz(asama, m == "ai" ? grokEtiketi : "ucretsiz",
                                 sonHata is null ? "bos-yanit" : ArizaGunlugu.Sebep(sonHata),
                                 GecenMs(), string.Join(" ", metinler));
                if (sonHata is not null) Gunluk.Hata("bloklariCevir/" + asama, sonHata);
                Onbellek.Birlestir(bellek, nesil);
                UretilenleriKaydet(bellek);
                return "çeviri hatası (ağ?)";
            }

            var liste = new string[metinler.Count];
            for (int i = 0; i < liste.Length; i++)
                liste[i] = i < ceviriler.Length ? ceviriler[i] ?? "" : "";

            // Ücretsiz motor bazı metinleri AYNEN geri verir (çeviremedi).
            // Onları İKİNCİ ücretsiz motorla bir kez daha dene — "bir kısmını
            // çevirmedi" boşluğunu kapatır, maliyeti sıfır.
            if (motorTuru is MotorTuru.Bing or MotorTuru.Google)
            {
                var tekrarIdx = new List<int>();
                for (int i = 0; i < eksikler.Count; i++)
                    if (Kalite.YankiMi(liste[i], eksikler[i].Metin, makineMetinleri[i]))
                        tekrarIdx.Add(i);
                if (tekrarIdx.Count > 0 && !iptal.IsCancellationRequested)
                {
                    var metin2 = tekrarIdx.Select(i => makineMetinleri[i]).ToList();
                    string?[]? ikinci = null;
                    try
                    {
                        var digeri = motorTuru == MotorTuru.Bing ? (MakineMotoru)_google : _bing;
                        ikinci = await digeri.DuzCevirAsync(metin2, ayar.HedefDil,
                                                            ayar.KaynakDilKodu, iptal,
                                                            s.Canli ? 1 : 3)
                                             .ConfigureAwait(false);
                    }
                    catch (Exception e) { Gunluk.Hata("ikinciUcretsiz", e); }
                    if (ikinci is not null && ikinci.Length == tekrarIdx.Count)
                        for (int j = 0; j < tekrarIdx.Count; j++)
                            liste[tekrarIdx[j]] = ikinci[j] ?? "";
                }
            }

            for (int i = 0; i < eksikler.Count; i++)
            {
                var blok = eksikler[i];
                var ceviri = Kalite.EmojileriKoru(blok.Metin, liste[i]);
                // Hâlâ aynen/karışık dönen metin = ücretsiz motorlar
                // çeviremedi. "" işareti konur; Grok tamamlama devralır.
                bool gercekCeviri = !Kalite.HicCevrilmemis(blok.Metin, ceviri)
                    && (motorTuru == MotorTuru.Grok
                        || !Kalite.YankiMi(ceviri, blok.Metin, makineMetinleri[i]));
                if (gercekCeviri)
                {
                    defter.Basarili(blok.Anahtar);
                    bellek[blok.Anahtar] = ceviri;
                    if (motorTuru == MotorTuru.Grok) grokAnahtarlari.Add(blok.Anahtar);
                    continue;
                }
                var ad = motorTuru switch
                {
                    MotorTuru.Bing => "bing",
                    MotorTuru.Google => "google",
                    MotorTuru.Grok => grokEtiketi,
                    _ => "yok",
                };
                // Motor metni değiştirmeden döndürdü. BURASI İKİ AYRI OLAY:
                // (b) çevrilecek bir şey yoktu → motor DOĞRU davrandı, ya da
                // (a) motor gerçekten çeviremedi. Eskiden ikisi de
                // `motor-cevirmedi` yazıp belleğe "" koyuyordu; "" ise
                // "çevrilemedi" PROTOKOLÜ olduğu için Grok tamamlaması ve
                // kalite turu aynı metni sonsuza dek yeniden gönderiyordu.
                // Ölçüm (78 dk): 17 benzersiz metin → 93 ücretli çağrı.
                // NOT: gerçek AĞ arızası buraya HİÇ GELMEZ — o dal yukarıda
                // bos-yanit/ag-hatasi yazıp döner. Buraya ancak motorun
                // YANIT VERDİĞİ durum girer.
                if (Kalite.CevrilecekSeyYokMu(blok.Metin, ayar.HedefDil))
                {
                    // (b): sonuç KABUL EDİLİR — orijinal metin gösterilir,
                    // anahtar KALICI olarak "değişmez", bir daha ücretli çağrı almaz.
                    ArizaGunlugu.Yaz(asama, ad, "cevrilecek-sey-yok", GecenMs(), blok.Metin);
                    defter.DegismezIsaretle(blok.Anahtar);
                    bellek[blok.Anahtar] = ceviri.Length == 0 ? blok.Metin : ceviri;
                    continue;
                }
                // (a): süreli defter tekrar dener AMA TAVAN var. Tavana varan
                // anahtar oturum boyunca vazgeçilir; yoksa aynı metin dakikada
                // bir ücretli çağrı yakmaya devam eder.
                if (defter.Denendi(blok.Anahtar) >= TekrarDefteri.Tavan)
                {
                    ArizaGunlugu.Yaz(asama, ad, "tekrar-tavani", GecenMs(), blok.Metin);
                    defter.DegismezIsaretle(blok.Anahtar);
                    bellek[blok.Anahtar] = ceviri.Length == 0 ? blok.Metin : ceviri;
                    continue;
                }
                ArizaGunlugu.Yaz(asama, ad, "motor-cevirmedi", GecenMs(), blok.Metin);
                bellek[blok.Anahtar] = "";
            }
        }

        // GROK TAMAMLAMA: çeviremediği için "" işaretlenmiş artıklar (bu
        // turdan ya da öncekilerden) Grok'a gider; ekranda çevrilmemiş mesaj
        // KALMAZ ("bazı mesajlara hiç dokunmuyor" şikayeti). YALNIZ motor
        // "ai" ve anahtar varken (sahip kararı #7). Canlı turda atlanır
        // (kuyruğu uzatmasın); ilk çeviri ve ✨ kullanır.
        if (!s.Canli && grokIzinli && !iptal.IsCancellationRequested)
        {
            var artiklar = hedefler.Where(b => bellek.TryGetValue(b.Anahtar, out var c)
                                               && c.Length == 0).ToList();
            if (artiklar.Count > 0)
            {
                try
                {
                    var sonuc = await _grok.CevirAsync(artiklar.Select(b => b.Metin).ToList(),
                                                       GrokBaglami([]), iptal,
                                                       artiklar.Select(b => b.Benim).ToList(),
                                                       s.Canli, s.Hizli, s.ZamanAsimi, asama)
                                           .ConfigureAwait(false);
                    for (int i = 0; i < artiklar.Count && i < sonuc.Ceviriler.Count; i++)
                    {
                        var c = sonuc.Ceviriler[i];
                        if (string.IsNullOrEmpty(c)) continue;
                        bellek[artiklar[i].Anahtar] = c;
                        grokAnahtarlari.Add(artiklar[i].Anahtar);
                    }
                    // Eski etiket "güncel" idi; sonra "Hafızadan ✓" oldu ama bu
                    // kıyas güncellenmediği için ekranda "Hafızadan ✓ +Grok"
                    // gibi yanlış etiket çıkıyordu.
                    motorAdi = motorAdi == "Hafızadan ✓" ? "Grok (artıklar)" : motorAdi + " +Grok";
                }
                catch (Exception e)
                {
                    ArizaGunlugu.Yaz(asama, grokEtiketi, ArizaGunlugu.Sebep(e), GecenMs(),
                                     string.Join(" ", artiklar.Select(b => b.Metin)));
                    Gunluk.Hata("grokTamamlama", e);
                }
            }
        }

        // Yeni çeviriler kalıcı hafızaya: bir daha asla motora gitmesinler.
        // YALNIZ: zehir kalkanından geçmiş, yapay zekâ üretimi (grokAnahtarlari)
        // VE sabit kareden gelen çeviriler. Ücretsiz motorun düşük kaliteli
        // çıktısı kalıcılaşıp sonsuza dek servis ediliyordu; hareketli karenin
        // kesik satırları da öyle.
        if (s.KaliciYaz && grokAnahtarlari.Count > 0)
        {
            foreach (var b in hedefler2)
            {
                if (!grokAnahtarlari.Contains(b.Anahtar)) continue;
                if (bellek.TryGetValue(b.Anahtar, out var c) && c.Length > 0
                    && !_hafiza.UretilenMi(b.Anahtar))
                    _hafiza.Ata(b.Anahtar, c, b.Metin, ayar.HedefDil);
            }
            _hafiza.Kaydet();
        }

        // BAR DÜRÜSTLÜĞÜ: bu turda hiçbir gerçek çeviri üretilmediyse ama
        // "değişiklik gerekmedi" sınıfı blok varsa etiket "Grok ✓" ya da
        // "Hafızadan ✓" deyip kullanıcıyı yanıltmasın. ("hata"/"yok" izleri
        // taşımadığı için DurumMetni bunu HATA rengiyle boyamaz — doğrudur.)
        int degismezBlok = hedefler.Count(b => defter.DegismezMi(b.Anahtar));
        if (degismezBlok > 0 && grokAnahtarlari.Count == 0)
        {
            motorAdi = motorTuru == MotorTuru.Yok && degismezBlok == hedefler.Count
                ? "✓ değişiklik gerekmedi"
                : motorAdi + " · değişiklik gerekmedi";
        }

        // Ekranda taşınan çeviri (anahtarı önbellekte olmayan) boşla ezilmez.
        foreach (var b in hedefler)
            if (bellek.TryGetValue(b.Anahtar, out var c)) b.Ceviri = c;
        Onbellek.Birlestir(bellek, nesil);
        UretilenleriKaydet(bellek);
        return motorAdi;
    }

    /// <summary>Ürettiğimiz her çevirinin normalize anahtarını kaydeder —
    /// zehir kalkanı bu kümeyle "kendi katmanımızı mı okuduk?" kontrolü
    /// yapar (Akis.swift uretilenleriKaydet). DEĞİŞMEZ ((b) sınıfı) KAYIT
    /// BİZİM ÜRETİMİMİZ DEĞİLDİR: metin motora gitti ve AYNEN döndü. Bunu
    /// "ürettiğimiz çeviri" saymak zehir kalkanını YALANCI ALARMA sokar.</summary>
    private void UretilenleriKaydet(IReadOnlyDictionary<string, string> bellek)
    {
        foreach (var (kaynakAnahtar, ceviri) in bellek)
        {
            if (ceviri.Length == 0) continue;
            var a = Kalite.Anahtarla(ceviri);
            if (a == kaynakAnahtar) continue;
            _hafiza.UretilenIsaretle(a);
        }
    }

    /// <summary>
    /// Kullanıcının Türkçe yazdığını karşı tarafın lehçesine çevirir —
    /// Giden.swift girdiCevir. Bu metin DOĞRUDAN müşteriye gidiyor: kapı
    /// (ham + biçimli) geçilmezse <see cref="GidenRet"/> FIRLATIR, hiçbir şey
    /// yapıştırılmaz ve kullanıcı nedeni görür. Etkin motor
    /// <see cref="GidenKapisi.MotorSecimi"/>: ana motor "Ücretsiz çeviri" ise
    /// burada da xAI'ye çıkılmaz (sahip kararı #7).
    /// </summary>
    public async Task<string> GidenCevirAsync(string turkce, CeviriBaglam baglam,
                                              CancellationToken iptal)
    {
        if (string.IsNullOrWhiteSpace(turkce)) throw new GidenRet("Çevrilecek metin boş", "bos");
        var ayar = _ayar();

        if (GidenKapisi.MotorSecimi(ayar.Motor, ayar.GidenMotor) == "bing")
        {
            // Makine motoruyla giden mesaj: lehçe üretemez, yalnız standart
            // dil. Yön tr → karşı dil; metne lehçe standartlaştırması UYGULANMAZ.
            var ham = (await _bing.DuzCevirAsync([turkce], ayar.KaynakDilKodu, "tr", iptal)
                                  .ConfigureAwait(false)).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(ham)) throw new GidenRet("Bing çevirisi başarısız", "motor");
            // Giden yolda EmojileriKoru uygulanmaz (Mac Giden.swift 906-921
            // ile birebir); kapı ham + biçimli çıktıya bakar.
            var bicimli = ayar.GidenKarakter ? Kalite.GidenFormatla(ham) : ham.Trim();
            // Günlük satırı Yonetici'de (kategoriyle): Neden içerik taşır.
            if (GidenKapisi.Kapi(turkce, ham, bicimli) is { } ret) throw ret;
            return bicimli;
        }

        if (!GrokHazir)
            throw new GidenRet("Yapay zekâ anahtarı girilmemiş (Gelişmiş → Yapay Zekâ Anahtarı)", "anahtar");
        return await _grok.GidenCevirAsync(turkce, baglam, iptal).ConfigureAwait(false);
    }

    /// <summary>Sohbete uygun cevap önerileri (AI + sohbet hafızası).
    /// Anahtar yoksa fırlatır — arayüz "Öneri için Grok anahtarı gerekli" der.</summary>
    public Task<List<(string Cevap, string Turkce)>> OneriAsync(IReadOnlyList<string> dokum,
                                                               CeviriBaglam b,
                                                               bool farkli,
                                                               CancellationToken iptal)
    {
        if (!GrokHazir) throw new InvalidOperationException("Öneri için Grok anahtarı gerekli");
        return _grok.OneriAsync(dokum, b, farkli, iptal);
    }
}
