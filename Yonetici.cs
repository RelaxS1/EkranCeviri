using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using EkranCeviri.Arayuz;
using EkranCeviri.Cekirdek;
using EkranCeviri.Ceviri;
using EkranCeviri.Ekran;
using EkranCeviri.Kisayol;
using Rect = System.Windows.Rect;
using Bitmap = System.Drawing.Bitmap;

namespace EkranCeviri;

/// <summary>
/// Uygulamanın beyni: bölge seçimi, canlı döngü, kısayol ve kurtarma
/// katmanları. macOS sürümündeki AppDelegate + Akis.swift'in karşılığı.
///
/// CANLI DÖNGÜ İKİ ŞERİT (Akis.swift, Mac P0 kök nedeni): yakalama/OCR turu
/// (0,8-1 sn ritim) ile ağ işi (Grok 5-28 sn) AYRI koşar. Tek şeritte ağ
/// beklerken kaydırma telafisi duruyor, yamalar yanlış yerde asılı kalıyordu.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public sealed partial class Yonetici : IDisposable
{
    private readonly Dispatcher _arayuz;
    private Ayarlar _ayar;
    private readonly Hafiza _hafiza;
    private readonly Cevirmen _cevirmen;
    private readonly OcrOkuyucu _ocr = new();
    private readonly GlobalKisayol _kisayol = new();
    private readonly TepsiSimgesi _tepsi;

    /// <summary>Ağ işi kuyruğu (Mac ceviriKuyrugu): ilk çeviri ve canlı ağ işi
    /// aynı Blok nesnelerine aynı anda yazamaz. Semafor ANA İŞ PARÇACIĞINI
    /// bloklamıyor — macOS'taki kilitlenmenin kök nedeni buydu.</summary>
    private readonly SemaphoreSlim _isKilidi = new(1, 1);

    private readonly object _durumKilidi = new();
    private List<Blok> _mevcutBloklar = [];

    private KatmanPenceresi? _katman;
    private KontrolCubugu? _cubuk;
    private Rect _bolge;                    // FİZİKSEL piksel

    /// <summary>Son OCR yapılan kare — yama renkleri (YamaBoyaci) bundan
    /// alınır. ARAYÜZ iş parçacığı atıyor/siliyor, İŞ kuyruğu okuyor —
    /// kilitsiz bırakmak "kullanımdan sonra serbest bırakılmış bitmap"
    /// çökmesi demek.</summary>
    private readonly object _kareKilidi = new();
    private Bitmap? _sonKare;

    // ---- canlı tur durumu (Akis.swift canlı alanları)
    /// <summary>Önceki turun 32×32 izi; hareket kararı bununla çalışır.
    /// null = ilk kıyas atlanır (bekçi/yakalama sıfırlaması).</summary>
    private volatile byte[]? _sonIz;
    private volatile double[]? _sonIzdusum;
    // volatile: iki iş parçacığından okunup yazılıyor; derleyicinin
    // yazmaça almasına izin verirsek bayrak değişimi hiç görülmeyebilir.
    private volatile bool _ekranSabitlendi;
    /// <summary>Atomik: `+= 1` oku-değiştir-yaz yarışıydı, artış kaybolunca
    /// geri çekilme eşiği hiç aşılmayabiliyordu (Mac ölçümü).</summary>
    private int _pesPeseHata;
    private HareketDurumu _hareket = new();
    private readonly BosNobetDefteri _bosDefter = new();
    /// <summary>Ağ hatası geri çekilmesinin bitişi (UTC tick). Ayrı zaman
    /// damgası: hareket yolu ve filtre tazelemesi onu ezemez.</summary>
    private long _geriCekilmeBitisTick;
    /// <summary>Yakalama başarısızlığı: üstel geri çekilme (2→4→…→60 sn).</summary>
    private int _yakalamaHataSayaci;
    private long _yakalamaSonrakiDenemeTick;
    /// <summary>Tek zaman kaynağı (saniye): Hareket.Karar Stopwatch tabanlı
    /// çalışır; duvar saati geri sıçrayınca yasak süresi bozuluyordu.</summary>
    private readonly Stopwatch _saat = Stopwatch.StartNew();

    private DispatcherTimer? _canliZaman;
    private readonly DispatcherTimer _saglikBekcisi;
    private readonly DispatcherTimer? _kaydetZaman;

    // ---- iş kimlikleri (bkz. macOS sürümü: geç gelen yanıt yeni katmanı eziyordu)
    private readonly object _epochKilidi = new();
    private int _isEpoch;        // bölge çevirisi
    private int _canliEpoch;     // canlı tur — AYRI tutulur
    /// <summary>Epoch ilerleyince ESKİ iş parçacığındaki ağ çağrısı ≤250 ms'de
    /// düşsün (Mac IsIptali): iptal belirteci epoch'a bağlıdır.</summary>
    private CancellationTokenSource _isIptal = new();
    private CancellationTokenSource _canliIptal = new();

    private volatile bool _isSuruyor;
    private DateTime _isBaslangic;
    private volatile bool _canliMesgul;
    private DateTime _canliBaslangic;
    private volatile bool _canliCeviriSuruyor;
    private volatile bool _canliCeviriBekleyen;
    private DateTime _canliCeviriBaslangic;
    /// <summary>Mac `ceviriKilidi` (Uygulama.swift 103-113) karşılığı: bayrak
    /// ve başlangıç damgası BİRLİKTE yazılır ki 45 sn bekçisi bayrağı görüp
    /// eski damgayı okuyarak taze uçuşu "takıldı" saymasın.</summary>
    private readonly object _canliCeviriKilidi = new();
    /// <summary>Uçuş kimliği: bekçi bayrağı düşürüp YENİ uçuş başladıktan sonra
    /// eski uçuşun `finally`si çalışırsa yeni uçuşu görünmez kılmasın (Mac
    /// Akis.swift 843 `defer` bu deseni taşır; burada uçuş kıyasıyla kapatıldı).
    /// </summary>
    private int _canliCeviriUcusNo;
    private volatile bool _gidenSuruyor;
    private DateTime _gidenBaslangic;

    // ---- kalite turu kuyruğu (KaliteTuru: tek uçuş, birleştirme, tavan)
    private readonly object _kaliteKilidi = new();
    private readonly Dictionary<string, (Blok Blok, DateTime Dogum)> _kaliteBekleyen =
        new(StringComparer.Ordinal);
    private bool _kaliteUcusta;
    private Ayarlar? _kaliteSonAyar;
    private Func<bool>? _kaliteSonEpoch;
    private CancellationToken _kaliteSonIptal;
    /// <summary>Kalite partisinin lehçe/tarz bağlamı için GÖRÜNÜR SOHBET.
    /// `_mevcutBloklar` ilk çeviride kalite işçisi başladığında henüz BOŞ
    /// (liste ancak ekrana basılırken atanır); oradan okumak partiyi bazen
    /// varsayılan lehçe + boş tarz örnekleriyle Grok'a gönderiyordu (yarışa
    /// bağlı). Mac Motorlar.swift 846 lehçeyi GEÇİRİLEN bloklardan algılar;
    /// burada da çağıranın verdiği liste taşınır.</summary>
    private IReadOnlyList<Blok> _kaliteSonSohbet = [];

    /// <summary>Bölge seçiminden önce ön planda olan pencere (odak iadesi).</summary>
    private IntPtr _oncekiPencere;
    /// <summary>Oturum içinde geçmişe yazılan anahtarlar (tekrar yazılmaz).</summary>
    private readonly HashSet<string> _gecmiseYazilan = new(StringComparer.Ordinal);

    public Yonetici(Dispatcher arayuz)
    {
        _arayuz = arayuz;
        _ayar = Ayarlar.Yukle();
        _hafiza = new Hafiza();
        _cevirmen = new Cevirmen(() => _ayar, _hafiza);
        _tepsi = new TepsiSimgesi(this);

        // 5 sn'de bir: takılı bayrakları sıfırla, sahipsiz zamanlayıcıyı durdur.
        // macOS'ta bu bekçi olmadan uygulama sessizce ölü kalıyordu.
        _saglikBekcisi = new DispatcherTimer(DispatcherPriority.Background, arayuz)
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        _saglikBekcisi.Tick += (_, _) => SaglikDenetimi();
        _saglikBekcisi.Start();

        // 20 sn'de bir hafızayı kaydet + geçmişi kırp (Cekirdek.swift kaydetZ).
        // Sınama modunda KURULMAZ: QA koşusu kullanıcının dosyalarına yazamaz.
        if (!Ayarlar.SinamaModuAktif)
        {
            _kaydetZaman = new DispatcherTimer(DispatcherPriority.Background, arayuz)
            {
                Interval = TimeSpan.FromSeconds(20),
            };
            _kaydetZaman.Tick += (_, _) => _ = Task.Run(() =>
            {
                try
                {
                    _hafiza.Kaydet();
                    SohbetGecmisi.Kirp();
                }
                catch (Exception e) { Gunluk.Hata("kaydetZamanlayici", e); }
            });
            _kaydetZaman.Start();
        }
    }

    public Ayarlar Ayar => _ayar;
    public Cevirmen Cevirmen => _cevirmen;
    public Hafiza Hafiza => _hafiza;

    // ================= iş kimlikleri =================

    private int YeniEpoch()
    {
        CancellationTokenSource eski;
        int e;
        lock (_epochKilidi)
        {
            e = ++_isEpoch;
            eski = _isIptal;
            _isIptal = new CancellationTokenSource();
        }
        // Kilit DIŞINDA: iptal geri çağrıları (HttpClient) senkron koşabilir.
        eski.Cancel();
        return e;
    }

    private bool EpochGuncelMi(int e)
    {
        lock (_epochKilidi) return e == _isEpoch;
    }

    /// <summary>Eski canlı işleri geçersiz kılar VE uçuştaki ağ çağrısını
    /// iptal eder. Zamanlayıcıyı durdurmak yetmiyor — iş kuyruğundaki tur
    /// çalışmaya devam edip sonucunu YENİ katmana boyuyordu.</summary>
    private int YeniCanliEpoch()
    {
        CancellationTokenSource eski;
        int e;
        lock (_epochKilidi)
        {
            e = ++_canliEpoch;
            eski = _canliIptal;
            _canliIptal = new CancellationTokenSource();
        }
        eski.Cancel();
        return e;
    }

    private bool CanliEpochGuncelMi(int e)
    {
        lock (_epochKilidi) return e == _canliEpoch;
    }

    private (int Epoch, CancellationToken Iptal) CanliEpochVeIptal()
    {
        lock (_epochKilidi) return (_canliEpoch, _canliIptal.Token);
    }

    private CancellationToken IsIptalBelirteci
    {
        get { lock (_epochKilidi) return _isIptal.Token; }
    }

    /// <summary>Kilit altında kopya verir; çağıran dispose eder.</summary>
    private Bitmap? KareKopyala()
    {
        lock (_kareKilidi)
            return _sonKare is null ? null : (Bitmap)_sonKare.Clone();
    }

    private void KareyiAta(Bitmap? yeni)
    {
        lock (_kareKilidi)
        {
            _sonKare?.Dispose();
            _sonKare = yeni;
        }
    }

    private DateTime GeriCekilmeBitis
    {
        get => new(Volatile.Read(ref _geriCekilmeBitisTick), DateTimeKind.Utc);
        set => Volatile.Write(ref _geriCekilmeBitisTick, value.Ticks);
    }

    private bool GeriCekilmede => DateTime.UtcNow < GeriCekilmeBitis;

    /// <summary>Hızlı-önce/kalite-sonra yolu açık mı? (Akis.swift hizliYol)</summary>
    private bool HizliYolMu(Ayarlar a) =>
        a.KaliteSonra && !a.HizOnceligi && a.Motor == "ai" && _cevirmen.GrokHazir;

    private static string Etiket(string motorAdi, string lehce) =>
        lehce == "Almanca" ? motorAdi : $"{motorAdi} · {lehce}";

    // ================= açılış =================

    public void Basla()
    {
        // Ana iş parçacığı bekçisi + 60 sn'lik özet: "donma" şikâyetinin
        // somut kaydı. WPF'i tanımaz; kuyruk delegeyle verilir.
        Teshis.Paylasilan.Baslat(a => _arayuz.BeginInvoke(a));
        GizlilikGoster();
        IlkKullanimKontrolu();
        OcrDiliKontrolu();
        KisayolKur();
        _tepsi.Goster();
    }

    internal void KisayolKur()
    {
        if (_kisayol.Kaydet(_ayar.KisayolMod, _ayar.KisayolTus, GidenCevirBaslat))
            return;
        // SESSİZCE BAŞARISIZ OLMA: kısayol başka uygulamada kullanılıyorsa
        // kullanıcı bunu bilmeli, yoksa "çalışmıyor" diye düşünür.
        Gunluk.Yaz("kısayol kaydedilemedi (başka uygulama kullanıyor olabilir)");
        _tepsi.Bilgi("Kısayol alınamadı",
                     "Kısayol başka bir uygulamada kullanılıyor olabilir. "
                     + "Ayarlar'dan başka bir tuş seç.");
    }

    private void OcrDiliKontrolu()
    {
        if (OcrOkuyucu.DilVarMi(_ayar.OcrDili)) return;
        var mevcut = string.Join(", ", OcrOkuyucu.MevcutDiller());
        Gunluk.Yaz($"OCR dili '{_ayar.OcrDili}' yok. Mevcut: {mevcut}");
        _tepsi.Bilgi(
            "Almanca yazı tanıma yüklü değil",
            "Windows Ayarlar → Saat ve dil → Dil ve bölge → Dil ekle → "
            + "Almanca (isteğe bağlı özelliklerde 'Temel yazma'yı işaretle). "
            + "Şimdilik mevcut dille okumaya çalışacağım.");
    }

    // ================= bölge çevirisi =================

    /// <summary>Yeni bölge = temiz sayfa (Akis.swift durumuSifirla). Eski
    /// seçimde başka pencereden sızan metinlerin çevirileri yeni bölgeye
    /// bulaşmasın: oturum önbelleği ve tekrar defteri de sıfırlanır.</summary>
    private void DurumuSifirla()
    {
        _hareket = new HareketDurumu();
        KatmaniKapat();
        lock (_durumKilidi) _mevcutBloklar = [];
        _sonIz = null;
        _sonIzdusum = null;
        _ekranSabitlendi = false;
        Interlocked.Exchange(ref _pesPeseHata, 0);
        GeriCekilmeBitis = DateTime.MinValue;
        _yakalamaHataSayaci = 0;
        Volatile.Write(ref _yakalamaSonrakiDenemeTick, 0);
        CanliCeviriUcusunuDusur();
        _canliCeviriBekleyen = false;
        _cevirmen.Sifirla();
    }

    public void BolgeCevirBaslat()
    {
        if (!CevirmeyeHazir())
        {
            _tepsi.Bilgi("Bekle", "Önceki çeviri sürüyor, birazdan tekrar dene.");
            return;
        }
        DurumuSifirla();

        // Seçim için öne çıkmak zorundayız; bitince odak sohbete iade edilir.
        _oncekiPencere = PencereAraclari.OnPlandakiPencere();
        Teshis.Paylasilan.Asama = "bölge seçimi";
        var secici = new BolgeSecici();
        secici.ShowDialog();
        // ODAK İADESİ (iptal dahil): her "Bölgeyi Çevir"den sonra sohbete
        // tıklamak zorunda kalmak "app hissi yok"un en görünür parçasıydı.
        PencereAraclari.OnePlanaGetir(_oncekiPencere);
        _oncekiPencere = IntPtr.Zero;
        if (secici.Sonuc is not { } bolge) return;

        _bolge = bolge;
        _isSuruyor = true;
        _isBaslangic = DateTime.UtcNow;
        var epoch = YeniEpoch();

        // Seçim penceresinin gerçekten kapanması için bir kare bekle;
        // hemen yakalarsak kendi karartmamızı OCR'a veriyoruz.
        _arayuz.BeginInvoke(DispatcherPriority.Background, () =>
            _ = IlkCeviriAsync(epoch));
    }

    private async Task IlkCeviriAsync(int epoch)
    {
        // ZAMAN AŞIMI ŞART: takılı bir tur semaforu sonsuza dek tutarsa
        // "Bölgeyi Çevir" kalıcı olarak ölür (macOS'ta tam olarak bu oldu).
        if (!await _isKilidi.WaitAsync(TimeSpan.FromSeconds(20))
                            .ConfigureAwait(false))
        {
            Gunluk.Yaz("ilkÇeviri: iş kilidi alınamadı (20sn), iptal");
            _isSuruyor = false;
            await Uyar("Meşgul", "Önceki işlem takıldı. Birazdan tekrar dene.")
                .ConfigureAwait(false);
            return;
        }
        var iptal = IsIptalBelirteci;
        try
        {
            if (!EpochGuncelMi(epoch)) return;
            await Task.Delay(180, iptal).ConfigureAwait(false);
            // AYARLARIN KOPYASI işin başında alınır; kullanıcı işin ortasında
            // ayar değiştirirse tur tutarlı kalır.
            var ayar = _ayar;

            using var kare = Yakalama.BolgeYakala(_bolge);
            if (kare is null)
            {
                await Uyar("Ekran yakalanamadı",
                           "Bölge okunamadı. Tekrar dene.").ConfigureAwait(false);
                return;
            }
            if (!EpochGuncelMi(epoch)) return;

            var ocrT0 = Stopwatch.GetTimestamp();
            IReadOnlyList<OcrSatir> satirlar;
            try
            {
                satirlar = await Teshis.Paylasilan
                    .OlcAsync("ocr", () => _ocr.OkuAsync(kare, ayar.OcrDili, iptal))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ArizaGunlugu.Yaz("ocr", "winocr", "ocr-basarisiz",
                                 (int)Stopwatch.GetElapsedTime(ocrT0).TotalMilliseconds, "");
                Gunluk.Hata("ilkOcr", e);
                await Uyar("Yazı tanıma başarısız", e.Message).ConfigureAwait(false);
                return;
            }
            if (satirlar.Count == 0)
            {
                await Uyar("Yazı bulunamadı",
                           "Seçtiğin alanda okunabilir yazı yok. Sohbet "
                           + "balonlarını içine alacak şekilde tekrar seç.")
                    .ConfigureAwait(false);
                return;
            }
            if (!EpochGuncelMi(epoch)) return;

            var (bloklar, _) = Bloklayici.Ayir(
                satirlar, new Size(kare.Width, kare.Height));
            var baglam = BaglamKur(bloklar, ayar);

            // HIZLI-ÖNCE / KALİTE-SONRA (Akis 187-201): hızlı model ekrana
            // basılır, kalıcı hafızaya YAZILMAZ; kalite modeli arka planda
            // düzeltir ve yalnız o yazılır.
            bool hizliYol = HizliYolMu(ayar);
            var onbellekOnce = _cevirmen.Onbellek.Kopyala().Kopya;
            Teshis.Paylasilan.Asama = "ilk çeviri";
            var motorAdi = await _cevirmen
                .BloklariCevirAsync(bloklar, baglam, iptal,
                                    new CeviriSecenekleri(KaliciYaz: !hizliYol, Hizli: hizliYol))
                .ConfigureAwait(false);
            if (hizliYol)
            {
                var motordan = bloklar.Where(b => b.Hedef
                                                  && !onbellekOnce.ContainsKey(b.Anahtar)
                                                  && _hafiza.Bul(b.Anahtar, ayar.HedefDil) is null)
                                      .ToList();
                KaliteyleDuzelt(motordan, bloklar, ayar, () => EpochGuncelMi(epoch), iptal);
            }
            if (bloklar.Any(b => b.Ceviri is not null)) GecmiseAktar(bloklar);

            // Hareket izi TOHUMU: ilk canlı tur "içerik değişti mi"yi bu ize
            // göre ölçer; tohumsuz ilk tur her zaman tam OCR yapıyordu.
            var ilkIz = Yakalama.GoruntuIzi(kare);
            _sonIz = ilkIz;
            _sonIzdusum = Yakalama.SatirIzdusumu(kare);
            _hareket = new HareketDurumu
            {
                SonOcrIzi = ilkIz,
                SonOcrZamani = _saat.Elapsed.TotalSeconds,
            };
            _ekranSabitlendi = false;
            Gunluk.Yaz($"boru: {bloklar.Count} blok, motor={motorAdi}, "
                       + $"çevrilen={bloklar.Count(b => b.Ceviri is not null)}");
            if (!EpochGuncelMi(epoch)) return;

            var kopya = (Bitmap)kare.Clone();
            await _arayuz.InvokeAsync(() =>
            {
                if (!EpochGuncelMi(epoch)) { kopya.Dispose(); return; }
                bool cevrilenVar = bloklar.Any(b => b.Ceviri is not null);
                if (!cevrilenVar && !bloklar.Any(b => b.Hedef))
                {
                    kopya.Dispose();
                    _tepsi.Bilgi("Çevrilecek yazı bulunamadı",
                                 "Karşı tarafın mesajlarını içine alacak şekilde tekrar seç.");
                    return;
                }
                if (!cevrilenVar)
                {
                    // Metin var, çeviri yok (ağ/motor): katmanı orijinallerle
                    // aç, canlı döngü geri çekilmeyle KENDİ toparlansın;
                    // kullanıcı bölgeyi yeniden seçmek zorunda kalmasın.
                    _tepsi.Bilgi("Çeviri alınamadı",
                                 "Bağlantı gelince otomatik denenecek.");
                    Interlocked.Exchange(ref _pesPeseHata, 1);
                }
                lock (_durumKilidi) _mevcutBloklar = bloklar;
                KareyiAta(kopya);
                KatmaniGoster(bloklar, kopya, motorAdi, baglam.LehceKisa);
                if (_ayar.CanliAcik) CanliBaslat();
            });
        }
        catch (OperationCanceledException)
        {
            Gunluk.Yaz($"ilkÇeviri: eski iş (epoch {epoch}) iptal edildi");
        }
        catch (Exception e)
        {
            Gunluk.Hata("ilkCeviri", e);
            await Uyar("Çeviri yapılamadı", e.Message).ConfigureAwait(false);
        }
        finally
        {
            _isSuruyor = false;
            _isKilidi.Release();
        }
    }

    internal CeviriBaglam BaglamKur(IReadOnlyList<Blok> bloklar) => BaglamKur(bloklar, _ayar);

    private static CeviriBaglam BaglamKur(IReadOnlyList<Blok> bloklar, Ayarlar ayar)
    {
        // Lehçe TÜM görünür sohbetten algılanır. Yalnız yeni mesajlara
        // bakmak "belirsiz" sonucu veriyordu.
        var karsiMetinler = bloklar.Where(b => !b.Benim)
                                   .Select(b => b.Metin).ToList();
        var (ad, kisa) = Lehce.Algila(karsiMetinler);
        var son = bloklar.TakeLast(12).ToList();
        return new CeviriBaglam
        {
            Ayar = ayar,
            LehceAd = ad,
            LehceKisa = kisa,
            TarzOrnekleri = karsiMetinler.TakeLast(8).ToList(),
            OncekiKonusma = son
                .Select(b => (b.Benim ? "ben: " : "o: ") + b.Metin).ToList(),
            OncekiKonusmaYapili = son
                .Select(b => new KonusmaSatiri(b.Benim, b.Metin, b.Ceviri)).ToList(),
        };
    }

    // ================= C/D fazlarının güvendiği yardımcılar =================

    internal KatmanPenceresi? Katman => _katman;
    internal KontrolCubugu? Cubuk => _cubuk;

    /// <summary>Kilit altında kopya: liste başka iş parçacığından yeniden
    /// atanırken üzerinde dolaşmak çöküyordu.</summary>
    internal List<Blok> MevcutBloklarKopya()
    {
        lock (_durumKilidi) return [.. _mevcutBloklar];
    }

    /// <summary>Bar etiketi yalnız arayüz iş parçacığından yazılır.</summary>
    internal void MotorEtiketiAyarla(string metin)
    {
        _arayuz.BeginInvoke(() =>
        {
            if (_cubuk is not null) _cubuk.MotorAdi = metin;
        });
    }

    /// <summary>Kullanıcıya sonucu bildir. Bar kapalıyken (katman yokken)
    /// durum metni hiçbir yere yazılmıyordu: yıkıcı ayar eylemleri sessizce
    /// gerçekleşiyordu (Mac geriBildir). Bar varsa etiket, yoksa tepsi
    /// bildirimi.</summary>
    internal void GeriBildir(string metin)
    {
        _arayuz.BeginInvoke(() =>
        {
            if (_cubuk is not null) _cubuk.MotorAdi = metin;
            else _tepsi.Bilgi("Ekran Çeviri", metin);
        });
    }

    /// <summary>
    /// Ekrandaki blokların çevirilerini düşürüp O ANKİ ayarlarla yeniden
    /// çevirir ve katmanı tazeler. Motor/dil değişince çağrılır: ekrandaki
    /// çeviriler ESKİ motorun çıktısı; yenisiyle çevrilmezse "değiştirdim
    /// ama hiçbir şey değişmedi" hissi oluşuyordu. Canlı zamanlayıcıdan
    /// bağımsızdır — canlı KAPALIYKEN çevirileri geri getirecek başka
    /// mekanizma yoktu, dil değiştiren kullanıcının yamaları hiç gelmiyordu.
    /// </summary>
    internal void EkrandakiCevirileriTazele()
    {
        var bloklar = MevcutBloklarKopya();
        if (bloklar.Count == 0 || _katman is null) return;
        foreach (var b in bloklar)
        {
            if (!b.Hedef) continue;
            b.Ceviri = null;
            // AÇIK KULLANICI İSTEĞİ: tekrar tavanı ve "değişmez" işareti
            // yalnız otomatik turları frenler.
            TekrarDefteri.Paylasilan.TekrarAc(b.Anahtar);
        }
        _ekranSabitlendi = false;
        MotorEtiketiAyarla("yeniden çevriliyor…");
        var baglam = BaglamKur(bloklar);
        var epoch = YeniEpoch();
        var iptal = IsIptalBelirteci;
        _ = Task.Run(async () =>
        {
            try
            {
                var motorAdi = await _cevirmen
                    .BloklariCevirAsync(bloklar, baglam, iptal)
                    .ConfigureAwait(false);
                if (!EpochGuncelMi(epoch)) return;
                var kare = KareKopyala();
                await _arayuz.InvokeAsync(() =>
                {
                    using (kare)
                    {
                        if (!EpochGuncelMi(epoch) || _katman is null || kare is null) return;
                        using var boyaci = new YamaBoyaci(kare);
                        _katman.MetniTazele(bloklar, boyaci);
                        if (_cubuk is not null)
                            _cubuk.MotorAdi = Etiket(motorAdi, baglam.LehceKisa);
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Gunluk.Hata("yenidenCevir", e);
                GeriBildir("Çeviri yapılamadı: " + e.Message);
            }
        });
    }

    private void KatmaniGoster(IReadOnlyList<Blok> bloklar, Bitmap kare,
                               string motorAdi, string lehce)
    {
        _katman = new KatmanPenceresi(_bolge);
        _katman.Show();
        using var boyaci = new YamaBoyaci(kare);
        _katman.Guncelle(bloklar, boyaci);

        _cubuk = new KontrolCubugu(this, _bolge);
        _cubuk.MotorAdi = Etiket(motorAdi, lehce);
        _cubuk.Show();
    }

    public void KatmaniKapat()
    {
        CanliDurdur();
        _katman?.Close();
        _katman = null;
        _cubuk?.Close();
        _cubuk = null;
        KareyiAta(null);
    }

    // ================= geçmiş =================

    /// <summary>Yeni görülen mesajları yerel sohbet geçmişine yazar (oturum
    /// içinde tekrar yazılmaz) — Akis.swift gecmiseAktar.</summary>
    private void GecmiseAktar(IReadOnlyList<Blok> bloklar)
    {
        if (!_ayar.GecmisAcik) return;
        foreach (var b in bloklar)
        {
            if (!b.Hedef) continue;
            // Kendi ürettiğimiz çeviri metni ASLA "gelen mesaj" olarak
            // kaydedilmez: gerçek geçmişte Türkçe çeviriler 'karsi' mesajı
            // olarak birikmişti — hem hafızayı kirletiyor hem tekrar çeviriye
            // yol açıyordu.
            if (_hafiza.UretilenMi(b.Anahtar)) continue;
            // BOŞ ÇEVİRİYLE YAZMA: kayıt "yazıldı" işaretlendiği için bir
            // daha güncellenmiyor ve geçmişte çevirisiz satır kalıyordu.
            var c = b.Ceviri;
            if (string.IsNullOrEmpty(c)) continue;
            bool yeni;
            lock (_gecmiseYazilan) yeni = _gecmiseYazilan.Add(b.Anahtar);
            if (!yeni) continue;
            SohbetGecmisi.Yaz(b.Benim ? "ben" : "karsi", b.Metin, c);
        }
    }

    /// <summary>Geçmiş silindi: oturum içi "yazıldı" kümesi de boşalır ki
    /// ekrandaki mesajlar yeni dosyaya yeniden düşsün.</summary>
    internal void GecmisKumesiniTemizle()
    {
        lock (_gecmiseYazilan) _gecmiseYazilan.Clear();
    }

    // ================= canlı mod =================

    public bool CanliAcik
    {
        get => _canliZaman is not null;
        set
        {
            _ayar.CanliAcik = value;
            _ayar.Kaydet();
            if (value) CanliBaslat(); else CanliDurdur();
        }
    }

    private void CanliBaslat()
    {
        CanliDurdur();
        if (_katman is null) return;
        // 0,8 sn (Mac ritmi): kaydırma telafisi bu ritimle akar; 1 sn'de
        // yamalar içerikten görünür biçimde geri kalıyordu.
        _canliZaman = new DispatcherTimer(DispatcherPriority.Background, _arayuz)
        {
            Interval = TimeSpan.FromMilliseconds(800),
        };
        _canliZaman.Tick += (_, _) => CanliTur();
        _canliZaman.Start();
    }

    private void CanliDurdur()
    {
        _canliZaman?.Stop();
        _canliZaman = null;
        _bosDefter.Sifirla();
        TekrarDefteri.Paylasilan.Sifirla();
        // Uçuştaki tur artık geçersiz; ağ çağrısı da iptal belirteciyle düşer.
        YeniCanliEpoch();
    }

    private void YakalamaSayaciSifirla()
    {
        _yakalamaHataSayaci = 0;
        Volatile.Write(ref _yakalamaSonrakiDenemeTick, 0);
    }

    /// <summary>Arayüz iş parçacığında, zamanlayıcıdan (Akis.swift canliTur).
    /// Yalnız yakalama/OCR turunu başlatır; ağ işi ayrı şeritte.</summary>
    private void CanliTur()
    {
        var simdi = DateTime.UtcNow;
        // BEKÇİ: bir tur 25 sn'yi aştıysa (OCR/yakalama yanıtsız) kilidi kır.
        // Bu kilit takılınca canlı mod sessizce ölüyordu. Epoch da ilerler:
        // takılı tur geç bitince eski bölgeye boyamasın.
        if (_canliMesgul && simdi - _canliBaslangic > TimeSpan.FromSeconds(25))
        {
            Gunluk.Yaz("canlı: bekçi kilidi kırdı (takılı tur)");
            _canliMesgul = false;
            _ekranSabitlendi = false;
            _sonIz = null;
            YakalamaSayaciSifirla();
            YeniCanliEpoch();
        }
        // Ağ işi ayrı şeritte; o da takılırsa (kara delik) serbest bırak:
        // epoch ilerleyince iptal belirteci HttpClient'ı düşürür.
        bool ceviriTakildi;
        lock (_canliCeviriKilidi)
        {
            ceviriTakildi = _canliCeviriSuruyor
                && simdi - _canliCeviriBaslangic > TimeSpan.FromSeconds(45);
        }
        if (ceviriTakildi)
        {
            Gunluk.Yaz("canlı: çeviri işi 45sn takıldı → iptal");
            CanliCeviriUcusunuDusur();
            _canliCeviriBekleyen = false;
            _ekranSabitlendi = false;
            YeniCanliEpoch();
        }
        if (_isSuruyor || _canliMesgul || _katman is null) return;
        // Yakalama geri çekilmesi: SCK/BitBlt üst üste düştüyse bekle.
        if (simdi.Ticks < Volatile.Read(ref _yakalamaSonrakiDenemeTick)) return;

        _canliMesgul = true;
        _canliBaslangic = simdi;
        var (turEpoch, iptal) = CanliEpochVeIptal();
        _ = Task.Run(() => CanliTurAsync(turEpoch, iptal));
    }

    /// <summary>1. ŞERİT: yakalama → iz/izdüşüm → kaydırma telafisi → hareket
    /// kararı → uygunsa OCR + eşleştirme + 1. faz boyama. Ağ beklemez.</summary>
    private async Task CanliTurAsync(int turEpoch, CancellationToken iptal)
    {
        try
        {
            if (!CanliEpochGuncelMi(turEpoch)) return;

            using var kare = Yakalama.BolgeYakala(_bolge);
            if (kare is null)
            {
                YakalamaBasarisiz(turEpoch);
                return;
            }
            if (_yakalamaHataSayaci > 0) YakalamaSayaciSifirla();

            var iz = Yakalama.GoruntuIzi(kare);
            var izdusum = Yakalama.SatirIzdusumu(kare);
            var onceki = _sonIz;
            var oncekiIzdusum = _sonIzdusum;
            _sonIz = iz;
            _sonIzdusum = izdusum;
            // İlk kıyas atlanır (bekçi/yakalama sıfırlaması sonrası taze iz).
            if (onceki is null) return;
            double fark = Yakalama.IzFarki(iz, onceki);

            // İÇERİK KAYDI MI? Kaydıysa yamaları OCR beklemeden kaydır;
            // tanınmayacak kadar değiştiyse yamaları GİZLE (yanlış yerde
            // yama göstermektense hiç gösterme).
            if (fark > 0.012 && oncekiIzdusum is not null)
            {
                var (kayma, benzerlik) = Yakalama.DikeyKayma(oncekiIzdusum, izdusum);
                double bolgeYukseklik = kare.Height;
                double satirBasina = bolgeYukseklik / 256.0;
                double dy = kayma * satirBasina;
                await _arayuz.InvokeAsync(() =>
                {
                    if (!CanliEpochGuncelMi(turEpoch) || _katman is null) return;
                    var g = _katman.Gorunum;
                    // Toplam ofset bölgenin %90'ını aşarsa yama zaten görünür
                    // alanın dışında: gizle (ofset DIU, dy fiziksel → ölçekle).
                    double toplamOfset = g.OfsetY * _katman.Olcek + dy;
                    if (benzerlik > 0.55 && Math.Abs(kayma) >= 1
                        && Math.Abs(dy) < bolgeYukseklik * 0.5
                        && Math.Abs(toplamOfset) < bolgeYukseklik * 0.9)
                    {
                        g.Gizle(false);
                        _katman.Kaydir(dy);      // içerikle birlikte kay
                    }
                    else
                    {
                        // ÖLÜ BÖLGE OLMAZ: güvenilir kaydıramıyorsak gizle.
                        g.Gizle(true);
                    }
                });
            }

            // HAREKET KARARI (animasyon maskesi + son-OCR-izi kıyası):
            // "yazıyor…" noktaları, GIF, ses dalgası gibi SÜREKLİ değişen
            // hücreler OCR tetiklemez; içerik gerçekten değiştiyse ekran
            // sabitlenince hemen, sürekli hareket altında en fazla 8 sn'de bir
            // OCR yapılır. Eski hâli: hareket sürerken her 3 turda zorla tam
            // OCR + ağ çağrısı. Onarımın yeniden-deneme yolları
            // (ekranSabitlendi=false) korunur: sabit karede o bayrak da tetikler.
            var durum = _hareket;
            double simdi = _saat.Elapsed.TotalSeconds;
            var geriBitis = GeriCekilmeBitis;
            var an = DateTime.UtcNow;
            durum.OcrYasakBitis = geriBitis > an
                ? simdi + (geriBitis - an).TotalSeconds : 0;
            var karar = Hareket.Karar(iz, onceki, durum, simdi);
            // "hareketli" = ortalama fark YA DA anlamlı hücre değişimi (küçük
            // balon ortalamada kaybolur; gerçek ekran testinde yakalandı)
            bool hareketli = !karar.Sabit;
            if (hareketli) _ekranSabitlendi = false;
            bool yenidenDene = !_ekranSabitlendi && !hareketli && !GeriCekilmede;
            // Uçuşta çeviri varken OCR yığılmasın (Grok'un 5-28 sn'si boyunca
            // her 0,8 sn tam OCR pil yakıyordu).
            bool uygun = (karar.OcrYap || yenidenDene) && !_canliCeviriSuruyor;
            if (!uygun)
            {
                Teshis.Paylasilan.Say(karar.IcerikDegisti ? "ocr-ertelendi" : "ocr-atlandi");
                return;
            }
            durum.SonOcrIzi = iz;
            durum.SonOcrZamani = simdi;
            _ekranSabitlendi = !hareketli;
            // Tek besleyici pesPeseHata olamaz: sağlıklı ağda hiç 3'e
            // ulaşmıyor ve çevrilemeyen mesaj sonsuza dek öyle kalıyordu.
            bool nobetciDene = _bosDefter.TurAcikMi(Volatile.Read(ref _pesPeseHata));
            if (nobetciDene) _bosDefter.TurBasladi();
            Teshis.Paylasilan.Say(hareketli ? "ocr-hareketli" : "ocr-sabit");
            await CanliGuncelleAsync(kare, turEpoch, iptal, hareketli, nobetciDene)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Gunluk.Hata("canliTur", e);
            _ekranSabitlendi = false;
        }
        finally
        {
            _canliMesgul = false;
        }
    }

    /// <summary>Yakalama düştü: ÜSTEL geri çekil (2→4→…→60 sn). Eski hâli her
    /// turda yeniden deneyip kullanıcıya hiçbir şey söylemiyordu.</summary>
    private void YakalamaBasarisiz(int turEpoch)
    {
        _sonIz = null;
        int sayac = Interlocked.Increment(ref _yakalamaHataSayaci);
        double bekle = Math.Min(60.0, Math.Pow(2.0, Math.Min(sayac, 6)));
        Volatile.Write(ref _yakalamaSonrakiDenemeTick,
                       DateTime.UtcNow.AddSeconds(bekle).Ticks);
        Gunluk.Yaz($"canlı: yakalama başarısız ({sayac}) → {(int)bekle}sn");
        if (sayac != 5) return;
        _arayuz.BeginInvoke(() =>
        {
            if (!CanliEpochGuncelMi(turEpoch) || _cubuk is null) return;
            _cubuk.MotorAdi = "Ekran yakalanamıyor — yeniden dene";
        });
    }

    /// <summary>İçerik duruldu: OCR, ESKİLERLE EŞLEŞTİRME (eşleşen balon
    /// ekranda hiç kıpırdamaz; yalnız gerçekten yeni mesaj çevrilir), 1. faz
    /// boyama. Eksik varsa ağ işi AYRI şeride verilir; bu tur burada biter.
    /// - kareHareketli: kaydırma ortasındaki kare; çeviriler kalıcı hafızaya yazılmaz.
    /// - bosNobetciDene: geri çekilme bitti; motor=ai'de "" işaretli (Grok boş
    ///   dönmüş) girdiler bir kez daha motora gitsin.</summary>
    private async Task CanliGuncelleAsync(Bitmap kare, int turEpoch, CancellationToken iptal,
                                          bool kareHareketli, bool bosNobetciDene)
    {
        var ayar = _ayar;
        IReadOnlyList<OcrSatir> satirlar;
        try
        {
            satirlar = await Teshis.Paylasilan
                .OlcAsync("ocr", () => _ocr.OkuAsync(kare, ayar.OcrDili, iptal))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            Gunluk.Hata("canliOcr", e);
            _ekranSabitlendi = false;   // OCR aksadı: sonraki tur dener
            return;
        }
        // Windows uyarlaması: OCR motoru yokken/aksayınca boş liste döner —
        // bunu "ekran boşaldı" sayıp yamaları silmek yanlış; sonraki tur dener.
        if (satirlar.Count == 0) { _ekranSabitlendi = false; return; }
        if (!CanliEpochGuncelMi(turEpoch)) return;

        var (yeniBloklar, _) = Bloklayici.Ayir(
            satirlar, new Size(kare.Width, kare.Height));
        Gunluk.Yaz($"canlı: OCR {satirlar.Count} satır → {yeniBloklar.Count} blok");

        // ZEHİR KALKANI: karede kendi ürettiğimiz çevirilerden 2+
        // görünüyorsa katmanımız yakalamaya girmiş demektir
        // (WDA_EXCLUDEFROMCAPTURE tutmamış). YALNIZ uzun metinler sayılır:
        // "Tamam" gibi kısa çeviriler ekranda gerçek mesaj olarak da geçer
        // ve yanlış alarm canlı modu tamamen durduruyordu.
        int kirli = yeniBloklar.Count(b =>
            b.Hedef && b.Anahtar.Length >= 12 && _hafiza.UretilenMi(b.Anahtar));
        if (kirli >= 2)
        {
            Gunluk.Yaz($"canlı: ZEHİR KALKANI devrede (kirli={kirli})");
            _ekranSabitlendi = false;
            return;
        }

        var onbellek = _cevirmen.Onbellek;
        var eskiler = MevcutBloklarKopya();
        foreach (var yeni in yeniBloklar)
        {
            int i = eskiler.FindIndex(e => Bloklayici.Eslesirler(e, yeni));
            if (i >= 0)
            {
                var eski = eskiler[i];
                eskiler.RemoveAt(i);
                var eskiCeviri = eski.Ceviri;
                // BOŞ ÇEVİRİ MİRAS ALINMAZ: "" taşınınca aşağıdaki önbellek
                // kurtarma dalı ölüyor ve mesaj kaydırmalı karede kalıcı
                // olarak çevrilmemiş kalıyordu.
                yeni.Ceviri = string.IsNullOrEmpty(eskiCeviri) ? null : eskiCeviri;
                // Eski liste çevirisiz kaldı ama çeviri bu arada önbelleğe
                // düştüyse (uçuş sürerken OCR turu listeyi yenilemişti) 1. FAZ
                // hemen boyasın; motora gidecek bir şey kalmasın.
                if (yeni.Ceviri is null && onbellek[yeni.Anahtar] is { Length: > 0 } hazir)
                    yeni.Ceviri = hazir;

                // OCR'ın milimetrik oynaması balonu kıpırdatmasın
                if (Math.Abs(eski.Kutu.X - yeni.Kutu.X) <= 8
                    && Math.Abs(eski.Kutu.Y - yeni.Kutu.Y) <= 8
                    && Math.Abs(eski.Kutu.Width - yeni.Kutu.Width) <= 12
                    && Math.Abs(eski.Kutu.Height - yeni.Kutu.Height) <= 12)
                    yeni.Kutu = eski.Kutu;

                // Anahtar değişiminde önbelleği güncelle: taşınan çeviri
                // önbellekte de bilinsin ki motor bir daha çağrılmasın ve
                // motor hata yolunda silinmesin.
                if (yeni.Anahtar != eski.Anahtar && !string.IsNullOrEmpty(eskiCeviri))
                    onbellek[yeni.Anahtar] = eskiCeviri;
            }
            else if (onbellek[yeni.Anahtar] is { } hazir)
            {
                if (hazir.Length > 0)
                {
                    yeni.Ceviri = hazir;
                }
                else if (ayar.Motor == "ai")
                {
                    // Grok denedi, boş döndü: her sabit turda boşuna motora
                    // gidilmesin. Geri çekilme bitince ya da ✨ ile yeniden denenir.
                    if (bosNobetciDene && _bosDefter.HakVarMi(yeni.Anahtar))
                        onbellek.Sil(yeni.Anahtar);   // null → eksik
                    else
                        yeni.Ceviri = "";
                }
                // motor != "ai": "" null kalır ki Grok tamamlama devralsın
            }
        }
        bool eksikVar = yeniBloklar.Any(b => b.Hedef && b.Ceviri is null);

        // OCR birkaç yüz ms sürdü: bu arada bölge kapandıysa/değiştiyse
        // ESKİ bölgenin bloklarını yeni katmana yazma.
        if (!CanliEpochGuncelMi(turEpoch)) return;
        lock (_durumKilidi) _mevcutBloklar = yeniBloklar;

        // 1. FAZ — HEMEN boya: eşleşen çeviriler yeni konumlarına anında
        // oturur. "Eski konumda asılı yama + yeni çeviri üst üste"
        // görüntüsünün çözümü budur: ekran her zaman gerçeği gösterir,
        // yalnız gerçekten yeni mesaj motoru bekler.
        var kopya = (Bitmap)kare.Clone();
        await _arayuz.InvokeAsync(() =>
        {
            if (!CanliEpochGuncelMi(turEpoch) || _katman is null)
            {
                kopya.Dispose();
                return;
            }
            using (var boyaci = new YamaBoyaci(kopya))
                _katman.Guncelle(yeniBloklar, boyaci);
            KareyiAta(kopya);   // sahiplik: kalite/tazele renkleri buradan alır
        });

        if (!eksikVar)
        {
            GecmiseAktar(yeniBloklar);
            return;
        }
        // 2. FAZ — yalnız eksik (yeni) bloklar motora gider; AYRI ŞERİTTE.
        // canliMesgul OCR süresi içinde serbest kalır, yakalama/kaydırma
        // ritmi Grok'u beklemez.
        CanliCeviriyiKuyrugaAl(yeniBloklar, turEpoch, iptal, kareHareketli, ayar);
    }

    /// <summary>2. ŞERİT — canlı ağ işi, TEK UÇUŞLU. Uçuşta bir çeviri varsa
    /// yenisi YIĞILMAZ; bekleyen bayrağı kalkar ki sonraki sabit tur yeniden
    /// denesin (çeviri gelince önbelleğe düşer, o tur onu ekrana taşır).</summary>
    private void CanliCeviriyiKuyrugaAl(List<Blok> yeniBloklar, int turEpoch,
                                        CancellationToken iptal, bool kareHareketli,
                                        Ayarlar ayar)
    {
        int ucusNo;
        lock (_canliCeviriKilidi)
        {
            if (_canliCeviriSuruyor)
            {
                _canliCeviriBekleyen = true;     // uçuş bitince yeniden dene
                return;
            }
            // Damga ve bayrak aynı kilit altında (Mac setter'ı gibi): bekçi
            // bayrağı gördüğünde damga da taze olsun.
            _canliCeviriBaslangic = DateTime.UtcNow;
            ucusNo = ++_canliCeviriUcusNo;
            _canliCeviriSuruyor = true;
        }
        var eksikler = yeniBloklar.Where(b => b.Hedef && b.Ceviri is null).ToList();
        Gunluk.Yaz($"canlı: eksik var, motora gidiyor ({eksikler.Count} blok)");
        // Kullanıcı beklerken bar dürüst olsun: "çevriliyor" görünsün
        _arayuz.BeginInvoke(() =>
        {
            if (CanliEpochGuncelMi(turEpoch) && _cubuk is not null)
                _cubuk.MotorAdi = "⏳ çevriliyor…";
        });
        bool hizliYol = HizliYolMu(ayar);

        _ = Task.Run(async () =>
        {
            try
            {
                Teshis.Paylasilan.Asama = "canlı çeviri";
                if (!CanliEpochGuncelMi(turEpoch)) return;
                var baglam = BaglamKur(yeniBloklar, ayar);
                string motorAdi;
                // Ağ işi kuyruğu: ilk çeviri/✨ ile aynı Blok'lara aynı anda yazılmaz.
                if (!await _isKilidi.WaitAsync(TimeSpan.FromSeconds(30), iptal)
                                    .ConfigureAwait(false))
                {
                    Gunluk.Yaz("canlı: iş kilidi alınamadı (30sn)");
                    _ekranSabitlendi = false;
                    return;
                }
                try
                {
                    motorAdi = await _cevirmen
                        .BloklariCevirAsync(yeniBloklar, baglam, iptal,
                            new CeviriSecenekleri(KaliciYaz: !kareHareketli && !hizliYol,
                                                  Canli: true, Hizli: hizliYol))
                        .ConfigureAwait(false);
                }
                finally
                {
                    _isKilidi.Release();
                }
                Gunluk.Yaz($"canlı: motor={motorAdi}, kalan eksik="
                           + $"{yeniBloklar.Count(b => b.Hedef && b.Ceviri is null)}");
                // Önbellek ve "üretilenler" Cevirmen içinde bölgeden bağımsız
                // tutuldu (nesil güncelse); ekrana boyama ve durum bayrakları
                // ise yalnız tur hâlâ geçerliyse.
                if (!CanliEpochGuncelMi(turEpoch)) return;

                // BAŞARI = BOŞ DEĞİL. Eskiden yalnız null başarısızlık sayılıyor,
                // "çevrilemedi" işareti olan "" BAŞARI olarak geçip pesPeseHata'yı
                // sıfırlıyordu. Deneme hakkı bitmiş anahtar başarısızlık saymaz
                // (yoksa çevrilemeyen tek mesaj kalıcı geri çekilme üretirdi).
                int basarisiz = 0;
                foreach (var b in yeniBloklar)
                {
                    if (!b.Hedef) continue;
                    if (!string.IsNullOrEmpty(b.Ceviri))
                    {
                        _bosDefter.Basarili(b.Anahtar);
                    }
                    else if (_bosDefter.HakVarMi(b.Anahtar))
                    {
                        _bosDefter.Denendi(b.Anahtar);
                        basarisiz++;
                    }
                }
                if (basarisiz == 0)
                {
                    Interlocked.Exchange(ref _pesPeseHata, 0);
                    GecmiseAktar(yeniBloklar);
                }
                else
                {
                    // Çeviri eksik kaldı: yeniden dene AMA sonsuz döngüye girme.
                    // Wi-Fi koptuğunda saniyede bir tam OCR + başarısız istek
                    // yapıp pili bitiriyordu: art arda 3 başarısızlıktan sonra
                    // 60 sn bekle (denetim bulgusu).
                    int hataSayisi = Interlocked.Increment(ref _pesPeseHata);
                    if (hataSayisi >= 3)
                    {
                        double bekle = hataSayisi >= 6 ? 120 : 60;
                        GeriCekilmeBitis = DateTime.UtcNow.AddSeconds(bekle);
                        Gunluk.Yaz($"canlı: {hataSayisi} hata → {(int)bekle}sn bekleme");
                    }
                    _ekranSabitlendi = false;   // bekleme bitince ilk sabit kare dener
                }

                // Çeviri bitince sadece TEK SEFERDE ekrana bas (yanıp sönmeyi önler)
                await _arayuz.InvokeAsync(() =>
                {
                    if (!CanliEpochGuncelMi(turEpoch)) return;
                    // Uçuş sırasında yeni eksik görüldüyse ya da bir OCR turu blok
                    // listesini değiştirdiyse bu listeyi boyama: çeviriler
                    // önbellekte, sonraki sabit tur alır.
                    bool bekleyen = _canliCeviriBekleyen;
                    _canliCeviriBekleyen = false;
                    bool ayniListe;
                    lock (_durumKilidi) ayniListe = ReferenceEquals(_mevcutBloklar, yeniBloklar);
                    if (bekleyen || !ayniListe || _katman is null)
                    {
                        _ekranSabitlendi = false;
                        return;
                    }
                    using var kareKopya = KareKopyala();
                    if (kareKopya is null) return;
                    using var boyaci = new YamaBoyaci(kareKopya);
                    _katman.Guncelle(yeniBloklar, boyaci);
                    if (_cubuk is not null) _cubuk.MotorAdi = Etiket(motorAdi, baglam.LehceKisa);
                });

                // KALİTE-SONRA: motora gidip hızlı çevrilenler (hafıza isabeti
                // olmayanlar) kalite modeliyle arka planda düzeltilir. BOŞ
                // kalanlar da dahil: hızlı model çeviremediyse kalite modeli
                // bir şans daha alsın (yoksa mesaj hiç çevrilmiyor).
                if (hizliYol && !kareHareketli)
                {
                    var motordan = eksikler
                        .Where(b => _hafiza.Bul(b.Anahtar, ayar.HedefDil) is null).ToList();
                    KaliteyleDuzelt(motordan, yeniBloklar, ayar,
                                    () => CanliEpochGuncelMi(turEpoch), iptal);
                }
            }
            catch (OperationCanceledException)
            {
                _ekranSabitlendi = false;
            }
            catch (Exception e)
            {
                Gunluk.Hata("canliCeviri", e);
                _ekranSabitlendi = false;
            }
            finally
            {
                // Yalnız KENDİ uçuşunun bayrağını indir: bekçi bu uçuşu düşürüp
                // yenisi başladıysa kimlik ilerlemiştir, dokunma.
                lock (_canliCeviriKilidi)
                {
                    if (_canliCeviriUcusNo == ucusNo) _canliCeviriSuruyor = false;
                }
            }
        });
    }

    /// <summary>Uçuş bayrağını indirir ve kimliği ilerletir: sürmekte olan
    /// uçuşun geç kalan `finally`si artık sahipsizdir, hiçbir şeye dokunmaz.
    /// Bekçi (45 sn) ve bölge sıfırlama buradan geçer.</summary>
    private void CanliCeviriUcusunuDusur()
    {
        lock (_canliCeviriKilidi)
        {
            _canliCeviriSuruyor = false;
            _canliCeviriUcusNo++;
        }
    }

    // ================= kalite turu (hızlı-önce / kalite-sonra) =================

    /// <summary>
    /// Hızlı modelin sonucu ekranda dururken aynı mesajlar kalite modeliyle
    /// yeniden çevrilir; anlamlı fark varsa TEK repaint ile değiştirilir ve
    /// KALICI hafızaya yalnız kalite sonucu yazılır. Paylaşılan Blok
    /// nesnelerine arka plandan DOKUNULMAZ: kopya bloklar çevrilir, sonuç
    /// arayüz kuyruğunda anahtarla uygulanır. Geri basınç: tek uçuşlu
    /// işçi, partiler birleşir, derinlik tavanı (KaliteTuru).
    /// </summary>
    private void KaliteyleDuzelt(IReadOnlyList<Blok> bloklar, IReadOnlyList<Blok> sohbet,
                                 Ayarlar ayar, Func<bool> epochKontrol,
                                 CancellationToken iptal)
    {
        var simdi = DateTime.UtcNow;
        var adaylar = new List<(string Anahtar, Blok Kopya)>();
        foreach (var b in bloklar)
        {
            if (!b.Hedef) continue;
            // İSABET KAPISI + TEKRAR TAVANI: `anahtar` verilmezse defter
            // görülmez ve aynı metin her sabit karede yeniden ücretlenir
            // (ölçüldü: tek bir metin için 13 `grok-kalite` çağrısı).
            if (!Kalite.KaliteAdayiMi(b.Anahtar, b.Metin, b.Ceviri, ayar.HedefDil)) continue;
            adaylar.Add((b.Anahtar, b.Kopya()));
        }
        if (adaylar.Count == 0) return;

        bool baslat = false;
        lock (_kaliteKilidi)
        {
            foreach (var (anahtar, kopya) in adaylar)
                _kaliteBekleyen[anahtar] = (kopya, simdi);
            if (_kaliteBekleyen.Count > KaliteTuru.DerinlikTavani)
            {
                var atilacak = _kaliteBekleyen.OrderBy(p => p.Value.Dogum)
                    .Take(_kaliteBekleyen.Count - KaliteTuru.DerinlikTavani)
                    .Select(p => p.Key).ToList();
                foreach (var a in atilacak) _kaliteBekleyen.Remove(a);
            }
            _kaliteSonAyar = ayar;
            _kaliteSonEpoch = epochKontrol;
            _kaliteSonIptal = iptal;
            // Liste kopyası: çağıranın listesi ekran turu tarafından yeniden
            // atanabilir; bağlam kurulurken üzerinde dolaşmak güvenli kalsın.
            _kaliteSonSohbet = [.. sohbet];
            if (!_kaliteUcusta) { _kaliteUcusta = true; baslat = true; }
        }
        // Uçuşta iş varsa bu parti ona eklendi; ikinci iş AÇILMAZ.
        if (baslat) _ = Task.Run(KaliteTuruCalistirAsync);
    }

    private async Task KaliteTuruCalistirAsync()
    {
        while (true)
        {
            Dictionary<string, (Blok Blok, DateTime Dogum)> parti;
            Ayarlar? ayar;
            Func<bool>? epochKontrol;
            CancellationToken iptal;
            IReadOnlyList<Blok> sohbet;
            lock (_kaliteKilidi)
            {
                parti = new Dictionary<string, (Blok, DateTime)>(_kaliteBekleyen, StringComparer.Ordinal);
                ayar = _kaliteSonAyar;
                epochKontrol = _kaliteSonEpoch;
                iptal = _kaliteSonIptal;
                sohbet = _kaliteSonSohbet;
                _kaliteBekleyen.Clear();
                if (parti.Count == 0 || ayar is null || epochKontrol is null)
                {
                    _kaliteUcusta = false;
                    return;
                }
            }
            try
            {
                await KalitePartisiniIsleAsync(parti, sohbet, ayar, epochKontrol, iptal)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Gunluk.Hata("kaliteTuru", e); }
        }
    }

    /// <summary>Kalite partisi ayarların KOPYASIYLA çalışır: motor "ai",
    /// hız önceliği kapalı (kalite modeli). Anahtar JSON'a yazılmadığı için
    /// ayrıca taşınır.</summary>
    private static Ayarlar KaliteAyari(Ayarlar a)
    {
        Ayarlar kopya;
        try
        {
            kopya = JsonSerializer.Deserialize<Ayarlar>(JsonSerializer.Serialize(a))
                    ?? SigKopya(a);
        }
        catch (Exception) { kopya = SigKopya(a); }
        // Hangi yoldan gelirse gelsin kalite koşulu GARANTİ: motor "ai", hız
        // önceliği kapalı. Eskiden JSON aksarsa `a` aynen dönüyor, kalite
        // partisi hızlı modelle gidebiliyordu (denetim bulgusu, savunmacı).
        kopya.GrokApiKey = a.GrokApiKey;
        kopya.Motor = "ai";
        kopya.HizOnceligi = false;
        return kopya;
    }

    /// <summary>JSON gidiş-dönüşü aksarsa yansımayla sığ kopya. Canlı ayar
    /// nesnesi ASLA yerinde değiştirilmez: kalite partisinin zorladığı
    /// Motor/HizOnceligi kullanıcının ayarına sızmamalı. Ayarlar düz
    /// skalarlardan oluştuğu için sığ kopya yeterli.</summary>
    private static Ayarlar SigKopya(Ayarlar a)
    {
        var k = new Ayarlar();
        foreach (var p in typeof(Ayarlar).GetProperties(
                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                p.SetValue(k, p.GetValue(a));
        }
        return k;
    }

    private async Task KalitePartisiniIsleAsync(
        Dictionary<string, (Blok Blok, DateTime Dogum)> parti, IReadOnlyList<Blok> sohbet,
        Ayarlar ayar, Func<bool> epochKontrol, CancellationToken iptal)
    {
        if (!epochKontrol() || iptal.IsCancellationRequested) return;
        var kalite = KaliteAyari(ayar);
        var sirali = parti.OrderBy(p => p.Value.Dogum).ToList();
        var kopyalar = sirali.Select(p => p.Value.Blok).ToList();
        Teshis.Paylasilan.Say("kalite-duzeltme");
        // Bağlam çağıranın verdiği sohbetten (Mac Motorlar.swift 846: lehçe
        // GEÇİRİLEN bloklardan). `_mevcutBloklar` ilk çeviride bu noktada
        // henüz boş olabilir → varsayılan lehçe. Sohbet yine de boşsa (ancak
        // savunmacı) partinin kendi kopyaları en azından lehçeyi taşır.
        var kaynak = sohbet.Count > 0 ? sohbet : kopyalar;
        var baglam = BaglamKur(kaynak, kalite);
        // Kalite isteği kendi zaman aşımını taşır (KaliteTuru.ZamanAsimi):
        // etkileşimli tavanla kesilince para harcanıp sonuç gelmiyordu.
        await Teshis.Paylasilan.OlcAsync("grok-kalite", () => _cevirmen
            .BloklariCevirAsync(kopyalar, baglam, iptal,
                new CeviriSecenekleri(Zorla: true, KaliciYaz: true, Canli: true,
                                      ZamanAsimi: KaliteTuru.ZamanAsimi, Asama: "kalite")))
            .ConfigureAwait(false);
        if (!epochKontrol()) return;

        var yeniler = new Dictionary<string, string>(StringComparer.Ordinal);
        var taze = new HashSet<string>(StringComparer.Ordinal);
        var simdi = DateTime.UtcNow;
        foreach (var (anahtar, kayit) in sirali)
        {
            var c = kayit.Blok.Ceviri;
            if (string.IsNullOrEmpty(c)) continue;
            yeniler[anahtar] = c;
            // Ölçülen şey balonun EKRANDA DURDUĞU süre: kuyruk + ağ çağrısı.
            if (KaliteTuru.EkranaYazilsinMi(kayit.Dogum, simdi)) taze.Add(anahtar);
        }
        if (yeniler.Count == 0) return;
        // Hafızaya HER durumda yazıldı (Cevirmen KaliciYaz:true); ekran yalnız
        // yaş kapısını geçenler için değişir.
        if (taze.Count == 0)
        {
            Teshis.Paylasilan.Say("kalite-yasli");
            return;
        }
        await _arayuz.InvokeAsync(() =>
        {
            if (!epochKontrol()) return;
            var bloklar = MevcutBloklarKopya();
            bool degisti = false;
            foreach (var b in bloklar)
            {
                if (!taze.Contains(b.Anahtar) || !yeniler.TryGetValue(b.Anahtar, out var yeni))
                    continue;
                // Yalnız noktalama/boşluk farkıysa ekranı oynatma
                if (Kalite.Anahtarla(b.Ceviri ?? "") == Kalite.Anahtarla(yeni)) continue;
                b.Ceviri = yeni;
                _cevirmen.Onbellek[b.Anahtar] = yeni;
                degisti = true;
            }
            if (!degisti || _katman is null) return;
            using var kare = KareKopyala();
            if (kare is null) return;
            using var boyaci = new YamaBoyaci(kare);
            // Guncelle DEĞİL: o taze OCR konumları varsayar ve kaydırma
            // telafisini sıfırlar. Burada yalnız metin değişti.
            _katman.MetniTazele(bloklar, boyaci);
            if (_cubuk is not null) _cubuk.MotorAdi = "Grok ✓ kalite";
            Teshis.Paylasilan.Say("kalite-degistirdi");
        });
    }

    // ================= giden çeviri (kısayol) =================

    public void GidenCevirBaslat()
    {
        Teshis.Paylasilan.Asama = "giden çeviri";
        Gunluk.Yaz("giden: kısayol tetiklendi");
        if (!GidenHazir())
        {
            _tepsi.Bilgi("Bekle", "Önceki çeviri sürüyor.");
            return;
        }
        _gidenSuruyor = true;
        _gidenBaslangic = DateTime.UtcNow;
        _ = GidenCevirAsync();
    }

    private async Task GidenCevirAsync()
    {
        var ayar = _ayar;
        var motor = ayar.EtkinGidenMotor;
        string turkce = "";
        var t0 = Stopwatch.GetTimestamp();
        int GecenMs() => (int)Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        try
        {
            var exe = Klavye.OnPlandakiUygulama();
            if (!Klavye.MesajlasmaUygulamasiMi(exe))
            {
                // VERİ KAYBI KORUMASI: Ctrl+A tüm belgeyi seçer. Yanlış
                // uygulamada tetiklenirse kullanıcının belgesini yok eder.
                Gunluk.Yaz($"giden: bilinmeyen uygulama '{exe}' — iptal");
                await Uyar("Bu pencerede çalışmaz",
                           $"'{exe}' bir mesajlaşma uygulaması değil. "
                           + "Kısayol yalnız mesaj kutusunda çalışır — "
                           + "belgeni yanlışlıkla silmemek için durduruldu.")
                      .ConfigureAwait(false);
                return;
            }

            // Etkin motor: ana motor "Ücretsiz çeviri" ise giden de ücretsiz
            // (sahip kararı #7) — anahtar yalnız gerçekten Grok'a gidecekse aranır.
            if (motor == "grok" && !_cevirmen.GrokHazir)
            {
                await Uyar("Anahtar yok",
                           "Yapay zekâ anahtarı girilmemiş. Tepsi menüsü → "
                           + "Gelişmiş → Yapay Zekâ Anahtarı.")
                      .ConfigureAwait(false);
                return;
            }
            // Sessiz düşüş yok: ücretsiz motor seçiliyken bunu bar söyler.
            MotorEtiketiAyarla(motor == "grok" ? "✍️ Çevriliyor…"
                                                : "✍️ Çevriliyor… (ücretsiz motor)");

            using var zamanAsimi = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var secilen = await Klavye.SecKopyalaAsync(zamanAsimi.Token)
                                      .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(secilen))
            {
                await Uyar("Metin alınamadı",
                           "Mesaj kutusunda yazı yok ya da çok uzun.")
                      .ConfigureAwait(false);
                return;
            }
            turkce = secilen;

            var baglam = BaglamKur(MevcutBloklarKopya(), ayar);
            var ceviri = await _cevirmen
                .GidenCevirAsync(turkce, baglam, zamanAsimi.Token)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(ceviri))
            {
                ArizaGunlugu.Yaz("giden", motor, "bos-yanit", GecenMs(), turkce);
                await Uyar("Çeviri yapılamadı",
                           "Mesajın DEĞİŞMEDİ — yanlış bir metin "
                           + "yapıştırmaktansa hiç yapıştırmadım.")
                      .ConfigureAwait(false);
                return;
            }

            await Klavye.YapistirAsync(ceviri, zamanAsimi.Token)
                        .ConfigureAwait(false);
            MotorEtiketiAyarla("✍️ çevrildi ✓");
        }
        catch (GidenRet ret)
        {
            // ÇIKTI KAPISI REDDETTİ: hedefe DOKUNULMAZ, neden kullanıcıya gösterilir.
            ArizaGunlugu.Yaz("giden", motor, "kalite-kapisi-reddetti", GecenMs(), turkce);
            Gunluk.Yaz("giden: çıktı reddedildi — " + ret.Neden);
            await Uyar("Çeviri yapılamadı", "Mesajın DEĞİŞMEDİ — " + ret.Neden)
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            ArizaGunlugu.Yaz("giden", motor, ArizaGunlugu.Sebep(e), GecenMs(), turkce);
            Gunluk.Hata("gidenCeviri", e);
            await Uyar("Çeviri yapılamadı", "Mesajın DEĞİŞMEDİ — " + e.Message)
                .ConfigureAwait(false);
        }
        finally
        {
            _gidenSuruyor = false;
        }
    }

    // ================= kurtarma katmanları =================

    /// <summary>Takılı iş bayrağını 15 sn sonra otomatik sıfırlar.
    /// ASLA sessizce false dönmez — macOS'ta "Bölgeyi Çevir hiçbir şey
    /// yapmıyor" şikayetinin kök nedeni takılı bayraktı.</summary>
    private bool CevirmeyeHazir()
    {
        if (!_isSuruyor) return true;
        if (DateTime.UtcNow - _isBaslangic <= TimeSpan.FromSeconds(15)) return false;
        Gunluk.Yaz("takılı iş bayrağı sıfırlandı");
        _isSuruyor = false;
        YeniEpoch();               // eski iş geç bitince katman açmasın
        return true;
    }

    private bool GidenHazir()
    {
        if (!_gidenSuruyor) return true;
        if (DateTime.UtcNow - _gidenBaslangic <= TimeSpan.FromSeconds(30)) return false;
        Gunluk.Yaz("giden: bekçi kilidi kırdı");
        _gidenSuruyor = false;
        return true;
    }

    private void SaglikDenetimi()
    {
        var simdi = DateTime.UtcNow;
        if (_isSuruyor && simdi - _isBaslangic > TimeSpan.FromSeconds(40))
        {
            Gunluk.Yaz("sağlık: iş bayrağı 40sn takılı, sıfırlandı");
            _isSuruyor = false;
        }
        if (_gidenSuruyor && simdi - _gidenBaslangic > TimeSpan.FromSeconds(40))
        {
            Gunluk.Yaz("sağlık: giden bayrağı 40sn takılı, sıfırlandı");
            _gidenSuruyor = false;
        }
        // Sahipsiz canlı zamanlayıcı: katman kapandı ama zamanlayıcı sürüyor
        if (_katman is null && _canliZaman is not null)
        {
            Gunluk.Yaz("sağlık: sahipsiz canlı zamanlayıcı durduruldu");
            CanliDurdur();
        }
    }

    // ================= diyaloglar =================

    private void GizlilikGoster()
    {
        if (_ayar.GizlilikGosterildi) return;
        _ayar.GizlilikGosterildi = true;
        _ayar.Kaydet();
        MessageBox.Show(
            "Yazı tanıma TAMAMEN bu bilgisayarda yapılır — ekran görüntüsü "
            + "hiçbir yere gönderilmez, diske kaydedilmez.\n\n"
            + "Çeviri motoruna yalnız okunan METİN gider.\n\n"
            + "API anahtarın yalnız bu bilgisayarda, Windows'un kendi "
            + "şifrelemesiyle (DPAPI) saklanır.\n\n"
            + "Sohbet hafızası yereldir; menüden kapatabilir ve silebilirsin.",
            "Gizlilik", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void IlkKullanimKontrolu()
    {
        if (!string.IsNullOrEmpty(_ayar.GrokApiKey) || _ayar.AnahtarSoruldu) return;
        _ayar.AnahtarSoruldu = true;
        _ayar.Kaydet();
        AnahtarSor(ilkKullanim: true);
    }

    public void AnahtarSor(bool ilkKullanim = false)
    {
        var pencere = new AnahtarPenceresi(_ayar.GrokApiKey, ilkKullanim);
        if (pencere.ShowDialog() != true) return;

        if (pencere.Silindi)
        {
            AnahtarKasasi.Sil();
            _ayar.GrokApiKey = "";
            _ayar.Kaydet();
            _tepsi.Bilgi("Anahtar silindi", "Ücretsiz motorlara geçildi.");
            return;
        }
        var deger = pencere.Anahtar;
        if (string.IsNullOrEmpty(deger)) return;
        AnahtarKasasi.Kaydet(deger);
        _ayar.GrokApiKey = deger;
        _ayar.Kaydet();
        _tepsi.Bilgi("Anahtar kaydedildi", "Lehçe çevirisi açık.");
    }

    public void AyarlariAc()
    {
        var pencere = new AyarPenceresi(_ayar);
        if (pencere.ShowDialog() != true) return;
        // Pencere _ayar'ı yerinde yazdı. Kalan iş menü yolundakiyle AYNI olmalı:
        // doğrula + kaydet + kısayolu yeniden kur + bar ipucunu tazele + oturum
        // önbelleğini boşalt + ekrandaki çevirileri yeniden çevir. Eskiden yalnız
        // Kaydet + KisayolKur + _ekranSabitlendi=false yapılıyordu: pencereden dil
        // ya da motor değişince eski çeviri önbellekten geri geliyor, bar ⌨️
        // ipucu eski kısayolu gösteriyordu (D denetim bulgusu).
        AyarDegistir(_ => { });
    }

    private Task Uyar(string baslik, string mesaj) =>
        _arayuz.InvokeAsync(() => _tepsi.Bilgi(baslik, mesaj)).Task;

    public void Cik()
    {
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _saglikBekcisi.Stop();
        _kaydetZaman?.Stop();
        CanliDurdur();
        YeniEpoch();
        _katman?.Close();
        _cubuk?.Close();
        KareyiAta(null);
        _kisayol.Dispose();
        _tepsi.Dispose();
        _hafiza.Kaydet();
        SohbetGecmisi.Kirp();
        Teshis.Paylasilan.Durdur();
        _isKilidi.Dispose();
    }
}
