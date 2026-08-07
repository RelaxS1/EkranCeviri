using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Rect = System.Windows.Rect;
// Color hem GDI'da hem WPF'te var. Bu sınıf GDI'dan piksel okur ama WPF'e
// renk döndürür — hangisini kastettiğimiz açık yazılmalı.
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;

namespace EkranCeviri.Ekran;

/// <summary>
/// Yakalanan kareden balonun ARKA PLAN RENGİNİ çıkarır. Çeviri yaması bu
/// renkle boyanınca mesaj "Türkçe yazılmış" gibi görünür; sabit bir renk
/// kullanmak koyu temada beyaz kutular, açık temada siyah kutular
/// bırakıyordu.
///
/// Yöntem: metin kutusunun ÇEVRESİNDEKİ ince şeritten örnek alınır (metnin
/// kendi pikselleri değil — orada yazı rengi baskın). En sık görülen renk
/// balonun rengidir. Yazı rengi arka planın parlaklığına göre seçilir.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class YamaBoyaci : IDisposable
{
    private readonly int _genislik;
    private readonly int _yukseklik;
    private readonly int _adim;
    private readonly byte[] _piksel;   // BGRA

    public YamaBoyaci(Bitmap kare)
    {
        _genislik = kare.Width;
        _yukseklik = kare.Height;
        var kilit = kare.LockBits(new Rectangle(0, 0, _genislik, _yukseklik),
                                  ImageLockMode.ReadOnly,
                                  PixelFormat.Format32bppArgb);
        try
        {
            _adim = kilit.Stride;
            _piksel = new byte[Math.Abs(_adim) * _yukseklik];
            Marshal.Copy(kilit.Scan0, _piksel, 0, _piksel.Length);
        }
        finally
        {
            // LockBits sonrası UnlockBits ŞART: yoksa GDI tutamacı sızar ve
            // saniyede bir çalışan canlı modda uygulama birkaç saatte ölür.
            kare.UnlockBits(kilit);
        }
    }

    private Color32 Oku(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _genislik || y >= _yukseklik)
            return new Color32(0, 0, 0, 0);
        int i = y * _adim + x * 4;
        return new Color32(_piksel[i + 2], _piksel[i + 1], _piksel[i], _piksel[i + 3]);
    }

    /// <summary>(arka plan, yazı) rengi. Kutu FİZİKSEL piksel.</summary>
    public (Color Arka, Color Yazi) Renkler(Rect kutu)
    {
        var sayac = new Dictionary<int, int>(64);
        int x0 = (int)Math.Floor(kutu.X), y0 = (int)Math.Floor(kutu.Y);
        int x1 = (int)Math.Ceiling(kutu.Right), y1 = (int)Math.Ceiling(kutu.Bottom);

        // Metin kutusunun 1–4 piksel DIŞINDAKİ şerit: balonun dolgusu.
        // Metnin kendi içinden örneklemek yazı rengini "arka plan" sanmaya
        // yol açıyordu.
        for (int pay = 1; pay <= 4; pay++)
        {
            for (int x = x0 - pay; x <= x1 + pay; x += 2)
            {
                Ekle(sayac, Oku(x, y0 - pay));
                Ekle(sayac, Oku(x, y1 + pay));
            }
            for (int y = y0 - pay; y <= y1 + pay; y += 2)
            {
                Ekle(sayac, Oku(x0 - pay, y));
                Ekle(sayac, Oku(x1 + pay, y));
            }
        }

        if (sayac.Count == 0) return (Color.FromRgb(240, 240, 240), Colors.Black);

        int enSik = 0, enCok = -1;
        foreach (var (renk, adet) in sayac)
            if (adet > enCok) { enCok = adet; enSik = renk; }

        byte r = (byte)((enSik >> 16) & 0xFF);
        byte g = (byte)((enSik >> 8) & 0xFF);
        byte b = (byte)(enSik & 0xFF);
        var arka = Color.FromRgb(r, g, b);

        // ITU-R BT.601 parlaklık: koyu balonda beyaz, açık balonda siyah yazı.
        double parlaklik = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        var yazi = parlaklik < 0.5
            ? Color.FromRgb(245, 245, 245)
            : Color.FromRgb(20, 20, 20);
        return (arka, yazi);
    }

    /// <summary>Renkleri 8'lik kovalara indirger: JPEG benzeri gürültü
    /// yüzünden aynı balon onlarca "farklı" renk üretiyordu.</summary>
    private static void Ekle(Dictionary<int, int> sayac, Color32 c)
    {
        if (c.A < 128) return;
        int anahtar = ((c.R & 0xF8) << 16) | ((c.G & 0xF8) << 8) | (c.B & 0xF8);
        sayac[anahtar] = sayac.TryGetValue(anahtar, out var n) ? n + 1 : 1;
    }

    public void Dispose() { }

    private readonly record struct Color32(byte R, byte G, byte B, byte A);
}
