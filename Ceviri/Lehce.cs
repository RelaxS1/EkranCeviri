using System.Text;
using System.Text.RegularExpressions;

namespace EkranCeviri.Ceviri;

/// <summary>
/// Lehçe algılama ve ön-normalizasyon.
///
/// İsviçre'de tek bir "İsviçre Almancası" yoktur: Zürih, Bern, Basel, Doğu
/// İsviçre ve Wallis belirgin şekilde farklı yazılır. Hangi lehçe
/// konuşuluyorsa hem GELEN çeviri hem GİDEN mesaj ona göre yapılır —
/// karşı taraf kendi ağzından cevap alsın diye.
/// </summary>
public static class Lehce
{
    private sealed record Tanim(string Ad, string Kisa,
                                string[] Isaretler, string[] Guclu);

    /// <summary>Zayıf işaret: karar için 2 tane gerekir.
    /// GÜÇLÜ işaret: yalnız o bölgede kullanılır, tek başına karar verdirir.
    /// ("nich/wat/u/ds" gibi gündelik Almanca kelimeler zayıf listede
    /// bulunduğunda tek eşleşmeyle yanlış lehçeye kayılıyordu.)</summary>
    private static readonly Tanim[] Isvicre =
    [
        new("Zürih İsviçre Almancası (Züridütsch)", "Züridütsch",
            ["nöd", "chli", "gaht", "gahts", "ez", "züri", "chunnsch",
             "öppis", "znacht", "sächsi", "zäme", "wott", "morn",
             "dänk", "hoi", "grüezi", "gseh", "hüt"],
            // "chli" GÜÇLÜ DEĞİL: Bern'de de kullanılıyor. Güçlü listede
            // olduğu sürece Bern yazan biri Zürih sanılıyor ve ona Zürih
            // lehçesinde cevap yazılıyordu (testte yakalandı).
            ["nöd", "gaht", "gahts", "ez", "chunnsch", "züri"]),

        new("Bern İsviçre Almancası (Bärndütsch)", "Bärndütsch",
            ["gäng", "itz", "wärche", "öppe", "müntschi", "gäbig",
             "hüür", "bärn", "chuum", "nid", "gwüss", "sträng", "gouf"],
            // Bern'e ÖZGÜ: Zürih "immer/jetzt/arbeiten" için
            // "immer/ez/schaffe" der, Bern "gäng/itz/wärche".
            ["gäng", "itz", "wärche", "müntschi", "gäbig", "bärn"]),

        new("Basel İsviçre Almancası (Baseldytsch)", "Baseldytsch",
            ["drämmli", "aifach", "rhy", "vyl", "zyt", "dry", "nit",
             "hänn", "basel", "yych", "glaubs", "bebbi"],
            ["drämmli", "baseldytsch", "bebbi", "basel"]),

        new("Doğu İsviçre Almancası (Ostschwyzerdütsch)", "Ostschwyz",
            ["ond", "hend", "gsii", "appezell", "sanggale", "khönd", "hoscht"],
            ["appezell", "sanggale", "hoscht"]),

        new("Wallis/İç İsviçre Almancası (Wallisertitsch)", "Wallis",
            ["ischt", "wier", "üsch", "iisch", "wallis", "briägglu",
             "chunt", "zbäärg"],
            ["ischt", "wier", "üsch", "wallis", "briägglu"]),
    ];

    /// <summary>Almanya/Avusturya bölgesel varyantları (Alman modu bunları
    /// da tanır).</summary>
    private static readonly Tanim[] Almanya =
    [
        new("Bavyera/Avusturya Almancası", "Bayrisch",
            ["servus", "oida", "ned", "mog", "hoid", "griaß", "bussi",
             "schmarrn", "wurscht", "dahoam", "gscheit"],
            ["servus", "oida", "griaß", "dahoam", "schmarrn"]),

        new("Kuzey Almanya Almancası", "Norddeutsch",
            ["moin", "büddel", "schnacken", "lütt", "tschüssing",
             "achtern", "büx"],
            ["moin", "schnacken", "tschüssing", "büddel"]),

        new("Ren/Köln Almancası", "Rheinisch",
            ["alaaf", "kölle", "jot", "ejal", "pittermännchen"],
            ["alaaf", "kölle", "pittermännchen"]),
    ];

