using System.Globalization;
using System.Text;

namespace EkranCeviri.Ceviri;

/// <summary>
/// Metin normalizasyonu ve KALİTE KAPILARI. Buradaki her fonksiyon bir
/// hatanın izidir: model veya makine motoru bozuk çıktı üretiyor, kapı
/// yakalayıp reddediyor. Hepsi saf fonksiyon — I/O yok, durum yok.
/// </summary>
public static class Kalite
{
    /// <summary>
    /// Eşleştirme anahtarı: harf/rakam dışı her şey atılır, küçültülür.
    /// SORU İŞARETİ KORUNUR — "chunnsch morn" ile "chunnsch morn?" aynı
    /// anahtara düşünce soru cümlesine düz cümle çevirisi yapışıyordu.
    /// </summary>
    public static string Anahtarla(string s)
    {
        var soru = s.Contains('?') ? "?" : "";
        var sb = new StringBuilder(s.Length + 1);
        foreach (var k in s)
            if (char.IsLetterOrDigit(k))
                sb.Append(char.ToLowerInvariant(k));
        return sb.Append(soru).ToString();
    }

    /// <summary>
    /// Ücretsiz motorların "yarı çeviri karışımı"nı yakalar: çeviri,
    /// kaynağın kelimelerinin yarısından fazlasını AYNEN içeriyorsa çeviri
    /// sayılmaz. Böyle saçma yamalar basılmaz; orijinal görünür ve Grok
    /// devralır.
    /// </summary>
    public static bool KarisikMi(string kaynak, string ceviri)
    {
        var k = Kelimeler(kaynak);
        if (k.Count < 2) return false;
        var c = Kelimeler(ceviri);
        int ortak = k.Count(c.Contains);
        return (double)ortak / k.Count > 0.5;
    }

    private static HashSet<string> Kelimeler(string s)
    {
        var kume = new HashSet<string>(StringComparer.Ordinal);
        var sb = new StringBuilder();
        foreach (var k in s)
        {
            if (char.IsLetter(k)) sb.Append(char.ToLowerInvariant(k));
            else { if (sb.Length >= 3) kume.Add(sb.ToString()); sb.Clear(); }
        }
        if (sb.Length >= 3) kume.Add(sb.ToString());
        return kume;
    }

    /// <summary>
    /// Makine motorları emojileri sık sık düşürüyor: kaynakta olup çeviride
    /// olmayanlar sona eklenir.
    ///
    /// GRAFEM KÜMESİ kullanılır. Tek tek UTF-16 birimine bakmak modern emoji
    /// dizilerini (❤️ = U+2764 U+FE0F, 👨‍👩‍👧 = ZWJ dizisi) parçalayıp
    /// çeviriye BOZUK kopya ekliyordu ("seni seviyorum ❤️ ❤").
    /// </summary>
    public static string EmojileriKoru(string kaynak, string ceviri)
    {
        // Boş çeviriye emoji eklemek " 😂" gibi sahte çeviriler üretip
        // kalıcı hafızaya yazılıyordu.
        if (string.IsNullOrWhiteSpace(ceviri)) return "";

        var eksik = new StringBuilder();
        var gorulen = new HashSet<string>(StringComparer.Ordinal);
        var sayac = StringInfo.GetTextElementEnumerator(kaynak);
        while (sayac.MoveNext())
        {
            var kume = (string)sayac.Current;
            if (!EmojiMi(kume)) continue;
            if (ceviri.Contains(kume, StringComparison.Ordinal)) continue;
            if (!gorulen.Add(kume)) continue;
            eksik.Append(kume);
        }
        if (eksik.Length == 0) return ceviri;
        return ceviri.Trim() + " " + eksik;
    }

    /// <summary>Grafem kümesi emoji mi? Eşik U+238C: altındaki semboller
    /// (ok, matematik işareti) emoji sayılmaz — onları çeviriye eklemek
    /// metni bozuyordu.</summary>
    private static bool EmojiMi(string grafem)
    {
        foreach (var rune in grafem.EnumerateRunes())
        {
            if (rune.Value <= 0x238C) continue;
            var kategori = Rune.GetUnicodeCategory(rune);
            if (kategori is UnicodeCategory.OtherSymbol
                          or UnicodeCategory.Surrogate
                          or UnicodeCategory.PrivateUse)
                return true;
            // Bölgesel gösterge (bayraklar) ve varyasyon seçiciler
            if (rune.Value is >= 0x1F000 and <= 0x1FAFF) return true;
            if (rune.Value is 0xFE0F or 0x200D) return true;
        }
        return false;
    }

