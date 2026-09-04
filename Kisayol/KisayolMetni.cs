namespace EkranCeviri.Kisayol;

/// <summary>
/// Kısayolun kullanıcıya görünen adı ("Ctrl + Alt + C"). SAF: WPF'siz, tuş
/// adı KENDİ tablosundan üretilir (Mac Giden.swift <c>tusAdi/kisayolMetni</c>
/// karşılığı) — <c>KeyInterop</c> WPF'e bağlı olduğu için saf koşucuda
/// derlenmiyor ve kısayol metni test edilemiyordu.
///
/// Kısayol SABİT YAZILMAZ: kullanıcı değiştirince menü ve bar ipucu yalan
/// söylüyordu (Mac'te menüde ^F, barda ⌃⌥C yazıyordu). Menü, bar ipucu ve
/// ayar penceresi hep buradan okur.
/// </summary>
public static class KisayolMetni
{
    // RegisterHotKey değiştirici bitleri (Win32 MOD_*)
    public const uint ModAlt = 0x0001;
    public const uint ModCtrl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    /// <summary>Sanal tuş kodu (VK_*) → görünen ad. Harf/rakam aralıkları
    /// hesaplanır; sık kullanılan özel tuşlar tabloda. Bilinmeyen kod
    /// "Tuş#0x.." olarak görünür ki kullanıcı boş bir kısayol görmesin.</summary>
    public static string TusAdi(uint tus)
    {
        if (tus is >= 0x41 and <= 0x5A) return ((char)tus).ToString();       // A..Z
        if (tus is >= 0x30 and <= 0x39) return ((char)tus).ToString();       // 0..9
        if (tus is >= 0x70 and <= 0x87) return "F" + (tus - 0x70 + 1);        // F1..F24
        if (tus is >= 0x60 and <= 0x69) return "Sayısal " + (tus - 0x60);     // NumPad0..9
        return tus switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x13 => "Pause",
            0x14 => "CapsLock",
            0x1B => "Esc",
            0x20 => "Boşluk",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Sol",
            0x26 => "Yukarı",
            0x27 => "Sağ",
            0x28 => "Aşağı",
            0x2C => "PrintScreen",
            0x2D => "Insert",
            0x2E => "Delete",
            0x6A => "Sayısal *",
            0x6B => "Sayısal +",
            0x6D => "Sayısal -",
            0x6E => "Sayısal .",
            0x6F => "Sayısal /",
            0xBA => ";",
            0xBB => "=",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            _ => $"Tuş#0x{tus:X2}",
        };
    }

    /// <summary>"Ctrl + Alt + C" — değiştirici sırası Windows alışkanlığı
    /// (Ctrl, Alt, Shift, Win); hiç değiştirici yoksa yalnız tuş adı.</summary>
    public static string Metin(uint mod, uint tus)
    {
        var parcalar = new List<string>(5);
        if ((mod & ModCtrl) != 0) parcalar.Add("Ctrl");
        if ((mod & ModAlt) != 0) parcalar.Add("Alt");
        if ((mod & ModShift) != 0) parcalar.Add("Shift");
        if ((mod & ModWin) != 0) parcalar.Add("Win");
        parcalar.Add(TusAdi(tus));
        return string.Join(" + ", parcalar);
    }
}
