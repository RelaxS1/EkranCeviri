using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using EkranCeviri.Cekirdek;
using EkranCeviri.Ekran;
using EkranCeviri.Kisayol;
using Rect = System.Windows.Rect;

namespace EkranCeviri.Arayuz;

/// <summary>
/// Seçili bölgenin üstünde duran kontrol çubuğu (Mac <c>barPaneliKur</c>):
/// solda durum etiketi, sağda ✕ 📋 👁 ✨ ⌨️ 💬 düğmeleri ve Canlı anahtarı.
///
/// Bu pencere TIKLANABİLİR olmalı (katmanın aksine) ama:
/// - ekran yakalamasına GİRMEMELİ — yoksa kendi arayüzümüzü OCR'a veriyoruz;
/// - odağı ÇALMAMALI — kullanıcı sohbetine yazmaya devam edebilmeli; bir
///   düğmeye basınca WhatsApp'ın metin kutusundaki imleç kaybolmamalı.
///   Bu yüzden <c>ShowActivated=false</c> + <c>WS_EX_NOACTIVATE</c>;
///   <see cref="Yakalama.TiklamayiGecir"/> DEĞİL (o fareyi de geçirir).
/// </summary>
public sealed class KontrolCubugu : Window
{
    /// <summary>Mac: min(max(bölge, 440), 580); Windows'ta düğme sayısı
    /// aynı ama emoji glifleri geniş — alt sınır 460.</summary>
    private const double EnDarDiu = 460, EnGenisDiu = 580, YukseklikDiu = 38;

    private static readonly Brush BilgiRengi = Firca("#D9D9D9");
    /// <summary>Hata metni turuncu: Mac'te hata ile bilgi aynı griydi,
    /// kullanıcı "anahtar gerekli" satırını fark etmiyordu.</summary>
    private static readonly Brush HataRengi = Firca("#FFA973");

