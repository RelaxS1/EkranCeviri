using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using EkranCeviri.Cekirdek;

namespace EkranCeviri.Arayuz;

/// <summary>
/// API anahtarı giriş penceresi.
///
/// Kullanıcının anahtarı ASLA açık gösterilmez: alan maskeli
/// (<see cref="PasswordBox"/>), kayıtlı anahtar yalnız
/// <c>xai-abcd…wxyz</c> biçiminde özetlenir. Anahtar bu bilgisayarda,
/// Windows'un kendi şifrelemesiyle (DPAPI) saklanır — ayar dosyasına veya
/// uygulamanın içine ASLA yazılmaz.
/// </summary>
public sealed class AnahtarPenceresi : Window
{
    private readonly PasswordBox _alan = new()
    {
        Width = 380,
        Padding = new Thickness(6, 4, 6, 4),
        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
    };

    public string Anahtar { get; private set; } = "";
    public bool Silindi { get; private set; }

    public AnahtarPenceresi(string mevcutAnahtar, bool ilkKullanim)
    {
        Title = ilkKullanim
            ? "Yapay zekâ anahtarı (isteğe bağlı ama önerilir)"
            : "xAI (Grok) API Anahtarı";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;

        var yigin = new StackPanel { Margin = new Thickness(18) };

        yigin.Children.Add(new TextBlock
        {
            Text = ilkKullanim
                ? "Anahtar OLMADAN da çalışır: ücretsiz çeviri motorları "
                + "kullanılır. Ancak İsviçre/Bavyera gibi LEHÇELERİ ücretsiz "
                + "motorlar doğru çeviremiyor — bu yüzden kendi xAI (Grok) "
                + "anahtarını girmen önerilir."
                : mevcutAnahtar.Length > 0
                    ? "Kayıtlı anahtar: " + AnahtarKasasi.Maske(mevcutAnahtar)
                    : "Şu an kayıtlı anahtar yok.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400,
            Margin = new Thickness(0, 0, 0, 12),
        });

        yigin.Children.Add(new TextBlock
        {
            Text = "Anahtar nasıl alınır:\n"
                 + "1. console.x.ai adresine gir, hesap aç\n"
                 + "2. API Keys → Create API Key\n"
                 + "3. Oluşan \"xai-...\" anahtarını buraya yapıştır",
            Margin = new Thickness(0, 0, 0, 6),
        });

        var bag = new Hyperlink(new Run("console.x.ai adresini aç"))
        {
            NavigateUri = new Uri("https://console.x.ai"),
        };
        bag.RequestNavigate += (_, e) => TarayicidaAc(e.Uri);
        yigin.Children.Add(new TextBlock(bag)
        {
            Margin = new Thickness(0, 0, 0, 12),
        });

        yigin.Children.Add(_alan);

        yigin.Children.Add(new TextBlock
        {
            Text = "Anahtarın YALNIZ bu bilgisayarda, Windows'un kendi "
                 + "şifrelemesiyle saklanır. Uygulamanın içine veya ayar "
                 + "dosyasına yazılmaz.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400,
            Opacity = 0.75,
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 14),
        });

        var dugmeler = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        if (mevcutAnahtar.Length > 0)
        {
            var sil = new Button
            {
                Content = "Anahtarı sil",
                Padding = new Thickness(14, 5, 14, 5),
                Margin = new Thickness(0, 0, 8, 0),
            };
            sil.Click += (_, _) => { Silindi = true; DialogResult = true; };
            dugmeler.Children.Add(sil);
        }

        var gec = new Button
        {
            Content = ilkKullanim ? "Şimdilik geç" : "Vazgeç",
            Padding = new Thickness(14, 5, 14, 5),
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true,
        };
        gec.Click += (_, _) => DialogResult = false;
        dugmeler.Children.Add(gec);

        var kaydet = new Button
        {
            Content = "Kaydet",
            Padding = new Thickness(14, 5, 14, 5),
            IsDefault = true,
        };
        kaydet.Click += (_, _) => Kaydet();
        dugmeler.Children.Add(kaydet);

        yigin.Children.Add(dugmeler);
        Content = yigin;
        Loaded += (_, _) => _alan.Focus();
    }

    private void Kaydet()
    {
        var deger = _alan.Password.Trim();
        if (deger.Length == 0) { DialogResult = false; return; }

        // Yanlış yapıştırılmış anahtar SESSİZCE her çeviriyi bozuyor:
        // biçimi burada tut.
        if (!AnahtarKasasi.BicimGecerli(deger))
        {
            MessageBox.Show(
                "Anahtar \"xai-\" ile başlamalı ve daha uzun olmalı. "
                + "console.x.ai → API Keys sayfasından kopyaladığın "
                + "tam metni yapıştır.",
                "Anahtar kaydedilmedi", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        Anahtar = deger;
        DialogResult = true;
    }

    private static void TarayicidaAc(Uri adres)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(adres.ToString())
                { UseShellExecute = true });
        }
        catch (Exception e) { Gunluk.Hata("tarayıcıAç", e); }
    }
}
