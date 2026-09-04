using System.Runtime.Versioning;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using EkranCeviri.Arayuz;
using EkranCeviri.Cekirdek;
using EkranCeviri.Ceviri;
using EkranCeviri.Ekran;
using EkranCeviri.Kisayol;

namespace EkranCeviri;

/// <summary>
/// Yönetici'nin ARAYÜZ eylemleri: bar düğmeleri (kopyala, orijinal, ✨,
/// cevap öner) ve menü eylemleri (ayar değişimi, hafıza/geçmiş silme,
/// kısayol, üslup). Akış (canlı döngü, ilk çeviri) <c>Yonetici.cs</c>'de;
/// buradaki her şey o dosyanın korunan üyelerine güvenir.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public sealed partial class Yonetici
{
    private OneriPenceresi? _oneri;
    private volatile bool _oneriSuruyor;
    private DateTime _oneriBaslangic;
    private int _oneriSayaci;

    /// <summary>Öneri bekçisi (Mac 40 sn): takılan bir istek düğmeyi
    /// sonsuza dek kilitlemesin.</summary>
    private static readonly TimeSpan OneriBekci = TimeSpan.FromSeconds(40);

    /// <summary>✨ bekçisi: sağlık denetimi 40 sn'de bayrağı sıfırlar; ağ
    /// isteği de aynı sürede iptal edilir ki geç gelen sonuç eski katmana yazmasın.</summary>
    private static readonly TimeSpan YenidenCeviriBekci = TimeSpan.FromSeconds(40);

    /// <summary>Menü doğrulaması için: bir iş sürüyor mu? (Mac
    /// <c>ogeEtkinMi</c> — "Bölgeyi Çevir" iş sürerken devre dışı.)</summary>
    public bool IsSuruyor => _isSuruyor;

    /// <summary>Katman açık mı? ("Çeviriyi Kapat" yalnız o zaman etkin.)</summary>
    public bool KatmanAcik => _katman is not null;

    /// <summary>"Yazdığımı Çevir" sürüyor mu? (Mac <c>ogeEtkinMi</c>: giden
    /// çeviri uçuştayken menü öğesi devre dışı — ikinci tıklama yığılmasın.)</summary>
    public bool GidenSuruyor => _gidenSuruyor;

    // ================= bar düğmeleri =================

    /// <summary>📋 Boş olmayan çevirileri satır satır panoya. "" nöbetçisi
    /// (çevrilememiş) boş satır üretmesin.</summary>
    public void CevirileriKopyala()
    {
        var metin = string.Join("\n", MevcutBloklarKopya()
            .Select(b => b.Ceviri)
            .Where(c => !string.IsNullOrEmpty(c)));
        _arayuz.BeginInvoke(() =>
        {
            try
            {
                Clipboard.SetText(metin);
                GeriBildir("Panoya kopyalandı ✓");
            }
            catch (Exception e)
            {
                // Pano başka uygulama tarafından kilitliyken COMException gelir.
                Gunluk.Hata("kopyala", e);
                GeriBildir("⚠ Panoya kopyalanamadı — tekrar dene");
            }
        });
    }

    /// <summary>👁 Yamaları gizle/göster; yeni durumu döner (true = orijinal
    /// görünüyor).</summary>
    public bool OrijinalDegistir()
    {
        if (_katman is null) return false;
        var g = _katman.Gorunum;
        g.OrijinalGoster = !g.OrijinalGoster;
        return g.OrijinalGoster;
    }

    /// <summary>
    /// ✨ Ekrandakileri KALİTE modeliyle yeniden çevir (Mac <c>aiIleCevir</c>).
    /// Her koşulda kaliteli model (ayar kopyasında HizOnceligi=false); tekrar
    /// tavanı ve "değişmez" işareti otomatik turlar içindir — düğmeye basan
    /// kullanıcıya sessizce "hiçbir şey olmuyor" dedirtmemek için bütçe
    /// yeniden açılır. Kullanıcı "daha iyi çevir" dedi: kalıcı hafızadaki
    /// ESKİ (kötü) kayıt da güncellenir, yoksa bir dahaki sefere yine eski
    /// çeviri servis edilir.
    /// </summary>
    public void KaliteyleYenidenCevir()
    {
        if (_katman is null || _isSuruyor) return;
        if (!_cevirmen.GrokHazir)
        {
            MotorEtiketiAyarla("Grok anahtarı yok (Gelişmiş → Yapay Zekâ Anahtarı)");
            return;
        }
        var bloklar = MevcutBloklarKopya();
        if (bloklar.Count == 0) return;

        _isSuruyor = true;
        _isBaslangic = DateTime.UtcNow;      // bekçi anında "yanıt vermedi" demesin
        MotorEtiketiAyarla("Grok AI çeviriyor…");

        var kaliteAyar = AyarKopyasi(a => { a.Motor = "ai"; a.HizOnceligi = false; });
        var baglam = BaglamKopyasi(BaglamKur(bloklar), kaliteAyar);
        foreach (var b in bloklar) TekrarDefteri.Paylasilan.TekrarAc(b.Anahtar);
        var epoch = YeniEpoch();

        _ = Task.Run(async () =>
        {
            try
            {
                // BEKÇİ SAATİ İŞ BAŞLANGICINDAN: tıklama anından ölçülünce
                // kuyrukta geçen bekleme kullanıcının bütçesinden yeniyordu.
                _isBaslangic = DateTime.UtcNow;
                // EPOCH İPTALİNE BAĞLI: Mac `IsIptali.ayarla { !epochGuncelMi(epoch) }`
                // — "Çeviriyi Kapat" ya da yeni "Bölgeyi Çevir" uçuştaki isteği
                // düşürür. Yalnız zaman aşımıyla iptal edilince istek 40 sn'ye
                // kadar sürüyor, sonuç epoch'la atılsa da _isSuruyor açık
                // kaldığı için yeni çeviri "Bekle — önceki çeviri sürüyor" diyordu.
                using var iptal = CancellationTokenSource.CreateLinkedTokenSource(IsIptalBelirteci);
                iptal.CancelAfter(YenidenCeviriBekci);
                // İŞ KİLİDİ: `bloklar` sığ kopya — _mevcutBloklar ile AYNI Blok
                // nesneleri. Canlı ağ işi aynı nesnelere _isKilidi altında yazar;
                // _isSuruyor yalnız YENİ turu durdurur, uçuştakini değil. Mac
                // aiIleCevir seri ceviriKuyrugu'nda koştuğu için yarış yoktu;
                // burada kilit o sıralamayı sağlar. Alınamazsa kullanıcıya söyle.
                if (!await _isKilidi.WaitAsync(TimeSpan.FromSeconds(20), iptal.Token)
                                    .ConfigureAwait(false))
                {
                    GeriBildir("Meşgul — tekrar dene");
                    return;
                }
                string motorAdi;
                try
                {
                    motorAdi = await _cevirmen.BloklariCevirAsync(
                        bloklar, baglam, iptal.Token,
                        new CeviriSecenekleri(Zorla: true, KaliciYaz: true, Asama: "yeniden-ceviri"))
                        .ConfigureAwait(false);
                }
                finally
                {
                    _isKilidi.Release();
                }
                if (!EpochGuncelMi(epoch)) return;

                foreach (var b in bloklar)
                    if (b.Hedef && b.Ceviri is { Length: > 0 } c)
                        _hafiza.Guncelle(b.Anahtar, c, b.Metin, kaliteAyar.HedefDil);
                _hafiza.Kaydet();

                var kare = KareKopyala();
                await _arayuz.InvokeAsync(() =>
                {
                    using (kare)
                    {
                        if (!EpochGuncelMi(epoch) || _katman is null || kare is null) return;
                        using var boyaci = new YamaBoyaci(kare);
                        _katman.MetniTazele(bloklar, boyaci);
                        if (_cubuk is not null) _cubuk.MotorAdi = motorAdi;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Epoch değiştiyse kullanıcı vazgeçti (Mac `guard epochGuncelMi
                // else return` — sessiz); yalnız gerçek zaman aşımında söyle.
                if (EpochGuncelMi(epoch))
                    GeriBildir("Grok yanıt vermedi (40 sn) — tekrar dene");
            }
            catch (Exception e)
            {
                Gunluk.Hata("aiIleCevir", e);
                GeriBildir("Çeviri yapılamadı: " + e.Message);
            }
            finally
            {
                _isSuruyor = false;
            }
        });
    }

    /// <summary>
    /// 💬 Cevap önerisi (Mac <c>cevapOner</c>): sohbet dökümü yukarıdan aşağıya,
    /// taraf etiketiyle ("BEN:"/"KARŞI:") Grok'a gider; 3 öneri panelde.
    /// Tek uçuş: sürerken ikinci tıklama yok sayılır; 40 sn bekçi takılan
    /// isteği düşürür. Kullanıcı yeniden istediyse eski sonuç atılır (sayaç).
    /// </summary>
    public async Task CevapOnerAsync(bool farkli)
    {
        if (_oneriSuruyor)
        {
            if (DateTime.UtcNow - _oneriBaslangic < OneriBekci) return;
            Gunluk.Yaz("öneri: takılı bayrak sıfırlandı");
            _oneriSuruyor = false;
        }
        if (!_cevirmen.GrokHazir)
        {
            MotorEtiketiAyarla("Öneri için Grok anahtarı gerekli");
            return;
        }
        var bloklar = MevcutBloklarKopya();
        var dokum = bloklar
            .Where(b => b.Hedef)
            .OrderBy(b => b.Kutu.Y)
            .Select(b => (b.Benim ? "BEN: " : "KARŞI: ") + b.Metin)
            .ToList();
        if (dokum.Count == 0) return;

        _oneriSuruyor = true;
        _oneriBaslangic = DateTime.UtcNow;
        var benimOneri = Interlocked.Increment(ref _oneriSayaci);
        MotorEtiketiAyarla("cevap hazırlanıyor…");
        var baglam = BaglamKur(bloklar);

        List<(string Cevap, string Turkce)> oneriler;
        try
        {
            using var iptal = new CancellationTokenSource(OneriBekci);
            oneriler = await Task.Run(() => _cevirmen.OneriAsync(dokum, baglam, farkli, iptal.Token))
                                 .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Gunluk.Hata("cevapOner", e);
            oneriler = [];
        }

        await _arayuz.InvokeAsync(() =>
        {
            // bekçi sıfırladı / kullanıcı yeniden istedi → eski sonuç atılır
            if (!_oneriSuruyor || benimOneri != _oneriSayaci) return;
            _oneriSuruyor = false;
            if (oneriler.Count == 0 || string.IsNullOrEmpty(oneriler[0].Cevap))
            {
                if (_cubuk is not null) _cubuk.MotorAdi = "öneri alınamadı";
                return;
            }
            if (_cubuk is null) return;
            _cubuk.MotorAdi = "öneriler hazır";
            OneriGoster(oneriler);
        });
    }

    private void OneriGoster(List<(string Cevap, string Turkce)> oneriler)
    {
        if (_cubuk is null) return;
        if (_oneri is null)
        {
            var pencere = new OneriPenceresi(this, _cubuk);
            pencere.Closed += (_, _) => { if (ReferenceEquals(_oneri, pencere)) _oneri = null; };
            _oneri = pencere;
        }
        _oneri.Goster(oneriler);
    }

    // ================= menü eylemleri =================

    /// <summary>
    /// Ayar değişiminin TEK kapısı: uygula → doğrula → kaydet → kısayol
    /// değiştiyse yeniden kaydet → motor/dil/mod/kimlik değiştiyse ekrandaki
    /// çevirileri tazele (Mac <c>motorSecildi/dilSecildi</c>: ekrandakiler
    /// ESKİ motorun çıktısı; yenisiyle çevrilmezse "değiştirdim ama hiçbir
    /// şey değişmedi" hissi oluşuyordu). Oturum önbelleği de boşaltılır,
    /// yoksa eski çeviri önbellekten aynen geri geliyordu.
    /// </summary>
    public void AyarDegistir(Action<Ayarlar> degisiklik)
    {
        var once = AyarIzi(_ayar);
        degisiklik(_ayar);
        _ayar.Dogrula();
        _ayar.Kaydet();
        var sonra = AyarIzi(_ayar);

        if (once.Kisayol != sonra.Kisayol)
        {
            KisayolKur();
            _arayuz.BeginInvoke(() => _cubuk?.KisayolIpucuTazele());
        }
        if (once.Ceviri != sonra.Ceviri)
        {
            _cevirmen.Onbellek.Temizle();
            EkrandakiCevirileriTazele();
        }
    }

    /// <summary>Kısayolu etkileyen ve çeviriyi etkileyen alanların izleri;
    /// karşılaştırma tek satırda.</summary>
    private static (string Kisayol, string Ceviri) AyarIzi(Ayarlar a) => (
        $"{a.KisayolMod}|{a.KisayolTus}",
        string.Join("|", a.Motor, a.HedefDil, a.DilModu, a.BenCinsiyet, a.KarsiCinsiyet));

    /// <summary>Kayıtlı Çevirileri Sil… (Mac <c>ceviriHafizasiniSil</c>).</summary>
    public void HafizayiSil()
    {
        var cevap = MessageBox.Show(
            $"{_hafiza.Adet} kayıtlı çeviri silinecek; bundan sonra aynı cümleler "
            + "yeniden çevrilir (biraz daha yavaş ve biraz daha maliyetli olur).\n\n"
            + "Yerel çeviri hafızası silinsin mi?",
            "Çeviri hafızası", MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No);
        if (cevap != MessageBoxResult.Yes) return;
        _hafiza.Temizle();
        _cevirmen.Onbellek.Temizle();
        GeriBildir("Çeviri hafızası silindi");
    }

    /// <summary>Sohbet Geçmişini Sil… (Mac <c>gecmisiSil</c>).</summary>
    public void GecmisiSil()
    {
        var cevap = MessageBox.Show(
            "Sohbet geçmişi kalıcı olarak silinsin mi?",
            "Sohbet geçmişi", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (cevap != MessageBoxResult.Yes) return;
        SohbetGecmisi.Sil();
        // Mac gecmisiSil: dosya silinir + gecmiseYazilan.temizle(). Küme
        // boşalmazsa ekrandaki mesajlar bu oturumda "zaten yazıldı" sayılıp
        // yeni geçmiş dosyasına bir daha düşmüyordu.
        GecmisKumesiniTemizle();
        GeriBildir("Geçmiş silindi");
    }

    /// <summary>Kısayolu Değiştir…: yakalama penceresi tek başına açılır;
    /// kullanıcı bir birleşime basar, o kalıcı olur (Mac <c>kisayolAta</c>).</summary>
    public void KisayolDegistir()
    {
        if (AyarPenceresi.KisayolYakala(null) is not { } yeni) return;
        AyarDegistir(a => { a.KisayolMod = yeni.Mod; a.KisayolTus = yeni.Tus; });
        GeriBildir("⌨️ Kısayol ayarlandı: " + KisayolMetni.Metin(yeni.Mod, yeni.Tus));
    }

    /// <summary>Nasıl Yazayım (üslup)…: Türkçe yazdıkların karşı dile
    /// çevrilirken mesaj bu karaktere göre yazılır (yalnız Grok motorunda).
    /// Tek metin hem gelen hem giden üslubu besler (Kisilik + GidenKarakterMetni).</summary>
    public void UslupDuzenle()
    {
        var mevcut = string.IsNullOrWhiteSpace(_ayar.GidenKarakterMetni)
            ? _ayar.Kisilik : _ayar.GidenKarakterMetni;
        var alan = new TextBox
        {
            Text = mevcut,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 8, 0, 12),
        };
        var pencere = new Window
        {
            Title = "Nasıl Yazayım (üslup)",
            Width = 440,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false,
            Topmost = true,
        };
        var kaydet = new Button { Content = "Kaydet", IsDefault = true, Padding = new Thickness(14, 5, 14, 5) };
        var vazgec = new Button
        {
            Content = "Vazgeç", IsCancel = true, Padding = new Thickness(14, 5, 14, 5),
            Margin = new Thickness(0, 0, 8, 0),
        };
        kaydet.Click += (_, _) => pencere.DialogResult = true;
        vazgec.Click += (_, _) => pencere.DialogResult = false;
        var dugmeler = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        dugmeler.Children.Add(vazgec);
        dugmeler.Children.Add(kaydet);
        var yigin = new StackPanel { Margin = new Thickness(18) };
        yigin.Children.Add(new TextBlock
        {
            Text = "Türkçe yazdıkların karşı dile çevrilirken mesaj bu karaktere göre "
                 + "yazılır (yalnız yapay zekâ motorunda). Örnek: \"kısa yazarım, "
                 + "samimi ama kaba değil, emoji az\".",
            TextWrapping = TextWrapping.Wrap,
        });
        yigin.Children.Add(alan);
        yigin.Children.Add(dugmeler);
        pencere.Content = yigin;
        pencere.Loaded += (_, _) => alan.Focus();
        if (pencere.ShowDialog() != true) return;

        var metin = alan.Text.Trim();
        AyarDegistir(a => { a.Kisilik = metin; a.GidenKarakterMetni = metin; });
        GeriBildir("Yazım karakteri kaydedildi ✓");
    }

    // ================= yardımcılar =================

    /// <summary>Ayarların bağımsız kopyası (JSON gidiş-dönüş; anahtar JSON'a
    /// girmediği için elle taşınır). Kalite turu HizOnceligi=false ister ama
    /// kullanıcının ayarına DOKUNMAMALI.</summary>
    private Ayarlar AyarKopyasi(Action<Ayarlar> degisiklik)
    {
        var kopya = JsonSerializer.Deserialize<Ayarlar>(JsonSerializer.Serialize(_ayar))
                    ?? new Ayarlar();
        kopya.GrokApiKey = _ayar.GrokApiKey;
        degisiklik(kopya);
        kopya.Dogrula();
        return kopya;
    }

    private static CeviriBaglam BaglamKopyasi(CeviriBaglam b, Ayarlar ayar) => new()
    {
        Ayar = ayar,
        LehceAd = b.LehceAd,
        LehceKisa = b.LehceKisa,
        TarzOrnekleri = b.TarzOrnekleri,
        OncekiKonusma = b.OncekiKonusma,
        OncekiKonusmaYapili = b.OncekiKonusmaYapili,
    };
}