    private readonly Yonetici _yonetici;
    private readonly Rect _fizikselBolge;
    private readonly TextBlock _durum = new()
    {
        Foreground = BilgiRengi,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(14, 0, 8, 0),
        FontSize = 11,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
    private readonly Button _orijinal;
    private readonly Button _kisayolDugme;
    private readonly CheckBox _canli;

    /// <summary>Barın oturduğu FİZİKSEL piksel dikdörtgeni; öneri paneli
    /// altına/üstüne yerleşmek için okur.</summary>
    public Rect FizikselKutu { get; private set; }

    public KontrolCubugu(Yonetici yonetici, Rect fizikselBolge)
    {
        _yonetici = yonetici;
        _fizikselBolge = fizikselBolge;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        Focusable = false;
        Width = EnDarDiu;
        Height = YukseklikDiu;

        var yerlesim = new DockPanel { LastChildFill = true };

        // Sağdan sola: Mac'teki dizilişle aynı (✕ en sağda).
        Sagdan(yerlesim, SimgeDugme("✕", "Kapat", () => _yonetici.KatmaniKapat()));
        Sagdan(yerlesim, SimgeDugme("📋", "Çevirileri kopyala",
                                    () => _yonetici.CevirileriKopyala()));
        _orijinal = SimgeDugme("👁", "Orijinali göster/gizle", OrijinalTiklandi);
        Sagdan(yerlesim, _orijinal);
        Sagdan(yerlesim, SimgeDugme("✨", "Grok AI ile yeniden çevir",
                                    () => _yonetici.KaliteyleYenidenCevir()));
        // Kısayol SABİT YAZILMAZ: kullanıcı değiştirince ipucu yalan
        // söylüyordu (menüde ^F, barda ⌃⌥C yazıyordu).
        _kisayolDugme = SimgeDugme("⌨️", KisayolIpucu(), () => _yonetici.GidenCevirBaslat());
        Sagdan(yerlesim, _kisayolDugme);
        Sagdan(yerlesim, SimgeDugme("💬", "Cevap öner (AI + sohbet hafızası)",
                                    () => _ = _yonetici.CevapOnerAsync(false)));

        _canli = new CheckBox
        {
            Content = "Canlı",
            Foreground = BilgiRengi,
            FontSize = 11,
            IsChecked = yonetici.Ayar.CanliAcik,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Focusable = false,
            ToolTip = "Canlı çeviri: ekran değişince otomatik yeniden çevir",
        };
        _canli.Checked += (_, _) => _yonetici.CanliAcik = true;
        _canli.Unchecked += (_, _) => _yonetici.CanliAcik = false;
        Sagdan(yerlesim, _canli);

        // GENİŞLİK SABİT DEĞİL: en kritik hata metni 291,5 pt ölçüldü,
        // kullanıcıya çözümü söyleyen yarısı hiç görünmüyordu. Sağdaki
        // denetimler yerleştikten sonra kalan alanın TAMAMI etikete kalır.
        yerlesim.Children.Add(_durum);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(225, 32, 34, 38)),
            CornerRadius = new CornerRadius(11),
            Child = yerlesim,
        };
    }

    /// <summary>Bar etiketi. Hata metni turuncu, ipucu tam metni taşır
    /// (kısa bar kesilmiş metni gösteremiyor; Mac DurumEtiketi).</summary>
    public string MotorAdi
    {
        get => _durum.Text;
        set
        {
            _durum.Text = value;
            _durum.Foreground = DurumMetni.HataMi(value) ? HataRengi : BilgiRengi;
            _durum.ToolTip = string.IsNullOrEmpty(value) ? null : value;
            AutomationProperties.SetName(_durum, "Durum: " + value);
        }
    }

    /// <summary>Kısayol değişince ipucu da değişsin (bar açıkken menüden
    /// değiştirilebiliyor).</summary>
    public void KisayolIpucuTazele() => _kisayolDugme.ToolTip = KisayolIpucu();

    /// <summary>Menüden Canlı değiştirilince bardaki kutu da uysun.</summary>
    public void CanliIsaretle(bool acik)
    {
        if (_canli.IsChecked != acik) _canli.IsChecked = acik;
    }

    private string KisayolIpucu() =>
        "Yazdığımı çevir " + KisayolMetni.Metin(_yonetici.Ayar.KisayolMod,
                                                _yonetici.Ayar.KisayolTus)
        + " (Türkçe → karşı dil)";

    private void OrijinalTiklandi()
    {
        // Mac: eye ↔ eye.slash
        _orijinal.Content = _yonetici.OrijinalDegistir() ? "🙈" : "👁";
    }

    private static void Sagdan(DockPanel kap, UIElement oge)
    {
        DockPanel.SetDock(oge, Dock.Right);
        kap.Children.Add(oge);
    }

    /// <summary>28×22 kenarlıksız simge düğmesi (Mac <c>simgeDugme</c>).
    /// Fare üstüne gelince hafif aydınlanır ki tıklanabilir olduğu anlaşılsın.</summary>
    private static Button SimgeDugme(string simge, string ipucu, Action eylem)
    {
        var dugme = new Button
        {
            Content = simge,
            ToolTip = ipucu,
            Width = 28,
            Height = 22,
            Margin = new Thickness(0, 0, 4, 0),
            Padding = new Thickness(0),
            FontSize = 13,
            FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe UI"),
            Foreground = Brushes.White,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
            Template = KenarliksizSablon(),
        };
        AutomationProperties.SetName(dugme, ipucu);
        dugme.Click += (_, _) => eylem();
        return dugme;
    }

    /// <summary>Varsayılan Button şablonu koyu barda beyaz bir kutu çiziyor;
    /// yalnız simge + hover vurgusu kalsın.</summary>
    private static ControlTemplate KenarliksizSablon()
    {
        var kutu = new FrameworkElementFactory(typeof(Border), "kutu");
        kutu.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        kutu.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        var icerik = new FrameworkElementFactory(typeof(ContentPresenter));
        icerik.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        icerik.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        kutu.AppendChild(icerik);

        var sablon = new ControlTemplate(typeof(Button)) { VisualTree = kutu };
        var ustunde = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        ustunde.Setters.Add(new Setter(Border.BackgroundProperty,
                                       new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)),
                                       "kutu"));
        var basili = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        basili.Setters.Add(new Setter(Border.BackgroundProperty,
                                      new SolidColorBrush(Color.FromArgb(96, 255, 255, 255)),
                                      "kutu"));
        sablon.Triggers.Add(ustunde);
        sablon.Triggers.Add(basili);
        return sablon;
    }

    private static Brush Firca(string hex)
    {
        var firca = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        firca.Freeze();
        return firca;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var tutamac = new WindowInteropHelper(this).Handle;
        // Yakalamadan çıkar AMA tıklamayı geçirme: bu pencere kullanılacak.
        Yakalama.KatmandanGizle(tutamac);
        // Odağı çalma + Alt+Tab'da görünme.
        OdaksizPencere.Uygula(tutamac);

        var olcek = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (olcek <= 0) olcek = 1.0;

        // Genişlik bölgeye uyar ama 460..580 DIU arasında kalır: dar bölgede
        // düğmeler üst üste biniyor, geniş bölgede etiket ekranın öbür
        // ucuna kaçıyordu.
        Width = Math.Clamp(_fizikselBolge.Width / olcek, EnDarDiu, EnGenisDiu);
        Height = YukseklikDiu;
        double g = Width * olcek, y = Height * olcek;

        // Konum FİZİKSEL pikselde (çok ekran + farklı ölçek): bölgenin üstü,
        // sığmazsa altı; görünür alana kıstırılır.
        var gorunur = GorunurAlan(_fizikselBolge);
        var (x, ust) = Geometri.BarKonumu(
            new Kutu(_fizikselBolge.X, _fizikselBolge.Y,
                     _fizikselBolge.Width, _fizikselBolge.Height),
            new Kutu(gorunur.X, gorunur.Y, gorunur.Width, gorunur.Height), g, y);
        FizikselKutu = new Rect(x, ust, g, y);
        Yakalama.PencereyiKonumlandir(tutamac, FizikselKutu);
    }

    /// <summary>Verilen fiziksel kutunun bulunduğu ekranın ÇALIŞMA alanı
    /// (görev çubuğu hariç, Mac <c>visibleFrame</c>). Alınamazsa tüm sanal
    /// masaüstü. Statik: öneri paneli de barın ekranına göre kıstırılır
    /// (Mac <c>oneriGoster</c> → <c>bar.screen.visibleFrame</c>); tüm sanal
    /// masaüstüne göre hesaplanınca panel görev çubuğunun üstüne düşüyor,
    /// çok ekranda yanlış kenara kıstırılıyordu.</summary>
    internal static Rect GorunurAlan(Rect fiziksel)
    {
        try
        {
            var ekran = System.Windows.Forms.Screen.FromRectangle(
                new System.Drawing.Rectangle(
                    (int)Math.Round(fiziksel.X), (int)Math.Round(fiziksel.Y),
                    Math.Max(1, (int)Math.Round(fiziksel.Width)),
                    Math.Max(1, (int)Math.Round(fiziksel.Height))));
            var a = ekran.WorkingArea;
            if (a.Width > 0 && a.Height > 0) return new Rect(a.X, a.Y, a.Width, a.Height);
        }
        catch (Exception e) { Gunluk.Hata("barEkran", e); }
        return Yakalama.TumMasaustu();
    }
}

/// <summary>
/// Odak çalmayan araç penceresi stili: <c>WS_EX_NOACTIVATE</c> (tıklanınca
/// ön plana geçmez, sohbetin imleci kalır) + <c>WS_EX_TOOLWINDOW</c>
/// (Alt+Tab'da görünmez). Fareyi GEÇİRMEZ — bar ve öneri paneli tıklanır.
/// </summary>
internal static class OdaksizPencere
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    public static void Uygula(IntPtr pencere, bool odakCalma = true)
    {
        if (pencere == IntPtr.Zero) return;
        var stil = GetWindowLongPtr(pencere, GWL_EXSTYLE).ToInt64();
        stil |= WS_EX_TOOLWINDOW;
        if (odakCalma) stil |= WS_EX_NOACTIVATE;
        SetWindowLongPtr(pencere, GWL_EXSTYLE, new IntPtr(stil));
    }
}
