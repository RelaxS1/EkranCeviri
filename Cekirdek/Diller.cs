namespace EkranCeviri.Cekirdek;

/// <summary>
/// Hedef dil listesi (kod → kullanıcıya görünen ad). Ayarlar.Dogrula bu
/// listeye bakar: bilinmeyen bir hedef dil kodu ayar dosyasından sızarsa
/// çeviri istemi anlamsızlaşıyordu. Saf: I/O yok, WPF yok.
/// </summary>
public static class Diller
{
    public static readonly (string Kod, string Ad)[] Adlar =
    {
        ("tr", "Türkçe"), ("en", "İngilizce"), ("de", "Almanca"),
        ("fr", "Fransızca"), ("es", "İspanyolca"), ("it", "İtalyanca"),
        ("ru", "Rusça"), ("ar", "Arapça"), ("pt", "Portekizce"),
    };

    /// <summary>Kodun görünen adı; bilinmiyorsa kodun kendisi (menüde boş
    /// satır görünmesin).</summary>
    public static string Ad(string kod)
    {
        foreach (var (k, ad) in Adlar)
            if (k == kod) return ad;
        return kod;
    }

    public static bool Gecerli(string kod)
    {
        foreach (var (k, _) in Adlar)
            if (k == kod) return true;
        return false;
    }
}
