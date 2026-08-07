using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EkranCeviri.Cekirdek;

namespace EkranCeviri.Arayuz;

/// <summary>
/// Ayarlar. Teknik olmayan biri kullanacak: her seçeneğin yanında ne işe
/// yaradığı düz Türkçe yazıyor, teknik terim yok.
/// </summary>
public sealed class AyarPenceresi : Window
{
    private readonly Ayarlar _ayar;
    private readonly ComboBox _motor = new();
    private readonly ComboBox _dilModu = new();
    private readonly ComboBox _ben = new();
    private readonly ComboBox _karsi = new();
    private readonly CheckBox _yetiskin = new() { Content = "+18 içerik sansürlenmesin" };
    private readonly CheckBox _emoji = new() { Content = "Emoji eklenebilsin" };
    private readonly CheckBox _hiz = new() { Content = "Hız öncelikli (daha ucuz, biraz daha düşük kalite)" };
    private readonly CheckBox _gidenBicim = new() { Content = "Giden mesajda noktalama kullanma (WhatsApp üslubu)" };
    private readonly TextBox _kisilik = new()
    {
        AcceptsReturn = true, Height = 60, TextWrapping = TextWrapping.Wrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    };
    private readonly TextBox _kisayol = new() { IsReadOnly = true, Width = 200 };

    private uint _yeniMod, _yeniTus;

    public AyarPenceresi(Ayarlar ayar)
    {
        _ayar = ayar;
        _yeniMod = ayar.KisayolMod;
        _yeniTus = ayar.KisayolTus;

        Title = "Ayarlar";
        SizeToContent = SizeToContent.Height;
        Width = 480;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;

        var yigin = new StackPanel { Margin = new Thickness(18) };

        _motor.Items.Add("Yapay zekâ (Grok) — lehçeleri anlar, anahtar gerekir");
        _motor.Items.Add("Ücretsiz (Bing/Google) — lehçede zayıf");
        _motor.SelectedIndex = ayar.Motor == "ai" ? 0 : 1;
        Ekle(yigin, "Çeviri motoru", _motor);

        _dilModu.Items.Add("Alman modu — Almanya, Avusturya, İsviçre (önerilir)");
        _dilModu.Items.Add("Yalnız İsviçre lehçeleri");
        _dilModu.Items.Add("Otomatik — dili kendi bulsun");
        _dilModu.SelectedIndex = ayar.DilModu switch
        {
            "isvicre" => 1, "otomatik" => 2, _ => 0,
        };
        Ekle(yigin, "Karşı taraf ne konuşuyor", _dilModu);

        foreach (var k in new[] { "Kadın", "Erkek", "Belirtme" })
        { _ben.Items.Add(k); _karsi.Items.Add(k); }
        _ben.SelectedIndex = Sec(ayar.BenCinsiyet);
        _karsi.SelectedIndex = Sec(ayar.KarsiCinsiyet);
        Ekle(yigin, "Ben", _ben);
        Ekle(yigin, "Karşı taraf", _karsi);

        _yetiskin.IsChecked = ayar.Yetiskin;
        _emoji.IsChecked = ayar.EmojiSerbest;
        _hiz.IsChecked = ayar.HizOnceligi;
        _gidenBicim.IsChecked = ayar.GidenKarakter;
        foreach (var c in new[] { _yetiskin, _emoji, _hiz, _gidenBicim })
        {
            c.Margin = new Thickness(0, 4, 0, 0);
            yigin.Children.Add(c);
        }

        _kisayol.Text = KisayolMetni(_yeniMod, _yeniTus);
        var kisayolDugme = new Button
        {
            Content = "Değiştir",
            Padding = new Thickness(12, 3, 12, 3),
            Margin = new Thickness(8, 0, 0, 0),
        };
        kisayolDugme.Click += (_, _) => KisayolYakala();
        var kisayolSatir = new StackPanel { Orientation = Orientation.Horizontal };
        kisayolSatir.Children.Add(_kisayol);
        kisayolSatir.Children.Add(kisayolDugme);
        Ekle(yigin, "\"Yazdığımı Çevir\" kısayolu", kisayolSatir);

        _kisilik.Text = ayar.Kisilik;
        Ekle(yigin, "Yazım üslubun (isteğe bağlı — nasıl yazdığını anlat)",
             _kisilik);

        var dugmeler = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        var vazgec = new Button
        {
            Content = "Vazgeç", Padding = new Thickness(14, 5, 14, 5),
            Margin = new Thickness(0, 0, 8, 0), IsCancel = true,
        };
        vazgec.Click += (_, _) => DialogResult = false;
        var kaydet = new Button
        {
            Content = "Kaydet", Padding = new Thickness(14, 5, 14, 5),
            IsDefault = true,
        };
        kaydet.Click += (_, _) => Uygula();
        dugmeler.Children.Add(vazgec);
        dugmeler.Children.Add(kaydet);
        yigin.Children.Add(dugmeler);

        Content = new ScrollViewer
        {
            Content = yigin,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 700,
        };
    }

