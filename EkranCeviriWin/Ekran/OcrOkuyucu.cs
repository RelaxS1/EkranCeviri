using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Versioning;
using EkranCeviri.Cekirdek;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Rect = System.Windows.Rect;

namespace EkranCeviri.Ekran;

/// <summary>
/// Windows'un yerleşik yazı tanıması (Windows.Media.Ocr) — macOS'taki
/// Apple Vision'ın karşılığı.
///
/// GİZLİLİK: tamamen bu bilgisayarda çalışır. Ekran görüntüsü hiçbir
/// sunucuya gitmez, diske yazılmaz. Ağa çıkan tek şey çıkarılan METİNdir.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public sealed class OcrOkuyucu
{
    /// <summary>
    /// Küçük yazıyı büyütmek tanıma doğruluğunu belirgin artırıyor
    /// (macOS tarafında Lanczos büyütmesi harf düşmesini bitirmişti).
    /// WhatsApp balon yazısı tipik olarak 12-14pt; 2x büyütme tatlı nokta —
    /// 3x'te kazanç durup süre iki katına çıkıyor.
    /// </summary>
    private const double Buyutme = 2.0;

    /// <summary>Windows OCR'ın kabul ettiği üst sınır. Aşan görüntü
    /// sessizce reddediliyor — büyütmeyi buna göre kısıyoruz.</summary>
    private const int EnFazlaKenar = 4000;

    private OcrEngine? _motor;
    private string _motorDili = "";

    public static bool DilVarMi(string etiket)
    {
        try
        {
            return OcrEngine.AvailableRecognizerLanguages.Any(d =>
                d.LanguageTag.StartsWith(etiket, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception e)
        {
            Gunluk.Hata("ocrDilVarMı", e);
            return false;
        }
    }

    public static IReadOnlyList<string> MevcutDiller()
    {
        try
        {
            return OcrEngine.AvailableRecognizerLanguages
                .Select(d => d.DisplayName).ToList();
        }
        catch (Exception e)
        {
            Gunluk.Hata("ocrDiller", e);
            return [];
        }
    }

    private OcrEngine? Motor(string dil)
    {
        if (_motor is not null && _motorDili == dil) return _motor;
        try
        {
            var motor = OcrEngine.TryCreateFromLanguage(new Language(dil));
            if (motor is null)
            {
                // Almanca dil paketi kurulu değilse kullanıcıyı çevirisiz
                // bırakmaktansa mevcut dille oku: Latin alfabesi motorları
                // Almancayı kabul edilebilir doğrulukla okuyor, yalnız
                // umlaut'larda zayıflıyor.
                motor = OcrEngine.TryCreateFromUserProfileLanguages();
                if (motor is not null)
                    Gunluk.Yaz($"OCR dili '{dil}' yok, kullanıcı diline düşüldü");
            }
            _motor = motor;
            _motorDili = dil;
            return motor;
        }
        catch (Exception e)
        {
            Gunluk.Hata("ocrMotor", e);
            return null;
        }
    }

    public async Task<IReadOnlyList<OcrSatir>> OkuAsync(Bitmap kare, string dil,
                                                        CancellationToken iptal)
    {
        var motor = Motor(dil);
        if (motor is null) return [];

        double olcek = Buyutme;
        if (kare.Width * olcek > EnFazlaKenar || kare.Height * olcek > EnFazlaKenar)
            olcek = Math.Min((double)EnFazlaKenar / kare.Width,
                             (double)EnFazlaKenar / kare.Height);
        if (olcek < 1.0) olcek = 1.0;

        try
        {
            using var buyuk = Buyut(kare, olcek);
            using var yazilim = await YazilimBitmapAsync(buyuk, iptal)
                                      .ConfigureAwait(false);
            if (yazilim is null) return [];

            // OCR askıda kalırsa canlı mod ölür: üst sınır koy.
            using var zamanAsimi = CancellationTokenSource
                .CreateLinkedTokenSource(iptal);
            zamanAsimi.CancelAfter(TimeSpan.FromSeconds(20));

            var sonuc = await motor.RecognizeAsync(yazilim)
                                   .AsTask(zamanAsimi.Token)
                                   .ConfigureAwait(false);

            var satirlar = new List<OcrSatir>(sonuc.Lines.Count);
            foreach (var satir in sonuc.Lines)
            {
                if (satir.Words.Count == 0) continue;
                // Satırın kendi dikdörtgeni yok: kelimelerin birleşimi.
                double sol = double.MaxValue, ust = double.MaxValue;
                double sag = double.MinValue, alt = double.MinValue;
                foreach (var k in satir.Words)
                {
                    var r = k.BoundingRect;
                    sol = Math.Min(sol, r.X);
                    ust = Math.Min(ust, r.Y);
                    sag = Math.Max(sag, r.X + r.Width);
                    alt = Math.Max(alt, r.Y + r.Height);
                }
                if (sag <= sol || alt <= ust) continue;

                var metin = satir.Text?.Trim();
                if (string.IsNullOrEmpty(metin)) continue;

                // Koordinatları BÜYÜTMEDEN ÖNCEKİ ölçeğe geri çevir —
                // yamalar orijinal karenin pikseline oturmalı.
                satirlar.Add(new OcrSatir
                {
                    Metin = metin,
                    Kutu = new Rect(sol / olcek, ust / olcek,
                                    (sag - sol) / olcek, (alt - ust) / olcek),
                });
            }
            return satirlar;
        }
        catch (OperationCanceledException) when (iptal.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            Gunluk.Hata("ocrOku", e);
            return [];
        }
    }

    private static Bitmap Buyut(Bitmap kaynak, double olcek)
    {
        if (olcek <= 1.0001)
            return kaynak.PixelFormat == PixelFormat.Format32bppArgb
                ? (Bitmap)kaynak.Clone()
                : new Bitmap(kaynak);

        int g = (int)(kaynak.Width * olcek), y = (int)(kaynak.Height * olcek);
        var hedef = new Bitmap(g, y, PixelFormat.Format32bppArgb);
        using var ciz = Graphics.FromImage(hedef);
        // HighQualityBicubic: yazının kenarları yumuşak kalsın, OCR keskin
        // ama zıplayan kenarlarda harf düşürüyor.
        ciz.InterpolationMode = System.Drawing.Drawing2D
            .InterpolationMode.HighQualityBicubic;
        ciz.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        ciz.DrawImage(kaynak, 0, 0, g, y);
        return hedef;
    }

    /// <summary>
    /// System.Drawing.Bitmap → WinRT SoftwareBitmap.
    /// PNG üzerinden gitmenin nedeni: doğrudan piksel aktarımı için
    /// BGRA8 düzeni, stride ve premultiplied alpha'yı elle eşlemek gerekiyor
    /// ve küçük bir hata OCR'a çöp veriyor. PNG kodlaması saniyede bir
    /// çalışan bu döngüde ölçülebilir bir yük değil ve BitmapDecoder
    /// dönüşümü kendisi doğru yapıyor.
    /// </summary>
    private static async Task<SoftwareBitmap?> YazilimBitmapAsync(
        Bitmap kare, CancellationToken iptal)
    {
        try
        {
            using var bellek = new MemoryStream();
            kare.Save(bellek, ImageFormat.Png);
            bellek.Position = 0;

            using var akis = new InMemoryRandomAccessStream();
            await akis.WriteAsync(bellek.ToArray().AsBuffer()).AsTask(iptal)
                      .ConfigureAwait(false);
            await akis.FlushAsync().AsTask(iptal).ConfigureAwait(false);
            akis.Seek(0);

            var cozucu = await BitmapDecoder.CreateAsync(akis).AsTask(iptal)
                                            .ConfigureAwait(false);
            return await cozucu.GetSoftwareBitmapAsync().AsTask(iptal)
                               .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (iptal.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            Gunluk.Hata("yazılımBitmap", e);
            return null;
        }
    }
}