    /// <summary>
    /// Giden mesajı Alfred kurallarına sokar: noktalama silinir, yalnız ilk
    /// harf büyük kalır.
    ///
    /// KRİTİK: bu metin DOĞRUDAN müşteriye gidiyor. Eski sürüm ":" "." ","
    /// "-" işaretlerini rakam komşuluğuna BAKMADAN siliyordu →
    /// "17:30"→"1730", "1,5 saat"→"15 saat", "150.-"→"150". Bu ticari
    /// hatadır. Artık rakama bitişik işaretler KORUNUR (İsviçre "150.-"
    /// fiyat biçimi dahil).
    /// </summary>
    public static string GidenFormatla(string metin)
    {
        var k = metin.ToCharArray();
        var sil = new HashSet<char> { '.', ',', '?', '!', ';', ':', '-',
                                      '_', '"', '\'', '(', ')' };
        var sb = new StringBuilder(k.Length);
        for (int i = 0; i < k.Length; i++)
        {
            char c = k[i];
            if (!sil.Contains(c)) { sb.Append(c); continue; }

            bool oncekiRakam = i > 0 && char.IsNumber(k[i - 1]);
            bool sonrakiRakam = i + 1 < k.Length && char.IsNumber(k[i + 1]);
            // İsviçre fiyat biçimi "150.-": "-" bir NOKTADAN sonra da gelebilir
            bool oncekiNoktaVeRakam = i > 1 && k[i - 1] == '.'
                                            && char.IsNumber(k[i - 2]);
            bool fiyatSonu = (oncekiRakam && (c == '.' || c == '-'))
                          || (c == '-' && oncekiNoktaVeRakam);
            if ((oncekiRakam && sonrakiRakam) || fiyatSonu) sb.Append(c);
        }

        var t = sb.ToString().Trim();
        // Küçültme YALNIZ harflere; rakam blokları dokunulmadan kalır.
        // ToLowerInvariant kullanılır: Türkçe kültürü 'I'→'ı' yapar ve
        // Almanca çıktıyı bozar.
        var kucuk = new StringBuilder(t.Length);
        foreach (var c in t)
            kucuk.Append(char.IsLetter(c) ? char.ToLowerInvariant(c) : c);
        t = kucuk.ToString();

        if (t.Length == 0) return t;
        return char.ToUpperInvariant(t[0]) + t[1..];
    }

