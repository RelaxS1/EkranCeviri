using System.Globalization;
using System.Windows;
using System.Windows.Media;
using EkranCeviri.Cekirdek;

namespace EkranCeviri.Arayuz;

/// <summary>Ekrana basılacak tek bir çeviri yaması.</summary>
public sealed class Yama
{
    public required Rect Kutu { get; init; }          // DIU, katmana göre
    public required string Metin { get; init; }
    public required Color Arka { get; init; }
    public required Color Yazi { get; init; }
}

/// <summary>
/// Çeviri katmanının çizim yüzeyi. Orijinal balonun üstüne, balonun kendi
/// arka plan rengiyle bir dikdörtgen basar ve çeviriyi oraya yazar —
/// mesaj Türkçe yazılmış gibi görünsün diye.
///
/// RENDER İNVARİANTLARI (hepsi macOS'ta hata sonrası konuldu):
/// • Ölçüm ve çizim AYNI <see cref="FormattedText"/> nesnesini kullanır.
///   Ayrı nesnelerle ölçüp çizmek son kelimeyi kırpıyordu.
/// • Yama KIRPILMAZ, gerekirse BÜYÜR. Kırpma yarım cümle gösteriyordu.
/// • Saat damgası için balonun altında ~15 DIU pay bırakılır.
/// • Çeviri kaynağıyla özdeşse (fiyat, numara, emoji) yama HİÇ çizilmez —
///   aynı şeyin üstüne aynı şeyi basmak titreme hissi veriyordu.
/// </summary>
public sealed class KatmanGorunumu : FrameworkElement
{
    private readonly object _kilit = new();
    private List<Yama> _yamalar = [];
    private double _ofsetY;
    private bool _gizli;

    private static readonly Typeface YaziTipi = new(
        new FontFamily("Segoe UI"), FontStyles.Normal,
        FontWeights.Normal, FontStretches.Normal);

    public KatmanGorunumu()
    {
        IsHitTestVisible = false;   // sohbet ekranı kullanıcının kalır
        SnapsToDevicePixels = true;
    }

    public void YamalariAta(List<Yama> yamalar)
    {
        lock (_kilit)
        {
            _yamalar = yamalar;
            _ofsetY = 0;
            _gizli = false;
        }
        Yenile();
    }

    /// <summary>İçerik kaydığında yamaları OCR beklemeden birlikte kaydır.
    /// Beklemek "eski konumda asılı yama" görüntüsü veriyordu.</summary>
    public void Kaydir(double dy)
    {
        lock (_kilit) { _ofsetY += dy; _gizli = false; }
        Yenile();
    }

    /// <summary>Güvenilir kaydırma hesaplanamıyorsa yamayı YANLIŞ YERDE
    /// bırakmaktansa gizle. Ölü bölge olmaz — ya doğru ya hiç.</summary>
    public void Gizle(bool gizli)
    {
        lock (_kilit)
        {
            if (_gizli == gizli) return;
            _gizli = gizli;
        }
        Yenile();
    }

    public double OfsetY { get { lock (_kilit) return _ofsetY; } }

    private void Yenile()
    {
        if (Dispatcher.CheckAccess()) InvalidateVisual();
        else Dispatcher.BeginInvoke(InvalidateVisual);
    }

    protected override void OnRender(DrawingContext ciz)
    {
        List<Yama> yamalar;
        double ofset;
        lock (_kilit)
        {
            if (_gizli) return;
            yamalar = _yamalar;
            ofset = _ofsetY;
        }

        foreach (var y in yamalar)
        {
            if (string.IsNullOrWhiteSpace(y.Metin)) continue;
            var kutu = y.Kutu;
            kutu.Y += ofset;
            // Tamamen katman dışına çıkmışsa çizme
            if (kutu.Bottom < -20 || kutu.Top > ActualHeight + 20) continue;
            YamaCiz(ciz, kutu, y);
        }
    }

    private void YamaCiz(DrawingContext ciz, Rect kutu, Yama y)
    {
        // Saat damgası balonun sağ altında duruyor: onu ezmeyelim.
        const double SaatPayi = 15;
        double genislik = Math.Max(20, kutu.Width);
        double kullanilabilirYukseklik = Math.Max(12, kutu.Height - SaatPayi);

        // Balona sığan en büyük punto: büyükten küçüğe dene. Sığmıyorsa
        // KIRPMA — en küçük puntoda yamayı aşağı doğru BÜYÜT.
        const double EnBuyuk = 15, EnKucuk = 9.5;
        double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (dip <= 0) dip = 1.0;

        FormattedText? yazi = null;
        for (double p = EnBuyuk; p >= EnKucuk; p -= 0.5)
        {
            var deneme = MetinKur(y.Metin, p, genislik - 8, y.Yazi, dip);
            yazi = deneme;
            if (deneme.Height <= kullanilabilirYukseklik) break;
        }
        if (yazi is null) return;

        // Yama gerçek metin yüksekliğine göre büyür (asla kırpılmaz)
        double yamaYukseklik = Math.Max(kutu.Height,
                                        yazi.Height + 8 + SaatPayi);
        var yamaKutu = new Rect(kutu.X, kutu.Y, genislik, yamaYukseklik);

        // Balonun kendi rengiyle doldur → Türkçe yazılmış gibi görünsün
        var firca = new SolidColorBrush(y.Arka);
        firca.Freeze();
        ciz.DrawRoundedRectangle(firca, null, yamaKutu, 6, 6);

        // ÖLÇÜLEN NESNENİN TA KENDİSİ çizilir (ayrı nesne = kırpılan son kelime)
        ciz.DrawText(yazi, new Point(yamaKutu.X + 4, yamaKutu.Y + 3));
    }

    private static FormattedText MetinKur(string metin, double punto,
                                          double genislik, Color renk,
                                          double dip)
    {
        var firca = new SolidColorBrush(renk);
        firca.Freeze();
        var t = new FormattedText(
            metin,
            CultureInfo.GetCultureInfo("tr-TR"),
            FlowDirection.LeftToRight,
            YaziTipi,
            punto,
            firca,
            // Ekranın GERÇEK ölçeği verilmeli: yanlış değer glif
            // rasterleştirmesini bozup ölçekli ekranda yazıyı bulanık
            // gösteriyor. Ölçüler zaten DIU'da, bu yalnız keskinliği
            // etkiler.
            pixelsPerDip: dip)
        {
            MaxTextWidth = Math.Max(20, genislik),
            Trimming = TextTrimming.None,
            MaxLineCount = 40,
        };
        return t;
    }
}
