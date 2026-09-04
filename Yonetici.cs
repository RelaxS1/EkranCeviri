using System.Runtime.Versioning;
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
/// katmanları. macOS sürümündeki AppDelegate'in karşılığı.
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

    /// <summary>İş kuyruğu: aynı anda tek çeviri turu. macOS'ta seri kuyruk
    /// vardı; burada semafor aynı işi görüyor ama ANA İŞ PARÇACIĞINI
    /// bloklamıyor — macOS'taki kilitlenmenin kök nedeni buydu.</summary>
    private readonly SemaphoreSlim _isKilidi = new(1, 1);

    private readonly object _durumKilidi = new();
    private List<Blok> _mevcutBloklar = [];

    private KatmanPenceresi? _katman;
    private KontrolCubugu? _cubuk;
    private Rect _bolge;                    // FİZİKSEL piksel

    /// <summary>Önceki kare. ARAYÜZ iş parçacığı atıyor/siliyor, İŞ
    /// kuyruğu okuyor — kilitsiz bırakmak "kullanımdan sonra serbest
    /// bırakılmış bitmap" çökmesi demek.</summary>
    private readonly object _kareKilidi = new();
    private Bitmap? _sonKare;
    private volatile double[]? _sonIzdusum;
    // volatile: iki iş parçacığından okunup yazılıyor; derleyicinin
    // yazmaça almasına izin verirsek bayrak değişimi hiç görülmeyebilir.
    private volatile bool _ekranSabitlendi;
    private int _hareketSayaci;
    private int _pesPeseHata;

    private DispatcherTimer? _canliZaman;
    private readonly DispatcherTimer _saglikBekcisi;

    // ---- iş kimlikleri (bkz. macOS sürümü: geç gelen yanıt yeni katmanı eziyordu)
    private readonly object _epochKilidi = new();
    private int _isEpoch;        // bölge çevirisi
    private int _canliEpoch;     // canlı tur — AYRI tutulur

    private volatile bool _isSuruyor;
    private DateTime _isBaslangic;
    private volatile bool _canliMesgul;
    private DateTime _canliBaslangic;
    private volatile bool _gidenSuruyor;
    private DateTime _gidenBaslangic;

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
    }

    public Ayarlar Ayar => _ayar;
    public Cevirmen Cevirmen => _cevirmen;
    public Hafiza Hafiza => _hafiza;

    // ================= iş kimlikleri =================

    private int YeniEpoch()
    {
        lock (_epochKilidi) return ++_isEpoch;
    }

    private bool EpochGuncelMi(int e)
    {
        lock (_epochKilidi) return e == _isEpoch;
    }

    private int YeniCanliEpoch()
    {
        lock (_epochKilidi) return ++_canliEpoch;
    }

    private bool CanliEpochGuncelMi(int e)
    {
        lock (_epochKilidi) return e == _canliEpoch;
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

    // ================= açılış =================

    public void Basla()
    {
        GizlilikGoster();
        IlkKullanimKontrolu();
        OcrDiliKontrolu();
        KisayolKur();
        _tepsi.Goster();
    }

    private void KisayolKur()
    {
        if (_kisayol.Kaydet(_ayar.KisayolMod, _ayar.KisayolTus, GidenCevirBaslat))
            return;
        // SESSİZCE BAŞARISIZ OLMA: kısayol başka uygulamada kullanılıyorsa
        // kullanıcı bunu bilmeli, yoksa "çalışmıyor" diye düşünür.
        Gunluk.Yaz("kısayol kaydedilemedi (başka uygulama kullanıyor olabilir)");
        _tepsi.Bilgi("Kısayol alınamadı",
                     "Ctrl+Alt+C başka bir uygulamada kullanılıyor olabilir. "
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

    public void BolgeCevirBaslat()
    {
        if (!CevirmeyeHazir())
        {
            _tepsi.Bilgi("Bekle", "Önceki çeviri sürüyor, birazdan tekrar dene.");
            return;
        }

        // Yeni bölge: eski her şeyi bırak.
        KatmaniKapat();
        lock (_durumKilidi) _mevcutBloklar = [];
        KareyiAta(null);
        _sonIzdusum = null;
        _ekranSabitlendi = false;
        _hareketSayaci = 0;
        _pesPeseHata = 0;

        var secici = new BolgeSecici();
        secici.ShowDialog();
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
        try
        {
            if (!EpochGuncelMi(epoch)) return;
            await Task.Delay(180).ConfigureAwait(false);

            using var kare = Yakalama.BolgeYakala(_bolge);
            if (kare is null)
            {
                await Uyar("Ekran yakalanamadı",
                           "Bölge okunamadı. Tekrar dene.").ConfigureAwait(false);
                return;
            }
            if (!EpochGuncelMi(epoch)) return;

            var satirlar = await _ocr.OkuAsync(kare, _ayar.OcrDili,
                                               CancellationToken.None)
                                     .ConfigureAwait(false);
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
            var baglam = BaglamKur(bloklar);

            var motorAdi = await _cevirmen
                .BloklariCevirAsync(bloklar, baglam, CancellationToken.None)
                .ConfigureAwait(false);
            if (!EpochGuncelMi(epoch)) return;

            lock (_durumKilidi) _mevcutBloklar = bloklar;
            _sonIzdusum = Yakalama.SatirIzdusumu(kare);
            var kopya = (Bitmap)kare.Clone();

            await _arayuz.InvokeAsync(() =>
            {
                if (!EpochGuncelMi(epoch)) { kopya.Dispose(); return; }
                KareyiAta(kopya);
                KatmaniGoster(bloklar, kopya, motorAdi, baglam.LehceKisa);
                _ekranSabitlendi = true;
                if (_ayar.CanliAcik) CanliBaslat();
            });
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

    internal CeviriBaglam BaglamKur(IReadOnlyList<Blok> bloklar)
    {
        // Lehçe TÜM görünür sohbetten algılanır. Yalnız yeni mesajlara
        // bakmak "belirsiz" sonucu veriyordu.
        var karsiMetinler = bloklar.Where(b => !b.Benim)
                                   .Select(b => b.Metin).ToList();
        var (ad, kisa) = Lehce.Algila(karsiMetinler);
        var son = bloklar.TakeLast(12).ToList();
        return new CeviriBaglam
        {
            Ayar = _ayar,
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
        _ = Task.Run(async () =>
        {
            try
            {
                var motorAdi = await _cevirmen
                    .BloklariCevirAsync(bloklar, baglam, CancellationToken.None)
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
                            _cubuk.MotorAdi = baglam.LehceKisa == "Almanca"
                                ? motorAdi : $"{motorAdi} · {baglam.LehceKisa}";
                    }
                });
            }
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
        _cubuk.MotorAdi = lehce == "Almanca" ? motorAdi : $"{motorAdi} · {lehce}";
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
        _canliZaman = new DispatcherTimer(DispatcherPriority.Background, _arayuz)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _canliZaman.Tick += (_, _) => CanliTur();
        _canliZaman.Start();
    }

    private void CanliDurdur()
    {
        _canliZaman?.Stop();
        _canliZaman = null;
        // Uçuştaki tur artık geçersiz. Zamanlayıcıyı durdurmak YETMEZ:
        // iş kuyruğundaki tur çalışmaya devam eder ve bitince sonucunu
        // YENİ katmana boyar.
        YeniCanliEpoch();
    }

    private void CanliTur()
    {
        // BEKÇİ: bir tur 25 sn'yi aştıysa (ağ askıda) kilidi kır.
        if (_canliMesgul
            && DateTime.UtcNow - _canliBaslangic > TimeSpan.FromSeconds(25))
        {
            Gunluk.Yaz("canlı: bekçi kilidi kırdı (takılı tur)");
            _canliMesgul = false;
            _ekranSabitlendi = false;
        }
        if (_isSuruyor || _canliMesgul || _katman is null) return;

        _canliMesgul = true;
        _canliBaslangic = DateTime.UtcNow;
        var turEpoch = _canliEpoch;
        _ = Task.Run(() => CanliTurAsync(turEpoch));
    }

    private async Task CanliTurAsync(int turEpoch)
    {
        try
        {
            if (!CanliEpochGuncelMi(turEpoch)) return;

            var kare = Yakalama.BolgeYakala(_bolge);
            if (kare is null) return;

            var izdusum = Yakalama.SatirIzdusumu(kare);
            var oncekiIzdusum = _sonIzdusum;
            using var onceki = KareKopyala();
            if (onceki is null) { kare.Dispose(); return; }

            double fark = Yakalama.IzFarki(onceki, kare);

            // İÇERİK KAYDI MI? Kaydıysa yamaları OCR beklemeden kaydır;
            // güvenilir kayma yoksa GİZLE (yanlış yerde yama göstermek yasak).
            if (fark > 0.012 && oncekiIzdusum is not null)
            {
                var (kayma, benzerlik) = Yakalama.DikeyKayma(oncekiIzdusum, izdusum);
                double satirBasina = kare.Height / 256.0;
                double dy = kayma * satirBasina;
                await _arayuz.InvokeAsync(() =>
                {
                    if (!CanliEpochGuncelMi(turEpoch) || _katman is null) return;
                    if (benzerlik > 0.55 && Math.Abs(kayma) >= 1
                        && Math.Abs(dy) < kare.Height * 0.5)
                    {
                        _katman.Gorunum.Gizle(false);
                        _katman.Kaydir(dy);
                    }
                    else
                    {
                        _katman.Gorunum.Gizle(true);
                    }
                });
            }

            _sonIzdusum = izdusum;

            if (fark > 0.012)
            {
                _ekranSabitlendi = false;
                // Aktif sohbette ekran hiç durulmayabilir (yazıyor animasyonu,
                // art arda mesaj). Çeviri açlığa düşmesin: 3 tur üst üste
                // hareket varsa mevcut kareyle YİNE DE güncelle.
                _hareketSayaci++;
                if (_hareketSayaci >= 3)
                {
                    _hareketSayaci = 0;
                    await CanliGuncelleAsync(kare, turEpoch).ConfigureAwait(false);
                }
                else
                {
                    kare.Dispose();
                }
                return;
            }

            _hareketSayaci = 0;
            if (!_ekranSabitlendi)
            {
                _ekranSabitlendi = true;
                await CanliGuncelleAsync(kare, turEpoch).ConfigureAwait(false);
            }
            else
            {
                // Ekran zaten sabit: tekrar OCR ÇALIŞTIRMA (titreme + CPU + para)
                kare.Dispose();
            }
        }
        catch (Exception e)
        {
            Gunluk.Hata("canliTur", e);
        }
        finally
        {
            _canliMesgul = false;
        }
    }

    private async Task CanliGuncelleAsync(Bitmap kare, int turEpoch)
    {
        if (!await _isKilidi.WaitAsync(0).ConfigureAwait(false))
        {
            kare.Dispose();
            return;
        }
        try
        {
            if (!CanliEpochGuncelMi(turEpoch)) return;

            var satirlar = await _ocr.OkuAsync(kare, _ayar.OcrDili,
                                               CancellationToken.None)
                                     .ConfigureAwait(false);
            if (satirlar.Count == 0) { _ekranSabitlendi = false; return; }
            if (!CanliEpochGuncelMi(turEpoch)) return;

            var (yeniBloklar, _) = Bloklayici.Ayir(
                satirlar, new Size(kare.Width, kare.Height));

            // ZEHİR KALKANI: karede kendi ürettiğimiz çevirilerden 2+
            // görünüyorsa katmanımız yakalamaya girmiş demektir
            // (WDA_EXCLUDEFROMCAPTURE tutmamış). YALNIZ uzun metinler sayılır:
            // "Tamam" gibi kısa çeviriler ekranda gerçek mesaj olarak da geçer
            // ve yanlış alarm canlı modu tamamen durduruyordu.
            int kirli = yeniBloklar.Count(b =>
                b.Hedef && b.Anahtar.Length >= 12
                && _hafiza.UretilenMi(b.Anahtar));
            if (kirli >= 2)
            {
                Gunluk.Yaz($"canlı: ZEHİR KALKANI devrede (kirli={kirli})");
                _ekranSabitlendi = false;
                return;
            }

            // ESKİLERLE EŞLEŞTİR: eşleşen balon ekranda hiç kıpırdamaz,
            // yalnız gerçekten yeni mesaj motora gider.
            List<Blok> eskiler;
            lock (_durumKilidi) eskiler = [.. _mevcutBloklar];
            foreach (var yeni in yeniBloklar)
            {
                int i = eskiler.FindIndex(e => Bloklayici.Eslesirler(e, yeni));
                if (i < 0) continue;
                var eski = eskiler[i];
                eskiler.RemoveAt(i);
                yeni.Ceviri = eski.Ceviri;
                // OCR'ın milimetrik oynaması balonu kıpırdatmasın
                if (Math.Abs(eski.Kutu.X - yeni.Kutu.X) <= 8
                    && Math.Abs(eski.Kutu.Y - yeni.Kutu.Y) <= 8
                    && Math.Abs(eski.Kutu.Width - yeni.Kutu.Width) <= 12
                    && Math.Abs(eski.Kutu.Height - yeni.Kutu.Height) <= 12)
                    yeni.Kutu = eski.Kutu;
            }

            var baglam = BaglamKur(yeniBloklar);
            lock (_durumKilidi) _mevcutBloklar = yeniBloklar;

            // 1. FAZ — HEMEN boya: eşleşen çeviriler yeni konumlarına anında
            // oturur. "Eski konumda asılı yama + yeni çeviri üst üste"
            // görüntüsünün çözümü budur.
            var kopya1 = (Bitmap)kare.Clone();
            await _arayuz.InvokeAsync(() =>
            {
                using (kopya1)
                {
                    if (!CanliEpochGuncelMi(turEpoch) || _katman is null) return;
                    using var boyaci = new YamaBoyaci(kopya1);
                    _katman.Guncelle(yeniBloklar, boyaci);
                }
            });

            bool eksikVar = yeniBloklar.Any(b => b.Hedef && b.Ceviri is null);
            if (!eksikVar)
            {
                await KareyiSakla(kare, turEpoch).ConfigureAwait(false);
                _pesPeseHata = 0;
                return;
            }

            // 2. FAZ — yalnız eksik (yeni) bloklar motora gider
            var motorAdi = await _cevirmen
                .BloklariCevirAsync(yeniBloklar, baglam, CancellationToken.None)
                .ConfigureAwait(false);

            bool tamam = !yeniBloklar.Any(b => b.Hedef && b.Ceviri is null);
            if (tamam)
            {
                _pesPeseHata = 0;
            }
            else
            {
                // Wi-Fi koptuğunda saniyede bir tam OCR + başarısız istek
                // yapıp pili bitiriyordu: art arda 3 hatadan sonra bekle.
                _pesPeseHata++;
                if (_pesPeseHata >= 3)
                {
                    _ekranSabitlendi = true;   // döngüyü durdur
                    double bekle = _pesPeseHata >= 6 ? 120 : 60;
                    Gunluk.Yaz($"canlı: {_pesPeseHata} hata → {bekle}sn bekleme");
                    _ = Task.Delay(TimeSpan.FromSeconds(bekle))
                            .ContinueWith(_ => _ekranSabitlendi = false,
                                          TaskScheduler.Default);
                }
                else
                {
                    _ekranSabitlendi = false;
                }
            }

            if (!CanliEpochGuncelMi(turEpoch)) return;

            // Çeviri bitince TEK SEFERDE ekrana bas (yanıp sönmeyi önler)
            var kopya2 = (Bitmap)kare.Clone();
            await _arayuz.InvokeAsync(() =>
            {
                using (kopya2)
                {
                    if (!CanliEpochGuncelMi(turEpoch) || _katman is null) return;
                    using var boyaci = new YamaBoyaci(kopya2);
                    _katman.Guncelle(yeniBloklar, boyaci);
                    if (_cubuk is not null)
                        _cubuk.MotorAdi = baglam.LehceKisa == "Almanca"
                            ? motorAdi : $"{motorAdi} · {baglam.LehceKisa}";
                }
            });

            await KareyiSakla(kare, turEpoch).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Gunluk.Hata("canliGuncelle", e);
            _ekranSabitlendi = false;
        }
        finally
        {
            kare.Dispose();
            _isKilidi.Release();
        }
    }

    private Task KareyiSakla(Bitmap kare, int turEpoch)
    {
        var kopya = (Bitmap)kare.Clone();
        return _arayuz.InvokeAsync(() =>
        {
            if (!CanliEpochGuncelMi(turEpoch)) { kopya.Dispose(); return; }
            KareyiAta(kopya);
        }).Task;
    }

    // ================= giden çeviri (kısayol) =================

    public void GidenCevirBaslat()
    {
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
        try
        {
            var exe = Klavye.OnPlandakiUygulama();
            if (!Klavye.MesajlasmaUygulamasiMi(exe))
            {
                // VERİ KAYBI KORUMASI: Ctrl+A tüm belgeyi seçer. Yanlış
                // uygulamada tetiklenirse kullanıcının belgesini yok eder.
                Gunluk.Yaz($"giden: bilinmeyen uygulama '{exe}' — iptal");
                _tepsi.Bilgi("Bu pencerede çalışmaz",
                             $"'{exe}' bir mesajlaşma uygulaması değil. "
                             + "Kısayol yalnız mesaj kutusunda çalışır — "
                             + "belgeni yanlışlıkla silmemek için durduruldu.");
                return;
            }

            if (!_cevirmen.GrokHazir && _ayar.GidenMotor == "grok")
            {
                _tepsi.Bilgi("Anahtar yok",
                             "Yapay zekâ anahtarı girilmemiş. Tepsi menüsü → "
                             + "Gelişmiş → Yapay Zekâ Anahtarı.");
                return;
            }

            using var zamanAsimi = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var turkce = await Klavye.SecKopyalaAsync(zamanAsimi.Token)
                                     .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(turkce))
            {
                _tepsi.Bilgi("Metin alınamadı",
                             "Mesaj kutusunda yazı yok ya da çok uzun.");
                return;
            }

            List<Blok> bloklar;
            lock (_durumKilidi) bloklar = [.. _mevcutBloklar];
            var baglam = BaglamKur(bloklar);

            var ceviri = await _cevirmen
                .GidenCevirAsync(turkce, baglam, zamanAsimi.Token)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(ceviri))
            {
                _tepsi.Bilgi("Çeviri yapılamadı",
                             "Mesajın DEĞİŞMEDİ — yanlış bir metin "
                             + "yapıştırmaktansa hiç yapıştırmadım.");
                return;
            }

            await Klavye.YapistirAsync(ceviri, zamanAsimi.Token)
                        .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Gunluk.Hata("gidenCeviri", e);
            _tepsi.Bilgi("Çeviri yapılamadı", e.Message);
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
        _ayar.Kaydet();
        // Kısayol değiştiyse yeniden kaydet
        KisayolKur();
        // Motor/dil değiştiyse ekrandaki çeviriler bayat kaldı: tazele.
        _ekranSabitlendi = false;
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
        CanliDurdur();
        _katman?.Close();
        _cubuk?.Close();
        KareyiAta(null);
        _kisayol.Dispose();
        _tepsi.Dispose();
        _hafiza.Kaydet();
        _isKilidi.Dispose();
    }
}
