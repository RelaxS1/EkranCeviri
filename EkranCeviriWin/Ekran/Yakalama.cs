using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EkranCeviri.Cekirdek;
using Rect = System.Windows.Rect;

namespace EkranCeviri.Ekran;

/// <summary>
/// Ekran yakalama ve pencere hileleri. macOS'taki ScreenCaptureKit'in
/// karşılığı — ama GDI BitBlt ile. Sohbet ekranı saniyede bir yakalanıyor;
/// Windows.Graphics.Capture'ın karmaşıklığı bu iş için gereksiz.
/// </summary>
[SupportedOSPlatform("windows")]
public static class Yakalama
{
    // ---- Win32
    private const int SRCCOPY = 0x00CC0020;
    /// <summary>Katmanlı (layered) pencereler ekran DC'sine ayrı çiziliyor;
    /// bu bayrak olmadan bazı sohbet uygulamalarının balonları eksik
    /// yakalanıyor.</summary>
    private const int CAPTUREBLT = 0x40000000;

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>Windows 10 2004 (build 19041) ve üstü: pencere ekran
    /// yakalamasında SİYAH görünür.</summary>
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(
        IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(
        IntPtr hdc, IntPtr h);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(
        IntPtr hdcDest, int xDest, int yDest, int w, int h,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex,
                                                    IntPtr dwNewLong);

    private static IntPtr StilOku(IntPtr h) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(h, GWL_EXSTYLE)
                         : GetWindowLong(h, GWL_EXSTYLE);

    private static void StilYaz(IntPtr h, IntPtr stil)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr64(h, GWL_EXSTYLE, stil);
        else SetWindowLong(h, GWL_EXSTYLE, stil.ToInt32());
    }

    // ================= yakalama =================

    /// <summary>
    /// Ekran bölgesini yakalar. Kutu FİZİKSEL pikselde ve sanal masaüstü
    /// koordinatındadır (çok ekranlı kurulumda negatif olabilir).
    /// Başarısızsa null döner — istisna FIRLATMAZ, canlı döngü bir turu
    /// atlayıp devam eder.
    /// </summary>
    public static Bitmap? BolgeYakala(Rect kutu)
    {
        int g = (int)Math.Round(kutu.Width);
        int y = (int)Math.Round(kutu.Height);
        if (g <= 0 || y <= 0 || (long)g * y > 64_000_000) return null;

        IntPtr masaustu = GetDesktopWindow();
        IntPtr kaynakDc = IntPtr.Zero, hedefDc = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero, eski = IntPtr.Zero;
        try
        {
            kaynakDc = GetWindowDC(masaustu);
            if (kaynakDc == IntPtr.Zero) return null;
            hedefDc = CreateCompatibleDC(kaynakDc);
            if (hedefDc == IntPtr.Zero) return null;
            bitmap = CreateCompatibleBitmap(kaynakDc, g, y);
            if (bitmap == IntPtr.Zero) return null;
            eski = SelectObject(hedefDc, bitmap);

            if (!BitBlt(hedefDc, 0, 0, g, y, kaynakDc,
                        (int)Math.Round(kutu.X), (int)Math.Round(kutu.Y),
                        SRCCOPY | CAPTUREBLT))
                return null;

            // Image.FromHbitmap kendi kopyasını üretir; GDI nesnesini
            // burada serbest bırakmak güvenli.
            using var ham = Image.FromHbitmap(bitmap);
            // OCR ve piksel okuma 32bppArgb bekliyor; ham bitmap
            // ekranın biçiminde geliyor.
            var kopya = new Bitmap(ham.Width, ham.Height, PixelFormat.Format32bppArgb);
            using (var ciz = Graphics.FromImage(kopya))
                ciz.DrawImageUnscaled(ham, 0, 0);
            return kopya;
        }
        catch (Exception e)
        {
            Gunluk.Hata("bölgeYakala", e);
            return null;
        }
        finally
        {
            // GDI SIZINTISI ÖLÜMCÜL: saniyede bir yakalayan bir uygulamada
            // serbest bırakılmayan DC/bitmap birkaç saatte uygulamayı
            // öldürür (Windows'ta işlem başına GDI nesnesi sınırı 10.000).
            if (eski != IntPtr.Zero && hedefDc != IntPtr.Zero)
                SelectObject(hedefDc, eski);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (hedefDc != IntPtr.Zero) DeleteDC(hedefDc);
            if (kaynakDc != IntPtr.Zero) ReleaseDC(masaustu, kaynakDc);
        }
    }

