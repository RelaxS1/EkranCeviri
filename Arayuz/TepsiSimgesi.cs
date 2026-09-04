using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using EkranCeviri.Cekirdek;
using EkranCeviri.Ekran;
using EkranCeviri.Kisayol;

namespace EkranCeviri.Arayuz;

/// <summary>
/// Görev çubuğu tepsi simgesi ve menüsü. macOS'taki menü çubuğu
/// uygulamasının karşılığı (Uygulama.swift <c>durumCubuguKur</c> ile
/// birebir yapı).
///
/// ARAYÜZ İLKESİ (kullanıcı isteği): üst seviyede yalnız günlük kullanılan
/// 3 eylem + 2 basit tercih (Kim yazıyor, Bana çevir). Geri kalan her şey
/// "Gelişmiş" altında gizli. Teknik olmayan biri kullanacak.
///
/// MENÜ DOĞRULAMASI: Mac'te "Çeviriyi Kapat" katman kapalıyken de etkin
/// görünüp sessizce hiçbir şey yapmıyordu; menü her açılışta durum bağımlı
/// öğeleri ve işaretleri AYARDAN tazeler (<see cref="MenuAcilirken"/>).
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public sealed class TepsiSimgesi : IDisposable
{
    private readonly NotifyIcon _simge;
    private readonly Yonetici _yonetici;

    // Durum bağımlı öğeler
    private readonly ToolStripMenuItem _bolgeOge;
    private readonly ToolStripMenuItem _kisayolOge;
    private readonly ToolStripMenuItem _kapatOge;
    private readonly ToolStripMenuItem _canliOge;
    /// <summary>Menü açılınca ayardan tazelenen işaretler: (öğe, ayardan okunan
    /// değer). Tıklama anında ayar değişip menü eski kalmasın.</summary>
    private readonly List<(ToolStripMenuItem Oge, Func<Ayarlar, bool> Isaretli)> _isaretler = [];

    public TepsiSimgesi(Yonetici yonetici)
    {
        _yonetici = yonetici;
        _bolgeOge = Oge("Bölgeyi Çevir", () => _yonetici.BolgeCevirBaslat());
        _kisayolOge = Oge("Yazdığımı Çevir", () => _yonetici.GidenCevirBaslat());
        _kapatOge = Oge("Çeviriyi Kapat", () => _yonetici.KatmaniKapat());
        _canliOge = new ToolStripMenuItem("Canlı çeviri") { CheckOnClick = true };
        _canliOge.Click += (_, _) =>
        {
            _yonetici.CanliAcik = _canliOge.Checked;
            // Bar açıksa oradaki kutu da uysun; iki yerde iki farklı durum
            // görünmesin.
            _yonetici.Cubuk?.CanliIsaretle(_canliOge.Checked);
        };

        _simge = new NotifyIcon
        {
            Icon = SimgeUret(),
            Text = "Ekran Çeviri",
            Visible = false,
        };
        _simge.ContextMenuStrip = MenuKur();
        // Çift tıklama en sık kullanılan eylemi çalıştırsın
        _simge.DoubleClick += (_, _) => _yonetici.BolgeCevirBaslat();
    }

    public void Goster() => _simge.Visible = true;

    private ContextMenuStrip MenuKur()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => MenuAcilirken();

        // ————— günlük 3 eylem —————
        menu.Items.Add(_bolgeOge);
        menu.Items.Add(_kisayolOge);
        menu.Items.Add(_kapatOge);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(_canliOge);

        // — Günlük tercih 1: ben kimim / karşımdaki kim (tonu belirler)
        var kimlik = new ToolStripMenuItem("Kim yazıyor");
        kimlik.DropDownItems.Add(Secim("Ben",
            [("Kadın", "kadin"), ("Erkek", "erkek"), ("Belirtme", "yok")],
            a => a.BenCinsiyet, (a, v) => a.BenCinsiyet = v));
        kimlik.DropDownItems.Add(Secim("Karşımdaki",
            [("Kadın", "kadin"), ("Erkek", "erkek"), ("Belirtme", "yok")],
            a => a.KarsiCinsiyet, (a, v) => a.KarsiCinsiyet = v));
        menu.Items.Add(kimlik);

        // — Günlük tercih 2: hedef dil
        menu.Items.Add(Secim("Bana çevir",
            Diller.Adlar.Select(d => (d.Ad, d.Kod)).ToArray(),
            a => a.HedefDil, (a, v) => a.HedefDil = v));

        menu.Items.Add(new ToolStripSeparator());

        // ————— GELİŞMİŞ (nadiren dokunulur) —————
        var gelismis = new ToolStripMenuItem("Gelişmiş");
        gelismis.DropDownItems.Add(Secim("Karşı tarafın dili",
            [("Alman modu — Almanya + İsviçre (önerilen)", "alman"),
             ("Yalnız İsviçre Almancası", "isvicre"),
             ("Otomatik (her dil)", "otomatik")],
            a => a.DilModu, (a, v) => a.DilModu = v));
        gelismis.DropDownItems.Add(Secim("Çeviri motoru",
            [("Yapay zekâ — en iyi kalite (önerilen)", "ai"),
             ("Ücretsiz çeviri", "hizli")],
            a => a.Motor, (a, v) => a.Motor = v));
        gelismis.DropDownItems.Add(Anahtar("Yetişkin içerik (sansürsüz)",
            a => a.Yetiskin, a => a.Yetiskin = !a.Yetiskin));
        gelismis.DropDownItems.Add(Anahtar("Hız önceliği (daha hızlı, biraz düşük kalite)",
            a => a.HizOnceligi, a => a.HizOnceligi = !a.HizOnceligi));
        gelismis.DropDownItems.Add(Anahtar("Önce hızlı göster, sonra kaliteyle düzelt",
            a => a.KaliteSonra, a => a.KaliteSonra = !a.KaliteSonra));
        gelismis.DropDownItems.Add(Anahtar("Yazdığımı Çevir hızlı modelle (lehçe kalitesi düşebilir)",
            a => a.GidenHizOnceligi, a => a.GidenHizOnceligi = !a.GidenHizOnceligi));
        gelismis.DropDownItems.Add(Anahtar("Emoji ekleyebilsin",
            a => a.EmojiSerbest, a => a.EmojiSerbest = !a.EmojiSerbest));

        gelismis.DropDownItems.Add(new ToolStripSeparator());
        gelismis.DropDownItems.Add(Oge("Kısayolu Değiştir…", () => _yonetici.KisayolDegistir()));
        gelismis.DropDownItems.Add(Oge("Nasıl Yazayım (üslup)…", () => _yonetici.UslupDuzenle()));
        gelismis.DropDownItems.Add(Oge("Yapay Zekâ Anahtarı…", () => _yonetici.AnahtarSor()));
        gelismis.DropDownItems.Add(Oge("Ayarlar…", () => _yonetici.AyarlariAc()));

        gelismis.DropDownItems.Add(new ToolStripSeparator());
        gelismis.DropDownItems.Add(Anahtar("Sohbet hafızası",
            a => a.GecmisAcik, a => a.GecmisAcik = !a.GecmisAcik));
        gelismis.DropDownItems.Add(Oge("Kayıtlı Çevirileri Sil…", () => _yonetici.HafizayiSil()));
        gelismis.DropDownItems.Add(Oge("Sohbet Geçmişini Sil…", () => _yonetici.GecmisiSil()));

        gelismis.DropDownItems.Add(new ToolStripSeparator());
        gelismis.DropDownItems.Add(Oge("Günlük dosyasını aç", GunlukAc));
        gelismis.DropDownItems.Add(Oge("Arıza günlüğünü aç", ArizaGunluguAc));
        gelismis.DropDownItems.Add(Oge("Yazı tanıma dilleri…", DilleriGoster));
        menu.Items.Add(gelismis);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Oge("Çık", () => _yonetici.Cik()));
        return menu;
    }

    /// <summary>Menü açılırken durum bağımlı öğeleri ve işaretleri tazele
    /// (Mac <c>menuNeedsUpdate</c> + <c>ogeEtkinMi</c>).</summary>
    private void MenuAcilirken()
    {
        var a = _yonetici.Ayar;
        _kapatOge.Enabled = _yonetici.KatmanAcik;
        _bolgeOge.Enabled = !_yonetici.IsSuruyor;
        // Mac ogeEtkinMi: giden çeviri uçuştayken "Yazdığımı Çevir" de kapalı.
        _kisayolOge.Enabled = !_yonetici.GidenSuruyor;
        // Kısayol SABİT YAZILMAZ: kullanıcı değiştirince menü yalan söylüyordu.
        _kisayolOge.Text = "Yazdığımı Çevir  ("
                         + KisayolMetni.Metin(a.KisayolMod, a.KisayolTus) + ")";
        _canliOge.Checked = a.CanliAcik;
        foreach (var (oge, isaretli) in _isaretler) oge.Checked = isaretli(a);
    }

    private static ToolStripMenuItem Oge(string baslik, Action eylem)
    {
        var oge = new ToolStripMenuItem(baslik);
        oge.Click += (_, _) => eylem();
        return oge;
    }

    /// <summary>Açık/kapalı tercih; tıklama <see cref="Yonetici.AyarDegistir"/>
    /// üzerinden gider (kaydet + gerekirse yeniden çevir tek kapıda).</summary>
    private ToolStripMenuItem Anahtar(string baslik, Func<Ayarlar, bool> oku,
                                      Action<Ayarlar> degistir)
    {
        var oge = new ToolStripMenuItem(baslik);
        oge.Click += (_, _) => _yonetici.AyarDegistir(degistir);
        _isaretler.Add((oge, oku));
        return oge;
    }

    /// <summary>Tek seçimli alt menü (radyo): işaret ayardaki değere göre.</summary>
    private ToolStripMenuItem Secim(string baslik, (string Ad, string Deger)[] secenekler,
                                    Func<Ayarlar, string> oku, Action<Ayarlar, string> yaz)
    {
        var ust = new ToolStripMenuItem(baslik);
        foreach (var (ad, deger) in secenekler)
        {
            var oge = new ToolStripMenuItem(ad);
            oge.Click += (_, _) => _yonetici.AyarDegistir(a => yaz(a, deger));
            _isaretler.Add((oge, a => oku(a) == deger));
            ust.DropDownItems.Add(oge);
        }
        return ust;
    }

    private static void GunlukAc() =>
        DosyaAc(System.IO.Path.Combine(Ayarlar.DestekDizini, "gunluk.txt"), "Henüz günlük yok.");

    /// <summary>Arıza günlüğü metin TAŞIMAZ (uzunluk + karma); kullanıcı
    /// "neden çevrilmedi" sorusuna buradan bakar.</summary>
    private static void ArizaGunluguAc() =>
        DosyaAc(ArizaGunlugu.DosyaYolu, "Henüz arıza kaydı yok — bu iyi haber.");

    private static void DosyaAc(string yol, string yoksaMesaj)
    {
        if (!System.IO.File.Exists(yol))
        {
            MessageBox.Show(yoksaMesaj, "Günlük");
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(yol)
                { UseShellExecute = true });
        }
        catch (Exception e) { Gunluk.Hata("günlükAç", e); }
    }

    private static void DilleriGoster()
    {
        var diller = OcrOkuyucu.MevcutDiller();
        var metin = diller.Count == 0
            ? "Hiç yazı tanıma dili bulunamadı."
            : "Bu bilgisayarda kurulu yazı tanıma dilleri:\n\n• "
              + string.Join("\n• ", diller);
        MessageBox.Show(
            metin + "\n\nAlmanca yoksa: Ayarlar → Saat ve dil → Dil ve bölge "
            + "→ Dil ekle → Almanca. Kurarken \"Temel yazma\" seçeneğini "
            + "işaretle — yazı tanıma o pakette geliyor.",
            "Yazı tanıma dilleri", MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    /// <summary>Kullanıcıyı rahatsız etmeyen kısa bildirim. macOS
    /// sürümündeki ekran ortası baloncuğun karşılığı.</summary>
    public void Bilgi(string baslik, string mesaj)
    {
        try
        {
            _simge.BalloonTipTitle = baslik;
            _simge.BalloonTipText = mesaj;
            _simge.BalloonTipIcon = ToolTipIcon.Info;
            _simge.ShowBalloonTip(4000);
        }
        catch (Exception e) { Gunluk.Hata("bildirim", e); }
    }

    /// <summary>
    /// Simgeyi kodla çiziyoruz: .ico dosyası taşımak tek dosyalık .exe
    /// dağıtımını karmaşıklaştırıyor ve dosya kaybolursa uygulama
    /// açılmıyor.
    /// </summary>
    private static Icon SimgeUret()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var ciz = Graphics.FromImage(bitmap))
        {
            ciz.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            ciz.Clear(Color.Transparent);
            using var arka = new SolidBrush(Color.FromArgb(37, 211, 102));
            ciz.FillEllipse(arka, 1, 1, 30, 30);
            using var yazi = new SolidBrush(Color.White);
            using var font = new Font("Segoe UI", 15, FontStyle.Bold,
                                      GraphicsUnit.Pixel);
            var bicim = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            ciz.DrawString("ç", font, yazi, new RectangleF(0, 0, 32, 32), bicim);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _simge.Visible = false;
        _simge.Dispose();
    }
}