    private static readonly HashSet<string> IsvicreGenel = new(StringComparer.Ordinal)
    {
        "isch", "gsi", "gsii", "hesch", "häsch", "chan", "cha", "chasch",
        "chum", "chume", "chunnt", "mues", "muess", "nöd", "nid", "nit",
        "öppis", "öpper", "hüt", "morn", "zäme", "guet", "gäll", "gell",
        "merci", "schaffe", "lueg", "vilmal", "grüezi", "hoi", "sali",
        "znacht", "zmorge", "chind", "lüt",
    };

    /// <summary>
    /// Ekrandaki sohbetten hangi lehçenin konuşulduğunu çıkarır.
    /// Algılama TÜM görünür sohbetten yapılır — yalnız yeni mesajlara
    /// bakmak "belirsiz" sonucu veriyordu.
    /// </summary>
    public static (string Ad, string Kisa) Algila(IEnumerable<string> metinler)
    {
        var kelimeler = new HashSet<string>(StringComparer.Ordinal);
        var sb = new StringBuilder();
        foreach (var m in metinler)
        {
            foreach (var c in m)
            {
                if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
                else { if (sb.Length > 0) { kelimeler.Add(sb.ToString()); sb.Clear(); } }
            }
            if (sb.Length > 0) { kelimeler.Add(sb.ToString()); sb.Clear(); }
        }

        // GÜÇLÜ işaret tek başına karar verdirir
        foreach (var l in Isvicre)
            if (l.Guclu.Any(kelimeler.Contains)) return (l.Ad, l.Kisa);
        foreach (var l in Almanya)
            if (l.Guclu.Any(kelimeler.Contains)) return (l.Ad, l.Kisa);

        Tanim? enIyi = null; int enIyiPuan = 0;
        foreach (var l in Isvicre)
        {
            int puan = l.Isaretler.Count(kelimeler.Contains);
            if (puan > enIyiPuan) { enIyi = l; enIyiPuan = puan; }
        }

        bool isvicreli = kelimeler.Overlaps(IsvicreGenel);

        // Almanya varyantları yalnız İsviçre işareti YOKSA değerlendirilir
        if (!isvicreli)
        {
            Tanim? enIyiDE = null; int puanDE = 0;
            foreach (var l in Almanya)
            {
                int puan = l.Isaretler.Count(kelimeler.Contains);
                if (puan > puanDE) { enIyiDE = l; puanDE = puan; }
            }
            if (enIyiDE is not null && puanDE >= 2) return (enIyiDE.Ad, enIyiDE.Kisa);
        }

        // Tek belirteçle lehçe kararı vermek yanlış varyanta kaydırıyordu
        // (Hochdeutsch yazan müşteriye sahte Plattdeutsch cevap).
        if (enIyi is not null && enIyiPuan >= 2) return (enIyi.Ad, enIyi.Kisa);

        if (isvicreli)
        {
            // Zürih en yaygın lehçe; tek işaret yeterli sayılır
            if (enIyi is not null && enIyiPuan == 1 && enIyi.Kisa == "Züridütsch")
                return (enIyi.Ad, enIyi.Kisa);
            return ("İsviçre Almancası (bölge belirsiz)", "İsviçre Almancası");
        }
        return ("Standart Almanca (Hochdeutsch)", "Hochdeutsch");
    }

    // ---------------- ön-normalizasyon (YALNIZ makine motorları) ----------------
    // KANIT: Bing/Google Zürih lehçesini ya hiç çeviremiyor ya da TERS anlam
    // üretiyor ("bim Bahnhof" → "istasyonun yarısı geldi"). Aynı cümleler
    // standart Almancaya çevrilip verildiğinde kusursuz çıkıyor.
    // Grok'a HAM metin gider — o lehçeyi zaten biliyor.

    private static readonly (string Kalip, string Karsilik)[] Kaliplar =
    [
        // Saatler AÇIK yazılır: Bing "halb sechs"i düzenli olarak "altı buçuk"
        // diye yanlış çeviriyor (doğrusu beş buçuk).
        ("halbi sächsi", "fünf uhr dreissig"), ("halbi sibni", "sechs uhr dreissig"),
        ("halbi achti", "sieben uhr dreissig"), ("halbi nüni", "acht uhr dreissig"),
        ("halbi zähni", "neun uhr dreissig"), ("halbi elfi", "zehn uhr dreissig"),
        ("halb sechs", "fünf uhr dreissig"), ("halb sieben", "sechs uhr dreissig"),
        ("halb acht", "sieben uhr dreissig"), ("halb neun", "acht uhr dreissig"),
        ("merci vilmal", "vielen dank"),
        ("uf widerluege", "auf wiedersehen"), ("uf wiederluege", "auf wiedersehen"),
        ("es bitzeli", "ein bisschen"), ("es bitzli", "ein bisschen"),
        ("e chli", "ein bisschen"), ("ich bi", "ich bin"), ("i bi", "ich bin"),
        ("wie gahts", "wie geht es"), ("wie gohts", "wie geht es"),
        ("was machsch", "was machst du"),
        ("bis spöter", "bis später"), ("bis dänn", "bis dann"),
        ("guete morge", "guten morgen"), ("guete abig", "guten abend"),
        ("schöne abig", "schönen abend"), ("gueti nacht", "gute nacht"),
        ("hoi zäme", "hallo zusammen"), ("ha di gärn", "habe dich gern"),
        ("han di gärn", "habe dich gern"), ("hesch zit", "hast du zeit"),
        ("weisch das", "weisst du das"), ("weisch no", "weisst du noch"),
        ("gseh mer eus", "sehen wir uns"),
    ];

