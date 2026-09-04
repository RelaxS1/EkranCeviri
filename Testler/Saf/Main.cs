using EkranCeviri.Testler;

namespace EkranCeviri.Testler.Saf;

/// <summary>Saf koşucu girişi: macOS'ta `dotnet run` ile koşar; EXIT=0 ve
/// "TÜM SAF TESTLER GEÇTİ ✅" kanıttır.</summary>
public static class SafKosucu
{
    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        SafYardimci.Sifirla();
        SafTestlerA.Kos();
        SafTestlerB.Kos();
        SafTestlerC.Kos();
        SafTestlerD.Kos();
        return SafYardimci.Bitir("SAF");
    }
}
