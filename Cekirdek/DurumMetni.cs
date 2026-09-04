namespace EkranCeviri.Cekirdek;

/// <summary>
/// Bardaki durum metninin HATA mı BİLGİ mi olduğunu ayırır. macOS'ta hata ile
/// bilgi aynı griydi; kullanıcı "anahtar gerekli" satırını fark etmiyordu.
/// Kural saf tutuldu: arayüz (KontrolCubugu) yalnız rengi seçer.
/// </summary>
public static class DurumMetni
{
    public static readonly string[] HataIzleri =
    {
        "hata", "yok", "alınamadı", "vermedi", "yakalanamıyor",
        "reddedildi", "başarısız", "gerekli", "⚠",
    };

    public static bool HataMi(string metin)
    {
        var d = metin.ToLowerInvariant();
        foreach (var iz in HataIzleri)
            if (d.Contains(iz, StringComparison.Ordinal)) return true;
        return false;
    }
}
