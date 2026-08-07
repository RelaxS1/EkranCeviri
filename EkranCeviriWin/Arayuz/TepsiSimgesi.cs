using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using EkranCeviri.Cekirdek;
using EkranCeviri.Ekran;

namespace EkranCeviri.Arayuz;

/// <summary>
/// Görev çubuğu tepsi simgesi ve menüsü. macOS'taki menü çubuğu
/// uygulamasının karşılığı.
///
/// ARAYÜZ İLKESİ (kullanıcı isteği): günlük kullanılan 3 eylem üstte,
/// geri kalan her şey "Gelişmiş" altında gizli. Teknik olmayan biri
/// kullanacak.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public sealed class TepsiSimgesi : IDisposable
{
    private readonly NotifyIcon _simge;
    private readonly Yonetici _yonetici;
    private ToolStripMenuItem? _canliOge;

    public TepsiSimgesi(Yonetici yonetici)
    {
        _yonetici = yonetici;
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

        menu.Items.Add(Oge("Bölgeyi Çevir", () => _yonetici.BolgeCevirBaslat()));
        menu.Items.Add(Oge("Yazdığımı Çevir", () => _yonetici.GidenCevirBaslat()));
        menu.Items.Add(Oge("Çeviriyi Kapat", () => _yonetici.KatmaniKapat()));
        menu.Items.Add(new ToolStripSeparator());

        _canliOge = new ToolStripMenuItem("Canlı çeviri")
        {
            Checked = _yonetici.Ayar.CanliAcik,
            CheckOnClick = true,
        };
        _canliOge.Click += (_, _) => _yonetici.CanliAcik = _canliOge.Checked;
        menu.Items.Add(_canliOge);

        var gelismis = new ToolStripMenuItem("Gelişmiş");
        gelismis.DropDownItems.Add(Oge("Ayarlar…", () => _yonetici.AyarlariAc()));
        gelismis.DropDownItems.Add(Oge("Yapay Zekâ Anahtarı…",
                                       () => _yonetici.AnahtarSor()));
        gelismis.DropDownItems.Add(new ToolStripSeparator());
        gelismis.DropDownItems.Add(Oge("Çeviri hafızasını temizle", HafizaTemizle));
        gelismis.DropDownItems.Add(Oge("Günlük dosyasını aç", GunlukAc));
        gelismis.DropDownItems.Add(Oge("Yazı tanıma dilleri…", DilleriGoster));
        menu.Items.Add(gelismis);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Oge("Çık", () => _yonetici.Cik()));
        return menu;
    }

    private static ToolStripMenuItem Oge(string baslik, Action eylem)
    {
        var oge = new ToolStripMenuItem(baslik);
        oge.Click += (_, _) => eylem();
        return oge;
    }

    private void HafizaTemizle()
    {
        var cevap = MessageBox.Show(
            $"Kayıtlı {_yonetici.Hafiza.Adet} çeviri silinecek. "
            + "Bundan sonra aynı cümleler yeniden çevrilecek (biraz daha "
            + "yavaş ve biraz daha maliyetli olur).\n\nSilinsin mi?",
            "Çeviri hafızası", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (cevap != DialogResult.Yes) return;
        _yonetici.Hafiza.Temizle();
        Bilgi("Hafıza temizlendi", "Kayıtlı çeviriler silindi.");
    }

    private static void GunlukAc()
    {
        var yol = System.IO.Path.Combine(Ayarlar.DestekDizini, "gunluk.txt");
        if (!System.IO.File.Exists(yol))
        {
            MessageBox.Show("Henüz günlük yok.", "Günlük");
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
