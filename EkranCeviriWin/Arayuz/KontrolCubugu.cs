using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using EkranCeviri.Ekran;
using Rect = System.Windows.Rect;

namespace EkranCeviri.Arayuz;

/// <summary>
/// Seçili bölgenin üstünde duran küçük kontrol çubuğu: hangi motorun ve
/// hangi lehçenin kullanıldığını gösterir, canlı modu açıp kapatır,
/// katmanı kapatır.
///
/// Bu pencere TIKLANABİLİR olmalı (katmanın aksine) ama yine de ekran
/// yakalamasına GİRMEMELİ — yoksa kendi arayüzümüzü OCR'a veriyoruz.
/// </summary>
public sealed class KontrolCubugu : Window
{
    private readonly Yonetici _yonetici;
    private readonly Rect _fizikselBolge;
    private readonly TextBlock _motor = new()
    {
        Foreground = Brushes.White,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(10, 0, 6, 0),
        FontSize = 12,
    };

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
        Width = 300;
        Height = 34;

        var canli = new CheckBox
        {
            Content = "Canlı",
            Foreground = Brushes.White,
            IsChecked = yonetici.Ayar.CanliAcik,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        canli.Checked += (_, _) => _yonetici.CanliAcik = true;
        canli.Unchecked += (_, _) => _yonetici.CanliAcik = false;

        var kapat = new Button
        {
            Content = "Kapat",
            Padding = new Thickness(10, 2, 10, 2),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        kapat.Click += (_, _) => _yonetici.KatmaniKapat();

        var yerlesim = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(kapat, Dock.Right);
        DockPanel.SetDock(canli, Dock.Right);
        yerlesim.Children.Add(kapat);
        yerlesim.Children.Add(canli);
        yerlesim.Children.Add(_motor);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 32, 34, 38)),
            CornerRadius = new CornerRadius(8),
            Child = yerlesim,
        };
    }

    public string MotorAdi
    {
        get => _motor.Text;
        set => _motor.Text = value;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var tutamac = new WindowInteropHelper(this).Handle;
        // Yakalamadan çıkar AMA tıklamayı geçirme: bu pencere kullanılacak.
        Yakalama.KatmandanGizle(tutamac);

        var olcek = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (olcek <= 0) olcek = 1.0;
        double g = Width * olcek, y = Height * olcek;

        // Bölgenin ÜSTÜNE otur; ekranın tepesindeysek altına geç ki
        // çubuk ekran dışında kalmasın.
        double ust = _fizikselBolge.Y - y - 6;
        var masaustu = Yakalama.TumMasaustu();
        if (ust < masaustu.Y) ust = _fizikselBolge.Bottom + 6;

        Yakalama.PencereyiKonumlandir(tutamac,
            new Rect(_fizikselBolge.X, ust, g, y));
    }
}