    private static readonly Dictionary<string, string> Sozluk = new(StringComparer.Ordinal)
    {
        // selamlama
        ["hoi"] = "hallo", ["sali"] = "hallo", ["salü"] = "hallo",
        ["grüezi"] = "guten tag", ["grüessech"] = "guten tag",
        ["tschau"] = "tschüss", ["merci"] = "danke",
        // olmak / sahip olmak
        ["isch"] = "ist", ["bisch"] = "bist", ["gsi"] = "gewesen",
        ["gsii"] = "gewesen", ["gsin"] = "gewesen", ["ha"] = "habe",
        ["han"] = "habe", ["hesch"] = "hast", ["häsch"] = "hast",
        ["hät"] = "hat", ["het"] = "hat", ["händ"] = "haben", ["hend"] = "haben",
        // kipler
        ["chan"] = "kann", ["cha"] = "kann", ["chasch"] = "kannst",
        ["chönd"] = "können", ["chönne"] = "können", ["chönnt"] = "könnt",
        ["muess"] = "muss", ["muesch"] = "musst", ["müend"] = "müssen",
        ["müesst"] = "müsst", ["söll"] = "soll", ["sött"] = "sollte",
        ["sötti"] = "sollte", ["söttsch"] = "solltest", ["wott"] = "will",
        ["wotsch"] = "willst", ["wänd"] = "wollen", ["wend"] = "wollen",
        ["dörf"] = "darf", ["dörfsch"] = "darfst",
        // gelmek / gitmek
        ["chum"] = "komme", ["chume"] = "komme", ["chunnsch"] = "kommst",
        ["chunnt"] = "kommt", ["chömed"] = "kommen", ["chömmed"] = "kommen",
        ["cho"] = "kommen", ["kho"] = "kommen", ["choo"] = "kommen",
        ["gang"] = "gehe", ["gasch"] = "gehst", ["gaht"] = "geht",
        ["goht"] = "geht", ["gönd"] = "gehen", ["gah"] = "gehen", ["goh"] = "gehen",
        // diğer fiiller
        ["machsch"] = "machst", ["mached"] = "machen", ["gseh"] = "sehen",
        ["gsehsch"] = "siehst", ["gseht"] = "sieht", ["weisch"] = "weisst",
        ["schaffe"] = "arbeiten", ["schaffsch"] = "arbeitest",
        ["schaffed"] = "arbeiten", ["luege"] = "schauen", ["luegsch"] = "schaust",
        ["lueg"] = "schau", ["träffe"] = "treffen", ["trüffe"] = "treffen",
        ["verzell"] = "erzähl", ["verzellsch"] = "erzählst",
        ["bruch"] = "brauche", ["bruuch"] = "brauche", ["bruchsch"] = "brauchst",
        ["nimmsch"] = "nimmst", ["gisch"] = "gibst", ["bliib"] = "bleibe",
        ["bliibsch"] = "bleibst", ["schriib"] = "schreibe",
        ["schriibsch"] = "schreibst",
        // zaman
        ["hüt"] = "heute", ["morn"] = "morgen", ["geschter"] = "gestern",
        ["jetz"] = "jetzt", ["spöter"] = "später", ["früeh"] = "früh",
        ["znacht"] = "abendessen", ["zmittag"] = "mittagessen",
        ["zmorge"] = "frühstück", ["abig"] = "abend", ["morge"] = "morgen",
        ["wuche"] = "woche", ["johr"] = "jahr", ["stund"] = "stunde",
        ["zit"] = "zeit", ["ziit"] = "zeit",
        // olumsuzluk / edatlar
        ["nöd"] = "nicht", ["nid"] = "nicht", ["nit"] = "nicht",
        ["nüt"] = "nichts", ["niemer"] = "niemand", ["gäll"] = "nicht wahr",
        ["gell"] = "nicht wahr", ["äbe"] = "eben", ["au"] = "auch",
        ["scho"] = "schon", ["grad"] = "gerade", ["nomol"] = "nochmal",
        ["no"] = "noch", ["wuchenänd"] = "wochenende", ["wucheend"] = "wochenende",
        ["gits"] = "gibt es", ["hets"] = "hat es", ["nomal"] = "nochmal",
        ["vilicht"] = "vielleicht", ["villicht"] = "vielleicht",
        ["wörkli"] = "wirklich", ["würkli"] = "wirklich", ["wüki"] = "wirklich",
        ["öppis"] = "etwas", ["öpper"] = "jemand", ["öppedie"] = "manchmal",
        ["chli"] = "bisschen", ["chlii"] = "bisschen", ["bitzli"] = "bisschen",
        ["zäme"] = "zusammen", ["allei"] = "allein",
        // sıfat / isim
        ["guet"] = "gut", ["guät"] = "gut", ["schlächt"] = "schlecht",
        ["schöni"] = "schöne", ["lüt"] = "leute", ["chind"] = "kind",
        ["chinder"] = "kinder", ["fründ"] = "freund", ["fründin"] = "freundin",
        ["huus"] = "haus", ["schuel"] = "schule", ["arbet"] = "arbeit",
        ["wätter"] = "wetter", ["gäld"] = "geld",
        // edat / zamir
        ["id"] = "in die", ["uf"] = "auf", ["ufem"] = "auf dem",
        ["bim"] = "beim", ["bi"] = "bei", ["vo"] = "von", ["dä"] = "der",
        ["mer"] = "wir", ["eus"] = "uns", ["öi"] = "euch", ["ihne"] = "ihnen",
        ["welchi"] = "welche", ["weles"] = "welches", ["wievil"] = "wieviel",
        ["jaa"] = "ja", ["nei"] = "nein", ["nöi"] = "nein", ["gärn"] = "gern",
        // saatler
        ["sächsi"] = "sechs", ["sibni"] = "sieben", ["achti"] = "acht",
        ["nüni"] = "neun", ["zähni"] = "zehn", ["elfi"] = "elf",
        ["zwölfi"] = "zwölf", ["füfi"] = "fünf", ["vieri"] = "vier",
        ["drüü"] = "drei", ["zwoi"] = "zwei",
        // WhatsApp kısaltmaları
        ["wrsch"] = "wahrscheinlich", ["vlt"] = "vielleicht",
        ["hdl"] = "hab dich lieb", ["lg"] = "liebe grüsse", ["gn8"] = "gute nacht",
    };