    /// <summary>
    /// Erken çıkışlı Levenshtein. OCR aynı balonu iki karede
    /// "trffe"/"träffe" okuyunca önbellek ıskalıyor, mesaj boşuna yeniden
    /// çevriliyordu.
    /// </summary>
    public static bool MesafeAzMi(string a, string b, int enFazla)
    {
        if (a == b) return true;
        if (Math.Abs(a.Length - b.Length) > enFazla) return false;
        if (b.Length == 0) return a.Length <= enFazla;

        var onceki = new int[b.Length + 1];
        var simdiki = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) onceki[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            simdiki[0] = i;
            int satirEnAz = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int bedel = a[i - 1] == b[j - 1] ? 0 : 1;
                simdiki[j] = Math.Min(Math.Min(onceki[j] + 1, simdiki[j - 1] + 1),
                                      onceki[j - 1] + bedel);
                if (simdiki[j] < satirEnAz) satirEnAz = simdiki[j];
            }
            // Erken çıkış: bu satırın en küçüğü bile eşiği aştıysa sonuç
            // kesin aşacaktır.
            if (satirEnAz > enFazla) return false;
            (onceki, simdiki) = (simdiki, onceki);
        }
        return onceki[b.Length] <= enFazla;
    }

    /// <summary>Metindeki rakam dizisi (saat, fiyat, süre). Bunlar
    /// FARKLIYSA iki mesaj asla eşleşmemeli: "30 dk 55.-" ile
    /// "60 dk 85.-" tek karakter farkla eşleşip yanlış çeviri
    /// gösteriyordu.</summary>
    public static string Rakamlari(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s) if (char.IsNumber(c)) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>
    /// GİDEN MESAJ KAPISI: kaynaktaki rakamlar çeviride aynen duruyor mu?
    /// Model sayıları HARFLE yazıyordu ("hundertfüfzg", "sibnähalb") —
    /// fiyat ve saat bozulunca ticari zarar.
    /// Sıra değişebilir (dil yapısı gereği); ÇOKLUK KÜMESİ karşılaştırılır.
    /// </summary>
    public static bool RakamlarKorundu(string kaynak, string ceviri)
    {
        var k = RakamGruplari(kaynak);
        if (k.Count == 0) return true;
        var c = RakamGruplari(ceviri);
        foreach (var (sayi, adet) in k)
            if (!c.TryGetValue(sayi, out var varAdet) || varAdet < adet)
                return false;
        return true;
    }

    private static Dictionary<string, int> RakamGruplari(string s)
    {
        var sonuc = new Dictionary<string, int>(StringComparer.Ordinal);
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (char.IsNumber(c)) sb.Append(c);
            else if (sb.Length > 0) { Ekle(sonuc, sb.ToString()); sb.Clear(); }
        }
        if (sb.Length > 0) Ekle(sonuc, sb.ToString());
        return sonuc;

        static void Ekle(Dictionary<string, int> d, string k) =>
            d[k] = d.TryGetValue(k, out var n) ? n + 1 : 1;
    }

    private static readonly HashSet<string> TurkceKelimeler = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "ve", "bir", "bu", "için", "ile", "ama", "çok", "gibi", "daha",
        "sonra", "şimdi", "değil", "var", "yok", "evet", "hayır", "tamam",
        "merhaba", "selam", "canım", "aşkım", "seni", "beni", "sana", "bana",
        "nasıl", "neden", "nerede", "ne", "kim", "hangi", "olur", "olacak",
        "yapıyorum", "yapıyor", "istiyorum", "istiyor", "geliyorum",
        "gidiyorum", "biliyorum", "seviyorum", "teşekkür", "lütfen",
        "günaydın", "iyi", "kötü", "güzel", "saat", "gün", "bugün", "yarın",
    };

    /// <summary>
    /// GİDEN MESAJ KAPISI: çıktıda Türkçe kalıntı var mı? Model
    /// "Tamam ... canim" gibi karışık çıktı sızdırıyordu — karşı taraf
    /// anlamıyor.
    /// </summary>
    public static bool TurkceKalintiVar(string s)
    {
        // Türkçeye özgü harfler tek başına yeterli kanıt
        foreach (var c in s)
            if (c is 'ı' or 'İ' or 'ş' or 'Ş' or 'ğ' or 'Ğ') return true;

        int sayac = 0;
        foreach (var kelime in Bol(s))
            if (TurkceKelimeler.Contains(kelime) && ++sayac >= 1) return true;
        return false;
    }

    private static readonly HashSet<string> AlmancaKelimeler = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "und", "oder", "aber", "nicht", "ich", "du", "wir", "ihr", "sie",
        "der", "die", "das", "ein", "eine", "einen", "mit", "auch", "noch",
        "schon", "sehr", "wenn", "dann", "weil", "dass", "hast", "habe",
        "haben", "bist", "sind", "kann", "kannst", "will", "willst", "muss",
        "musst", "geht", "gehen", "kommt", "kommen", "machen", "machst",
        "heute", "morgen", "gestern", "immer", "wieder", "danke", "bitte",
        "gut", "schön", "liebe", "mich", "dich", "mir", "dir", "war",
        "wird", "werde", "wurde", "gewesen", "vielleicht", "wirklich",
    };

    /// <summary>
    /// GELEN ÇEVİRİ KAPISI: Türkçe çeviride 2+ Almanca kelime kaldıysa
    /// model o satırı tam çevirememiş — o satır tek tek yeniden çevrilir.
    /// ("Bazı mesajları çevirmedi" şikayetinin çözümü.)
    /// Eşik 2: tek kelime yabancı ad veya alıntı olabilir.
    /// </summary>
    public static bool AlmancaKalintiVar(string ceviri)
    {
        int sayac = 0;
        foreach (var kelime in Bol(ceviri))
            if (AlmancaKelimeler.Contains(kelime) && ++sayac >= 2) return true;
        return false;
    }

    /// <summary>Çeviri kaynakla neredeyse aynıysa hiç çevrilmemiş demektir.
    /// Kısa metinler (emoji, fiyat, "ok") HARİÇ — onlar zaten aynı kalır.
    /// </summary>
    public static bool HicCevrilmemis(string kaynak, string ceviri)
    {
        var k = Anahtarla(kaynak);
        var c = Anahtarla(ceviri);
        if (k.Length < 12) return false;
        return k == c;
    }

    private static IEnumerable<string> Bol(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (char.IsLetter(c)) sb.Append(c);
            else if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
        }
        if (sb.Length > 0) yield return sb.ToString();
    }
}
