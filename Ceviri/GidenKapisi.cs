using System.Text.RegularExpressions;

namespace EkranCeviri.Ceviri;

/// <summary>
/// Giden mesajın kapıdan geri çevrilme nedeni. <see cref="Neden"/> kullanıcıya
/// balonda gösterilir (içerik parçası taşıyabilir: "sayı değişti: 0791…");
/// çıktı YAPIŞTIRILMAZ. <see cref="Kategori"/> GÜNLÜĞE gider: gunluk.txt
/// "içerik yok" vaadi (README/SECURITY) verir, oysa Neden'deki telefon/IBAN/
/// e-posta parçası diske düşüyordu. Günlüğe yalnız kategori yazılır.
/// </summary>
public sealed class GidenRet : Exception
{
    public string Neden { get; }

    /// <summary>İçerik taşımayan sınıf: "sayi" · "yonlendirici" · "uzunluk"
    /// · "turkce" · "bos" · "motor" · "anahtar". Günlük yalnız bunu yazar.</summary>
    public string Kategori { get; }

    public GidenRet(string neden, string kategori = "diger") : base(neden)
    {
        Neden = neden;
        Kategori = kategori;
    }
}

/// <summary>
/// GİDEN ÇIKTI KAPISI — SECURITY.md'nin "doğrulanmazsa reddedilir" vaadini
/// kod yapar. Ekrandaki karşı taraf mesajları tarz örneği olarak modele
/// gidiyor; enjekte bir IBAN/URL kullanıcı okumadan yapıştırma kutusuna
/// yapışabiliyordu. Saf: I/O yok.
/// </summary>
public static partial class GidenKapisi
{
    // Bir kez derlenir. URL/alan adı, IBAN, e-posta, telefon (7+ rakam,
    // ayraçlı). Saat (17:30), ondalık (1,5), fiyat (150.-) bu desenlere GİRMEZ.
    [GeneratedRegex(@"\b(?:https?://|www\.)\S+|\b[a-z0-9-]+\.(?:com|ch|de|at|net|org|io|ly|me|app)\b",
                    RegexOptions.IgnoreCase)]
    private static partial Regex UrlDeseni();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?:\s?[A-Z0-9]{4}){3,7}\b")]
    private static partial Regex IbanDeseni();

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EpostaDeseni();

    [GeneratedRegex(@"\+?\d[\d\s().-]{6,}\d")]
    private static partial Regex TelefonDeseni();

    /// <summary>Metindeki yönlendirici izler (bağlantı/hesap/numara): küçük
    /// harf ve boşluksuz — "079 123 45 67" ile "0791234567" aynı izdir.</summary>
    public static HashSet<string> YonlendiriciIzler(string s)
    {
        var izler = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in new[] { UrlDeseni(), IbanDeseni(), EpostaDeseni(), TelefonDeseni() })
            foreach (Match m in d.Matches(s))
                izler.Add(BosluksuzKucuk(m.Value));
        return izler;
    }

    private static string BosluksuzKucuk(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s.ToLowerInvariant())
            if (!char.IsWhiteSpace(c)) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>
    /// null = güvenli. Aksi hâlde ret nedeni:
    /// - rakam dizisi birebir korunmalı (eklenen rakam da ihlal)
    /// - kaynakta olmayan URL / IBAN / e-posta / telefon → ret
    /// - aşırı uzama (lehçe Türkçeden ~2× uzayabilir; 3× üstü şüpheli)
    /// - Türkçe kalıntı → ret
    /// uzunlukDenetle yalnız YAPIŞTIRILAN (biçimli) metinde anlamlı; ham
    /// yanıt noktalama taşıdığı için birkaç karakter daha uzundur.
    /// </summary>
    public static GidenRet? GuvenliMi(string kaynak, string cikti, bool uzunlukDenetle = true)
    {
        var sorun = Kalite.SayilarKorunduMu(kaynak, cikti);
        if (sorun is not null)
            return new GidenRet($"Sayılar korunmadı ({sorun})", "sayi");

        var yeni = YonlendiriciIzler(cikti);
        yeni.ExceptWith(YonlendiriciIzler(kaynak));
        if (yeni.Count > 0)
        {
            var ilk = yeni.Order(StringComparer.Ordinal).First();
            var kisa = ilk.Length > 24 ? ilk[..24] : ilk;
            return new GidenRet("Kaynakta olmayan bağlantı/hesap/numara eklendi: "
                                + kisa + "…", "yonlendirici");
        }

        if (uzunlukDenetle && cikti.Length > Math.Max(80, kaynak.Length * 3))
            return new GidenRet($"Çıktı beklenenden çok uzun ({cikti.Length} kr)", "uzunluk");

        if (Kalite.TurkceKalintiVar(cikti))
            return new GidenRet("Çeviride Türkçe kaldı", "turkce");

        return null;
    }

    /// <summary>
    /// HEM HAM YANITA HEM BİÇİMLİ ÇIKTIYA. GidenFormatla "." ":" gibi
    /// işaretleri siliyor: "www.kotu.com" biçimden sonra "wwwkotucom" oluyor
    /// ve URL/e-posta deseni artık EŞLEŞMİYORDU — kapının bağlantı/hesap
    /// katmanı fiilen ölüydü (yalnız biçimli çıktı denetleniyordu). Ham
    /// yanıtta uzunluk denetimi yapılmaz.
    /// </summary>
    public static GidenRet? Kapi(string kaynak, string ham, string bicimli)
    {
        var ret = GuvenliMi(kaynak, ham, uzunlukDenetle: false);
        if (ret is not null) return ret;
        return GuvenliMi(kaynak, bicimli);
    }

    /// <summary>Giden çevirinin ETKİN motoru. ÜCRETSİZ GERÇEKTEN ÜCRETSİZ
    /// (sahip kararı #7): ana motoru "ücretsiz" yapan kullanıcı, giden
    /// motor tercihini hiç görmeden yazdığı her mesajı xAI'ye gönderiyordu.</summary>
    public static string MotorSecimi(string motor, string gidenMotor) =>
        motor == "ai" ? gidenMotor : "bing";

    /// <summary>Giden istemin karakter metni: ayrı giden üslup doluysa o,
    /// yoksa genel kişilik. TEK YERDE: menü ("Nasıl Yazayım") ve Ayarlar
    /// penceresi aynı metni her iki alana yazar; seçim kuralı burada
    /// sabitlenmezse iki arayüz iki farklı üslup gösterebiliyordu.</summary>
    public static string UslupMetni(string kisilik, string gidenKarakterMetni) =>
        string.IsNullOrWhiteSpace(gidenKarakterMetni) ? kisilik : gidenKarakterMetni;
}
