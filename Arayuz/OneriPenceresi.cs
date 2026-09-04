using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using EkranCeviri.Cekirdek;
using EkranCeviri.Ekran;
using Rect = System.Windows.Rect;

namespace EkranCeviri.Arayuz;

/// <summary>
/// Cevap önerisi paneli (Mac <c>oneriGoster</c>): barın altında (sığmazsa
/// üstünde) duran koyu yarı saydam kutu; her öneri = karşı dildeki cevap +
/// (Türkçe anlamı) + Kopyala; altta Yenile ve Kapat.
///
/// Yakalamadan GİZLİ: panel bölgenin üstüne düşerse OCR kendi önerimizi
/// okuyup çevirmeye kalkıyordu. Açılırken odak ALMAZ (kullanıcı yazmaya
/// devam edebilsin); tıklanınca odak alması normaldir — metin seçilebilir.
/// Bar kapanınca panel de kapanır (Mac <c>katmaniKapat</c> ikisini
/// birlikte kapatıyordu; burada kapanış barın <c>Closed</c> olayına bağlı).
/// </summary>
public sealed class OneriPenceresi : Window
{
    private const double EnDarDiu = 420;

    private readonly Yonetici _yonetici;
    private readonly KontrolCubugu _bar;
    private readonly StackPanel _liste = new();
    private List<(string Cevap, string Turkce)> _sonOneriler = [];

    public OneriPenceresi(Yonetici yonetici, KontrolCubugu bar)
    {
        _yonetici = yonetici;
        _bar = bar;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        SizeToContent = SizeToContent.Height;
        Width = Math.Max(EnDarDiu, bar.Width);

        var dugmeler = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 10, 8),
        };
        var yenile = KucukDugme("Yenile", "Farklı öneriler iste");
        yenile.Click += (_, _) =>
        {
            Hide();
            _ = _yonetici.CevapOnerAsync(farkli: true);
        };
        var kapat = KucukDugme("Kapat", "Paneli kapat");
        kapat.Click += (_, _) => Close();
        dugmeler.Children.Add(yenile);
        dugmeler.Children.Add(kapat);

        var govde = new StackPanel();
        govde.Children.Add(_liste);
        govde.Children.Add(dugmeler);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(232, 32, 34, 38)),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(0, 8, 0, 0),
            Child = govde,
        };

        bar.Closed += (_, _) => { try { Close(); } catch (InvalidOperationException) { } };
    }

    /// <summary>Önerileri doldurup gösterir; açıksa yerinde yeniler.</summary>
    public void Goster(List<(string Cevap, string Turkce)> oneriler)
    {
        _sonOneriler = oneriler;
        _liste.Children.Clear();
        for (var i = 0; i < oneriler.Count; i++)
        {
            _liste.Children.Add(OneriSatiri(i, oneriler[i].Cevap, oneriler[i].Turkce));
            if (i < oneriler.Count - 1)
                _liste.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(14, 4, 14, 4),
                    Background = new SolidColorBrush(Color.FromArgb(56, 255, 255, 255)),
                });
        }
        Width = Math.Max(EnDarDiu, _bar.Width);
        if (!IsVisible) Show();
        UpdateLayout();
        Konumlandir();
    }

    private UIElement OneriSatiri(int sira, string cevap, string turkce)
    {
        var satir = new DockPanel { Margin = new Thickness(14, 4, 10, 4), LastChildFill = true };

        var kopyala = KucukDugme("Kopyala", "Cevabı panoya kopyala");
        kopyala.VerticalAlignment = VerticalAlignment.Center;
        kopyala.Click += (_, _) => Kopyala(sira);
        DockPanel.SetDock(kopyala, Dock.Right);
        satir.Children.Add(kopyala);

        var metinler = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        // Seçilebilir metin: kullanıcı yalnız bir kısmını almak isteyebilir.
        metinler.Children.Add(new TextBox
        {
            Text = cevap,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            Foreground = Brushes.White,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.IBeam,
        });
        if (!string.IsNullOrWhiteSpace(turkce))
            metinler.Children.Add(new TextBlock
            {
                Text = $"({turkce})",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(178, 178, 178)),
                Margin = new Thickness(0, 3, 0, 0),
            });
        satir.Children.Add(metinler);
        return satir;
    }

    private void Kopyala(int sira)
    {
        if (sira < 0 || sira >= _sonOneriler.Count) return;
        try
        {
            Clipboard.SetText(_sonOneriler[sira].Cevap);
            _yonetici.GeriBildir($"Öneri {sira + 1} panoya kopyalandı ✓");
        }
        catch (Exception e)
        {
            // Pano başka uygulama tarafından kilitliyken COMException geliyor;
            // kullanıcı tekrar dener, uygulama düşmez.
            Gunluk.Hata("oneriKopyala", e);
            _yonetici.GeriBildir("⚠ Panoya kopyalanamadı — tekrar dene");
        }
    }

    private static Button KucukDugme(string baslik, string ipucu) => new()
    {
        Content = baslik,
        ToolTip = ipucu,
        FontSize = 11,
        Padding = new Thickness(10, 2, 10, 2),
        Margin = new Thickness(6, 0, 0, 0),
        Focusable = false,
    };

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var tutamac = new WindowInteropHelper(this).Handle;
        Yakalama.KatmandanGizle(tutamac);
        // Alt+Tab'da görünmesin; odak çalmama yalnız açılışta (ShowActivated).
        OdaksizPencere.Uygula(tutamac, odakCalma: false);
        Konumlandir();
    }

    /// <summary>Barın altına, 6 px boşlukla; ekranın altına sığmazsa barın
    /// üstüne (Mac). Fiziksel pikselde konumlandırılır.</summary>
    private void Konumlandir()
    {
        var tutamac = new WindowInteropHelper(this).Handle;
        if (tutamac == IntPtr.Zero) return;
        var olcek = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (olcek <= 0) olcek = 1.0;
        var bar = _bar.FizikselKutu;
        double g = Width * olcek;
        double y = Math.Max(ActualHeight, 40) * olcek;
        // Barın EKRANININ çalışma alanı (Mac bar.screen.visibleFrame) — tüm
        // sanal masaüstü değil; yoksa panel görev çubuğuna biner.
        var masaustu = KontrolCubugu.GorunurAlan(bar);

        double ust = bar.Bottom + 6;
        if (ust + y > masaustu.Bottom) ust = bar.Y - y - 6;
        ust = Math.Max(masaustu.Y, ust);
        double x = Math.Max(masaustu.X, Math.Min(bar.X, masaustu.Right - g));
        Yakalama.PencereyiKonumlandir(tutamac, new Rect(x, ust, g, y));
    }
}
