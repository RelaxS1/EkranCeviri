namespace EkranCeviri.Testler;

/// <summary>
/// Saf test koşucusunun ortak sayacı ve çıktı biçimi. Hem Windows test
/// projesi (Testler.csproj) hem macOS'ta koşan saf koşucu (Saf.csproj) bunu
/// kullanır — sayaçlar tek yerde birleşir, iki farklı "geçti" mesajı çıkmaz.
/// Console burada serbesttir: bu bir test ikilisi, ürün kodu değil.
/// </summary>
public static class SafYardimci
{
    private static int _gecen, _kalan;

    public static void Baslik(string s) => Console.WriteLine($"\n{s}:");

    public static void Dogru(bool kosul, string ad)
    {
        if (kosul) { _gecen++; Console.WriteLine($"  ✓ {ad}"); }
        else { _kalan++; Console.WriteLine($"  ✗ {ad}"); }
    }

    public static void Esit<T>(T beklenen, T gelen, string ad)
    {
        if (EqualityComparer<T>.Default.Equals(beklenen, gelen))
        {
            _gecen++;
            Console.WriteLine($"  ✓ {ad}");
        }
        else
        {
            _kalan++;
            Console.WriteLine($"  ✗ {ad}\n      beklenen: \"{beklenen}\""
                            + $"\n      gelen   : \"{gelen}\"");
        }
    }

    public static void Sifirla() { _gecen = 0; _kalan = 0; }

    /// <summary>Özet satırı ve çıkış kodu. Etiket "SAF" ise
    /// "TÜM SAF TESTLER GEÇTİ ✅" — CI bu dizgeyi arar.</summary>
    public static int Bitir(string etiket)
    {
        Console.WriteLine();
        var ad = string.IsNullOrEmpty(etiket) ? "TÜM TESTLER" : $"TÜM {etiket} TESTLER";
        if (_kalan == 0)
        {
            Console.WriteLine($"{ad} GEÇTİ ✅  ({_gecen} test)");
            return 0;
        }
        Console.WriteLine($"BAŞARISIZ ❌  {_kalan} test kaldı, {_gecen} geçti");
        return 1;
    }
}