    /// <summary>Kalıplar uzundan kısaya sıralı — kısa kalıp uzun olanın
    /// içini yiyip anlamı bozuyordu.</summary>
    private static readonly (Regex Desen, string Karsilik)[] DerlenmisKaliplar =
        Kaliplar.OrderByDescending(k => k.Kalip.Length)
                .Select(k => (
                    new Regex(@"(?<!\p{L})" + Regex.Escape(k.Kalip) + @"(?!\p{L})",
                              RegexOptions.IgnoreCase | RegexOptions.Compiled),
                    k.Karsilik))
                .ToArray();

    /// <summary>Lehçeyi standart Almancaya çevirir. YALNIZ makine
    /// motorlarına giden metne uygulanır.</summary>
    public static string Standartlastir(string metin)
    {
        var t = metin;

        // 1) çok kelimeli kalıplar. KELİME SINIRI ŞART: "ich bi" kalıbı
        // "ich bin müde" içinde eşleşip "ich binn müde" üretiyordu.
        foreach (var (desen, karsilik) in DerlenmisKaliplar)
        {
            t = desen.Replace(t, es =>
                es.Value.Length > 0 && char.IsUpper(es.Value[0])
                    ? char.ToUpperInvariant(karsilik[0]) + karsilik[1..]
                    : karsilik);
        }

        // 2) kelime bazlı sözlük (harf olmayanlar korunur)
        var sonuc = new StringBuilder(t.Length + 16);
        var kelime = new StringBuilder();
        foreach (var c in t)
        {
            if (char.IsLetter(c)) kelime.Append(c);
            else { Bosalt(); sonuc.Append(c); }
        }
        Bosalt();
        return sonuc.ToString();

        void Bosalt()
        {
            if (kelime.Length == 0) return;
            var ham = kelime.ToString();
            kelime.Clear();
            if (Sozluk.TryGetValue(ham.ToLowerInvariant(), out var karsilik))
                sonuc.Append(char.IsUpper(ham[0])
                    ? char.ToUpperInvariant(karsilik[0]) + karsilik[1..]
                    : karsilik);
            else sonuc.Append(ham);
        }
    }

