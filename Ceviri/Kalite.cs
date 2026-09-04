using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EkranCeviri.Ceviri;

/// <summary>
/// Metin normalizasyonu ve KALİTE KAPILARI. Buradaki her fonksiyon bir
/// hatanın izidir: model veya makine motoru bozuk çıktı üretiyor, kapı
/// yakalayıp reddediyor. Hepsi saf fonksiyon — I/O yok, durum yok.
/// </summary>
public static partial class Kalite
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
        // BOŞ DİZGE KORUMASI (Mac'te 1...0 aralığı süreci ÇÖKERTİYORDU).
        // Buraya bugün ulaşılamıyor (çağıranlar uzunluk kapısı koyuyor) ama
        // tek bir yeni çağıran uygulamayı düşürebilirdi.
        if (a.Length == 0 || b.Length == 0)
            return Math.Max(a.Length, b.Length) <= enFazla;

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

    /// <summary>Metindeki rakamların ÇOKLUK KÜMESİ (hangi rakamdan kaç
    /// tane). Sıraya ve biçime duyarsızdır.</summary>
    public static Dictionary<char, int> RakamCoklugu(string s)
    {
        var m = new Dictionary<char, int>();
        foreach (var k in s)
            if (char.IsNumber(k)) m[k] = m.TryGetValue(k, out var n) ? n + 1 : 1;
        return m;
    }

    /// <summary>Bitişik rakam öbekleri ("saat 17:30 · 150.-" → ["17","30","150"]).</summary>
    public static List<string> RakamObekleri(string s)
    {
        var sonuc = new List<string>();
        var obek = new StringBuilder();
        foreach (var k in s)
        {
            if (char.IsNumber(k)) obek.Append(k);
            else if (obek.Length > 0) { sonuc.Add(obek.ToString()); obek.Clear(); }
        }
        if (obek.Length > 0) sonuc.Add(obek.ToString());
        return sonuc;
    }

    /// <summary>
    /// GİDEN MESAJ SAYI KAPISI — iki katmanlı. null = sayılar korunmuş.
    /// Eskiden tüm rakamlar TEK dizgede, SIRAYLA karşılaştırılıyordu; Almanca
    /// kelime sırası Türkçeden farklı olduğu için "150 frank yarın 17:30" →
    /// "morgen um 17:30 für 150 franken" meşru çevirisi REDDEDİLİYORDU.
    /// Şimdi iki katman:
    ///  1) rakam ÇOKLUĞU eşit olmalı — kaybolan/uydurulan sayı yakalanır; sıra
    ///     ve biçim serbest (telefon "079 123 45 67" ↔ "0791234567").
    ///  2) kaynaktaki 2+ haneli her öbek, çıktının rakamları içinde AYNEN
    ///     geçmeli — "12" → "21" gibi rakam karıştırması burada takılır.
    /// Model sayıları HARFLE de yazıyordu ("hundertfüfzg") — fiyat ve saat
    /// bozulunca ticari zarar; o da 1. katmana takılır.
    /// </summary>
    public static string? SayilarKorunduMu(string kaynak, string cikti)
    {
        var ka = RakamCoklugu(kaynak);
        var ca = RakamCoklugu(cikti);
        bool esit = ka.Count == ca.Count
                    && ka.All(p => ca.TryGetValue(p.Key, out var n) && n == p.Value);
        if (!esit)
        {
            var eksik = string.Concat(ka.Where(p => (ca.TryGetValue(p.Key, out var n) ? n : 0) < p.Value)
                                        .Select(p => p.Key).Order());
            var fazla = string.Concat(ca.Where(p => (ka.TryGetValue(p.Key, out var n) ? n : 0) < p.Value)
                                        .Select(p => p.Key).Order());
            var ayrinti = new List<string>();
            if (eksik.Length > 0) ayrinti.Add($"kayıp rakam: {eksik}");
            if (fazla.Length > 0) ayrinti.Add($"uydurulan rakam: {fazla}");
            return string.Join(" · ", ayrinti);
        }
        var ciktiDijit = Rakamlari(cikti);
        foreach (var obek in RakamObekleri(kaynak))
            if (obek.Length >= 2 && !ciktiDijit.Contains(obek, StringComparison.Ordinal))
                return $"sayı değişti: {obek} çıktıda böyle geçmiyor";
        return null;
    }

    /// <summary>Mevcut çağıranlar için bool biçimi: sayı kapısı sorun bulmadı mı?</summary>
    public static bool RakamlarKorundu(string kaynak, string ceviri) =>
        SayilarKorunduMu(kaynak, ceviri) is null;

    /// <summary>Türkçe kalıntı işaretleri: Mac kümesi ∪ Windows kümesi.
    /// "ne" ve "saat" ÇIKARILDI — Almanca "ne?" (nicht wahr) ve "Saat"
    /// (tohum) ile çakışıp meşru çeviriyi reddettiriyordu.</summary>
    private static readonly HashSet<string> TurkceKelimeler = new(
        StringComparer.OrdinalIgnoreCase)
    {
        // Mac kümesi (ölçülen sızıntılar: model "tamam/canım"ı bırakıyor)
        "tamam", "canim", "canım", "seni", "sen", "ben", "icin", "için",
        "cok", "çok", "gorusuruz", "görüşürüz", "evet", "hayir", "hayır",
        "ama", "simdi", "şimdi", "lazim", "lazım", "olur", "tabii",
        "merhaba", "selam", "tesekkur", "teşekkür", "biraz", "sonra",
        "yapacagim", "yapacağım", "bugun", "bugün", "yarin", "yarın",
        // Windows kümesi
        "ve", "bir", "bu", "ile", "gibi", "daha", "değil", "var", "yok",
        "aşkım", "beni", "sana", "bana", "nasıl", "neden", "nerede", "kim",
        "hangi", "olacak", "yapıyorum", "yapıyor", "istiyorum", "istiyor",
        "geliyorum", "gidiyorum", "biliyorum", "seviyorum", "lütfen",
        "günaydın", "iyi", "kötü", "güzel", "gün",
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

    /// <summary>
    /// Metin bütünüyle Türkçe gibi mi? Hafıza savunması için: hedef dil
    /// Türkçe DEĞİLKEN Türkçe bir çeviri o dilin anahtarına yazılmamalı
    /// (uçuş ortasında dil değişimi). <see cref="TurkceKalintiVar"/> tek
    /// kelimeyle tetiklenir — İspanyolca "ama", "ve" gibi meşru kelimeler
    /// yabancı dilde de geçer; burada Türkçeye özgü harf YA DA en az iki
    /// Türkçe kelime aranır.
    /// </summary>
    public static bool TurkceMetinGibi(string s)
    {
        foreach (var c in s)
            if (c is 'ı' or 'İ' or 'ş' or 'Ş' or 'ğ' or 'Ğ') return true;
        int sayac = 0;
        foreach (var kelime in Bol(s))
            if (TurkceKelimeler.Contains(kelime) && ++sayac >= 2) return true;
        return false;
    }

    /// <summary>Almanca/lehçe kalıntı işaretleri: Mac `almancaIsaretler` ∪
    /// Windows kümesi. Çeviride bunlar kaldıysa çeviri EKSİKTİR (kullanıcı
    /// şikayeti: "bazen tam çeviremiyor").</summary>
    private static readonly HashSet<string> AlmancaKelimeler = new(
        StringComparer.OrdinalIgnoreCase)
    {
        // Mac kümesi
        "ich", "isch", "ist", "nicht", "nöd", "nid", "nit", "und", "aber",
        "der", "die", "das", "mit", "für", "auch", "noch", "schon", "wenn",
        "mues", "muss", "chli", "gsi", "hesch", "chunnsch", "morn", "hüt",
        "zit", "zyt", "wärche", "schaffe", "gäll", "eus", "mir", "dir",
        "vill", "viel", "geht", "gaht", "kommt", "chunnt", "machen", "mache",
        // Windows kümesi
        "oder", "du", "wir", "ihr", "sie", "ein", "eine", "einen", "sehr",
        "dann", "weil", "dass", "hast", "habe", "haben", "bist", "sind",
        "kann", "kannst", "will", "willst", "musst", "gehen", "kommen",
        "machst", "heute", "morgen", "gestern", "immer", "wieder", "danke",
        "bitte", "gut", "schön", "liebe", "mich", "dich", "war", "wird",
        "werde", "wurde", "gewesen", "vielleicht", "wirklich",
    };

    /// <summary>
    /// GELEN ÇEVİRİ KAPISI: Türkçe çeviride Almanca kelime kaldıysa model o
    /// satırı tam çevirememiş — o satır tek tek yeniden çevrilir. ("Bazı
    /// mesajları çevirmedi" şikayetinin çözümü.) Kural (Mac): en az 2 kelime
    /// ve (2+ Almanca kelime YA DA kelimelerin üçte biri Almanca). Tek
    /// kelime yabancı ad veya alıntı olabilir; kısa cümlede tek Almanca
    /// kelime ise oran kapısına takılır.
    /// </summary>
    public static bool AlmancaKalintiVar(string ceviri)
    {
        int kelime = 0, kalinti = 0;
        foreach (var k in Bol(ceviri))
        {
            kelime++;
            if (AlmancaKelimeler.Contains(k)) kalinti++;
        }
        if (kelime < 2) return false;
        return kalinti >= 2 || (kalinti >= 1 && kalinti * 3 >= kelime);
    }

    /// <summary>Çeviri kaynakla neredeyse aynıysa model HİÇ çevirmemiştir.
    /// Kısa anahtarlar (emoji, fiyat, "ok") HARİÇ — onlar zaten aynı kalır.
    /// Tam eşitlik yetmez: noktalama/OCR farkı bir karakter oynatınca yankı
    /// çeviri sanılıyordu — anahtarın onda biri kadar mesafe de yankıdır.</summary>
    public static bool HicCevrilmemis(string kaynak, string ceviri)
    {
        var k = Anahtarla(kaynak);
        var c = Anahtarla(ceviri);
        if (k.Length < 6) return false;
        if (k == c) return true;
        return MesafeAzMi(k, c, Math.Max(1, k.Length / 10));
    }

    /// <summary>
    /// Makine motoru metni AYNEN (ya da yarı karışık) geri verdi mi? Motora
    /// STANDARTLAŞTIRILMIŞ metin gittiği için yankı hem ham metinle hem de
    /// standart metinle kıyaslanır: "Kommst du morgen auch in die Stadt"
    /// dönen Bing çıktısı ham "Chunnsch du morn…" anahtarıyla eşleşmeyip
    /// çeviri sayılıyor, ekranda Almanca kalıyordu.
    /// </summary>
    public static bool YankiMi(string ceviri, string kaynak, string makineMetni)
    {
        var ck = Anahtarla(ceviri);
        if (ck == Anahtarla(kaynak) || ck == Anahtarla(makineMetni)) return true;
        if (HicCevrilmemis(makineMetni, ceviri)) return true;
        return KarisikMi(kaynak, ceviri) || KarisikMi(makineMetni, ceviri);
    }

    [GeneratedRegex(@"https?://[^\s]+|www\.[^\s]+|[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}|[@#][A-Za-z0-9._-]+")]
    private static partial Regex BaglantiDeseni();

    /// <summary>
    /// (b) SINIFI — KAYNAKTA ÇEVRİLECEK BİR ŞEY YOK.
    ///
    /// "motor çevirmedi" iki AYRI olayı aynı kefeye koyuyordu:
    ///  (a) motor gerçekten başarısız oldu (ağ, boş yanıt, kesik yanıt)
    ///      → tekrar denemek DOĞRU;
    ///  (b) motor metni BİLEREK değiştirmeden döndürdü — metin zaten hedef
    ///      dilde, ya da yalnız bağlantı/sayı/emoji taşıyor → bu bir
    ///      BAŞARISIZLIK DEĞİL, doğru sonuçtur. Tekrar denemek her seferinde
    ///      aynı yanıtı ve aynı FATURAYI üretir.
    ///
    /// ÖLÇÜM (kullanıcının 24 saatlik arıza günlüğü): 78 "motor çevirmedi"
    /// satırının TAMAMI ücretli Grok'tan geldi ama yalnız 17 benzersiz karma
    /// vardı; aynı karma 14/10/10/9 kez yeniden gönderildi ve iki modelde de
    /// AYNI sonucu verdi. Belirlenimci sonuç = (b). 61 çağrı boşa gitti.
    ///
    /// KASITLI OLARAK DAR TUTULDU: "özel isim" gibi DİLBİLİMSEL TAHMİNLER
    /// buraya girmez. Tek büyük harfli bir Almanca kelimeyi ("Danke")
    /// yanlışlıkla (b) saymak o mesajı sonsuza dek çevrilmemiş bırakırdı.
    /// Tahmin gerektiren her şey TekrarDefteri tavanına düşer.
    /// </summary>
    public static bool CevrilecekSeyYokMu(string kaynak, string hedefDil = "tr")
    {
        // 1) Bağlantı / e-posta / @kullanıcı / #etiket atıldıktan sonra geriye
        //    HİÇ HARF kalmıyorsa çevrilecek bir şey yoktur (sayı, saat, emoji).
        var kalan = BaglantiDeseni().Replace(kaynak, " ");
        if (!kalan.Any(char.IsLetter)) return true;
        // 2) Metin ZATEN hedef dilde: hedef dil işareti VAR, kaynak dil işareti
        //    YOK. (Kullanıcının kendi Türkçe mesajları da hedef bloktur ve her
        //    turda ücretli motora gidiyordu.) Karışık metin AlmancaKalintiVar
        //    ile elenir, yani "yarısı Almanca" olan mesaj (b) sayılmaz.
        if (hedefDil == "tr" && TurkceKalintiVar(kalan) && !AlmancaKalintiVar(kalan))
            return true;
        return false;
    }

    /// <summary>Kalite turu nüans eşiği: bundan kısa kaynaklarda kalite
    /// modeli ölçülebilir bir fark üretmiyor ("tamam", "yarın 17:30").</summary>
    public const int KaliteNuansEsigi = 25;

    /// <summary>
    /// İSABET KAPISI — ücretli kalite çağrısı yalnız fark yaratabilecek
    /// bloklara. Ölçüldü: 7 kalite çağrısının yalnız 3'ü ekranda bir şey
    /// değiştirdi. <paramref name="anahtar"/> verilirse tekrar tavanı da
    /// burada uygulanır: 24 saatte ölçülen 61 israf çağrısının 59'u tam
    /// olarak bu kapıdan geçmişti.
    /// </summary>
    public static bool KaliteAdayiMi(string anahtar, string kaynak, string? ceviri,
                                     string hedefDil = "tr")
    {
        // VAZGEÇİLEN ANAHTAR: (b) işareti ya da tekrar tavanı → ücretli çağrı YOK.
        if (anahtar.Length > 0 && TekrarDefteri.Paylasilan.VazgecildiMi(anahtar))
            return false;
        // (b): çevrilecek bir şey yoksa "yankı" bir kusur değil, DOĞRU sonuçtur.
        if (CevrilecekSeyYokMu(kaynak, hedefDil)) return false;
        if (string.IsNullOrEmpty(ceviri)) return true;
        if (AlmancaKalintiVar(ceviri)) return true;
        if (YankiMi(ceviri, kaynak, kaynak)) return true;
        return kaynak.Length >= KaliteNuansEsigi;
    }

    /// <summary>Modele giden etiketli zarfın SINIR dizgileri. Ekrandan/geçmişten
    /// gelen güvenilmez metin bu zarflara KAÇIŞSIZ konuyordu.</summary>
    [GeneratedRegex(@"</?\s*(?:tarz_ornekleri|cevrilecek|gecmis|sohbet)\b[^>]*>",
                    RegexOptions.IgnoreCase)]
    private static partial Regex ZarfSinirDeseni();

    /// <summary>
    /// Güvenilmez metni etiketli zarfa koymadan önce sınır taklidini
    /// etkisizleştir. Sohbete gömülü "&lt;/cevrilecek&gt;" gibi bir dizge
    /// zarfı erken kapatıp modele kendi yönergesini geçirebiliyordu.
    /// DAVRANIŞ KORUYUCU: yalnız sınırı taklit eden diziler değişir; normal
    /// metin, içindeki tekil "&lt;" "&gt;" dahil, aynen kalır.
    /// </summary>
    public static string ZarfaGuvenli(string metin) =>
        ZarfSinirDeseni().Replace(metin, m => m.Value.Replace('<', '‹').Replace('>', '›'));

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