    // ================= pencere hileleri =================

    /// <summary>
    /// Pencereyi ekran yakalamasından ÇIKARIR.
    ///
    /// BU EN KRİTİK ÇAĞRI: kendi çeviri katmanımız yakalamaya girerse kendi
    /// çevirimizi tekrar okuyup tekrar çeviriyoruz — macOS'ta uygulama
    /// böyle sonsuz döngüye girmişti. Başarısız olursa üst katmandaki
    /// "zehir kalkanı" devreye girer, o yüzden çökmüyoruz ama günlüğe
    /// yazıyoruz.
    /// </summary>
    public static void KatmandanGizle(IntPtr pencere)
    {
        if (pencere == IntPtr.Zero) return;
        if (!SetWindowDisplayAffinity(pencere, WDA_EXCLUDEFROMCAPTURE))
        {
            // Windows 10 2004 öncesinde bu bayrak yok
            Gunluk.Yaz("uyarı: pencere yakalamadan çıkarılamadı "
                     + $"(hata {Marshal.GetLastWin32Error()}) — "
                     + "zehir kalkanına güveniliyor");
        }
    }

    /// <summary>Katman fareyi TAMAMEN geçirir: sohbet ekranı kullanıcının
    /// kalır — tıklayabilir, kaydırabilir, yazabilir.</summary>
    public static void TiklamayiGecir(IntPtr pencere)
    {
        if (pencere == IntPtr.Zero) return;
        var stil = StilOku(pencere).ToInt64();
        stil |= WS_EX_TRANSPARENT | WS_EX_LAYERED
              | WS_EX_TOOLWINDOW      // Alt+Tab'da görünmesin
              | WS_EX_NOACTIVATE;     // odağı çalmasın
        StilYaz(pencere, new IntPtr(stil));
    }

    /// <summary>Pencereyi fiziksel piksel dikdörtgenine oturtur.</summary>
    public static void PencereyiKonumlandir(IntPtr pencere, Rect kutu) =>
        PencereAraclari.Konumlandir(pencere, kutu);

    /// <summary>Tüm ekranları kapsayan sanal masaüstü (fiziksel piksel).</summary>
    public static Rect TumMasaustu() => PencereAraclari.TumMasaustu();

    // ================= kare karşılaştırma =================

    private const int IzGenislik = 32, IzYukseklik = 32;

    /// <summary>
    /// İki karenin farkı (0 = aynı, 1 = tamamen farklı). Küçük bir ızgaraya
    /// indirgeyip karşılaştırır — canlı döngü saniyede bir çalıştığı için
    /// hızlı olmak zorunda.
    /// </summary>
    public static double IzFarki(Bitmap a, Bitmap b)
    {
        try
        {
            var ia = Iz(a);
            var ib = Iz(b);
            if (ia.Length != ib.Length || ia.Length == 0) return 1.0;
            long toplam = 0;
            for (int i = 0; i < ia.Length; i++) toplam += Math.Abs(ia[i] - ib[i]);
            return toplam / (255.0 * ia.Length);
        }
        catch (Exception e)
        {
            Gunluk.Hata("izFarkı", e);
            return 1.0;
        }
    }

    private static byte[] Iz(Bitmap kaynak)
    {
        using var kucuk = new Bitmap(IzGenislik, IzYukseklik,
                                     PixelFormat.Format32bppArgb);
        using (var ciz = Graphics.FromImage(kucuk))
        {
            ciz.InterpolationMode = System.Drawing.Drawing2D
                .InterpolationMode.HighQualityBilinear;
            ciz.DrawImage(kaynak, 0, 0, IzGenislik, IzYukseklik);
        }
        var kilit = kucuk.LockBits(new Rectangle(0, 0, IzGenislik, IzYukseklik),
                                   ImageLockMode.ReadOnly,
                                   PixelFormat.Format32bppArgb);
        try
        {
            var ham = new byte[Math.Abs(kilit.Stride) * IzYukseklik];
            Marshal.Copy(kilit.Scan0, ham, 0, ham.Length);
            var iz = new byte[IzGenislik * IzYukseklik];
            for (int y = 0; y < IzYukseklik; y++)
                for (int x = 0; x < IzGenislik; x++)
                {
                    int i = y * kilit.Stride + x * 4;
                    // BT.601 gri: renk değişimi değil İÇERİK değişimi arıyoruz
                    iz[y * IzGenislik + x] = (byte)(
                        (ham[i + 2] * 299 + ham[i + 1] * 587 + ham[i] * 114) / 1000);
                }
            return iz;
        }
        finally
        {
            kucuk.UnlockBits(kilit);
        }
    }

