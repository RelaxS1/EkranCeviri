using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using EkranCeviri.Cekirdek;
using EkranCeviri.Ekran;

namespace EkranCeviri.Arayuz;

/// <summary>
/// Çeviri katmanı: seçilen bölgenin tam üstünde duran, tıklamayı geçiren,
/// ekran yakalamasına GİRMEYEN saydam pencere.
///
/// Yakalamaya girmemesi hayati: kendi çevirimizi tekrar okursak onu tekrar
/// çeviriyoruz ve uygulama sonsuz döngüye giriyor (macOS'ta yaşandı).
/// Windows'ta çözüm <c>WDA_EXCLUDEFROMCAPTURE</c>.
/// </summary>
public sealed class KatmanPenceresi : Window
{
    private readonly KatmanGorunumu _gorunum = new();

    /// <summary>Bölge, FİZİKSEL ekran pikselinde.</summary>
    private readonly Rect _fizikselBolge;

    public KatmanPenceresi(Rect fizikselBolge)
    {
        _fizikselBolge = fizikselBolge;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        // Odağı ASLA çalma: kullanıcı sohbetine yazmaya devam edebilmeli
        ShowActivated = false;
        Focusable = false;
        Content = _gorunum;
    }

    public KatmanGorunumu Gorunum => _gorunum;

    /// <summary>Fiziksel piksel → DIU çarpanı (ölçekli ekranda 1 değildir).</summary>
    public double Olcek { get; private set; } = 1.0;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var tutamac = new WindowInteropHelper(this).Handle;

        // Sıra önemli: önce yakalamadan çıkar, sonra tıklamayı geçir.
        Yakalama.KatmandanGizle(tutamac);
        Yakalama.TiklamayiGecir(tutamac);

        // Konumlandırmayı Win32 ile FİZİKSEL pikselde yap. WPF'in Left/Top'ı
        // DIU'dur ve çok ekranlı + farklı ölçekli kurulumda yanlış yere
        // düşüyor; fiziksel piksel her ekranda tek anlamlıdır.
        Yakalama.PencereyiKonumlandir(tutamac, _fizikselBolge);

        Olcek = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (Olcek <= 0) Olcek = 1.0;
    }

    /// <summary>Bloklardan yama listesi üretip ekrana basar.
    /// Koordinatlar bloklarda FİZİKSEL piksel; burada DIU'ya çevrilir.</summary>
    public void Guncelle(IReadOnlyList<Blok> bloklar, YamaBoyaci boyaci)
    {
        var yamalar = new List<Yama>(bloklar.Count);
        foreach (var b in bloklar)
        {
            var ceviri = b.Ceviri;
            if (!b.Hedef || string.IsNullOrWhiteSpace(ceviri)) continue;
            // Çeviri kaynağın aynısıysa (fiyat, numara, tek emoji) yama
            // çizmek anlamsız — üstüne aynısını basmak titreme yaratıyor.
            if (string.Equals(ceviri.Trim(), b.Metin.Trim(),
                              StringComparison.OrdinalIgnoreCase)) continue;

            var (arka, yazi) = boyaci.Renkler(b.Kutu);
            yamalar.Add(new Yama
            {
                Kutu = new Rect(b.Kutu.X / Olcek, b.Kutu.Y / Olcek,
                                b.Kutu.Width / Olcek, b.Kutu.Height / Olcek),
                Metin = ceviri,
                Arka = arka,
                Yazi = yazi,
            });
        }
        _gorunum.YamalariAta(yamalar);
    }

    /// <summary>İçerik kaydı: fiziksel piksel cinsinden gelen kaymayı
    /// DIU'ya çevirip uygular.</summary>
    public void Kaydir(double fizikselDy) => _gorunum.Kaydir(fizikselDy / Olcek);
}
