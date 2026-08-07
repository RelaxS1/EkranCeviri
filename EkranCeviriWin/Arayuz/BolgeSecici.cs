using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using EkranCeviri.Ekran;

namespace EkranCeviri.Arayuz;

/// <summary>
/// Ekran görüntüsü alır gibi bölge seçtirir: tüm masaüstünü kaplayan hafif
/// karartılmış bir pencere, sürükleyerek dikdörtgen.
///
/// Bu pencere de yakalamadan ÇIKARILIR — seçim yaparken kendi karartmamızı
/// yakalayıp OCR'a vermek anlamsız olurdu.
/// </summary>
public sealed class BolgeSecici : Window
{
    private Point? _baslangic;
    private Rect _secim;
    private readonly SecimGorunumu _gorunum = new();

    /// <summary>Seçilen bölge, FİZİKSEL ekran pikselinde. İptal edilirse null.</summary>
    public Rect? Sonuc { get; private set; }

    public BolgeSecici()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        // Karartmayı görünüm kendisi çiziyor (seçili alanı AÇIKTA bırakmak
        // için). Pencerenin arka planı yine de Transparent olmalı: null
        // olursa WPF fare olaylarını hiç almaz.
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Cursor = Cursors.Cross;
        Content = _gorunum;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var tutamac = new WindowInteropHelper(this).Handle;
        Yakalama.KatmandanGizle(tutamac);
        // TÜM masaüstünü (çok ekranlı dahil) kapla — fiziksel pikselde.
        Yakalama.PencereyiKonumlandir(tutamac, Yakalama.TumMasaustu());
        Activate();
        Focus();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _baslangic = e.GetPosition(this);
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_baslangic is not { } b) return;
        var p = e.GetPosition(this);
        _secim = new Rect(Math.Min(b.X, p.X), Math.Min(b.Y, p.Y),
                          Math.Abs(p.X - b.X), Math.Abs(p.Y - b.Y));
        _gorunum.Secim = _secim;
        _gorunum.InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (_baslangic is null) return;
        _baslangic = null;

        // Kazara tek tıklama seçim sayılmasın
        if (_secim.Width < 40 || _secim.Height < 40) { Kapat(null); return; }

        var olcek = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (olcek <= 0) olcek = 1.0;
        var masaustu = Yakalama.TumMasaustu();
        // Pencere içi DIU → fiziksel ekran pikseli
        Kapat(new Rect(masaustu.X + _secim.X * olcek,
                       masaustu.Y + _secim.Y * olcek,
                       _secim.Width * olcek, _secim.Height * olcek));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Kapat(null);
    }

    private void Kapat(Rect? sonuc)
    {
        Sonuc = sonuc;
        DialogResult = sonuc is not null;
        Close();
    }

    private sealed class SecimGorunumu : FrameworkElement
    {
        private static readonly Brush Perde =
            new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        private static readonly Pen Cerceve =
            new(new SolidColorBrush(Color.FromRgb(90, 190, 255)), 2);

        static SecimGorunumu() { Perde.Freeze(); Cerceve.Freeze(); }

        public Rect Secim { get; set; }

        protected override void OnRender(DrawingContext ciz)
        {
            double g = ActualWidth, y = ActualHeight;
            if (Secim.Width < 1 || Secim.Height < 1)
            {
                ciz.DrawRectangle(Perde, null, new Rect(0, 0, g, y));
                return;
            }
            // Seçili alan AÇIKTA kalsın: perdeyi dört parça hâlinde çiz.
            // Tek dikdörtgeni "silmek" WPF'te mümkün değil.
            var s = Secim;
            ciz.DrawRectangle(Perde, null, new Rect(0, 0, g, s.Top));
            ciz.DrawRectangle(Perde, null,
                              new Rect(0, s.Bottom, g, Math.Max(0, y - s.Bottom)));
            ciz.DrawRectangle(Perde, null, new Rect(0, s.Top, s.Left, s.Height));
            ciz.DrawRectangle(Perde, null,
                              new Rect(s.Right, s.Top,
                                       Math.Max(0, g - s.Right), s.Height));
            ciz.DrawRectangle(null, Cerceve, s);
        }
    }
}