    /// <summary>
    /// Giden mesaj için few-shot örnek. Aynı Türkçe cümlenin varyanta göre
    /// nasıl değiştiğini modele GÖSTERMEK, tarif etmekten çok daha iyi
    /// sonuç veriyor (ölçüldü). Hochdeutsch/Bayrisch için Zürih örneği
    /// göstermek modeli yanlış varyanta itiyordu — her varyantın kendi
    /// örneği var. Örnekler Mac sürümünde ölçülmüş olanlardır; Windows'taki
    /// "ich has dr gseh gfehlt" uydurmaydı. Ostschwyz/Rheinisch için Mac'te
    /// örnek yok; Windows'takiler BİREBİR duruyor (madde öneki de eklenmedi —
    /// ölçülmemiş gövdeye biçim bile dokunulmasın).
    /// </summary>
    public static string GidenOrnek(string lehceKisa) => lehceKisa switch
    {
        "Bärndütsch" =>
            "- \"tamam görüşürüz\" → \"guet bis spöter\"\n"
          + "- \"müsaitim\" → \"i ha ziit\"\n"
          + "- \"biraz çalışmam lazım\" → \"i mues no chli wärche\"",
        "Baseldytsch" =>
            "- \"tamam görüşürüz\" → \"guet bis spöter\"\n"
          + "- \"müsaitim\" → \"y ha zyt\"\n"
          + "- \"biraz çalışmam lazım\" → \"y mues no e bitz schaffe\"",
        "Wallis" =>
            "- \"tamam görüşürüz\" → \"guet bis spääter\"\n"
          + "- \"müsaitim\" → \"ich ha ziit\"",
        "Hochdeutsch" =>
            "- \"tamam görüşürüz\" → \"okay bis später\"\n"
          + "- \"müsaitim\" → \"ich hab zeit\"\n"
          + "- \"biraz çalışmam lazım\" → \"ich muss noch bisschen arbeiten\"",
        "Bayrisch" =>
            "- \"tamam görüşürüz\" → \"passt, bis später\"\n"
          + "- \"müsaitim\" → \"i hob zeit\"\n"
          + "- \"biraz çalışmam lazım\" → \"i muass no a bissl schaffn\"",
        "Norddeutsch" =>
            "- \"tamam görüşürüz\" → \"jo bis später\"\n"
          + "- \"müsaitim\" → \"ich hab zeit\"\n"
          + "- \"biraz çalışmam lazım\" → \"muss noch n bisschen schaffen\"",
        "Ostschwyz" =>
            "\"biraz daha çalışmam lazım\" → \"i mues no es bitzli schaffe\"\n"
          + "\"ve\" → \"ond\"  ·  \"var\" → \"hend\"",
        "Rheinisch" =>
            "\"şu\" → \"dat\"  ·  \"ne\" → \"wat\"\n"
          + "\"biraz daha çalışmam lazım\" → \"ich muss noch e bessje schaffe\"",
        // Züridütsch ve genel İsviçre
        _ =>
            "- \"tamam görüşürüz\" → \"okey bis spöter\"\n"
          + "- \"müsaitim\" → \"ich ha zit\"\n"
          + "- \"biraz çalışmam lazım\" → \"ich mues no chli schaffe\"",
    };

    /// <summary>Grok istemine giren lehçeler-arası fark tablosu. Modelin
    /// lehçeleri BİRBİRİNE KARIŞTIRMASINI önler.</summary>
    public static string LehceFarkTablosu() =>
        "anlam        | Züridütsch | Bärndütsch | Baseldytsch | Bayrisch\n"
      + "değil        | nöd        | nid        | nit         | ned\n"
      + "biraz        | chli       | chli       | e bitz      | a bissl\n"
      + "çalışmak     | schaffe    | wärche     | schaffe     | schaffa\n"
      + "şimdi        | ez / jetz  | itz        | jetz        | jetzad\n"
      + "-dir         | isch       | isch       | isch        | is\n"
      + "selam        | hoi/grüezi | hoi        | sali        | servus";
}