    private const int Kova = 256;

    /// <summary>
    /// Satır izdüşümü: her satırın ortalama koyuluğu. Sohbet kaydığında bu
    /// dizi de kayar; çapraz korelasyonla kaç piksel kaydığını buluruz ve
    /// çeviri yamalarını OCR beklemeden birlikte kaydırırız.
    /// </summary>
    public static double[] SatirIzdusumu(Bitmap kare)
    {
        var sonuc = new double[Kova];
        try
        {
            var kilit = kare.LockBits(
                new Rectangle(0, 0, kare.Width, kare.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int adim = kilit.Stride;
                var ham = new byte[Math.Abs(adim) * kare.Height];
                Marshal.Copy(kilit.Scan0, ham, 0, ham.Length);

                // Yatayda 64 noktada örnekle: tam tarama saniyede bir
                // çalışan döngüde gereksiz pahalı.
                int adimX = Math.Max(1, kare.Width / 64);
                for (int k = 0; k < Kova; k++)
                {
                    int y = (int)((long)k * kare.Height / Kova);
                    if (y >= kare.Height) y = kare.Height - 1;
                    long toplam = 0; int adet = 0;
                    for (int x = 0; x < kare.Width; x += adimX)
                    {
                        int i = y * adim + x * 4;
                        toplam += (ham[i + 2] * 299 + ham[i + 1] * 587
                                 + ham[i] * 114) / 1000;
                        adet++;
                    }
                    sonuc[k] = adet > 0 ? toplam / (double)adet : 0;
                }
            }
            finally
            {
                kare.UnlockBits(kilit);
            }
        }
        catch (Exception e)
        {
            Gunluk.Hata("satırİzdüşümü", e);
        }
        return sonuc;
    }

    /// <summary>
    /// İki izdüşüm arasındaki dikey kayma (kova cinsinden) ve benzerlik.
    ///
    /// KORUMALAR (macOS'ta hata sonrası konuldu):
    /// • KONTRAST: düz arka planda (kontrast yok) korelasyon rastgele tepe
    ///   üretiyor ve yamaları rastgele kaydırıyordu.
    /// • TEPE KESKİNLİĞİ: en iyi eşleşme ikinciden belirgin iyi değilse
    ///   kayma güvenilir değildir — üst katman yamaları GİZLER.
    /// </summary>
    public static (int Kayma, double Benzerlik) DikeyKayma(double[] eski,
                                                           double[] yeni)
    {
        if (eski.Length != yeni.Length || eski.Length < 16) return (0, 0);

        double ortEski = eski.Average(), ortYeni = yeni.Average();
        double sapmaEski = Math.Sqrt(eski.Sum(v => (v - ortEski) * (v - ortEski))
                                     / eski.Length);
        double sapmaYeni = Math.Sqrt(yeni.Sum(v => (v - ortYeni) * (v - ortYeni))
                                     / yeni.Length);
        // Kontrast yoksa kayma hesaplamak anlamsız
        if (sapmaEski < 3.0 || sapmaYeni < 3.0) return (0, 0);

        int enFazla = eski.Length / 3;
        double enIyi = double.NegativeInfinity, ikinci = double.NegativeInfinity;
        int enIyiKayma = 0;

        for (int k = -enFazla; k <= enFazla; k++)
        {
            double toplam = 0; int adet = 0;
            for (int i = 0; i < eski.Length; i++)
            {
                int j = i + k;
                if (j < 0 || j >= yeni.Length) continue;
                toplam += (eski[i] - ortEski) * (yeni[j] - ortYeni);
                adet++;
            }
            if (adet < eski.Length / 2) continue;
            double puan = toplam / (adet * sapmaEski * sapmaYeni);
            if (puan > enIyi) { ikinci = enIyi; enIyi = puan; enIyiKayma = k; }
            else if (puan > ikinci) ikinci = puan;
        }

        if (double.IsNegativeInfinity(enIyi)) return (0, 0);
        // Tepe keskin değilse (ikinciyle arasında belirgin fark yok)
        // güvenilir sayma: benzerliği düşür ki üst katman gizlesin.
        double keskinlik = double.IsNegativeInfinity(ikinci)
            ? 1.0 : enIyi - ikinci;
        double benzerlik = keskinlik < 0.02 ? enIyi * 0.5 : enIyi;
        return (enIyiKayma, benzerlik);
    }
}