    private static int Sec(string k) => k switch
    {
        "kadin" => 0, "erkek" => 1, _ => 2,
    };

    private static string Kod(int i) => i switch
    {
        0 => "kadin", 1 => "erkek", _ => "",
    };

    private static void Ekle(Panel kap, string baslik, UIElement icerik)
    {
        kap.Children.Add(new TextBlock
        {
            Text = baslik,
            Margin = new Thickness(0, 12, 0, 4),
            FontWeight = FontWeights.SemiBold,
        });
        kap.Children.Add(icerik);
    }

    /// <summary>Kısayolu tuşa basarak yakalar — kullanıcıya "MOD_ALT|0x43"
    /// gibi bir şey yazdırmak anlamsız.</summary>
    private void KisayolYakala()
    {
        var pencere = new Window
        {
            Title = "Yeni kısayol",
            Width = 340, Height = 130,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Content = new TextBlock
            {
                Text = "Kullanmak istediğin tuş birleşimine bas.\n"
                     + "(En az bir Ctrl/Alt/Shift ile birlikte)",
                Margin = new Thickness(18),
                TextWrapping = TextWrapping.Wrap,
            },
        };
        pencere.KeyDown += (_, e) =>
        {
            var tus = e.Key == Key.System ? e.SystemKey : e.Key;
            if (tus is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt
                    or Key.RightAlt or Key.LeftShift or Key.RightShift
                    or Key.LWin or Key.RWin) return;

            uint mod = 0;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mod |= 0x0001;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mod |= 0x0002;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mod |= 0x0004;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mod |= 0x0008;
            // Değiştirici olmadan kısayol atamak tehlikeli: sıradan yazarken
            // tetiklenir ve Ctrl+A ile mesaj kutusunu seçer.
            if (mod == 0) return;

            _yeniMod = mod;
            _yeniTus = (uint)KeyInterop.VirtualKeyFromKey(tus);
            _kisayol.Text = KisayolMetni(_yeniMod, _yeniTus);
            e.Handled = true;
            pencere.Close();
        };
        pencere.ShowDialog();
    }

    private static string KisayolMetni(uint mod, uint tus)
    {
        var parcalar = new List<string>();
        if ((mod & 0x0002) != 0) parcalar.Add("Ctrl");
        if ((mod & 0x0001) != 0) parcalar.Add("Alt");
        if ((mod & 0x0004) != 0) parcalar.Add("Shift");
        if ((mod & 0x0008) != 0) parcalar.Add("Win");
        parcalar.Add(KeyInterop.KeyFromVirtualKey((int)tus).ToString());
        return string.Join(" + ", parcalar);
    }

    private void Uygula()
    {
        _ayar.Motor = _motor.SelectedIndex == 0 ? "ai" : "hizli";
        _ayar.DilModu = _dilModu.SelectedIndex switch
        {
            1 => "isvicre", 2 => "otomatik", _ => "alman",
        };
        _ayar.BenCinsiyet = Kod(_ben.SelectedIndex);
        _ayar.KarsiCinsiyet = Kod(_karsi.SelectedIndex);
        _ayar.Yetiskin = _yetiskin.IsChecked == true;
        _ayar.EmojiSerbest = _emoji.IsChecked == true;
        _ayar.HizOnceligi = _hiz.IsChecked == true;
        _ayar.GidenKarakter = _gidenBicim.IsChecked == true;
        _ayar.Kisilik = _kisilik.Text.Trim();
        _ayar.KisayolMod = _yeniMod;
        _ayar.KisayolTus = _yeniTus;
        DialogResult = true;
    }
}
