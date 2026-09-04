namespace EkranCeviri.Cekirdek;

/// <summary>
/// WPF'e bağımsız dikdörtgen (saf koşucuda System.Windows.Rect yok).
/// Y ekseni Windows'ta AŞAĞI büyür; macOS'taki ters eksen burada
/// dönüştürülmüştür.
/// </summary>
public readonly record struct Kutu(double X, double Y, double Genislik, double Yukseklik)
{
    public double Sag => X + Genislik;
    public double Alt => Y + Yukseklik;
    public double OrtaX => X + Genislik / 2;
}

/// <summary>Yerleşim hesapları. Saf: pencere açmaz, ölçmez.</summary>
public static class Geometri
{
    /// <summary>
    /// Kontrol barının sol-üst köşesi. macOS `barPanelKonumu` karşılığı:
    /// x bölgeye ortalanır ve görünür alana kıstırılır; y önce bölgenin
    /// ÜSTÜ (bolge.Y - yukseklik - 8), oraya sığmazsa ALTI (bolge.Alt + 8).
    /// Sonra iki eksende de 8 piksel payla kıstırılır: bölge ekranın tam
    /// yüksekliğini kaplayınca bar ekran DIŞINA taşıyor, kullanıcı düğmeleri
    /// bulamıyordu.
    /// </summary>
    public static (double X, double Y) BarKonumu(Kutu bolge, Kutu gorunur,
                                                 double genislik, double yukseklik)
    {
        double x = bolge.OrtaX - genislik / 2;
        x = Math.Max(gorunur.X + 8, Math.Min(x, gorunur.Sag - genislik - 8));

        double y = bolge.Y - yukseklik - 8;
        if (y < gorunur.Y) y = bolge.Alt + 8;
        y = Math.Max(gorunur.Y + 8, Math.Min(y, gorunur.Alt - yukseklik - 8));
        return (x, y);
    }
}
