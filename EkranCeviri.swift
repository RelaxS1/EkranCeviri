// Ekran Çeviri — yerli macOS uygulaması
// Bölge seç → Vision OCR → çevir (varsayılan: Google ücretsiz; AI yalnız
// istenirse) → orijinal balonun üstüne, balona sığacak şekilde yaz.
// Canlı mod: yalnız YENİ mesaj çevrilir, ekrandaki çeviriler yerinden oynamaz.
// Cevap önerisi: seçili sohbete göre, kişilik tanımı + yerel sohbet
// hafızasıyla (üslup örnekleri + benzer geçmiş) karşı tarafın dilinde cevap.
//
// Derleme: swiftc -swift-version 5 -O EkranCeviri.swift -o EkranCeviri

import AppKit
import Carbon.HIToolbox
import Security
import ScreenCaptureKit
import Vision

// MARK: - Ayarlar

let destekDizini = FileManager.default.homeDirectoryForCurrentUser
    .appendingPathComponent("Library/Application Support/EkranCeviri")
let configURL = destekDizini.appendingPathComponent("config.json")
let gecmisURL = destekDizini.appendingPathComponent("sohbet_gecmisi.jsonl")

let dilAdlari: [(String, String)] = [
    ("tr", "Türkçe"), ("en", "İngilizce"), ("de", "Almanca"),
    ("fr", "Fransızca"), ("it", "İtalyanca"), ("es", "İspanyolca"),
]

/// API anahtarları macOS Anahtar Zinciri'nde saklanır — dosyada,
/// pakette veya logda düz metin anahtar bulunmaz.
enum AnahtarKasasi {
    static let servis = "com.sami.ekranceviri"
    /// Keychain erişimi yalnız bu kuyrukta yapılır. ad-hoc imzalı derlemede
    /// SecItem çağrısı görünmez bir izin diyaloğunda takılabiliyor; bu
    /// çağrı run loop başlamadan yapılırsa UYGULAMA HİÇ AÇILMIYOR
    /// (QA'da doğrulandı). Bu yüzden okuma/yazma daima asenkron.
    static let kuyruk = DispatchQueue(label: "ekranceviri.kasa")
    /// Keychain yanıt vermezse (izin diyaloğu) düşülecek yedek: yalnız
    /// kullanıcıya okunabilir dosya (0600), depoya/pakete asla girmez.
    static var yedekURL: URL { destekDizini.appendingPathComponent("anahtar") }

    static func oku(_ ad: String) -> String? {
        let sorgu: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: servis,
            kSecAttrAccount as String: ad,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]
        var sonuc: AnyObject?
        guard SecItemCopyMatching(sorgu as CFDictionary, &sonuc) == errSecSuccess,
              let veri = sonuc as? Data else { return nil }
        return String(data: veri, encoding: .utf8)
    }

    static func yaz(_ ad: String, _ deger: String) {
        let temel: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: servis,
            kSecAttrAccount as String: ad,
        ]
        SecItemDelete(temel as CFDictionary)
        var ekle = temel
        ekle[kSecValueData as String] = Data(deger.utf8)
        SecItemAdd(ekle as CFDictionary, nil)
    }

    /// Dosya yedeği (0600) — Keychain takılırsa/boşsa buradan okunur.
    static func yedektenOku() -> String? {
        guard let s = try? String(contentsOf: yedekURL, encoding: .utf8)
        else { return nil }
        let t = s.trimmingCharacters(in: .whitespacesAndNewlines)
        return t.isEmpty ? nil : t
    }

    static func yedegeYaz(_ deger: String) {
        try? FileManager.default.createDirectory(
            at: destekDizini, withIntermediateDirectories: true)
        try? deger.write(to: yedekURL, atomically: true, encoding: .utf8)
        try? FileManager.default.setAttributes(
            [.posixPermissions: 0o600], ofItemAtPath: yedekURL.path)
    }

    /// Anahtarı Keychain'den AÇIKÇA ister (kullanıcı menüden istediğinde).
    /// Sistem parola diyaloğu çıkarabilir; bu yüzden asla otomatik çağrılmaz.
    static func getir(_ ad: String, bitti: @escaping (String) -> Void) {
        if let y = yedektenOku() { bitti(y); return }
        kuyruk.async {
            let deger = oku(ad) ?? ""
            if !deger.isEmpty { yedegeYaz(deger) }   // bir daha sorulmasın
            DispatchQueue.main.async { bitti(deger) }
        }
    }

    /// Hem Keychain'e hem dosya yedeğine yazar (ikisi de arka planda).
    static func kaydet(_ ad: String, _ deger: String) {
        yedegeYaz(deger)
        kuyruk.async { yaz(ad, deger) }
    }
}

struct Ayarlar {
    var hedefDil = "tr"
    var motor = "hizli"           // hizli (varsayılan) | ai
    var grokApiKey = ""
    var grokModel = "grok-4.20-0309-non-reasoning"      // canlı/hızlı
    var grokModelKalite = "grok-4.3"                   // varsayılan çeviri
    // Kaliteli model ~5 sn, hızlı model ~1 sn. Yerel hafıza tekrarları
    // anında karşıladığı için varsayılan KALİTE.
    var hizOnceligi = false
    var ocrDilleri = ["de-DE"]   // tek dil: satır satır dil
                                 // değişimi hatalara yol açıyordu
    var kisilik = ""              // cevap önerisi için kullanıcı kimliği
    var kaynakDilAdi = "İsviçre Almancası (Zürih lehçesi)"
    var kisayolTus = 8                        // varsayılan: C
    var kisayolMod = controlKey | optionKey   // varsayılan: ⌃⌥
    var gidenMotor = "grok"       // Yazdığımı Çevir motoru: grok|bing
    var gidenKarakter = ""        // giden mesajlar için karakter promptu
    var kaynakDilKodu = "de"
    var bulutOnay = false
    var gecmisAcik = true
    var gizlilikGosterildi = false
    // ALMAN MODU (varsayılan): Almanya ve İsviçre'de konuşulan tüm Almanca
    // varyantlarını otomatik algılar. "isvicre" = yalnız İsviçre lehçeleri,
    // "otomatik" = dil kısıtlaması yok.
    var dilModu = "alman"
    var benCinsiyet = "kadin"     // yazan kişi (varsayılan: kadın)
    var karsiCinsiyet = "erkek"   // karşı taraf (varsayılan: erkek)
    var yetiskin = true           // +18 içerik sansürlenmez
    var emojiSerbest = true       // giden mesaja uygun emoji eklenebilir

    static func yukle() -> Ayarlar {
        var a = Ayarlar()
        var anahtarTasindi = false
        var veri = try? Data(contentsOf: configURL)
        if veri == nil, let paketVarsayilan = Bundle.main.url(
                forResource: "config.varsayilan", withExtension: "json") {
            veri = try? Data(contentsOf: paketVarsayilan)
        }
        if let veri = veri,
           let d = (try? JSONSerialization.jsonObject(with: veri)) as? [String: Any] {
            a.hedefDil = d["hedef_dil"] as? String ?? a.hedefDil
            a.motor = d["motor"] as? String ?? a.motor
            a.grokApiKey = d["grok_api_key"] as? String ?? a.grokApiKey
            a.grokModel = d["grok_model"] as? String ?? a.grokModel
            a.grokModelKalite = d["grok_model_kalite"] as? String
                ?? a.grokModelKalite
            a.hizOnceligi = d["hiz_onceligi"] as? Bool ?? a.hizOnceligi
            a.ocrDilleri = d["ocr_dilleri"] as? [String] ?? a.ocrDilleri
            a.kisilik = d["kisilik"] as? String ?? a.kisilik
            a.kaynakDilAdi = d["kaynak_dil_adi"] as? String ?? a.kaynakDilAdi
            a.kisayolTus = d["kisayol_tus"] as? Int ?? a.kisayolTus
            a.kisayolMod = d["kisayol_mod"] as? Int ?? a.kisayolMod
            a.gidenMotor = d["giden_motor"] as? String ?? a.gidenMotor
            a.gidenKarakter = d["giden_karakter"] as? String ?? a.gidenKarakter
            a.kaynakDilKodu = d["kaynak_dil_kodu"] as? String ?? a.kaynakDilKodu
            a.bulutOnay = d["bulut_onay"] as? Bool ?? a.bulutOnay
            a.gecmisAcik = d["gecmis_acik"] as? Bool ?? a.gecmisAcik
            a.gizlilikGosterildi = d["gizlilik_gosterildi"] as? Bool
                ?? a.gizlilikGosterildi
            a.dilModu = d["dil_modu"] as? String ?? a.dilModu
            a.benCinsiyet = d["ben_cinsiyet"] as? String ?? a.benCinsiyet
            a.karsiCinsiyet = d["karsi_cinsiyet"] as? String ?? a.karsiCinsiyet
            a.yetiskin = d["yetiskin"] as? Bool ?? a.yetiskin
            a.emojiSerbest = d["emoji_serbest"] as? Bool ?? a.emojiSerbest
            // GÖÇ: dosyadaki düz anahtar güvenli kasaya taşınır, dosyadan
            // silinir. Keychain YAZIMI arka planda (açılışı bloklamaz).
            if let eskiAnahtar = d["grok_api_key"] as? String,
               !eskiAnahtar.isEmpty {
                AnahtarKasasi.kaydet("grok_api_key", eskiAnahtar)
                a.grokApiKey = eskiAnahtar
                anahtarTasindi = true
            }
        }
        // Açılışta SADECE dosya yedeği okunur (anında, bloklamaz).
        // Keychain, run loop başladıktan sonra asenkron denenir.
        if a.grokApiKey.isEmpty {
            a.grokApiKey = AnahtarKasasi.yedektenOku() ?? ""
        }
        if anahtarTasindi { a.kaydet() }
        if !FileManager.default.fileExists(atPath: configURL.path) { a.kaydet() }
        return a
    }

    func kaydet() {
        let d: [String: Any] = [
            "hedef_dil": hedefDil, "motor": motor, "grok_api_key": "",
            "grok_model": grokModel, "grok_model_kalite": grokModelKalite,
            "hiz_onceligi": hizOnceligi, "ocr_dilleri": ocrDilleri,
            "kisilik": kisilik, "kaynak_dil_adi": kaynakDilAdi,
            "kisayol_tus": kisayolTus, "kisayol_mod": kisayolMod,
            "giden_motor": gidenMotor, "giden_karakter": gidenKarakter,
            "kaynak_dil_kodu": kaynakDilKodu, "bulut_onay": bulutOnay,
            "gecmis_acik": gecmisAcik, "gizlilik_gosterildi": gizlilikGosterildi,
            "dil_modu": dilModu, "ben_cinsiyet": benCinsiyet,
            "karsi_cinsiyet": karsiCinsiyet, "yetiskin": yetiskin,
            "emoji_serbest": emojiSerbest,
        ]
        try? FileManager.default.createDirectory(
            at: destekDizini, withIntermediateDirectories: true)
        if let veri = try? JSONSerialization.data(
                withJSONObject: d, options: [.prettyPrinted]) {
            try? veri.write(to: configURL)
        }
    }
}

// MARK: - Metin normalizasyonu
// OCR aynı mesajı her karede birebir aynı okumaz (noktalama/boşluk oynar).
// Önbellek ve blok eşleştirme bu yüzden normalize anahtarla yapılır —
// titremenin kökten çözümü budur.

func anahtarla(_ s: String) -> String {
    String(s.lowercased().unicodeScalars.filter {
        CharacterSet.alphanumerics.contains($0)
    })
}

/// Ücretsiz motorların "yarı çeviri karışımı"nı yakalar: çeviri, kaynağın
/// kelimelerinin yarısından fazlasını aynen içeriyorsa çeviri sayılmaz
/// ("Hiç aylık verchaufed wuche... isch ok mach der kei sorge" gibi saçma
/// yamalar basılmaz; orijinal görünür, ✨ Grok'la doldurulur).
func karisikMi(_ kaynak: String, _ ceviri: String) -> Bool {
    let kelimele = { (s: String) -> Set<String> in
        Set(s.lowercased().split(whereSeparator: { !$0.isLetter })
            .map(String.init).filter { $0.count >= 3 })
    }
    let k = kelimele(kaynak)
    guard k.count >= 2 else { return false }
    let ortak = k.intersection(kelimele(ceviri)).count
    return Double(ortak) / Double(k.count) > 0.5
}

// MARK: - Lehçe algılama (İsviçre'de tek bir "İsviçre Almancası" yok)
// Zürih, Bern, Basel, Doğu İsviçre ve Wallis belirgin şekilde farklı yazılır.
// Hangi lehçe konuşuluyorsa hem ÇEVİRİ hem de GİDEN mesaj ona göre yapılır.

struct Lehce {
    let ad: String            // kullanıcıya/modele verilen tam ad
    let kisa: String          // panelde gösterilen kısa etiket
    let isaretler: [String]   // ayırt edici kelimeler
}

let lehceler: [Lehce] = [
    Lehce(ad: "Zürih İsviçre Almancası (Züridütsch)", kisa: "Züridütsch",
          isaretler: ["nöd", "chli", "gaht", "gahts", "ez", "züri", "chunnsch",
                      "öppis", "znacht", "sächsi", "zäme", "wott", "morn",
                      "dänk", "hoi", "grüezi", "gseh", "hüt"]),
    Lehce(ad: "Bern İsviçre Almancası (Bärndütsch)", kisa: "Bärndütsch",
          isaretler: ["gäng", "itz", "wärche", "öppe", "müntschi", "gäbig",
                      "hüür", "bärn", "chuum", "nid", "u", "ds", "gwüss",
                      "sträng", "gouf"]),
    Lehce(ad: "Basel İsviçre Almancası (Baseldytsch)", kisa: "Baseldytsch",
          isaretler: ["drämmli", "aifach", "rhy", "vyl", "zyt", "dry", "nit",
                      "hänn", "basel", "yych", "glaubs", "dank dr", "bebbi"]),
    Lehce(ad: "Doğu İsviçre Almancası (Ostschwyzerdütsch)", kisa: "Ostschwyz",
          isaretler: ["ond", "hend", "möcht", "wa", "gsii", "appezell",
                      "sanggale", "khönd", "hoscht", "bisch au"]),
    Lehce(ad: "Wallis/İç İsviçre Almancası (Wallisertitsch)", kisa: "Wallis",
          isaretler: ["ischt", "wier", "üsch", "iisch", "wallis", "briägglu",
                      "gsii wier", "chunt", "zbäärg"]),
]

/// Almanya/Avusturya bölgesel varyantları (Alman modu bunları da tanır)
let almanyaVaryantlari: [Lehce] = [
    Lehce(ad: "Bavyera/Avusturya Almancası", kisa: "Bayrisch",
          isaretler: ["servus", "oida", "ned", "mog", "hoid", "passt scho",
                      "griaß", "bussi", "schmarrn", "geh bitte", "wurscht",
                      "dahoam", "gscheit"]),
    Lehce(ad: "Kuzey Almanya Almancası", kisa: "Norddeutsch",
          isaretler: ["moin", "nich", "wat", "büddel", "schnacken", "lütt",
                      "tschüssing", "jo moin", "dat"]),
    Lehce(ad: "Ren/Köln Almancası", kisa: "Rheinisch",
          isaretler: ["alaaf", "kölle", "jot", "dat is", "wat is", "ejal",
                      "pittermännchen"]),
]

let isvicreGenelIsaretler: Set<String> = [
    "isch", "gsi", "gsii", "hesch", "häsch", "chan", "cha", "chasch", "chum",
    "chume", "chunnt", "mues", "muess", "nöd", "nid", "nit", "öppis", "öpper",
    "hüt", "morn", "zäme", "guet", "gäll", "gell", "merci", "schaffe", "lueg",
    "vilmal", "grüezi", "hoi", "sali", "znacht", "zmorge", "chind", "lüt",
]

/// Ekrandaki sohbetten hangi İsviçre Almancası lehçesinin konuşulduğunu
/// çıkarır. Dönen: (modele verilecek tam ad, panelde gösterilecek kısa ad).
func lehceyiAlgila(_ metinler: [String]) -> (ad: String, kisa: String) {
    let kelimeler = Set(metinler.joined(separator: " ").lowercased()
        .split(whereSeparator: { !$0.isLetter }).map(String.init))
    var enIyi: (Lehce, Int)? = nil
    for l in lehceler {
        let puan = l.isaretler.reduce(0) { $0 + (kelimeler.contains($1) ? 1 : 0) }
        if puan > (enIyi?.1 ?? 0) { enIyi = (l, puan) }
    }
    let isvicreli = !kelimeler.intersection(isvicreGenelIsaretler).isEmpty
    // Almanya/Avusturya varyantları: İsviçre işareti yoksa değerlendir
    if !isvicreli {
        var enIyiDE: (Lehce, Int)? = nil
        for l in almanyaVaryantlari {
            let puan = l.isaretler.reduce(0) {
                $0 + (kelimeler.contains($1) ? 1 : 0) }
            if puan > (enIyiDE?.1 ?? 0) { enIyiDE = (l, puan) }
        }
        if let (l, puan) = enIyiDE, puan >= 1 { return (l.ad, l.kisa) }
    }
    if let (l, puan) = enIyi, puan >= 2 { return (l.ad, l.kisa) }
    if isvicreli {
        if let (l, puan) = enIyi, puan == 1 { return (l.ad, l.kisa) }
        return ("İsviçre Almancası (bölge belirsiz)", "İsviçre Almancası")
    }
    return ("Standart Almanca (Hochdeutsch)", "Hochdeutsch")
}

// MARK: - Lehçe ön-normalizasyonu (ücretsiz motorlar için)
// KANIT: Bing/Google Zürih lehçesini ya hiç çeviremiyor ya da ters anlam
// üretiyor ("bim Bahnhof" → "istasyonun yarısı geldi"). Aynı cümleler
// standart Almancaya çevrilip verildiğinde kusursuz çıkıyor. Bu yüzden
// makine motorlarına giden metin önce burada standartlaştırılır.
// (Grok'a HAM metin gider — o lehçeyi zaten biliyor.)

let lehceKaliplari: [(String, String)] = [
    // Saatler AÇIK yazılır: Bing "halb sechs"i düzenli olarak "altı buçuk"
    // diye yanlış çeviriyor (doğrusu beş buçuk)
    ("halbi sächsi", "fünf uhr dreissig"), ("halbi sibni", "sechs uhr dreissig"),
    ("halbi achti", "sieben uhr dreissig"), ("halbi nüni", "acht uhr dreissig"),
    ("halbi zähni", "neun uhr dreissig"), ("halbi elfi", "zehn uhr dreissig"),
    ("halb sechs", "fünf uhr dreissig"), ("halb sieben", "sechs uhr dreissig"),
    ("halb acht", "sieben uhr dreissig"), ("halb neun", "acht uhr dreissig"), ("merci vilmal", "vielen dank"),
    ("uf widerluege", "auf wiedersehen"), ("uf wiederluege", "auf wiedersehen"),
    ("es bitzeli", "ein bisschen"), ("es bitzli", "ein bisschen"),
    ("e chli", "ein bisschen"), ("ich bi", "ich bin"), ("i bi", "ich bin"),
    ("wie gahts", "wie geht es"), ("wie gohts", "wie geht es"),
    ("wie gaht s", "wie geht es"), ("was machsch", "was machst du"),
    ("bis spöter", "bis später"), ("bis dänn", "bis dann"),
    ("guete morge", "guten morgen"), ("guete abig", "guten abend"),
    ("schöne abig", "schönen abend"), ("gueti nacht", "gute nacht"),
    ("hoi zäme", "hallo zusammen"), ("ha di gärn", "habe dich gern"),
    ("han di gärn", "habe dich gern"), ("hesch zit", "hast du zeit"),
    ("weisch das", "weisst du das"), ("weisch no", "weisst du noch"),
    ("gseh mer eus", "sehen wir uns"),
]

let lehceSozluk: [String: String] = [
    // selamlama
    "hoi": "hallo", "sali": "hallo", "salü": "hallo", "grüezi": "guten tag",
    "grüessech": "guten tag", "tschau": "tschüss", "merci": "danke",
    // olmak / sahip olmak
    "isch": "ist", "bisch": "bist", "gsi": "gewesen", "gsii": "gewesen",
    "gsin": "gewesen", "ha": "habe", "han": "habe", "hesch": "hast",
    "häsch": "hast", "hät": "hat", "het": "hat", "händ": "haben",
    "hend": "haben",
    // kipler
    "chan": "kann", "cha": "kann", "chasch": "kannst", "chönd": "können",
    "chönne": "können", "chönnt": "könnt", "muess": "muss", "muesch": "musst",
    "müend": "müssen", "müesst": "müsst", "söll": "soll", "sött": "sollte",
    "sötti": "sollte", "söttsch": "solltest", "wott": "will",
    "wotsch": "willst", "wänd": "wollen", "wend": "wollen", "dörf": "darf",
    "dörfsch": "darfst",
    // gelmek / gitmek
    "chum": "komme", "chume": "komme", "chunnsch": "kommst", "chunnt": "kommt",
    "chömed": "kommen", "chömmed": "kommen", "cho": "kommen", "kho": "kommen",
    "choo": "kommen", "gang": "gehe", "gasch": "gehst", "gaht": "geht",
    "goht": "geht", "gönd": "gehen", "gah": "gehen", "goh": "gehen",
    // diğer fiiller
    "machsch": "machst", "mached": "machen", "gseh": "sehen",
    "gsehsch": "siehst", "gseht": "sieht", "weisch": "weisst",
    "schaffe": "arbeiten", "schaffsch": "arbeitest", "schaffed": "arbeiten",
    "luege": "schauen", "luegsch": "schaust", "lueg": "schau",
    "träffe": "treffen", "trüffe": "treffen", "verzell": "erzähl",
    "verzellsch": "erzählst", "bruch": "brauche", "bruuch": "brauche",
    "bruchsch": "brauchst", "nimmsch": "nimmst", "gisch": "gibst",
    "bliib": "bleibe", "bliibsch": "bleibst", "schriib": "schreibe",
    "schriibsch": "schreibst",
    // zaman
    "hüt": "heute", "morn": "morgen", "geschter": "gestern", "jetz": "jetzt",
    "spöter": "später", "früeh": "früh", "znacht": "abendessen",
    "zmittag": "mittagessen", "zmorge": "frühstück", "abig": "abend",
    "morge": "morgen", "wuche": "woche", "johr": "jahr", "stund": "stunde",
    "zit": "zeit", "ziit": "zeit",
    // olumsuzluk / edatlar
    "nöd": "nicht", "nid": "nicht", "nit": "nicht", "nüt": "nichts",
    "niemer": "niemand", "gäll": "nicht wahr", "gell": "nicht wahr", "äbe": "eben",
    "au": "auch", "scho": "schon", "grad": "gerade", "nomol": "nochmal", "no": "noch", "wuchenänd": "wochenende", "wucheend": "wochenende", "di": "dich", "dir": "dir", "gits": "gibt es", "hets": "hat es",
    "nomal": "nochmal", "vilicht": "vielleicht", "villicht": "vielleicht",
    "wörkli": "wirklich", "würkli": "wirklich", "wüki": "wirklich",
    "öppis": "etwas", "öpper": "jemand", "öppedie": "manchmal",
    "chli": "bisschen", "chlii": "bisschen", "bitzli": "bisschen",
    "zäme": "zusammen", "allei": "allein",
    // sıfat / isim
    "guet": "gut", "guät": "gut", "schlächt": "schlecht", "schöni": "schöne",
    "lüt": "leute", "chind": "kind", "chinder": "kinder", "fründ": "freund",
    "fründin": "freundin", "huus": "haus", "schuel": "schule",
    "arbet": "arbeit", "wätter": "wetter", "gäld": "geld",
    // edat / zamir
    "id": "in die", "uf": "auf", "ufem": "auf dem", "bim": "beim",
    "bi": "bei", "vo": "von", "dä": "der", "mer": "wir", "eus": "uns",
    "öi": "euch", "ihne": "ihnen", "welchi": "welche", "weles": "welches",
    "wievil": "wieviel", "jaa": "ja", "nei": "nein", "nöi": "nein",
    "gärn": "gern",
    // saatler
    "sächsi": "sechs", "sibni": "sieben", "achti": "acht", "nüni": "neun",
    "zähni": "zehn", "elfi": "elf", "zwölfi": "zwölf", "füfi": "fünf",
    "vieri": "vier", "drüü": "drei", "zwoi": "zwei",
    // WhatsApp kısaltmaları
    "wrsch": "wahrscheinlich", "vlt": "vielleicht", "hdl": "hab dich lieb",
    "lg": "liebe grüsse", "gn8": "gute nacht",
]

/// Makine motorları emojileri sık sık düşürüyor. Kaynakta olup çeviride
/// olmayan emojiler sona eklenir (sıra korunur, tekrar edilmez).
func emojileriKoru(kaynak: String, ceviri: String) -> String {
    let emojiler = kaynak.unicodeScalars.filter { $0.properties.isEmoji
        && $0.value > 0x238C }
    guard !emojiler.isEmpty else { return ceviri }
    var eksik = ""
    for e in emojiler {
        let s = String(Character(e))
        if !ceviri.contains(s), !eksik.contains(s) { eksik += s }
    }
    return eksik.isEmpty ? ceviri
        : ceviri.trimmingCharacters(in: .whitespaces) + " " + eksik
}

func lehceyiStandartlastir(_ metin: String) -> String {
    var t = metin
    // 1) çok kelimeli kalıplar (uzun olan önce)
    for (k, v) in lehceKaliplari.sorted(by: { $0.0.count > $1.0.count }) {
        if let r = t.range(of: k, options: [.caseInsensitive]) {
            let bas = t[t.startIndex..<r.lowerBound]
            let ilkBuyuk = t[r].first?.isUppercase == true
            let yeni = ilkBuyuk ? v.prefix(1).uppercased() + v.dropFirst() : v
            t = bas + yeni + t[r.upperBound...]
        }
    }
    // 2) kelime bazlı sözlük (harf olmayanlar korunur)
    var sonuc = ""
    var kelime = ""
    func kelimeyiBosalt() {
        guard !kelime.isEmpty else { return }
        let kucuk = kelime.lowercased()
        if let karsilik = lehceSozluk[kucuk] {
            sonuc += kelime.first?.isUppercase == true
                ? karsilik.prefix(1).uppercased() + karsilik.dropFirst()
                : karsilik
        } else {
            sonuc += kelime
        }
        kelime = ""
    }
    for k in t {
        if k.isLetter { kelime.append(k) }
        else { kelimeyiBosalt(); sonuc.append(k) }
    }
    kelimeyiBosalt()
    return sonuc
}

// MARK: - Ağ yardımcıları

// Çevrimiçi onay kapısı: delege atar; ilk ağ çağrısından önce sorar
var bulutOnayDenetimi: (() -> Bool)?

func httpGetir(_ istek: URLRequest) throws -> Data {
    if let denetim = bulutOnayDenetimi, !denetim() {
        throw NSError(domain: "bulut", code: 1, userInfo: [
            NSLocalizedDescriptionKey: "Çevrimiçi çeviriye izin verilmedi"])
    }
    var sonuc: Data?
    var hata: Error?
    let bekleyici = DispatchSemaphore(value: 0)
    let gorev = URLSession.shared.dataTask(with: istek) { veri, yanit, err in
        if let err = err { hata = err }
        else if let http = yanit as? HTTPURLResponse, http.statusCode >= 400 {
            hata = NSError(domain: "http", code: http.statusCode, userInfo: [
                NSLocalizedDescriptionKey: "HTTP \(http.statusCode)"])
        } else { sonuc = veri }
        bekleyici.signal()
    }
    gorev.resume()
    // Sonsuz bekleme YOK: uyku sonrası askıda kalan bağlantı tüm iş
    // kuyruğunu kalıcı kilitliyordu ("bir süre sonra çevirmeyi bırakıyor")
    if bekleyici.wait(timeout: .now() + istek.timeoutInterval + 8) == .timedOut {
        gorev.cancel()
        throw NSError(domain: "http", code: -1001, userInfo: [
            NSLocalizedDescriptionKey: "İstek zaman aşımına uğradı"])
    }
    if let hata = hata { throw hata }
    return sonuc ?? Data()
}

// MARK: - Çeviri motorları

/// Google'ın anahtarsız clients5 uç noktası — tüm metinler TEK istekte.
func googleCevir(_ metinler: [String], hedef: String,
                 kaynak: String? = nil) throws -> [String] {
    let urlString = "https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=\(kaynak ?? "auto")&tl=\(hedef)"
    var istek = URLRequest(url: URL(string: urlString)!, timeoutInterval: 15)
    istek.httpMethod = "POST"
    istek.setValue("Mozilla/5.0", forHTTPHeaderField: "User-Agent")
    istek.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
    
    var bodyComponents = URLComponents()
    bodyComponents.queryItems = metinler.map { URLQueryItem(name: "q", value: $0) }
    istek.httpBody = bodyComponents.query?.data(using: .utf8)
    let veri = try httpGetir(istek)
    guard let dizi = try JSONSerialization.jsonObject(with: veri) as? [Any] else {
        throw NSError(domain: "ceviri", code: 1, userInfo: [
            NSLocalizedDescriptionKey: "Google yanıtı çözülemedi"])
    }
    let sonuc: [String] = dizi.map { oge in
        if let s = oge as? String { return s }
        if let ikili = oge as? [Any], let s = ikili.first as? String { return s }
        return ""
    }
    guard sonuc.count == metinler.count else {
        throw NSError(domain: "ceviri", code: 2, userInfo: [
            NSLocalizedDescriptionKey: "Google eksik yanıt döndü"])
    }
    return sonuc
}

// Bing (Microsoft Edge) ücretsiz uç noktası — anahtarsız; jeton ~10 dk
// geçerli olduğundan önbelleklenir. Almanca→Türkçe'de Google'dan tutarlı.
var bingJeton: (deger: String, zaman: Date)?

func bingCevir(_ metinler: [String], hedef: String,
               kaynak: String? = nil) throws -> [String] {
    let jeton: String
    if let j = bingJeton, Date().timeIntervalSince(j.zaman) < 480 {
        jeton = j.deger
    } else {
        var jetonIstek = URLRequest(
            url: URL(string: "https://edge.microsoft.com/translate/auth")!,
            timeoutInterval: 10)
        jetonIstek.setValue("Mozilla/5.0", forHTTPHeaderField: "User-Agent")
        let yeni = String(data: try httpGetir(jetonIstek), encoding: .utf8) ?? ""
        guard yeni.count > 100 else {
            throw NSError(domain: "bing", code: 1, userInfo: [
                NSLocalizedDescriptionKey: "Bing jetonu alınamadı"])
        }
        bingJeton = (yeni, Date())
        jeton = yeni
    }
    // Kaynak dili ZORLA: otomatik algılama lehçeyi bazen Malayca sanıp
    // metni hiç çevirmiyordu (canlı testte doğrulandı)
    let kaynakEk = kaynak.map { "&from=\($0)" } ?? ""
    var istek = URLRequest(
        url: URL(string: "https://api-edge.cognitive.microsofttranslator.com/"
               + "translate?api-version=3.0&to=\(hedef)\(kaynakEk)")!,
        timeoutInterval: 15)
    istek.httpMethod = "POST"
    istek.setValue("application/json", forHTTPHeaderField: "Content-Type")
    istek.setValue("Bearer \(jeton)", forHTTPHeaderField: "Authorization")
    istek.httpBody = try JSONSerialization.data(
        withJSONObject: metinler.map { ["Text": $0] })
    let veri = try httpGetir(istek)
    guard let dizi = try JSONSerialization.jsonObject(with: veri)
            as? [[String: Any]] else {
        throw NSError(domain: "bing", code: 2, userInfo: [
            NSLocalizedDescriptionKey: "Bing yanıtı çözülemedi"])
    }
    let sonuc: [String] = dizi.map {
        (($0["translations"] as? [[String: Any]])?
            .first?["text"] as? String) ?? ""
    }
    guard sonuc.count == metinler.count else {
        throw NSError(domain: "bing", code: 3, userInfo: [
            NSLocalizedDescriptionKey: "Bing eksik yanıt döndü"])
    }
    return sonuc
}

func grokIstek(_ mesajlar: [[String: String]], ayarlar: Ayarlar,
               sicaklik: Double, sema: [String: Any]? = nil,
               model: String? = nil) throws -> String {
    var govde: [String: Any] = [
        "model": model ?? ayarlar.grokModel,
        "messages": mesajlar,
        "temperature": sicaklik,
    ]
    // Yapılandırılmış çıktı: dizi uzunluğu ve sıra GARANTİ altında olur;
    // serbest metinden JSON ayıklama kırılganlığı ortadan kalkar.
    if let sema = sema {
        govde["response_format"] = [
            "type": "json_schema",
            "json_schema": ["name": "ceviriler", "strict": true,
                            "schema": sema],
        ]
    }
    var istek = URLRequest(url: URL(string: "https://api.x.ai/v1/chat/completions")!,
                           timeoutInterval: 60)
    istek.httpMethod = "POST"
    istek.setValue("application/json", forHTTPHeaderField: "Content-Type")
    istek.setValue("Bearer \(ayarlar.grokApiKey)", forHTTPHeaderField: "Authorization")
    istek.httpBody = try JSONSerialization.data(withJSONObject: govde)
    let veri = try httpGetir(istek)
    guard let d = try JSONSerialization.jsonObject(with: veri) as? [String: Any],
          let secenekler = d["choices"] as? [[String: Any]],
          let mesaj = secenekler.first?["message"] as? [String: Any],
          let icerik = mesaj["content"] as? String else {
        throw NSError(domain: "grok", code: 1, userInfo: [
            NSLocalizedDescriptionKey: "Grok yanıtı çözülemedi"])
    }
    return icerik.trimmingCharacters(in: .whitespacesAndNewlines)
}

func citCizgileriniAt(_ s: String) -> String {
    var icerik = s
    if icerik.hasPrefix("```") {
        icerik = icerik
            .replacingOccurrences(of: "```json", with: "")
            .replacingOccurrences(of: "```", with: "")
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }
    return icerik
}

func grokCevir(_ metinler: [String], ayarlar: Ayarlar,
               roller: [Bool]? = nil,
               lehceAdi: String? = nil) throws -> [String] {
    let dilAdi = dilAdlari.first(where: { $0.0 == ayarlar.hedefDil })?.1
        ?? ayarlar.hedefDil
    // Bu istem gerçek sohbet verisiyle ölçülerek yazıldı: makine motorları
    // (Bing/Google) lehçeyi ya hiç çeviremiyor ya da anlamı bozuyor;
    // sözlüklü istemle Grok tüm altın test setinde doğru sonuç verdi.
    let algilanan = lehceAdi ?? lehceyiAlgila(metinler).ad
    // İSTEM SIRASI: değişmeyen (cache'lenebilir) blok ÖNCE, sohbete özel
    // bilgi SONRA — xAI prompt cache'i ön ekten yakalar.
    let sistem = """
    Sen Almanca'nın TÜM bölgesel varyantlarında (Hochdeutsch, Bavyera/\
    Avusturya, Kuzey Almanya, Ren bölgesi, Züridütsch, Bärndütsch, \
    Baseldytsch, Ostschwyzerdütsch, Wallisertitsch) uzman bir çevirmensin.
    Metinler bir WhatsApp ekranından OCR ile okundu.

    ÖRNEK ÇEVİRİLER (bu kaliteyi hedefle):
    - "würsch mer churz en memo mache" → "bana kısa bir sesli mesaj çeker misin"
    - "bin nur 3 tag in rom gsi" → "sadece 3 gün Roma'daydım"
    - "Hesch gern alli?" → "hepsini beğendin mi"
    - "I ha gäng no z wärche" → "hâlâ çalışmam gerekiyor"
    - "Servus, wia gehts da heid?" → "selam bugün nasılsın"
    - "chasch mir es foti schicke?" → "bana bir fotoğraf gönderebilir misin"

    LEHÇE FARKLARI (aynı anlam, farklı yazım):
    - "nicht": nöd (ZH) / nid (BE) / nit (BS) / nöd-nid (OS)
    - "jetzt": ez (ZH) / itz (BE) / jetz (genel)
    - "immer": immer (ZH) / gäng (BE)
    - "und": und (ZH) / u (BE) / ond (OS)
    - "arbeiten": schaffe (ZH/BS) / wärche (BE)
    - "ist": isch (genel) / ischt (VS)
    - "wir": mir (ZH/BE) / wier (VS)
    - Basel'de i→y yazılır: zyt=Zeit, vyl=viel, dry=drei

    GİRDİNİN DOĞASI:
    - Yazım standart DEĞİL: herkes duyduğu gibi yazar (gaht/goht, \
    hesch/häsch, chunnsch/kunsch).
    - OCR bozuk/eksik harf ve birleşmiş kelime üretir: "trffe"=träffe, \
    "biz"=bis, "amerkig"=Anmerkung.
    - WhatsApp kısaltmaları: vlt=vielleicht, jz=jetzt, wrsch=wahrscheinlich.
    - İçerik yetişkin/cinsel (+18) olabilir; ASLA sansürleme, yumuşatma, \
    kibarlaştırma veya atlama yapma — ne denmişse aynı açıklıkta çevir.

    SELAMLAMALAR (asla özel isim/gün adı sanma): sali/salü/hoi/hoi zäme/\
    grüezi/grüessech (CH), servus/griaß di/pfiat di (Bayern-AT), moin/\
    tach (Kuzey DE), ciao/tschau = merhaba/selam/hoşça kal.

    LEHÇE ANAHTARI: gsi=gewesen, chunnsch=kommst, cho/kho=kommen, \
    hesch=hast, isch=ist, gaht/gahts=geht, hüt=heute, morn=morgen, \
    nöd/nid=nicht, öppis=etwas, öpper=jemand, chli=ein bisschen, \
    zäme=zusammen, gäll=nicht wahr, wott=will, mues/muess=muss, \
    derf=darf, welles=welches, alli=alle, eus=uns, mer=wir, au=auch, \
    no=noch, schaffe=arbeiten, lueg=schau, träffe=treffen, \
    memo=sesli mesaj, stutz=Franken, halbi sächsi=saat beş buçuk (17:30).

    KURALLAR:
    1. Önce zihninde standart Almancaya çöz, sonra DOĞAL \(dilAdi) yaz. \
    Kaynak dilden kelime BIRAKMA.
    2. Bozuk kelimeyi bağlamdan tahmin et; asla olduğu gibi geri verme, \
    asla boş bırakma.
    3. HİÇBİR bilgi UYDURMA: metinde olmayan gün, saat, isim ekleme.
    4. Gündelik WhatsApp \(dilAdi)si kullan (resmi dil değil). \
    Emojileri aynen koru.
    5. Mesajlar bir sohbetin akışıdır (rol: "ben" = kullanıcı, \
    "karsi" = karşı taraf); sırayı ve bağlamı dikkate al.
    6. EKSİKSİZ ÇEVİR: cümlenin bir kısmını atlama, yarım bırakma. \
    Çıktıda hiç Almanca/lehçe kelime kalmamalı.
    7. Her öğe için önce `standart` alanına OCR'ı ONARILMIŞ standart \
    Almanca karşılığı, sonra `ceviri` alanına doğal \(dilAdi) çeviriyi yaz.
    8. ÇIKTI DİZİSİ, GİRDİ DİZİSİYLE AYNI SIRADA ve AYNI UZUNLUKTA olmalı: \
    n. öğe n. mesajın çevirisidir. Hiçbir mesajı atlama veya birleştirme. \
    `indeks` alanı 0'dan başlar (ilk mesaj 0).

    ——— BU SOHBETE ÖZEL ———
    \(modTanimi(ayarlar))
    Algılanan varyant: \(algilanan).\(kimlikTanimi(ayarlar))
    """
    // Rol bilgisi verilirse sohbet akışı olarak gönderilir (tutarlılık artar)
    let girdiNesnesi: Any
    if let roller = roller, roller.count == metinler.count {
        girdiNesnesi = zip(roller, metinler).map {
            ["rol": $0 ? "ben" : "karsi", "metin": $1] }
    } else {
        girdiNesnesi = metinler
    }
    let girdi = String(
        data: try JSONSerialization.data(withJSONObject: girdiNesnesi),
        encoding: .utf8) ?? "[]"
    // YAPILANDIRILMIŞ ÇIKTI: model önce OCR'ı ONARIP standart Almancaya
    // çevirir (`standart`), sonra hedefe çevirir (`ceviri`). İki aşamalı
    // düşünme doğruluğu artırıyor; `indeks` sıra/uzunluk garantisi verir.
    let sema: [String: Any] = [
        "type": "object",
        "properties": [
            "ceviriler": [
                "type": "array",
                "items": [
                    "type": "object",
                    "properties": [
                        "indeks": ["type": "integer"],
                        "standart": ["type": "string"],
                        "ceviri": ["type": "string"],
                    ],
                    "required": ["indeks", "standart", "ceviri"],
                    "additionalProperties": false,
                ],
            ],
        ],
        "required": ["ceviriler"],
        "additionalProperties": false,
    ]
    let icerik = citCizgileriniAt(try grokIstek([
        ["role": "system", "content": sistem],
        ["role": "user", "content": girdi],
    ], ayarlar: ayarlar, sicaklik: 0, sema: sema,
       model: ayarlar.hizOnceligi ? ayarlar.grokModel
                                  : ayarlar.grokModelKalite))
    var liste = [String](repeating: "", count: metinler.count)
    if let d = try? JSONSerialization.jsonObject(with: Data(icerik.utf8))
            as? [String: Any],
       let kayitlar = d["ceviriler"] as? [[String: Any]] {
        // SIRALAMA MODELE EMANET EDİLMEZ: model indeksi bazen 1'den
        // başlatıyor ve TÜM çeviriler bir kayıyordu (QA'da yakalandı:
        // 1. mesajın çevirisi 2. mesaja yazıldı). Kural: sayı tutuyorsa
        // DİZİ SIRASI esas alınır; tutmuyorsa indeks 0-tabanına
        // normalize edilerek yerleştirilir.
        let ceviriler = kayitlar.map { ($0["ceviri"] as? String) ?? "" }
        if ceviriler.count == metinler.count {
            liste = ceviriler
        } else {
            let indeksler = kayitlar.compactMap { $0["indeks"] as? Int }
            // 1-tabanlı olduğunun TEK güvenilir kanıtı: indeks sayıya eşit
            // veya büyük (0-tabanlıda en fazla sayı-1 olabilir)
            let kaydir = (indeksler.max() ?? 0) >= metinler.count ? 1 : 0
            for (n, kayit) in kayitlar.enumerated() {
                let ham = (kayit["indeks"] as? Int) ?? (n + kaydir)
                let i = ham - kaydir
                guard i >= 0, i < liste.count else { continue }
                liste[i] = (kayit["ceviri"] as? String) ?? ""
            }
        }
    } else if let dizi = try? JSONSerialization.jsonObject(
                    with: Data(icerik.utf8)) as? [Any] {
        // Yedek yol: model düz dizi döndürürse
        for (i, oge) in dizi.enumerated() where i < liste.count {
            if let s = oge as? String { liste[i] = s }
            else if let o = oge as? [String: Any] {
                liste[i] = (o["ceviri"] as? String) ?? ""
            }
        }
    } else {
        throw NSError(domain: "grok", code: 2, userInfo: [
            NSLocalizedDescriptionKey: "Grok yanıtı çözülemedi"])
    }
    return liste
}

/// Grok seçiliyken anahtar bulunmadığında true olur (arayüz uyarır).
var anahtarUyarisi = false

/// Uygulamanın ürettiği çevirilerin normalize anahtarları (delege doldurur).
/// Kendi çıktımızı yeniden çevirmeyi ve geçmişe yazmayı engeller.
var uretilmisCeviriler = Set<String>()

func bloklariCevir(_ bloklar: [Blok], motor: String, ayarlar: Ayarlar,
                   onbellek: [String: String],
                   zorla: Bool = false) -> (String, [String: String]) {
    var bellek = onbellek
    let hedefler = bloklar.filter { $0.hedef }
    if hedefler.isEmpty { return ("yok", bellek) }
    // Ekranda görülen metin bizim ürettiğimiz bir çeviriyse (katman
    // yakalamaya sızdıysa) tekrar çevirmeye çalışma
    let hedefler2 = hedefler.filter { !uretilmisCeviriler.contains($0.anahtar) }
    if !zorla {
        // 1) oturum önbelleğinde bulanık arama (OCR titremesi)
        for b in hedefler2 where bellek[b.anahtar] == nil {
            if let benzer = bulanikBul(b.anahtar, bellek) {
                bellek[b.anahtar] = benzer
            }
        }
        // 2) KALICI YEREL HAFIZA: motora gitmeden önce diskteki çevirilere bak
        for b in hedefler2 where bellek[b.anahtar] == nil {
            if let yerel = CeviriHafizasi.paylasilan.ara(b.anahtar,
                                                         dil: ayarlar.hedefDil) {
                bellek[b.anahtar] = yerel
            }
        }
    }
    let eksikler = zorla ? hedefler2
                         : hedefler2.filter { bellek[$0.anahtar] == nil }
    var motorAdi = "güncel"
    if !eksikler.isEmpty {
        let metinler = eksikler.map { $0.metin }
        var m = motor
        if m == "ai" && ayarlar.grokApiKey.isEmpty {
            // Kullanıcı Grok seçti ama anahtar yok: sessizce ücretsiz motora
            // düşmek "Grok kötü çeviriyor" yanılgısı yaratıyordu.
            m = "hizli"
            anahtarUyarisi = true
        }

        // Lehçe algılama TÜM görünür sohbetten yapılır: yalnız yeni
        // mesajlara bakmak "bölge belirsiz" sonucu veriyordu
        let sohbetLehcesi = lehceyiAlgila(hedefler.map { $0.metin })
        // Makine motorlarına lehçe DEĞİL, standartlaştırılmış metin gider
        let makineMetinleri = metinler.map { lehceyiStandartlastir($0) }
        let kaynakKodu = ayarlar.kaynakDilKodu
        var ceviriler: [String]?
        if m == "ai" {
            // SADECE GROK: kullanıcı açıkça Grok seçtiyse başka motor yok
            let roller = eksikler.map { $0.benim }
            if var c = try? grokCevir(metinler, ayarlar: ayarlar,
                                      roller: roller,
                                      lehceAdi: sohbetLehcesi.ad) {
                // EKSİKSİZLİK KAPISI: Almanca kalıntısı olan satırları
                // (yarım çeviri) bir kez daha, tek tek çevirt
                var yeniden: [Int] = []
                // (a) boş veya yarım çeviri
                for (i, ceviri) in c.enumerated()
                where ceviri.isEmpty || almancaKalintiVar(ceviri) {
                    yeniden.append(i)
                }
                // (b) HİZA DENETİMİ: farklı iki kaynağa AYNI çeviri
                // atandıysa model sırayı kaçırmıştır (gözlendi: 3 mesajlık
                // partide 1. mesajın çevirisi 2.'ye de yazıldı).
                var gorulen: [String: Int] = [:]
                for (i, ceviri) in c.enumerated() where !ceviri.isEmpty {
                    let ck = anahtarla(ceviri)
                    if let ilk = gorulen[ck],
                       anahtarla(metinler[ilk]) != anahtarla(metinler[i]) {
                        if !yeniden.contains(i) { yeniden.append(i) }
                        if !yeniden.contains(ilk) { yeniden.append(ilk) }
                    } else {
                        gorulen[ck] = i
                    }
                }
                // Düzeltme TEK TEK yapılır: tek öğeli çağrıda hiza kayması
                // matematiksel olarak imkânsız
                for i in yeniden.prefix(6) {
                    if let d = try? grokCevir([metinler[i]], ayarlar: ayarlar,
                                              roller: [roller[i]]),
                       let tek = d.first, !tek.isEmpty,
                       !almancaKalintiVar(tek) {
                        c[i] = tek
                    }
                }
                ceviriler = c
                motorAdi = "Grok · " + sohbetLehcesi.kisa
            }
        } else {
            // Ücretsiz zincir: Bing (de→tr'de Google'dan tutarlı) → Google →
            // Grok (son çare; Google ara ara IP engeli koyuyor, 302→sorry)
            if let c = try? bingCevir(makineMetinleri, hedef: ayarlar.hedefDil,
                                      kaynak: kaynakKodu) {
                ceviriler = c
                motorAdi = anahtarUyarisi
                    ? "⚠︎ Grok anahtarı yok → Bing" : "Bing (ücretsiz)"
            } else if let c = try? googleCevir(makineMetinleri,
                                               hedef: ayarlar.hedefDil,
                                               kaynak: kaynakKodu) {
                ceviriler = c; motorAdi = "Google (ücretsiz)"
            } else if !ayarlar.grokApiKey.isEmpty,
                      let c = try? grokCevir(metinler, ayarlar: ayarlar) {
                ceviriler = c; motorAdi = "Grok AI (ücretsizler kapalı)"
            }
        }
        guard let tamam = ceviriler else {
            for b in hedefler { b.ceviri = bellek[b.anahtar] }
            return ("çeviri hatası (ağ?)", bellek)
        }
        var liste = tamam
        // Ücretsiz motor bazı metinleri AYNEN geri verir (çeviremedi).
        // Onları İKİNCİ ücretsiz motorla bir kez daha dene — "bir kısmını
        // çevirmedi" boşluğunu kapatır, maliyeti sıfır.
        if m != "ai" && !motorAdi.contains("Grok") {
            var tekrarIdx: [Int] = []
            for (i, blok) in eksikler.enumerated()
            where anahtarla(liste[i]) == blok.anahtar
               || karisikMi(blok.metin, liste[i]) { tekrarIdx.append(i) }
            if !tekrarIdx.isEmpty {
                let metin2 = tekrarIdx.map {
                    lehceyiStandartlastir(eksikler[$0].metin) }
                let ikinci = motorAdi.contains("Bing")
                    ? (try? googleCevir(metin2, hedef: ayarlar.hedefDil,
                                        kaynak: kaynakKodu))
                    : (try? bingCevir(metin2, hedef: ayarlar.hedefDil,
                                      kaynak: kaynakKodu))
                if let ikinci = ikinci, ikinci.count == tekrarIdx.count {
                    for (j, i) in tekrarIdx.enumerated() {
                        liste[i] = ikinci[j]
                    }
                }
            }
        }
        for (blok, hamCeviri) in zip(eksikler, liste) {
            let ceviri = emojileriKoru(kaynak: blok.metin, ceviri: hamCeviri)
            // Hâlâ aynen/karışık dönen metin = ücretsiz motorlar çeviremedi.
            // "" işareti konur; hemen aşağıdaki Grok tamamlama devralır.
            let gercekCeviri = motorAdi.contains("Grok")
                || (anahtarla(ceviri) != blok.anahtar
                    && !karisikMi(blok.metin, ceviri))
            bellek[blok.anahtar] = gercekCeviri ? ceviri : ""
        }
    }

    // GROK TAMAMLAMA: ücretsiz motorların çeviremediği artıklar ("" işaretli
    // — bu turdan veya öncekilerden) Grok'a gider. Her şey önce ücretsizden
    // geçer; Grok yalnız artıkları alır → maliyet minik, ekranda çevrilmemiş
    // mesaj KALMAZ ("bazı mesajlara hiç dokunmuyor" şikayetinin çözümü).
    if motor != "ai", !ayarlar.grokApiKey.isEmpty {
        let artiklar = hedefler.filter { bellek[$0.anahtar] == "" }
        if !artiklar.isEmpty,
           let grokSonuc = try? grokCevir(artiklar.map { $0.metin },
                                          ayarlar: ayarlar,
                                          roller: artiklar.map { $0.benim }) {
            for (blok, ceviri) in zip(artiklar, grokSonuc)
            where !ceviri.isEmpty {
                bellek[blok.anahtar] = ceviri
            }
            motorAdi = motorAdi == "güncel"
                ? "Grok (artıklar)" : motorAdi + " +Grok"
        }
    }

    // Yeni çeviriler kalıcı hafızaya: bir daha asla motora gitmesinler
    for b in hedefler {
        if let c = bellek[b.anahtar], !c.isEmpty {
            CeviriHafizasi.paylasilan.yaz(b.anahtar, c, dil: ayarlar.hedefDil)
        }
    }
    // Sınırsız büyümesin: uzun oturumda bellek şişiyordu
    if bellek.count > 3000 {
        var kirpik: [String: String] = [:]
        for (k, v) in bellek.suffix(2000) { kirpik[k] = v }
        for b in hedefler { if let c = bellek[b.anahtar] { kirpik[b.anahtar] = c } }
        bellek = kirpik
    }
    for b in hedefler { b.ceviri = bellek[b.anahtar] }
    return (motorAdi, bellek)
}

// MARK: - OCR

// Mesaj saati (14:32, 9.05, 12-37 vb.) — Çeviriye girmemeli ve birleşik satırları BÖLMELİ!
let icSaatDeseni = try? NSRegularExpression(pattern: "\\d{1,2}[:.-]\\d{2}")

// OCR öncesi büyütme — ölçüldü (araştırma): 1x'te karakter hata oranı
// %0.50, 3x'te %0.16; tam doğru satır %88.5 → %96.6. "Harf düşmesi"
// şikayetinin birincil nedeni küçük metin çözünürlüğü.
let ocrCIBaglam = CIContext(options: [.cacheIntermediates: false])

func ocrIcinBuyut(_ g: CGImage, _ istenen: CGFloat) -> CGImage {
    let ham = Double(g.width) * Double(g.height)
    let tavan = CGFloat((12_000_000 / max(1, ham)).squareRoot())  // bellek tavanı
    let k = min(istenen, tavan)
    guard k > 1.15 else { return g }
    let ci = CIImage(cgImage: g)
    guard let f = CIFilter(name: "CILanczosScaleTransform") else { return g }
    f.setValue(ci, forKey: kCIInputImageKey)
    f.setValue(k, forKey: kCIInputScaleKey)
    f.setValue(1.0, forKey: kCIInputAspectRatioKey)
    guard let c = f.outputImage,
          let yeni = ocrCIBaglam.createCGImage(c, from: c.extent) else { return g }
    return yeni
}

/// Yakalama ölçeğinden hedef büyütme (13pt metin ~34px'e tamamlanır).
func ocrBuyutmeHesapla(olcek: CGFloat) -> CGFloat {
    max(1, min(3, 34.0 / max(1, 13.0 * olcek)))
}

func ocrYap(_ goruntu: CGImage, diller: [String],
            buyutme: CGFloat = 1) throws -> [(String, CGRect)] {
    let goruntu = ocrIcinBuyut(goruntu, buyutme)
    let isleyici = VNImageRequestHandler(cgImage: goruntu, options: [:])
    let istek = VNRecognizeTextRequest()
    istek.recognitionLevel = .accurate
    istek.usesLanguageCorrection = true
    // Otomatik dil algılama KAPALI: Vision satır satır dil değiştirip
    // "bis" → "biz" gibi hatalar üretiyordu (araştırma bulgusu).
    // Almanca sabitlemek (de-CH ayrı model yok) hem lehçeyi hem
    // Latin alfabesindeki diğer kelimeleri doğru okur.
    istek.recognitionLanguages = diller
    // Satır başına otomatik dil algılama: 5 dilli sabit liste tanımayı
    // bulandırıyordu (umlaut ve harf düşmeleri → Grok'a bile kırık metin
    // gidiyordu). Liste artık yalnız ipucu görevi görür.
    try isleyici.perform([istek])
    var sonuc: [(String, CGRect)] = []
    for gozlem in istek.results ?? [] {
        guard let aday = gozlem.topCandidates(1).first else { continue }
        let tamMetin = aday.string
        
        let nsTam = NSRange(tamMetin.startIndex..<tamMetin.endIndex, in: tamMetin)
        let matches = icSaatDeseni?.matches(in: tamMetin, options: [], range: nsTam) ?? []
        
        if matches.isEmpty {
            let trm = tamMetin.trimmingCharacters(in: .whitespaces)
            if !trm.isEmpty { sonuc.append((trm, gozlem.boundingBox)) }
        } else {
            var lastIndex = tamMetin.startIndex
            for match in matches {
                if let matchRange = Range(match.range, in: tamMetin) {
                    let beforeText = String(tamMetin[lastIndex..<matchRange.lowerBound]).trimmingCharacters(in: .whitespaces)
                    if !beforeText.isEmpty {
                        // Parçanın ekrandaki fiziksel yerini bul
                        if let partRange = tamMetin.range(of: beforeText, range: lastIndex..<matchRange.lowerBound),
                           let dar = (try? aday.boundingBox(for: partRange)) ?? nil {
                            sonuc.append((beforeText, dar.boundingBox))
                        } else {
                            sonuc.append((beforeText, gozlem.boundingBox))
                        }
                    }
                    // Zaman damgasını atlıyoruz (hiçbir bloğa eklenmiyor)
                    lastIndex = matchRange.upperBound
                }
            }
            // En sondaki saatten sonra kalan metin
            let afterText = String(tamMetin[lastIndex..<tamMetin.endIndex]).trimmingCharacters(in: .whitespaces)
            if !afterText.isEmpty {
                if let partRange = tamMetin.range(of: afterText, range: lastIndex..<tamMetin.endIndex),
                   let dar = (try? aday.boundingBox(for: partRange)) ?? nil {
                    sonuc.append((afterText, dar.boundingBox))
                } else {
                    sonuc.append((afterText, gozlem.boundingBox))
                }
            }
        }
    }
    return sonuc
}

// MARK: - Blok gruplama

struct OCRSatiri {
    let metin: String
    var rect: CGRect   // bölge-yerel, sol-ÜST orijinli, punto
}

final class Blok {
    var satirlar: [OCRSatiri]
    var rect: CGRect
    var ceviri: String?
    var benim = false          // balon sağ yarıda mı (kullanıcının mesajı)
    var atla = false           // sohbet balonu değil (tarih çipi, sistem
                               // bildirimi, kişi kartı): çevirme, yama yapma
    var hedef: Bool { cevrilebilir && !atla }

    init(_ s: OCRSatiri) { satirlar = [s]; rect = s.rect }

    func ekle(_ s: OCRSatiri) { satirlar.append(s); rect = rect.union(s.rect) }

    var metin: String { satirlar.map { $0.metin }.joined(separator: " ") }

    lazy var anahtar: String = anahtarla(metin)

    var satirYuksekligi: CGFloat {
        satirlar.map { $0.rect.height }.reduce(0, +) / CGFloat(satirlar.count)
    }

    var cevrilebilir: Bool { metin.contains(where: { $0.isLetter }) }

    var sagaYasli: Bool {
        guard satirlar.count >= 2 else { return false }
        let saglar = satirlar.map { $0.rect.maxX }
        let sollar = satirlar.map { $0.rect.minX }
        return (saglar.max()! - saglar.min()!) * 2 < (sollar.max()! - sollar.min()!)
    }
}

/// Döner: (çevrilecek bloklar, sessiz kırıntı kutuları).
/// Sessiz kırıntılar = harf içermeyen satırlar (saat, sayı, emoji): çevrilmez
/// ama bir balonun yamasıyla kesişiyorsa yamaya YUTULUR — balon içinde açıkta
/// ":02", "dk." gibi artıklar kalmasın.
func bloklaraAyir(_ satirlar: [(String, CGRect)],
                  boyut: CGSize) -> ([Blok], [CGRect]) {
    var sessizler: [CGRect] = []
    var yerel: [OCRSatiri] = satirlar.compactMap { (metin, nb) in
        let r = CGRect(
            x: nb.origin.x * boyut.width,
            y: (1 - nb.origin.y - nb.height) * boyut.height,
            width: max(1, nb.width * boyut.width),
            height: max(1, nb.height * boyut.height))
        guard metin.contains(where: { $0.isLetter }) else {
            sessizler.append(r)
            return nil
        }
        return OCRSatiri(metin: metin, rect: r)
    }
    yerel.sort {
        let fark = $0.rect.minY - $1.rect.minY
        return abs(fark) < 3 ? $0.rect.minX < $1.rect.minX : fark < 0
    }

    // WhatsApp özeli: sol / sağ tarafı belirleyip farklı taraftaki satırları
    // ASLA aynı bloğa koyma. Aynı taraf için de satırlar arası boşluk
    // satır yüksekliğinin %80'ınden fazlaysa yeni balon kabul et.
    var bloklar: [Blok] = []
    for satir in yerel {
        let satirSol = satir.rect.midX < boyut.width * 0.52
        var eklendi = false
        for blok in bloklar.suffix(6).reversed() {
            let blokSol = blok.rect.midX < boyut.width * 0.52
            guard satirSol == blokSol else { continue }

            let son = blok.satirlar.last!.rect
            let avgYukseklik = (son.height + satir.rect.height) / 2
            let dikeyBosluk = satir.rect.minY - son.maxY

            // WhatsApp'ta aynı kişi art arda mesaj atarsa baloncuklar arası dikey boşluk azdır.
            // Bu boşluk bazen %30 bazen %50 olabilir. Kırpışmayı ve kararsız bölünmeyi önlemek için
            // eşiği %85'e çıkarıyoruz. Böylece alt alta gelen aynı kişinin mesajları güvenle tek blok olur.
            // Saat damgalarını zaten sildiğimiz için farklı mesajların birleşmesi sorun yaratmaz.
            guard dikeyBosluk > -son.height * 0.3,
                  dikeyBosluk < avgYukseklik * 0.5 else { continue }
            // %85 eşiği art arda gelen AYRI balonları tek bloğa
            // yapıştırıyordu; balon içi satır aralığı %40'ı geçmez

            let xFarki = abs(satir.rect.minX - blok.satirlar.first!.rect.minX)
            guard xFarki < boyut.width * 0.10 else { continue }

            blok.ekle(satir)
            eklendi = true
            break
        }
        if !eklendi { bloklar.append(Blok(satir)) }
    }
    for blok in bloklar {
        blok.benim = blok.rect.midX > boyut.width * 0.52
        // Sohbet balonları ya sola ya sağa yaslıdır. ORTALANMIŞ bloklar
        // (tarih çipi, uçtan uca şifreleme bildirimi, kişi kartı, sistem
        // mesajı) sohbet değildir: çevrilmez, olduğu gibi görünür.
        let solda = blok.rect.minX < boyut.width * 0.22
        let sagda = blok.rect.maxX > boyut.width * 0.78
        // Yalnız DAR ve ortalanmış bloklar sistem öğesidir; geniş blok her
        // zaman çevrilir (dar bölge seçiminde geniş balon yanlışlıkla
        // "ortalanmış" görünüp çevirisiz kalıyordu)
        blok.atla = !solda && !sagda && blok.rect.width < boyut.width * 0.5
    }
    return (bloklar, sessizler)
}

// MARK: - Ekran yakalama

func cgKoordinat(_ r: NSRect) -> CGRect {
    let anaYukseklik = NSScreen.screens.first?.frame.height ?? 0
    return CGRect(x: r.origin.x, y: anaYukseklik - r.origin.y - r.height,
                  width: r.width, height: r.height)
}

func bolgeyiYakala(_ cg: CGRect) throws -> URL {
    let yol = FileManager.default.temporaryDirectory
        .appendingPathComponent("ekran_ceviri_\(getpid()).png")
    let p = Process()
    p.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
    p.arguments = ["-x", "-R\(Int(cg.origin.x)),\(Int(cg.origin.y)),"
                 + "\(Int(cg.width)),\(Int(cg.height))", yol.path]
    try p.run()
    p.waitUntilExit()
    guard FileManager.default.fileExists(atPath: yol.path) else {
        throw NSError(domain: "yakala", code: 1, userInfo: [
            NSLocalizedDescriptionKey: "Ekran görüntüsü alınamadı"])
    }
    return yol
}

var sinamaIstisnaPencere: CGWindowID?

func sckFiltreKur(_ bolgeCG: CGRect) throws -> (SCContentFilter, SCDisplay) {
    let bekleyici = DispatchSemaphore(value: 0)
    var icerik: SCShareableContent?
    var hata: Error?
    SCShareableContent.getExcludingDesktopWindows(
        false, onScreenWindowsOnly: true) { c, e in
        icerik = c; hata = e; bekleyici.signal()
    }
    bekleyici.wait()
    if let hata = hata { throw hata }
    guard let icerik = icerik,
          let ekran = icerik.displays.first(where: {
              $0.frame.contains(CGPoint(x: bolgeCG.midX, y: bolgeCG.midY))
          }) ?? icerik.displays.first else {
        throw NSError(domain: "sck", code: 1, userInfo: [
            NSLocalizedDescriptionKey: "Paylaşılabilir ekran bulunamadı"])
    }
    let biz = icerik.applications.filter { $0.processID == getpid() }
    // Sınama modu: sahte sohbet penceresi bizim ama yakalamada GÖRÜNMELİ
    var istisnalar: [SCWindow] = []
    if let no = sinamaIstisnaPencere,
       let w = icerik.windows.first(where: { $0.windowID == no }) {
        istisnalar = [w]
    }
    let filtre = SCContentFilter(display: ekran, excludingApplications: biz,
                                 exceptingWindows: istisnalar)
    return (filtre, ekran)
}

func sckYakala(filtre: SCContentFilter, ekran: SCDisplay,
               bolgeCG: CGRect, olcek: CGFloat) throws -> CGImage {
    let ayar = SCStreamConfiguration()
    ayar.sourceRect = CGRect(x: bolgeCG.minX - ekran.frame.minX,
                             y: bolgeCG.minY - ekran.frame.minY,
                             width: bolgeCG.width, height: bolgeCG.height)
    ayar.width = Int(bolgeCG.width * olcek)
    ayar.height = Int(bolgeCG.height * olcek)
    ayar.showsCursor = false
    let bekleyici = DispatchSemaphore(value: 0)
    var goruntu: CGImage?
    var hata: Error?
    SCScreenshotManager.captureImage(contentFilter: filtre,
                                     configuration: ayar) { g, e in
        goruntu = g; hata = e; bekleyici.signal()
    }
    bekleyici.wait()
    if let hata = hata { throw hata }
    guard let goruntu = goruntu else {
        throw NSError(domain: "sck", code: 2, userInfo: [
            NSLocalizedDescriptionKey: "SCK görüntü vermedi"])
    }
    return goruntu
}

/// yedekKullan=false (canlı mod): SCK çökerse screencapture yedeğine ASLA
/// düşülmez — screencapture kendi çeviri katmanımızı da çeker ve uygulama
/// kendi çevirisini "yeni mesaj" sanıp sonsuz döngüye girer.
func bolgeGoruntusu(_ cgBolge: CGRect,
                    yakalayici: (SCContentFilter, SCDisplay)?,
                    olcek: CGFloat,
                    yedekKullan: Bool = true) -> CGImage? {
    if let (f, e) = yakalayici,
       let g = try? sckYakala(filtre: f, ekran: e, bolgeCG: cgBolge,
                              olcek: olcek) {
        return g
    }
    guard yedekKullan else { return nil }
    guard let dosya = try? bolgeyiYakala(cgBolge),
          let veri = try? Data(contentsOf: dosya),
          let temsil = NSBitmapImageRep(data: veri) else { return nil }
    try? FileManager.default.removeItem(at: dosya)
    return temsil.cgImage
}

func goruntuIzi(_ goruntu: CGImage) -> [UInt8] {
    let boy = 32
    var piksel = [UInt8](repeating: 0, count: boy * boy)
    piksel.withUnsafeMutableBytes { tampon in
        if let baglam = CGContext(
                data: tampon.baseAddress, width: boy, height: boy,
                bitsPerComponent: 8, bytesPerRow: boy,
                space: CGColorSpaceCreateDeviceGray(),
                bitmapInfo: CGImageAlphaInfo.none.rawValue) {
            baglam.interpolationQuality = .low
            baglam.draw(goruntu, in: CGRect(x: 0, y: 0, width: boy, height: boy))
        }
    }
    return piksel
}

func izFarki(_ a: [UInt8], _ b: [UInt8]) -> Double {
    guard a.count == b.count, !a.isEmpty else { return 1 }
    var toplam = 0
    for i in 0..<a.count { toplam += abs(Int(a[i]) - Int(b[i])) }
    return Double(toplam) / Double(a.count) / 255.0
}

// MARK: - Yerel sohbet hafızası (hafif RAG)
// Her yeni mesaj diske yazılır. Cevap önerisi istenince: kullanıcının geçmiş
// cevapları üslup örneği olur; son gelen mesajla kelime kesişimi en yüksek
// eski konuşmalar bağlam olarak eklenir. Hafıza büyüdükçe öneriler kişiselleşir.

func gecmiseYaz(kim: String, metin: String, ceviri: String?) {
    let kayit: [String: Any] = [
        "t": Int(Date().timeIntervalSince1970), "kim": kim,
        "metin": metin, "ceviri": ceviri ?? "",
    ]
    guard let veri = try? JSONSerialization.data(withJSONObject: kayit) else { return }
    try? FileManager.default.createDirectory(
        at: destekDizini, withIntermediateDirectories: true)
    if !FileManager.default.fileExists(atPath: gecmisURL.path) {
        FileManager.default.createFile(atPath: gecmisURL.path, contents: nil)
    }
    if let dosya = try? FileHandle(forWritingTo: gecmisURL) {
        dosya.seekToEndOfFile()
        dosya.write(veri)
        dosya.write(Data("\n".utf8))
        try? dosya.close()
    }
}

func gecmisOku(son: Int = 500) -> [[String: Any]] {
    guard let icerik = try? String(contentsOf: gecmisURL, encoding: .utf8)
    else { return [] }
    return icerik.split(separator: "\n").suffix(son).compactMap {
        (try? JSONSerialization.jsonObject(with: Data($0.utf8))) as? [String: Any]
    }
}

func uslupOrnekleri(_ kayitlar: [[String: Any]], adet: Int = 12) -> [String] {
    var gorulen = Set<String>()
    var ornekler: [String] = []
    for k in kayitlar.reversed() where (k["kim"] as? String) == "ben" {
        guard let m = k["metin"] as? String, !m.isEmpty else { continue }
        let a = anahtarla(m)
        if gorulen.insert(a).inserted {
            ornekler.append(m)
            if ornekler.count >= adet { break }
        }
    }
    return ornekler
}

func benzerGecmis(_ kayitlar: [[String: Any]], sorgu: String,
                  adet: Int = 3) -> [String] {
    let sorguKelimeleri = Set(sorgu.lowercased()
        .split(whereSeparator: { !$0.isLetter }).map(String.init)
        .filter { $0.count > 3 })
    guard !sorguKelimeleri.isEmpty else { return [] }
    var ciftler: [(Int, String)] = []
    for i in 0..<kayitlar.count {
        guard (kayitlar[i]["kim"] as? String) == "karsi",
              let gelen = kayitlar[i]["metin"] as? String else { continue }
        let kelimeler = Set(gelen.lowercased()
            .split(whereSeparator: { !$0.isLetter }).map(String.init))
        let skor = sorguKelimeleri.intersection(kelimeler).count
        guard skor > 0 else { continue }
        // Bu gelen mesajı izleyen ilk "ben" cevabını bul
        for j in (i + 1)..<min(i + 4, kayitlar.count)
        where (kayitlar[j]["kim"] as? String) == "ben" {
            if let cevap = kayitlar[j]["metin"] as? String {
                ciftler.append((skor, "KARŞI: \(gelen)\nBEN: \(cevap)"))
            }
            break
        }
    }
    return ciftler.sorted { $0.0 > $1.0 }.prefix(adet).map { $0.1 }
}

/// Sohbete uygun cevap önerisi: [(öneri, Türkçe anlamı)]
func grokOneri(dokum: [String], ayarlar: Ayarlar,
               farkliOlsun: Bool) throws -> [(String, String)] {
    let kayitlar = gecmisOku()
    let uslup = uslupOrnekleri(kayitlar)
    let sonGelen = dokum.last(where: { $0.hasPrefix("KARŞI:") }) ?? ""
    let benzer = benzerGecmis(kayitlar, sorgu: sonGelen)

    var sistem = """
    Sen kullanıcının yerine yazan bir sohbet asistanısın. Karşı taraf \
    \(ayarlar.kaynakDilAdi) yazıyor; kullanıcı da AYNI dilde cevap veriyor.
    Görev: sohbetin gidişatına uygun, doğal, samimi 3 FARKLI alternatif \
    önerisi yaz (örn: 1. Kısa/Onaylayıcı, 2. Detaylı, 3. Farklı bir yaklaşım) — \(ayarlar.kaynakDilAdi) ile.
    """
    if !ayarlar.kisilik.isEmpty {
        sistem += "\n\nKullanıcının kimliği/durumu: \(ayarlar.kisilik)"
    }
    if !uslup.isEmpty {
        sistem += "\n\nKullanıcının geçmiş mesajlarından üslup örnekleri "
                + "(bu tona benzet):\n- " + uslup.joined(separator: "\n- ")
    }
    if !benzer.isEmpty {
        sistem += "\n\nBenzer geçmiş konuşmalar:\n" + benzer.joined(separator: "\n---\n")
    }
    sistem += "\n\nSADECE şu JSON'u döndür, başka hiçbir şey yazma:\n"
            + "{\"oneriler\": [{\"cevap\": \"öneri 1\", \"turkce\": \"anlam 1\"}, {\"cevap\": \"öneri 2\", \"turkce\": \"anlam 2\"}, {\"cevap\": \"öneri 3\", \"turkce\": \"anlam 3\"}]}"

    let icerik = citCizgileriniAt(try grokIstek([
        ["role": "system", "content": sistem],
        ["role": "user", "content": dokum.suffix(15).joined(separator: "\n")],
    ], ayarlar: ayarlar, sicaklik: 0.8))
    guard let d = (try? JSONSerialization.jsonObject(with: Data(icerik.utf8)))
            as? [String: Any],
          let oneriler = d["oneriler"] as? [[String: Any]] else {
        // JSON gelmediyse ham metni önü olarak kullan
        return [(icerik, "")]
    }
    return oneriler.prefix(3).map { o in
        (o["cevap"] as? String ?? "", o["turkce"] as? String ?? "")
    }
}

// MARK: - Yazdığımı Çevir (Alfred akışının yerleşik hâli, ⌃⌥C)

/// Alfred script'indeki format_output kuralları: noktalama yok, hepsi
/// küçük, yalnız ilk harf büyük.
func gidenFormatla(_ metin: String) -> String {
    var t = metin
    for isaret in [".", ",", "?", "!", ";", ":", "-", "_", "\"", "'",
                   "(", ")"] {
        t = t.replacingOccurrences(of: isaret, with: "")
    }
    t = t.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
    guard let ilk = t.first else { return t }
    return String(ilk).uppercased() + t.dropFirst()
}

/// Kullanıcının Türkçe yazdığını, karşı tarafa gidecek dilde ve üslupta
/// mesaja çevirir (Grok, kullanıcının anahtarıyla).
/// İki anahtar arasındaki düzenleme mesafesi (erken çıkışlı).
/// OCR aynı balonu iki karede "trffe"/"träffe" okuyunca önbellek ıskalıyor,
/// mesaj boşuna yeniden çevriliyordu.
func mesafeAzMi(_ a: String, _ b: String, enFazla: Int) -> Bool {
    if a == b { return true }
    let x = Array(a), y = Array(b)
    if abs(x.count - y.count) > enFazla { return false }
    var onceki = Array(0...y.count)
    var simdiki = [Int](repeating: 0, count: y.count + 1)
    for i in 1...x.count {
        simdiki[0] = i
        var satirEnAz = i
        for j in 1...y.count {
            let bedel = x[i - 1] == y[j - 1] ? 0 : 1
            simdiki[j] = min(onceki[j] + 1, simdiki[j - 1] + 1,
                             onceki[j - 1] + bedel)
            satirEnAz = min(satirEnAz, simdiki[j])
        }
        if satirEnAz > enFazla { return false }   // erken çıkış
        swap(&onceki, &simdiki)
    }
    return onceki[y.count] <= enFazla
}

/// Önbellekte birebir yoksa OCR titremesi toleranslı eşleşme arar.
func bulanikBul(_ anahtar: String, _ bellek: [String: String]) -> String? {
    guard anahtar.count >= 14 else { return nil }   // kısa metinde riskli
    let tolerans = max(1, min(3, anahtar.count / 12))
    var bakilan = 0
    for (k, v) in bellek where abs(k.count - anahtar.count) <= tolerans {
        bakilan += 1
        if bakilan > 300 { break }                  // maliyet sınırı
        if !v.isEmpty, mesafeAzMi(k, anahtar, enFazla: tolerans) { return v }
    }
    return nil
}

/// KALICI YEREL ÇEVİRİ HAFIZASI
/// Her çeviri diske yazılır; yeni bir mesaj geldiğinde ÖNCE buraya bakılır.
/// Aynı cümle bir daha asla motora gitmez (anında + ücretsiz). Uygulama
/// kapansa da hafıza kalır. Dosya 0600, yalnız bu kullanıcıya okunur.
final class CeviriHafizasi {
    static let paylasilan = CeviriHafizasi()
    private let kilit = NSLock()
    private var kayitlar: [String: [String: String]] = [:]   // dil → (anahtar → çeviri)
    private var sira: [String] = []                          // yaşlandırma için
    private var kirli = false
    private let sinir = 20_000
    private var url: URL { destekDizini.appendingPathComponent("ceviri_hafizasi.json") }

    func yukle() {
        guard let veri = try? Data(contentsOf: url),
              let d = try? JSONSerialization.jsonObject(with: veri)
                as? [String: Any] else { return }
        kilit.lock(); defer { kilit.unlock() }
        kayitlar = (d["kayitlar"] as? [String: [String: String]]) ?? [:]
        sira = (d["sira"] as? [String]) ?? []
        NSLog("EC-hafiza: \(sira.count) çeviri yüklendi")
    }

    /// Birebir, yoksa OCR titremesi toleranslı arama.
    func ara(_ anahtar: String, dil: String) -> String? {
        kilit.lock(); defer { kilit.unlock() }
        guard let dilKayit = kayitlar[dil] else { return nil }
        if let c = dilKayit[anahtar], !c.isEmpty { return c }
        return bulanikBul(anahtar, dilKayit)
    }

    func yaz(_ anahtar: String, _ ceviri: String, dil: String) {
        guard !anahtar.isEmpty, !ceviri.isEmpty else { return }
        kilit.lock(); defer { kilit.unlock() }
        if kayitlar[dil]?[anahtar] == ceviri { return }
        kayitlar[dil, default: [:]][anahtar] = ceviri
        sira.append("\(dil)|\(anahtar)")
        kirli = true
        if sira.count > sinir {                    // en eskileri at
            for bilesik in sira.prefix(sinir / 4) {
                let p = bilesik.split(separator: "|", maxSplits: 1)
                if p.count == 2 { kayitlar[String(p[0])]?[String(p[1])] = nil }
            }
            sira.removeFirst(sinir / 4)
        }
    }

    /// Diske yaz (yalnız değişiklik varsa).
    func kaydet() {
        kilit.lock()
        guard kirli else { kilit.unlock(); return }
        let d: [String: Any] = ["kayitlar": kayitlar, "sira": sira]
        kirli = false
        kilit.unlock()
        try? FileManager.default.createDirectory(
            at: destekDizini, withIntermediateDirectories: true)
        guard let veri = try? JSONSerialization.data(withJSONObject: d)
        else { return }
        try? veri.write(to: url)
        try? FileManager.default.setAttributes(
            [.posixPermissions: 0o600], ofItemAtPath: url.path)
    }

    func temizle() {
        kilit.lock()
        kayitlar = [:]; sira = []; kirli = false
        kilit.unlock()
        try? FileManager.default.removeItem(at: url)
    }

    var sayi: Int { kilit.lock(); defer { kilit.unlock() }; return sira.count }
}

/// Seçili dil modunun modele verilecek tanımı.
func modTanimi(_ ayarlar: Ayarlar) -> String {
    switch ayarlar.dilModu {
    case "isvicre":
        return "Karşı taraf İsviçre'de konuşulan Almanca lehçelerinden "
             + "biriyle yazıyor (Züridütsch, Bärndütsch, Baseldytsch, "
             + "Ostschwyzerdütsch, Wallisertitsch)."
    case "otomatik":
        return "Karşı taraf herhangi bir dilde yazabilir; dili kendin algıla."
    default:   // alman modu (varsayılan)
        return "ALMAN MODU: Karşı taraf Almanya, Avusturya veya İsviçre'de "
             + "konuşulan Almanca varyantlarından biriyle yazıyor — standart "
             + "Almanca (Hochdeutsch), Bavyera/Avusturya, Kuzey Almanya, Ren "
             + "bölgesi ya da İsviçre lehçeleri (Züridütsch, Bärndütsch, "
             + "Baseldytsch, Ostschwyzerdütsch, Wallisertitsch). Hangisi "
             + "olduğunu metinden ALGILA ve ona göre çöz."
    }
}

/// Kimlik/cinsiyet bağlamı: hitap, sıfat çekimi ve ton için.
func kimlikTanimi(_ ayarlar: Ayarlar) -> String {
    func ad(_ k: String) -> String {
        k == "kadin" ? "kadın" : (k == "erkek" ? "erkek" : "belirtilmemiş")
    }
    let ben = ad(ayarlar.benCinsiyet), karsi = ad(ayarlar.karsiCinsiyet)
    guard ben != "belirtilmemiş" || karsi != "belirtilmemiş" else { return "" }
    return "\nKİMLİK: Yazan kişi (kullanıcı) bir \(ben), karşı taraf bir "
         + "\(karsi). Hitap, sıfat çekimi ve tonu buna göre seç "
         + "(ör. bir \(karsi)e yazan bir \(ben) gibi)."
}

/// Çeviride hâlâ Almanca/lehçe kelime kaldıysa çeviri EKSİKTİR
/// (kullanıcı şikayeti: "bazen tam çeviremiyor").
let almancaIsaretler: Set<String> = [
    "ich", "isch", "ist", "nicht", "nöd", "nid", "nit", "und", "aber",
    "der", "die", "das", "mit", "für", "auch", "noch", "schon", "wenn",
    "mues", "muss", "chli", "gsi", "hesch", "chunnsch", "morn", "hüt",
    "zit", "zyt", "wärche", "schaffe", "gäll", "eus", "mir", "dir",
    "vill", "viel", "geht", "gaht", "kommt", "chunnt", "machen", "mache",
]

func almancaKalintiVar(_ ceviri: String) -> Bool {
    let kelimeler = ceviri.lowercased()
        .split(whereSeparator: { !$0.isLetter }).map(String.init)
    guard kelimeler.count >= 2 else { return false }
    let kalinti = kelimeler.filter { almancaIsaretler.contains($0) }.count
    // 2+ Almanca kelime ya da kelimelerin üçte biri → eksik çeviri
    return kalinti >= 2 || (kalinti >= 1 && kalinti * 3 >= kelimeler.count)
}

/// Giden mesajda Türkçe kelime kaldıysa çeviri başarısızdır (ölçüldü:
/// model "tamam/canım" gibi kelimeleri olduğu gibi bırakabiliyor).
func turkceKalintiVar(_ s: String) -> Bool {
    if s.contains("ğ") || s.contains("ş") || s.contains("ı")
        || s.contains("İ") { return true }
    let tr: Set<String> = [
        "tamam", "canim", "canım", "seni", "sen", "ben", "icin", "için",
        "cok", "çok", "gorusuruz", "görüşürüz", "evet", "hayir", "hayır",
        "ama", "simdi", "şimdi", "lazim", "lazım", "olur", "tabii",
        "merhaba", "selam", "tesekkur", "teşekkür", "biraz", "sonra",
        "yapacagim", "yapacağım", "bugun", "bugün", "yarin", "yarın",
    ]
    let kelimeler = Set(s.lowercased()
        .split(whereSeparator: { !$0.isLetter }).map(String.init))
    return !kelimeler.isDisjoint(with: tr)
}

/// Hedef lehçede birkaç örnek: model doğru yazımı taklit etsin.
/// Hedef varyantta birkaç örnek: model doğru yazımı ve yaygın ifadeyi
/// taklit etsin. (Hochdeutsch/Bayrisch için Zürih örneği göstermek modeli
/// yanlış varyanta itiyordu — her varyantın kendi örneği var.)
func gidenOrnekler(_ kisa: String) -> String {
    switch kisa {
    case "Bärndütsch":
        return "- \"tamam görüşürüz\" → \"guet bis spöter\"\n"
             + "- \"müsaitim\" → \"i ha ziit\"\n"
             + "- \"biraz çalışmam lazım\" → \"i mues no chli wärche\""
    case "Baseldytsch":
        return "- \"tamam görüşürüz\" → \"guet bis spöter\"\n"
             + "- \"müsaitim\" → \"y ha zyt\"\n"
             + "- \"biraz çalışmam lazım\" → \"y mues no e bitz schaffe\""
    case "Wallis":
        return "- \"tamam görüşürüz\" → \"guet bis spääter\"\n"
             + "- \"müsaitim\" → \"ich ha ziit\""
    case "Hochdeutsch":
        return "- \"tamam görüşürüz\" → \"okay bis später\"\n"
             + "- \"müsaitim\" → \"ich hab zeit\"\n"
             + "- \"biraz çalışmam lazım\" → \"ich muss noch bisschen "
             + "arbeiten\""
    case "Bayrisch":
        return "- \"tamam görüşürüz\" → \"passt, bis später\"\n"
             + "- \"müsaitim\" → \"i hob zeit\"\n"
             + "- \"biraz çalışmam lazım\" → \"i muass no a bissl "
             + "schaffn\""
    case "Norddeutsch":
        return "- \"tamam görüşürüz\" → \"jo bis später\"\n"
             + "- \"müsaitim\" → \"ich hab zeit\"\n"
             + "- \"biraz çalışmam lazım\" → \"muss noch n bisschen "
             + "schaffen\""
    default:   // Züridütsch ve genel İsviçre
        return "- \"tamam görüşürüz\" → \"okey bis spöter\"\n"
             + "- \"müsaitim\" → \"ich ha zit\"\n"
             + "- \"biraz çalışmam lazım\" → \"ich mues no chli schaffe\""
    }
}

/// Kullanıcının Türkçe yazdığını, O ANKİ SOHBETTE konuşulan lehçeye ve
/// karşı tarafın yazım tarzına uygun bir mesaja çevirir.
/// - ornekler: ekrandaki karşı taraf mesajları (lehçe + tarz kaynağı)
func girdiCevir(_ turkce: String, ayarlar: Ayarlar,
                ornekler: [String] = []) throws -> String {
    // grok = lehçe + karakter (LLM) | bing = çevrimiçi, standart dil
    if ayarlar.gidenMotor == "bing" {
        guard let ceviri = try bingCevir([turkce],
                                         hedef: ayarlar.kaynakDilKodu,
                                         kaynak: "tr").first,
              !ceviri.isEmpty else {
            throw NSError(domain: "giden", code: 2, userInfo: [
                NSLocalizedDescriptionKey: "Bing çevirisi başarısız"])
        }
        return gidenFormatla(ceviri)
    }
    // Hedef lehçe SABİT DEĞİL: ekrandaki sohbetten algılanır. Karşı taraf
    // Bärndütsch yazıyorsa cevap da Bärndütsch olur.
    let algi = ornekler.isEmpty
        ? (ad: ayarlar.kaynakDilAdi, kisa: "Züridütsch")
        : lehceyiAlgila(ornekler)
    let hedefLehce = algi.ad
    var sistem = """
    Sen Almanca'nın tüm bölgesel varyantlarında (Almanya, Avusturya, \
    İsviçre) uzman bir çevirmensin. Kullanıcının Türkçe mesajını, karşı \
    tarafa gidecek doğal bir WhatsApp mesajı olarak ŞU VARYANTTA yaz: \
    \(hedefLehce).\(kimlikTanimi(ayarlar))

    TON: Mesajı, karşı tarafın SON mesajlarındaki ton ve açıklık \
    seviyesiyle EŞLEŞTİR (samimi/flörtöz/cinsel/ciddi neyse o). \
    \(ayarlar.yetiskin ? "İçerik +18 olabilir; sansürleme, yumuşatma." : "")

    GİRDİ TÜRKÇEDİR ve kullanıcı NOKTALAMA KULLANMAZ: cümleler birleşik, \
    virgülsüz, noktasız gelir ve yazım hatası içerebilir.
    ÖNCE zihninde cümleyi çöz: nerede bitip nerede başladığını, soru mu \
    ifade mi olduğunu, hangi kelimenin hangi cümleye ait olduğunu belirle; \
    yazım hatalarını düzelt. SONRA o lehçede SIFIRDAN yaz.
    Örnek: "tamam görüşürüz o zaman canım ben de biraz çalışacağım sonra \
    yazarım sana" → iki ayrı düşünce: (1) tamam, sonra görüşürüz (2) biraz \
    çalışacağım, sonra yazarım. İkisini de doğal biçimde aktar.
    Çıktıda TEK BİR Türkçe kelime bile kalmamalı; Türkçe harf (ı, ş, ğ) \
    geçmemeli. Kelime kelime çevirme, anlamı aktar.

    ÖRNEKLER (\(algi.kisa)):
    \(gidenOrnekler(algi.kisa))

    Kurallar:
    - Metni tam olarak çevir, anlamı yumuşatma veya değiştirme, ekleme yapma.
    - KELİME UYDURMA: emin olmadığın bir biçimi kullanma; o varyantta \
    gerçekten konuşulan yaygın ifadeyi seç (ör. "müsaitim" → "i ha ziit" / \
    "es passt mir", uydurma bir kelime değil).
    - HEDEF LEHÇEYE SADIK KAL: standart Almanca yazma; o bölgenin gerçek
      yazım alışkanlığını kullan (Zürih: nöd/ez/chli, Bern: nid/itz/gäng/u,
      Basel: nit/zyt/vyl, Wallis: ischt/wier).
    - Sadece mesajın en başındaki ilk harf büyük, geri kalan tümü küçük.
    - HİÇBİR noktalama işareti kullanma (nokta, virgül, soru işareti vb.) — \
    gerçek WhatsApp yazışması gibi görünmeli.
    - Samimi, günlük WhatsApp üslubu; kullanıcının yazdığı emojileri koru\
    \(ayarlar.emojiSerbest
        ? " ve tona uygun düşüyorsa 1 emoji ekleyebilirsin." : ".")
    - SADECE çevrilmiş metni ver; açıklama, dil etiketi, not ekleme.
    """
    if !ornekler.isEmpty {
        // Karşı tarafın gerçek mesajları: yazımını ve tonunu birebir taklit et
        let son = ornekler.suffix(6).joined(separator: "\n- ")
        sistem += "\n\nKARŞI TARAFIN GERÇEK MESAJLARI (yazım tarzını, "
                + "kısaltmalarını ve tonunu BUNLARA benzet):\n- \(son)"
    }
    let karakter = ayarlar.gidenKarakter.isEmpty
        ? ayarlar.kisilik : ayarlar.gidenKarakter
    if !karakter.isEmpty {
        sistem += "\n\nKULLANICININ KARAKTER TANIMI — mesajı bu kişi "
                + "yazıyormuş gibi, bu üslupla yaz:\n\(karakter)"
    }
    var mesajlar: [[String: String]] = [
        ["role": "system", "content": sistem],
        ["role": "user", "content": turkce],
    ]
    var yanit = try grokIstek(mesajlar, ayarlar: ayarlar, sicaklik: 0.4)
    // KALİTE KAPISI: Türkçe sızıntısı varsa bir kez düzelttir
    if turkceKalintiVar(yanit) {
        mesajlar.append(["role": "assistant", "content": yanit])
        mesajlar.append(["role": "user", "content":
            "Bu çeviride hâlâ Türkçe kelimeler var. TAMAMEN "
            + "\(hedefLehce) yaz; hiçbir Türkçe kelime veya harf "
            + "(ı, ş, ğ) kalmasın. Sadece düzeltilmiş mesajı ver."])
        if let ikinci = try? grokIstek(mesajlar, ayarlar: ayarlar,
                                       sicaklik: 0.3),
           !turkceKalintiVar(ikinci) {
            yanit = ikinci
        }
    }
    return gidenFormatla(yanit)
}

/// Klavye kısayolu tuşu gönderir (⌘A/⌘C/⌘V) — Erişilebilirlik izni ister.
func tusBas(_ tus: CGKeyCode, bayraklar: CGEventFlags) {
    let kaynak = CGEventSource(stateID: .combinedSessionState)
    for asagi in [true, false] {
        let olay = CGEvent(keyboardEventSource: kaynak, virtualKey: tus,
                           keyDown: asagi)
        olay?.flags = bayraklar
        olay?.post(tap: .cghidEventTap)
    }
}

func tusAdi(_ kod: Int) -> String {
    let isimler: [Int: String] = [
        0: "A", 11: "B", 8: "C", 2: "D", 14: "E", 3: "F", 5: "G", 4: "H",
        34: "I", 38: "J", 40: "K", 37: "L", 46: "M", 45: "N", 31: "O",
        35: "P", 12: "Q", 15: "R", 1: "S", 17: "T", 32: "U", 9: "V",
        13: "W", 7: "X", 16: "Y", 6: "Z", 18: "1", 19: "2", 20: "3",
        21: "4", 23: "5", 22: "6", 26: "7", 28: "8", 25: "9", 29: "0",
        49: "Boşluk", 36: "Enter", 48: "Tab", 122: "F1", 120: "F2",
        99: "F3", 118: "F4", 96: "F5", 97: "F6", 98: "F7", 100: "F8",
        101: "F9", 109: "F10", 103: "F11", 111: "F12",
    ]
    return isimler[kod] ?? "Tuş#\(kod)"
}

func kisayolMetni(_ tus: Int, _ mod: Int) -> String {
    var s = ""
    if mod & controlKey != 0 { s += "⌃" }
    if mod & optionKey != 0 { s += "⌥" }
    if mod & shiftKey != 0 { s += "⇧" }
    if mod & cmdKey != 0 { s += "⌘" }
    return s + tusAdi(tus)
}

final class YakalaPanel: NSPanel {
    override var canBecomeKey: Bool { true }
}

/// Kısayol atama: kullanıcının bastığı ilk kombinasyonu yakalar.
final class KisayolYakalamaGorunumu: NSView {
    var yakalandi: ((Int, Int) -> Void)?
    override var acceptsFirstResponder: Bool { true }

    private func isle(_ e: NSEvent) -> Bool {
        if e.keyCode == 53 { yakalandi?(-1, 0); return true }   // Esc
        var mod = 0
        if e.modifierFlags.contains(.control) { mod |= controlKey }
        if e.modifierFlags.contains(.option) { mod |= optionKey }
        if e.modifierFlags.contains(.shift) { mod |= shiftKey }
        if e.modifierFlags.contains(.command) { mod |= cmdKey }
        guard mod != 0 else { return false }
        yakalandi?(Int(e.keyCode), mod)
        return true
    }

    override func keyDown(with e: NSEvent) {
        if !isle(e) { super.keyDown(with: e) }
    }

    override func performKeyEquivalent(with e: NSEvent) -> Bool {
        e.type == .keyDown ? isle(e) : false
    }

    override func draw(_ r: NSRect) {
        NSColor(white: 0.12, alpha: 0.97).setFill()
        NSBezierPath(roundedRect: bounds, xRadius: 12, yRadius: 12).fill()
        let p = NSMutableParagraphStyle()
        p.alignment = .center
        ("Yeni kısayol için tuşlara bas\n(⌃, ⌥ veya ⌘ + bir tuş)  •  Esc: vazgeç"
         as NSString).draw(in: bounds.insetBy(dx: 16, dy: 20), withAttributes: [
            .font: NSFont.systemFont(ofSize: 14),
            .foregroundColor: NSColor.white,
            .paragraphStyle: p])
    }
}

// MARK: - Kısa bilgi baloncuğu

final class Baloncuk: NSWindow {
    convenience init(_ metin: String, orta: NSPoint, sureSn: Double) {
        let yazi = metin as NSString
        let nitelikler: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: 13),
            .foregroundColor: NSColor.white,
        ]
        let olcu = yazi.boundingRect(
            with: NSSize(width: 460, height: 300),
            options: [.usesLineFragmentOrigin], attributes: nitelikler)
        let boyut = NSSize(width: olcu.width + 30, height: olcu.height + 18)
        self.init(contentRect: NSRect(
            x: orta.x - boyut.width / 2, y: orta.y - boyut.height / 2,
            width: boyut.width, height: boyut.height),
            styleMask: .borderless, backing: .buffered, defer: false)
        isOpaque = false
        backgroundColor = .clear
        level = .screenSaver
        hasShadow = true
        hidesOnDeactivate = false
        let gorunum = BaloncukGorunumu()
        gorunum.metin = yazi
        gorunum.nitelikler = nitelikler
        contentView = gorunum
        orderFrontRegardless()
        if sureSn > 0 {
            DispatchQueue.main.asyncAfter(deadline: .now() + sureSn) {
                [weak self] in self?.orderOut(nil)
            }
        }
    }
}

final class BaloncukGorunumu: NSView {
    var metin: NSString = ""
    var nitelikler: [NSAttributedString.Key: Any] = [:]
    override func draw(_ kirli: NSRect) {
        NSColor(white: 0.1, alpha: 0.93).setFill()
        NSBezierPath(roundedRect: bounds, xRadius: 9, yRadius: 9).fill()
        metin.draw(in: bounds.insetBy(dx: 15, dy: 9), withAttributes: nitelikler)
    }
}

// MARK: - Bölge seçim penceresi

final class SecimPenceresi: NSWindow {
    override var canBecomeKey: Bool { true }
}

final class SecimGorunumu: NSView {
    var tamamlandi: ((NSRect?) -> Void)?
    private var baslangic: NSPoint?
    private var simdiki: NSPoint?

    override var acceptsFirstResponder: Bool { true }

    private var secim: NSRect? {
        guard let b = baslangic, let s = simdiki else { return nil }
        return NSRect(x: min(b.x, s.x), y: min(b.y, s.y),
                      width: abs(b.x - s.x), height: abs(b.y - s.y))
    }

    override func draw(_ kirli: NSRect) {
        NSColor(white: 0, alpha: 0.28).setFill()
        bounds.fill()
        if let r = secim {
            NSColor.clear.setFill()
            r.fill(using: .copy)
            NSColor(white: 1, alpha: 0.9).setStroke()
            let cerceve = NSBezierPath(rect: r)
            cerceve.lineWidth = 1.5
            cerceve.stroke()
            let bilgi = "\(Int(r.width)) × \(Int(r.height))  —  bırakınca çevrilir"
            (bilgi as NSString).draw(
                at: NSPoint(x: r.minX, y: r.maxY + 6),
                withAttributes: [.font: NSFont.systemFont(ofSize: 12),
                                 .foregroundColor: NSColor.white])
        } else {
            let ipucu = "Çevrilecek bölgeyi sürükleyerek seç  (Esc: vazgeç)" as NSString
            let n: [NSAttributedString.Key: Any] = [
                .font: NSFont.systemFont(ofSize: 17),
                .foregroundColor: NSColor(white: 1, alpha: 0.85)]
            let o = ipucu.size(withAttributes: n)
            ipucu.draw(at: NSPoint(x: bounds.midX - o.width / 2,
                                   y: bounds.midY - o.height / 2),
                       withAttributes: n)
        }
    }

    override func mouseDown(with olay: NSEvent) {
        baslangic = convert(olay.locationInWindow, from: nil)
        simdiki = baslangic
        needsDisplay = true
    }

    override func mouseDragged(with olay: NSEvent) {
        simdiki = convert(olay.locationInWindow, from: nil)
        needsDisplay = true
    }

    override func mouseUp(with olay: NSEvent) {
        defer { baslangic = nil; simdiki = nil }
        guard let r = secim, r.width > 15, r.height > 15,
              let pencere = window else {
            tamamlandi?(nil)
            return
        }
        let pencerede = convert(r, to: nil)
        let global = pencere.convertToScreen(pencerede)
        tamamlandi?(global)
    }

    override func keyDown(with olay: NSEvent) {
        if olay.keyCode == 53 { tamamlandi?(nil) }
    }
}

// MARK: - Çeviri katmanı

final class KatmanGorunumu: NSView {
    var bloklar: [Blok] = []
    var sessizKutular: [CGRect] = []   // balon içi saat/kırıntı kutuları
    var bitmap: NSBitmapImageRep?
    var bolgeBoyut = CGSize.zero
    var orijinalGoster = false
    // Kararlılık önbellekleri: aynı mesaj her karede aynı boyut/renkle
    // çizilir — "bir büyüyüp bir küçülme" bunun yokluğundan oluyordu
    private var boyutOnbellek: [String: CGFloat] = [:]
    private var renkOnbellek: [String: NSColor] = [:]

    override var isFlipped: Bool { true }

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        // SwiftyCrow/ScreenTranslate yaklaşımı: Layer-backed view GPU ile çizim yapar,
        // böylece needsDisplay çağrıldığında ekranda titreme olmaz.
        self.wantsLayer = true
        self.layer?.drawsAsynchronously = true
    }
    
    required init?(coder: NSCoder) {
        super.init(coder: coder)
    }

    // ---- Çizim yönetimi ----
    // Konum anahtarı ile önbellekler: 5px grid → OCR küçük oynamaları
    // ne fontu ne rengi değiştirir.
    private func konumKey(_ blok: Blok) -> String {
        "\(Int(blok.rect.minX/5)*5)_\(Int(blok.rect.minY/5)*5)_\(Int(blok.rect.width/5)*5)"
    }

    private func arkaPlanRengi(_ r: CGRect) -> NSColor {
        guard let bmp = bitmap else { return NSColor(white: 0.94, alpha: 1) }
        let olcek = CGFloat(bmp.pixelsWide) / max(1, bolgeBoyut.width)
        // Orijinal kutuyu biraz genişleterek örnek al, böylece metnin (beyaz/siyah)
        // anti-aliasing piksellerine takılmadan gerçek balon rengini (yeşil/gri) bulur.
        let genisR = r.insetBy(dx: -3, dy: -3)
        let px = CGRect(x: genisR.origin.x * olcek, y: genisR.origin.y * olcek,
                        width: genisR.width * olcek, height: genisR.height * olcek)
        var kirmizi: [CGFloat] = [], yesil: [CGFloat] = [], mavi: [CGFloat] = []
        let adim = max(2, Int(px.width / 30))
        var noktalar: [(Int, Int)] = []
        for x in stride(from: Int(px.minX), through: Int(px.maxX), by: adim) {
            noktalar.append((x, Int(px.minY))); noktalar.append((x, Int(px.maxY)))
        }
        for y in stride(from: Int(px.minY), through: Int(px.maxY), by: adim) {
            noktalar.append((Int(px.minX), y)); noktalar.append((Int(px.maxX), y))
        }
        for (x, y) in noktalar
        where x >= 0 && x < bmp.pixelsWide && y >= 0 && y < bmp.pixelsHigh {
            if let c = bmp.colorAt(x: x, y: y) {
                kirmizi.append(c.redComponent)
                yesil.append(c.greenComponent)
                mavi.append(c.blueComponent)
            }
        }
        guard !kirmizi.isEmpty else { return NSColor(white: 0.94, alpha: 1) }
        let orta = kirmizi.count / 2
        return NSColor(red: kirmizi.sorted()[orta], green: yesil.sorted()[orta],
                       blue: mavi.sorted()[orta], alpha: 1)
    }

    private func maskeKutusuBul(_ r: CGRect, arkaPlan: NSColor) -> CGRect {
        guard let bmp = bitmap else { return r.insetBy(dx: -8, dy: -6) }
        let olcek = CGFloat(bmp.pixelsWide) / max(1, bolgeBoyut.width)
        
        let px = CGRect(x: r.origin.x * olcek, y: r.origin.y * olcek,
                        width: r.width * olcek, height: r.height * olcek)
        
        // Kenar taraması: eşik 0.3 idi ve KOYU temalarda (balon 0.15 vs
        // duvar kâğıdı 0.08 → fark ≈0.29) balon sınırı HİÇ bulunamıyordu;
        // yama yanlış büyüyüp saat damgasını yarım bırakıyordu. Eşik
        // düşürüldü, ayrıca yanlış erken duruşu önlemek için ART ARDA İKİ
        // farklı örnek isteniyor ve tarama metnin 3px dışından başlıyor.
        func kenarAra(basX: Int, basY: Int, dX: Int, dY: Int) -> Int {
            var x = basX + dX * 3
            var y = basY + dY * 3
            let adim = 2
            var ardArda = 0
            for _ in 0..<34 {
                if x < 0 || x >= bmp.pixelsWide || y < 0 || y >= bmp.pixelsHigh { break }
                guard let c = bmp.colorAt(x: x, y: y) else { break }
                let fark = abs(c.redComponent - arkaPlan.redComponent)
                         + abs(c.greenComponent - arkaPlan.greenComponent)
                         + abs(c.blueComponent - arkaPlan.blueComponent)
                if fark > 0.14 {
                    ardArda += 1
                    if ardArda >= 2 { return dX != 0 ? x : y }
                } else {
                    ardArda = 0
                }
                x += dX * adim
                y += dY * adim
            }
            return dX != 0 ? basX + (dX * 15) : basY + (dY * 15)
        }
        
        let sol = kenarAra(basX: Int(px.minX), basY: Int(px.midY), dX: -1, dY: 0)
        let sag = kenarAra(basX: Int(px.maxX), basY: Int(px.midY), dX: 1, dY: 0)
        let ust = kenarAra(basX: Int(px.midX), basY: Int(px.minY), dX: 0, dY: -1)
        let alt = kenarAra(basX: Int(px.midX), basY: Int(px.maxY), dX: 0, dY: 1)
        
        let gercekUst = min(ust, alt)
        let gercekAlt = max(ust, alt)
        
        let nR = CGRect(x: CGFloat(sol) / olcek,
                        y: CGFloat(gercekUst) / olcek,
                        width: CGFloat(sag - sol) / olcek,
                        height: CGFloat(gercekAlt - gercekUst) / olcek)
        // Bulunan kenar balonun DIŞINDAKİ ilk pikseldir; yama balonun
        // içinde bitsin ki balondan taşma olmasın
        return nR.insetBy(dx: 1, dy: 1)
    }

    // NOT: Balonları renk/yakınlıkla birleştiren "grup" yaklaşımı DENENDİ ve
    // KALDIRILDI: birleşim zinciri kaçıp yarım ekranı kaplayan dev yamalar
    // üretiyordu (kullanıcı ekran görüntüsüyle doğrulandı). Yama BALON BAŞINA.

    func stilOnbelleginiTemizle() {
        boyutOnbellek.removeAll()
        renkOnbellek.removeAll()
    }

    private struct YamaOgesi {
        var rect: CGRect
        var parcalar: [(y: CGFloat, metin: String)]
        var renk: NSColor
        var alan: CGFloat
        var anahtarlar: [String]
        var satirYuksekligi: CGFloat
    }

    override func draw(_ kirli: NSRect) {
        guard !orijinalGoster else { return }
        CATransaction.begin()
        CATransaction.setDisableActions(true)

        // 1) Blok başına yama adayı — rect şimdilik HAM metin kutusudur;
        //    balon maskesi birleşme kararından SONRA uygulanır. Yoksa
        //    maskeyle büyüyen komşu yamalar birbirine değip AYRI balonları
        //    tek yamada eritiyordu.
        var yamalar: [YamaOgesi] = []
        for blok in bloklar {
            guard let ceviri = blok.ceviri, !ceviri.isEmpty else { continue }
            // Çeviri kaynakla AYNIYSA (fiyat, numara, "ok") yama çizme:
            // orijinal pikseller daha net ve gereksiz kapatma olmaz
            if anahtarla(ceviri) == blok.anahtar { continue }
            let anahtar = blok.anahtar
            let renk: NSColor
            if let r = renkOnbellek[anahtar] { renk = r }
            else {
                renk = arkaPlanRengi(blok.rect)
                renkOnbellek[anahtar] = renk
            }
            yamalar.append(YamaOgesi(
                rect: blok.rect, parcalar: [(blok.rect.minY, ceviri)],
                renk: renk,
                alan: blok.rect.width * blok.rect.height,
                anahtarlar: [anahtar],
                satirYuksekligi: blok.satirYuksekligi))
        }

        // 2) Yalnız HAM METİN KUTULARI kesişenleri birleştir (OCR'ın
        //    parçaladığı balon); ayrı balonlar asla birleşmez
        var i = 0
        while i < yamalar.count {
            var j = i + 1
            var birlesti = false
            while j < yamalar.count {
                if yamalar[i].rect.insetBy(dx: -3, dy: -2)
                    .intersects(yamalar[j].rect) {
                    let b = yamalar.remove(at: j)
                    yamalar[i].rect = yamalar[i].rect.union(b.rect)
                    yamalar[i].parcalar += b.parcalar
                    yamalar[i].anahtarlar += b.anahtarlar
                    if b.alan > yamalar[i].alan {
                        yamalar[i].renk = b.renk
                        yamalar[i].alan = b.alan
                        yamalar[i].satirYuksekligi = b.satirYuksekligi
                    }
                    birlesti = true
                } else {
                    j += 1
                }
            }
            if !birlesti { i += 1 }   // büyüyen kutu yeni kesişme yaratabilir
        }

        // 2b) Birleşme bitti: balon maskesi uygulanır ama maske artık yamayı
        //     yalnız KIRPABİLİR — büyüme metin kutusundan en çok 9×6 punto.
        //     (Koyu gri balon ↔ koyu duvar kâğıdı ayrımı bazen taramadan
        //     kaçıyor ve maske komşu balonun içine yürüyordu; sınır bunu
        //     fiziksel olarak imkânsız kılar.)
        for k in yamalar.indices {
            let metinK = yamalar[k].rect
            let genis = metinK.insetBy(dx: -9, dy: -6)
            let maske = maskeKutusuBul(metinK, arkaPlan: yamalar[k].renk)
            var r = genis.intersection(maske)
            if r.isNull || r.isEmpty || r.width < metinK.width {
                r = metinK.insetBy(dx: -4, dy: -3)   // maske şaştı: güvenli pay
            }
            r = r.union(metinK.insetBy(dx: -3, dy: -2)).intersection(bounds)
            if !r.isNull && !r.isEmpty { yamalar[k].rect = r }
        }

        // 3) Balon içindeki sessiz kırıntıları (saat, tik, sayı) yamaya yut
        for s in sessizKutular {
            for k in yamalar.indices
            where yamalar[k].rect.insetBy(dx: -10, dy: -8).intersects(s) {
                yamalar[k].rect = yamalar[k].rect
                    .union(s.insetBy(dx: -2, dy: -2))
                    .intersection(bounds)
                break
            }
        }

        // 4) Çiz
        for oge in yamalar {
            let yamaHam = oge.rect
            let metin = oge.parcalar.sorted { $0.y < $1.y }
                .map { $0.metin }.joined(separator: "\n")
            let anahtar = oge.anahtarlar.sorted().joined(separator: "|")
                + "#\(Int(oge.rect.width / 8))"
            let renk = oge.renk

            let parlaklik = 0.299 * renk.redComponent
                          + 0.587 * renk.greenComponent
                          + 0.114 * renk.blueComponent
            let yaziRengi: NSColor = parlaklik > 0.55
                ? NSColor(white: 0.1, alpha: 1) : NSColor(white: 0.96, alpha: 1)

            let p = NSMutableParagraphStyle()
            p.alignment = .left
            p.lineBreakMode = .byWordWrapping

            // Çeviri orijinalden uzunsa yamayı AŞAĞI büyüt: kırpmak
            // ("...lazım, sonra" gibi yarım cümle) okunabilirliği öldürüyor.
            // WhatsApp balonları arasında boşluk var, büyüme göze batmıyor.
            var yama = yamaHam
            let ilkPunto = min(14, max(9, oge.satirYuksekligi * 0.9))
            let gereken = (metin as NSString).boundingRect(
                with: CGSize(width: yama.width - 10, height: 10_000),
                options: [.usesLineFragmentOrigin, .usesFontLeading],
                attributes: [.font: NSFont.systemFont(ofSize: ilkPunto),
                             .paragraphStyle: p])
            if gereken.height > yama.height - 6 {
                // Balonun GERÇEK alt sınırını bul ve saat damgası için altta
                // ~15pt boş bırak; büyüme oraya taşarsa "21:04" → "1:04"
                // gibi yarım rakam görünüyor. Kalan yere sığmazsa yazı
                // küçülür (kırpma ASLA olmaz).
                let balon = maskeKutusuBul(yamaHam, arkaPlan: renk)
                let guvenliAlt = (balon.isNull || balon.isEmpty)
                    ? yama.maxY + max(24, yama.height)
                    : balon.maxY - 15
                let hedefAlt = min(yama.minY + gereken.height + 6,
                                   max(guvenliAlt, yama.maxY))
                yama.size.height = max(yama.height, hedefAlt - yama.minY)
                yama = yama.intersection(bounds)
                for s in sessizKutular where yama.intersects(s) {
                    yama = yama.union(s.insetBy(dx: -2, dy: -1))
                        .intersection(bounds)
                }
            }

            var ic = yama.insetBy(dx: 5, dy: 3)
            guard ic.width > 8, ic.height > 6 else { continue }

            // Yazı boyutu yama başına BİR kez hesaplanır ve sabit kalır —
            // "büyüyüp küçülme" bu önbelleğin her güncellemede silinmesindendi
            var boyut: CGFloat
            if let b = boyutOnbellek[anahtar] {
                boyut = b
            } else {
                boyut = min(14, max(9, oge.satirYuksekligi * 0.9))
                while boyut > 6 {
                    let olcu = (metin as NSString).boundingRect(
                        with: CGSize(width: ic.width, height: 10_000),
                        options: [.usesLineFragmentOrigin, .usesFontLeading],
                        attributes: [.font: NSFont.systemFont(ofSize: boyut),
                                     .paragraphStyle: p])
                    if olcu.height <= ic.height + 2 { break }
                    boyut -= 0.5
                }
                boyutOnbellek[anahtar] = boyut
            }

            let nit: [NSAttributedString.Key: Any] = [
                .font: NSFont.systemFont(ofSize: boyut),
                .foregroundColor: yaziRengi,
                .paragraphStyle: p]
            let olcu = (metin as NSString).boundingRect(
                with: CGSize(width: ic.width, height: 10_000),
                options: [.usesLineFragmentOrigin, .usesFontLeading],
                attributes: nit)

            // SON ÇARE: en küçük puntoda bile sığmıyorsa yamayı büyüt.
            // Kırpma ("tamam mı?" → "tamam") kabul edilemez.
            if olcu.height > ic.height + 2 {
                yama.size.height += olcu.height - ic.height + 6
                yama = yama.intersection(bounds)
                for s in sessizKutular where yama.intersects(s) {
                    yama = yama.union(s.insetBy(dx: -2, dy: -1))
                        .intersection(bounds)
                }
                ic = yama.insetBy(dx: 5, dy: 3)
            }

            let yamaYolu = NSBezierPath(roundedRect: yama, xRadius: 7, yRadius: 7)
            renk.setFill()
            yamaYolu.fill()

            // Yazı yamaya KIRPILIR: balon dışına taşma imkânsız
            NSGraphicsContext.current?.saveGraphicsState()
            yamaYolu.addClip()
            // ÖNEMLİ: ölçüm boundingRect(.usesLineFragmentOrigin) ile
            // yapılıyor; çizim de AYNI yerleşim yolunu kullanmalı. Aksi
            // halde ölçüm "sığıyor" derken çizim son kelimeyi kırpıyordu
            // ("tamam mı?" → "tamam").
            let metinY = ic.minY + max(0, (ic.height - olcu.height) / 2)
            NSAttributedString(string: metin, attributes: nit).draw(
                with: NSRect(x: ic.minX, y: metinY, width: ic.width,
                             height: max(olcu.height, ic.height)),
                options: [.usesLineFragmentOrigin, .usesFontLeading],
                context: nil)
            NSGraphicsContext.current?.restoreGraphicsState()
        }
        CATransaction.commit()
        cizimiLogla(yamaSayisi: yamalar.count)
    }

    // Teşhis log'u: repaint sıklığı ve font kararlılığı buradan okunur
    // (~/Library/Logs/EkranCeviri-cizim.log)
    private var sonLogZamani = Date.distantPast
    private func cizimiLogla(yamaSayisi: Int) {
        let simdi = Date()
        guard simdi.timeIntervalSince(sonLogZamani) > 0.5 else { return }
        sonLogZamani = simdi
        let satir = "\(Int(simdi.timeIntervalSince1970)) draw blok=\(bloklar.count) yama=\(yamaSayisi)\n"
        let url = FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Logs/EkranCeviri-cizim.log")
        if !FileManager.default.fileExists(atPath: url.path) {
            FileManager.default.createFile(atPath: url.path, contents: nil)
        }
        if let h = try? FileHandle(forWritingTo: url), let d = satir.data(using: .utf8) {
            h.seekToEndOfFile(); h.write(d); try? h.close()
        }
    }

    /// Blokları güncelle ve TEK SEFERDE yeniden çiz (yanıp sönmeyi önler).
    /// Stil önbelleği BİLEREK korunur: her güncellemede silmek yazıların
    /// "bir büyüyüp bir küçülmesine" yol açıyordu.
    func guncelle(yeniBloklar: [Blok], yeniBitmap: NSBitmapImageRep?,
                  yeniBoyut: CGSize, yeniSessizler: [CGRect]? = nil) {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        self.bloklar = yeniBloklar
        if let bmp = yeniBitmap { self.bitmap = bmp }
        if let s = yeniSessizler { self.sessizKutular = s }
        self.bolgeBoyut = yeniBoyut
        self.needsDisplay = true
        CATransaction.commit()
    }

    /// Blokları temizle (kaydırma anında)
    func temizle() {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        self.bloklar = []
        self.needsDisplay = true
        CATransaction.commit()
    }
}

// MARK: - Uygulama

final class UygulamaDelege: NSObject, NSApplicationDelegate {
    var ayarlar = Ayarlar.yukle()
    var durumOgesi: NSStatusItem!
    var secimPenceresi: SecimPenceresi?
    var katmanPenceresi: NSWindow?
    var barPenceresi: NSPanel?
    var oneriPaneli: NSPanel?
    var katmanGorunumu: KatmanGorunumu?
    var motorEtiketi: NSTextField?
    var bekleme: Baloncuk?
    var isSuruyor = false
    var oneriSuruyor = false

    let isKuyrugu = DispatchQueue(label: "ekranceviri.is", qos: .userInitiated)
    var ceviriOnbellek: [String: String] = [:]
    var canliAcik = true
    var canliZamanlayici: Timer?
    var canliYakalayici: (SCContentFilter, SCDisplay)?
    var canliCG: CGRect?
    var canliNS: NSRect?
    var canliOlcek: CGFloat = 2
    var canliOcrBuyutme: CGFloat = 1
    var sonIz: [UInt8]?
    var turSayaci = 0
    var kisayolRef: EventHotKeyRef?

    private func ekranOrtasi() -> NSPoint {
        let f = (NSScreen.main ?? NSScreen.screens[0]).visibleFrame
        return NSPoint(x: f.midX, y: f.maxY - 120)
    }
    var yakalamaPaneli: NSPanel?
    var kisayolMenuOge: NSMenuItem?
    var gidenSuruyor = false
    let gidenKuyrugu = DispatchQueue(label: "ekranceviri.giden",
                                     qos: .userInteractive)
    var canliMesgul = false
    var canliBaslangic = Date()
    var gosterilenCeviriDizisi = "" // Ekrandaki çevirileri takip etmek için
    var ekranSabitlendi = false
    var hareketSayaci = 0     // üst üste hareketli tur sayısı (açlık önleyici)
    var sonAnahtarDizisi = ""      // normalize blok anahtarları (karşılaştırma)
    var mevcutBloklar: [Blok] = []
    var gecmiseYazilan = Set<String>()
    // Ürettiğimiz çevirilerin normalize anahtarları: yakalanan karede bunlar
    // görülüyorsa kare kendi katmanımızı içeriyor demektir (zehir kalkanı)
    var uretilenCeviriler = Set<String>()

    // ---- kuruluş

    func applicationDidFinishLaunching(_ bildirim: Notification) {
        if !CommandLine.arguments.contains("--gizli-sinama"),
           !CommandLine.arguments.contains("--gizli-soak") {
            durumCubuguKur()
            kisayolKur()
        }
        bulutOnayDenetimi = { [weak self] in self?.bulutOnayAl() ?? false }
        // Yerel çeviri hafızası: açılışta yükle (arka planda), düzenli kaydet
        DispatchQueue.global(qos: .utility).async {
            CeviriHafizasi.paylasilan.yukle()
        }
        Timer.scheduledTimer(withTimeInterval: 20, repeats: true) { _ in
            DispatchQueue.global(qos: .utility).async {
                CeviriHafizasi.paylasilan.kaydet()
            }
        }
        NotificationCenter.default.addObserver(
            forName: NSApplication.willTerminateNotification,
            object: nil, queue: .main) { _ in
            CeviriHafizasi.paylasilan.kaydet()
        }
        // NOT: Keychain OTOMATİK okunmaz. ad-hoc imzalı derlemede
        // SecItemCopyMatching kullanıcıya "anahtar zinciri parolanızı girin"
        // diyaloğu gösteriyor (QA'da yakalandı) — kabul edilemez. Anahtar
        // 0600 izinli yerel dosyadan anında okunur; Keychain'den almak
        // isteyen menüden açıkça ister.
        if let i = CommandLine.arguments.firstIndex(of: "--gizli-soak") {
            ayarlar.bulutOnay = true
            ayarlar.gecmisAcik = false
            let dk = i + 1 < CommandLine.arguments.count
                ? Double(CommandLine.arguments[i + 1]) ?? 5 : 5
            let a = ayarlar
            DispatchQueue.global(qos: .userInitiated).async {
                GizliSoak.calistir(ayarlar: a, dakika: dk)
                exit(0)
            }
            return
        }
        if CommandLine.arguments.contains("--gizli-sinama") {
            // Ekrana HİÇBİR ŞEY çıkmaz; her şey bellekte + dosyada
            ayarlar.bulutOnay = true
            ayarlar.gecmisAcik = false
            let a = ayarlar
            DispatchQueue.global(qos: .userInitiated).async {
                GizliSinama.calistir(ayarlar: a)
                exit(0)
            }
            return
        }
        if CommandLine.arguments.contains("--sinama") {
            ayarlar.bulutOnay = true
            ayarlar.gecmisAcik = false
            sinamaBaslat()
            return
        }
        gizlilikGoster()
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.4) {
            self.cevirBaslat()
        }
    }

    // ---- Yazdığımı Çevir kısayolu (⌃⌥C — Carbon, ek izin gerektirmez)

    var sinama: SinamaSahnesi?

    private func sinamaBaslat() {
        let sahne = SinamaSahnesi()
        sinama = sahne
        NSLog("EC-sinama: sahne kuruldu")
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) {
            self.secimBitti(sahne.bolgeNS())
        }
        if CommandLine.arguments.contains("--soak") {
            sahne.soakBaslat(mesajlar: [
                "Hesch hüt scho öppis vor?",
                "Ich mues no schnäll id Migros",
                "Ja klar, chum vorbi wenn d wottsch 😊",
                "Wie lang bisch no da?",
                "Merci vilmal für alles!",
                "Chasch mir es foti schicke?",
                "Bin grad am schaffe, schriib spöter",
                "Was machsch am wuchenend?",
            ], aralik: 22, adet: 16)
            return
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 12) {
            NSLog("EC-sinama: yeni mesaj")
            sahne.mesajEkle("Was machsch grad? 😊", benim: false)
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 22) {
            NSLog("EC-sinama: kaydırma")
            sahne.kaydir(70)
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 32) {
            NSLog("EC-sinama: ikinci mesaj")
            sahne.mesajEkle("Muss hüt no chli schaffe, bis spöter!",
                            benim: false)
        }
    }

    private func gizlilikGoster() {
        guard !ayarlar.gizlilikGosterildi else { return }
        NSApp.activate(ignoringOtherApps: true)
        let uyari = NSAlert()
        uyari.messageText = "Gizlilik — kısa özet"
        uyari.informativeText = """
        • Yazı tanıma (OCR) tamamen bu Mac'te çalışır.
        • Motorlara yalnız ÇIKARILAN METİN gider; ekran görüntüsü asla \
        gönderilmez, diske de kaydedilmez.
        • Çevrimiçi gönderim ilk seferinde onayına sunulur.
        • Sohbet geçmişi yereldir; menüden kapatıp silebilirsin.
        • API anahtarın macOS Anahtar Zinciri'nde saklanır.
        """
        uyari.addButton(withTitle: "Anladım")
        uyari.runModal()
        ayarlar.gizlilikGosterildi = true
        ayarlar.kaydet()
    }

    private func kisayolKur() {
        var tur = EventTypeSpec(eventClass: OSType(kEventClassKeyboard),
                                eventKind: UInt32(kEventHotKeyPressed))
        InstallEventHandler(GetEventDispatcherTarget(), { _, _, veri in
            guard let veri = veri else { return noErr }
            let delege = Unmanaged<UygulamaDelege>.fromOpaque(veri)
                .takeUnretainedValue()
            DispatchQueue.main.async { delege.yazdigimiCevir() }
            return noErr
        }, 1, &tur, Unmanaged.passUnretained(self).toOpaque(), nil)
        kisayolYenidenKaydet()
    }

    func kisayolYenidenKaydet() {
        if let ref = kisayolRef { UnregisterEventHotKey(ref); kisayolRef = nil }
        let kimlik = EventHotKeyID(signature: 0x454B4356, id: 1)
        RegisterEventHotKey(UInt32(ayarlar.kisayolTus),
                            UInt32(ayarlar.kisayolMod),
                            kimlik, GetEventDispatcherTarget(), 0, &kisayolRef)
    }

    /// "Kısayol Ata…": kullanıcı bir kombinasyona basar, o kalıcı olur.
    @objc private func kisayolAta() {
        yakalamaPaneli?.orderOut(nil)
        let ekran = NSScreen.main?.frame ?? .zero
        let panel = YakalaPanel(
            contentRect: NSRect(x: ekran.midX - 200, y: ekran.midY - 48,
                                width: 400, height: 96),
            styleMask: .borderless, backing: .buffered, defer: false)
        panel.level = .floating
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.hidesOnDeactivate = false
        let gorunum = KisayolYakalamaGorunumu()
        gorunum.yakalandi = { [weak self] tus, mod in
            guard let self = self else { return }
            self.yakalamaPaneli?.orderOut(nil)
            self.yakalamaPaneli = nil
            guard tus >= 0 else { return }
            self.ayarlar.kisayolTus = tus
            self.ayarlar.kisayolMod = mod
            self.ayarlar.kaydet()
            self.kisayolYenidenKaydet()
            let metin = kisayolMetni(tus, mod)
            self.kisayolMenuOge?.title = "Yazdığımı Çevir (\(metin))"
            _ = Baloncuk("⌨️ Kısayol ayarlandı: \(metin)",
                         orta: NSPoint(x: ekran.midX, y: ekran.midY - 100),
                         sureSn: 3)
        }
        panel.contentView = gorunum
        yakalamaPaneli = panel
        NSApp.activate(ignoringOtherApps: true)
        panel.makeKeyAndOrderFront(nil)
        panel.makeFirstResponder(gorunum)
    }

    @objc private func apiAnahtariGir() {
        NSApp.activate(ignoringOtherApps: true)
        let uyari = NSAlert()
        uyari.messageText = "xAI (Grok) API Anahtarı"
        uyari.informativeText = "Anahtar yalnız bu Mac'te, macOS Anahtar "
            + "Zinciri'nde saklanır."
        let alan = NSSecureTextField(
            frame: NSRect(x: 0, y: 0, width: 340, height: 24))
        alan.placeholderString = "xai-..."
        uyari.accessoryView = alan
        uyari.addButton(withTitle: "Kaydet")
        uyari.addButton(withTitle: "Vazgeç")
        if uyari.runModal() == .alertFirstButtonReturn {
            let deger = alan.stringValue.trimmingCharacters(in: .whitespaces)
            if !deger.isEmpty {
                AnahtarKasasi.kaydet("grok_api_key", deger)
                ayarlar.grokApiKey = deger
                ayarlar.kaydet()
                motorEtiketi?.stringValue = "Anahtar Keychain'e kaydedildi ✓"
            }
        }
    }

    @objc private func anahtariKasadanAl() {
        motorEtiketi?.stringValue = "Anahtar zinciri sorgulanıyor…"
        AnahtarKasasi.getir("grok_api_key") { [weak self] deger in
            guard let self = self else { return }
            if deger.isEmpty {
                _ = Baloncuk("Anahtar zincirinde kayıt bulunamadı",
                             orta: self.ekranOrtasi(), sureSn: 3)
            } else {
                self.ayarlar.grokApiKey = deger
                _ = Baloncuk("Anahtar yüklendi ✓",
                             orta: self.ekranOrtasi(), sureSn: 2)
            }
        }
    }

    @objc private func dilModuSecildi(_ oge: NSMenuItem) {
        ayarlar.dilModu = oge.representedObject as? String ?? "alman"
        ayarlar.kaydet()
        oge.menu?.items.forEach { $0.state = $0 == oge ? .on : .off }
        ceviriOnbellek.removeAll()   // yeni modda yeniden çevrilsin
    }

    @objc private func cinsiyetSecildi(_ oge: NSMenuItem) {
        let parcalar = (oge.representedObject as? String ?? "").split(
            separator: ":").map(String.init)
        guard parcalar.count == 2 else { return }
        if parcalar[0] == "ben" { ayarlar.benCinsiyet = parcalar[1] }
        else { ayarlar.karsiCinsiyet = parcalar[1] }
        ayarlar.kaydet()
        oge.menu?.items.forEach { $0.state = $0 == oge ? .on : .off }
    }

    @objc private func hizDegistir(_ oge: NSMenuItem) {
        ayarlar.hizOnceligi.toggle()
        ayarlar.kaydet()
        oge.state = ayarlar.hizOnceligi ? .on : .off
    }

    @objc private func ceviriHafizasiniSil() {
        NSApp.activate(ignoringOtherApps: true)
        let uyari = NSAlert()
        uyari.messageText = "Yerel çeviri hafızası silinsin mi?"
        uyari.informativeText = "\(CeviriHafizasi.paylasilan.sayi) kayıtlı "
            + "çeviri silinecek; bundan sonra aynı cümleler yeniden çevrilir."
        uyari.addButton(withTitle: "Sil")
        uyari.addButton(withTitle: "Vazgeç")
        if uyari.runModal() == .alertFirstButtonReturn {
            CeviriHafizasi.paylasilan.temizle()
            ceviriOnbellek.removeAll()
            motorEtiketi?.stringValue = "Çeviri hafızası silindi"
        }
    }

    @objc private func yetiskinDegistir(_ oge: NSMenuItem) {
        ayarlar.yetiskin.toggle()
        ayarlar.kaydet()
        oge.state = ayarlar.yetiskin ? .on : .off
    }

    @objc private func emojiDegistir(_ oge: NSMenuItem) {
        ayarlar.emojiSerbest.toggle()
        ayarlar.kaydet()
        oge.state = ayarlar.emojiSerbest ? .on : .off
    }

    @objc private func gidenMotorSecildi(_ oge: NSMenuItem) {
        ayarlar.gidenMotor = oge.representedObject as? String ?? "grok"
        ayarlar.kaydet()
        oge.menu?.items.forEach { $0.state = $0 == oge ? .on : .off }
    }

    @objc private func karakterAyarla() {
        let uyari = NSAlert()
        uyari.messageText = "Yazım Karakteri (Yazdığımı Çevir)"
        uyari.informativeText = "Türkçe yazdıkların karşı dile çevrilirken "
            + "mesaj bu karaktere göre yazılır (yalnız Grok motorunda)."
        let kaydirma = NSScrollView(
            frame: NSRect(x: 0, y: 0, width: 360, height: 110))
        kaydirma.hasVerticalScroller = true
        let metinAlani = NSTextView(
            frame: NSRect(x: 0, y: 0, width: 360, height: 110))
        metinAlani.isRichText = false
        metinAlani.font = NSFont.systemFont(ofSize: 13)
        metinAlani.string = ayarlar.gidenKarakter
        metinAlani.autoresizingMask = [.width]
        kaydirma.documentView = metinAlani
        uyari.accessoryView = kaydirma
        uyari.addButton(withTitle: "Kaydet")
        uyari.addButton(withTitle: "İptal")
        NSApp.activate(ignoringOtherApps: true)
        if uyari.runModal() == .alertFirstButtonReturn {
            ayarlar.gidenKarakter = metinAlani.string
                .trimmingCharacters(in: .whitespacesAndNewlines)
            ayarlar.kaydet()
            motorEtiketi?.stringValue = "Yazım karakteri kaydedildi ✓"
        }
    }

    /// Grok'a/çevrimiçi motora gönderimden önce onay.
    func bulutOnayAl() -> Bool {
        if ayarlar.bulutOnay { return true }
        var izin = false
        let sor = {
            NSApp.activate(ignoringOtherApps: true)
            let uyari = NSAlert()
            uyari.messageText = "Çevrimiçi çeviriye izin ver?"
            uyari.informativeText = "Yalnız çıkarılan METİN çevrimiçi motora "
                + "gönderilecek; ekran görüntüsü asla gönderilmez."
            uyari.addButton(withTitle: "Gönder")
            uyari.addButton(withTitle: "Vazgeç")
            uyari.showsSuppressionButton = true
            uyari.suppressionButton?.title = "Hatırla, bir daha sorma"
            izin = uyari.runModal() == .alertFirstButtonReturn
            if izin, uyari.suppressionButton?.state == .on {
                self.ayarlar.bulutOnay = true
                self.ayarlar.kaydet()
            }
        }
        if Thread.isMainThread { sor() }
        else { DispatchQueue.main.sync(execute: sor) }
        return izin
    }

    /// Mesaj kutusundaki Türkçe yazıyı alır, hedef dil+üslupta çevirir ve
    /// kutudaki metni çeviriyle DEĞİŞTİRİR (Alfred akışının yerleşik hâli).
    @objc func yazdigimiCevir() {
        // Arka planda ve alan seçili OLMASA da çalışır
        NSLog("EC-giden: kısayol tetiklendi")
        guard !gidenSuruyor else {
            NSLog("EC-giden: önceki işlem sürüyor, atlandı")
            return
        }
        if ayarlar.gidenMotor == "grok" && ayarlar.grokApiKey.isEmpty {
            _ = Baloncuk("Grok anahtarı yok — menüden 'API Anahtarı Gir…'",
                         orta: ekranOrtasi(), sureSn: 4)
            return
        }
        let bar = barPenceresi?.frame
        let bilgiKonum = bar.map { NSPoint(x: $0.midX, y: $0.minY - 30) }
            ?? ekranOrtasi()
        // Tuş göndermek (⌘A/⌘C/⌘V) Erişilebilirlik izni ister
        let secenekler = [kAXTrustedCheckOptionPrompt.takeUnretainedValue()
                          as String: true] as CFDictionary
        guard AXIsProcessTrustedWithOptions(secenekler) else {
            _ = Baloncuk("⌨️ Yazdığını çevirebilmem için Erişilebilirlik "
                       + "izni gerekli\nAyarlar → Gizlilik ve Güvenlik → "
                       + "Erişilebilirlik → EkranCeviri'yi aç",
                         orta: bilgiKonum, sureSn: 6)
            return
        }
        let pano = NSPasteboard.general
        let eskiPano = pano.string(forType: .string)
        let oncekiSayac = pano.changeCount
        tusBas(CGKeyCode(kVK_ANSI_A), bayraklar: .maskCommand)
        usleep(60_000)
        tusBas(CGKeyCode(kVK_ANSI_C), bayraklar: .maskCommand)
        let bilgi = Baloncuk("✍️ Çevriliyor…", orta: bilgiKonum, sureSn: 0)
        let ayarlar = self.ayarlar
        gidenSuruyor = true
        gidenKuyrugu.async {
            defer { DispatchQueue.main.async { self.gidenSuruyor = false } }
            var deneme = 0
            while pano.changeCount == oncekiSayac && deneme < 20 {
                usleep(50_000); deneme += 1
            }
            guard pano.changeCount != oncekiSayac else {
                NSLog("EC-giden: pano değişmedi (kutu boş / imleç dışarıda)")
                DispatchQueue.main.async {
                    bilgi.orderOut(nil)
                    _ = Baloncuk("Kopyalanacak yazı yok — imleç mesaj "
                               + "kutusunda mı?", orta: bilgiKonum, sureSn: 4)
                }
                return
            }
            let turkce = (pano.string(forType: .string) ?? "")
                .trimmingCharacters(in: .whitespacesAndNewlines)
            guard !turkce.isEmpty else {
                DispatchQueue.main.async {
                    bilgi.orderOut(nil)
                    _ = Baloncuk("Mesaj kutusunda yazı bulamadım — imleç "
                               + "yazı kutusunda mı?", orta: bilgiKonum,
                                 sureSn: 3)
                }
                return
            }
            // Ekrandaki karşı taraf mesajları: hedef lehçe + tarz kaynağı
            let ornekler = DispatchQueue.main.sync {
                self.mevcutBloklar.filter { !$0.benim && $0.hedef }
                    .map { $0.metin }
            }
            let ceviri = try? girdiCevir(turkce, ayarlar: ayarlar,
                                         ornekler: ornekler)
            DispatchQueue.main.async {
                bilgi.orderOut(nil)
                guard let ceviri = ceviri, !ceviri.isEmpty else {
                    _ = Baloncuk("Çeviri alınamadı (ağ/Grok)",
                                 orta: bilgiKonum, sureSn: 3)
                    return
                }
                pano.clearContents()
                pano.setString(ceviri, forType: .string)
                gecmiseYaz(kim: "ben", metin: ceviri, ceviri: turkce)
                self.gidenKuyrugu.async {
                    usleep(80_000)
                    tusBas(CGKeyCode(kVK_ANSI_V), bayraklar: .maskCommand)
                    usleep(400_000)
                    // kullanıcının eski panosunu geri koy
                    if let eski = eskiPano {
                        DispatchQueue.main.async {
                            pano.clearContents()
                            pano.setString(eski, forType: .string)
                        }
                    }
                }
            }
        }
    }

    func applicationShouldHandleReopen(_ uygulama: NSApplication,
                                       hasVisibleWindows: Bool) -> Bool {
        cevirBaslat()
        return false
    }

    private func durumCubuguKur() {
        durumOgesi = NSStatusBar.system.statusItem(
            withLength: NSStatusItem.variableLength)
        durumOgesi.button?.image = durumIkonu()

        // ARAYÜZ İLKESİ: üst seviyede yalnız günlük kullanılan 3 eylem +
        // 2 basit tercih. Geri kalan her şey "Gelişmiş" altında gizli.
        // (Son kullanıcı için sadeleştirildi.)
        let menu = NSMenu()

        menu.addItem(withTitle: "Bölgeyi Çevir",
                     action: #selector(cevirTiklandi), keyEquivalent: "t")
        let kisayolOge = NSMenuItem(
            title: "Yazdığımı Çevir  (\(kisayolMetni(ayarlar.kisayolTus, ayarlar.kisayolMod)))",
            action: #selector(yazdigimiCevir), keyEquivalent: "")
        kisayolOge.target = self
        menu.addItem(kisayolOge)
        kisayolMenuOge = kisayolOge
        menu.addItem(.separator())

        // — Günlük tercih 1: ben kimim / karşımdaki kim (tonu belirler)
        let kimlikMenu = NSMenu()
        for (baslik, alan) in [("Ben", "ben"), ("Karşımdaki", "karsi")] {
            let altMenu = NSMenu()
            for (ad, deger) in [("Kadın", "kadin"), ("Erkek", "erkek"),
                                ("Belirtme", "yok")] {
                let oge = NSMenuItem(title: ad,
                                     action: #selector(cinsiyetSecildi(_:)),
                                     keyEquivalent: "")
                oge.representedObject = "\(alan):\(deger)"
                let simdiki = alan == "ben" ? ayarlar.benCinsiyet
                                            : ayarlar.karsiCinsiyet
                oge.state = simdiki == deger ? .on : .off
                oge.target = self
                altMenu.addItem(oge)
            }
            let ustOge = NSMenuItem(title: baslik, action: nil, keyEquivalent: "")
            ustOge.submenu = altMenu
            kimlikMenu.addItem(ustOge)
        }
        let kimlikUst = NSMenuItem(title: "Kim yazıyor", action: nil,
                                   keyEquivalent: "")
        kimlikUst.submenu = kimlikMenu
        menu.addItem(kimlikUst)

        // — Günlük tercih 2: hedef dil
        let dilMenu = NSMenu()
        for (kod, ad) in dilAdlari {
            let oge = NSMenuItem(title: ad, action: #selector(dilSecildi(_:)),
                                 keyEquivalent: "")
            oge.representedObject = kod
            oge.state = ayarlar.hedefDil == kod ? .on : .off
            oge.target = self
            dilMenu.addItem(oge)
        }
        let dilOge = NSMenuItem(title: "Bana çevir", action: nil,
                                keyEquivalent: "")
        dilOge.submenu = dilMenu
        menu.addItem(dilOge)

        menu.addItem(.separator())

        // ————— GELİŞMİŞ (nadiren dokunulur) —————
        let gelismis = NSMenu()

        let modMenu = NSMenu()
        for (ad, deger) in [
            ("Alman modu — Almanya + İsviçre (önerilen)", "alman"),
            ("Yalnız İsviçre Almancası", "isvicre"),
            ("Otomatik (her dil)", "otomatik"),
        ] {
            let oge = NSMenuItem(title: ad, action: #selector(dilModuSecildi(_:)),
                                 keyEquivalent: "")
            oge.representedObject = deger
            oge.state = ayarlar.dilModu == deger ? .on : .off
            oge.target = self
            modMenu.addItem(oge)
        }
        let modOge = NSMenuItem(title: "Karşı tarafın dili", action: nil,
                                keyEquivalent: "")
        modOge.submenu = modMenu
        gelismis.addItem(modOge)

        let motorMenu = NSMenu()
        for (ad, deger) in [("Yapay zekâ — en iyi kalite (önerilen)", "ai"),
                            ("Ücretsiz çeviri", "hizli")] {
            let oge = NSMenuItem(title: ad, action: #selector(motorSecildi(_:)),
                                 keyEquivalent: "")
            oge.representedObject = deger
            oge.state = ayarlar.motor == deger ? .on : .off
            oge.target = self
            motorMenu.addItem(oge)
        }
        let motorOge = NSMenuItem(title: "Çeviri motoru", action: nil,
                                  keyEquivalent: "")
        motorOge.submenu = motorMenu
        gelismis.addItem(motorOge)

        let yetiskinOge = NSMenuItem(title: "Yetişkin içerik (sansürsüz)",
                                     action: #selector(yetiskinDegistir(_:)),
                                     keyEquivalent: "")
        yetiskinOge.target = self
        yetiskinOge.state = ayarlar.yetiskin ? .on : .off
        gelismis.addItem(yetiskinOge)

        let hizOge = NSMenuItem(title: "Hız önceliği (daha hızlı, biraz düşük kalite)",
                                action: #selector(hizDegistir(_:)),
                                keyEquivalent: "")
        hizOge.target = self
        hizOge.state = ayarlar.hizOnceligi ? .on : .off
        gelismis.addItem(hizOge)

        let emojiOge = NSMenuItem(title: "Emoji ekleyebilsin",
                                  action: #selector(emojiDegistir(_:)),
                                  keyEquivalent: "")
        emojiOge.target = self
        emojiOge.state = ayarlar.emojiSerbest ? .on : .off
        gelismis.addItem(emojiOge)

        gelismis.addItem(.separator())
        for (baslik, eylem) in [
            ("Kısayolu Değiştir…", #selector(kisayolAta)),
            ("Nasıl Yazayım (üslup)…", #selector(karakterAyarla)),
            ("Yapay Zekâ Anahtarı…", #selector(apiAnahtariGir)),
            ("Anahtarı Anahtar Zincirinden Al…", #selector(anahtariKasadanAl)),
        ] {
            let oge = NSMenuItem(title: baslik, action: eylem, keyEquivalent: "")
            oge.target = self
            gelismis.addItem(oge)
        }

        gelismis.addItem(.separator())
        let gecmisOge = NSMenuItem(title: "Sohbet hafızası",
                                   action: #selector(gecmisDegistir(_:)),
                                   keyEquivalent: "")
        gecmisOge.target = self
        gecmisOge.state = ayarlar.gecmisAcik ? .on : .off
        gelismis.addItem(gecmisOge)
        let ceviriHafizaOge = NSMenuItem(
            title: "Çeviri Hafızasını Sil…",
            action: #selector(ceviriHafizasiniSil), keyEquivalent: "")
        ceviriHafizaOge.target = self
        gelismis.addItem(ceviriHafizaOge)
        let gecmisSilOge = NSMenuItem(title: "Hafızayı Sil…",
                                      action: #selector(gecmisiSil),
                                      keyEquivalent: "")
        gecmisSilOge.target = self
        gelismis.addItem(gecmisSilOge)

        let gelismisUst = NSMenuItem(title: "Gelişmiş", action: nil,
                                     keyEquivalent: "")
        gelismisUst.submenu = gelismis
        menu.addItem(gelismisUst)

        menu.addItem(.separator())
        menu.addItem(withTitle: "Çıkış",
                     action: #selector(NSApplication.terminate(_:)),
                     keyEquivalent: "q")
        durumOgesi.menu = menu
    }

    private func durumIkonu() -> NSImage {
        let img = NSImage(size: NSSize(width: 18, height: 17), flipped: false) { _ in
            NSColor.black.setFill()
            NSBezierPath(roundedRect: NSRect(x: 1, y: 5, width: 16, height: 11),
                         xRadius: 3.5, yRadius: 3.5).fill()
            let kuyruk = NSBezierPath()
            kuyruk.move(to: NSPoint(x: 4, y: 6))
            kuyruk.line(to: NSPoint(x: 9, y: 6))
            kuyruk.line(to: NSPoint(x: 4, y: 0))
            kuyruk.close()
            kuyruk.fill()
            NSGraphicsContext.current?.compositingOperation = .destinationOut
            ("ç" as NSString).draw(
                at: NSPoint(x: 6, y: 5.5),
                withAttributes: [
                    .font: NSFont.boldSystemFont(ofSize: 10),
                    .foregroundColor: NSColor.white])
            return true
        }
        img.isTemplate = true
        return img
    }

    @objc private func cevirTiklandi() { cevirBaslat() }

    @objc private func motorSecildi(_ oge: NSMenuItem) {
        ayarlar.motor = oge.representedObject as? String ?? "hizli"
        ayarlar.kaydet()
        oge.menu?.items.forEach { $0.state = $0 == oge ? .on : .off }
    }

    @objc private func dilSecildi(_ oge: NSMenuItem) {
        ayarlar.hedefDil = oge.representedObject as? String ?? "tr"
        ayarlar.kaydet()
        oge.menu?.items.forEach { $0.state = $0 == oge ? .on : .off }
        ceviriOnbellek.removeAll()   // yeni dile göre yeniden çevrilsin
    }

    // ---- çeviri akışı

    func cevirBaslat() {
        if isSuruyor { return }
        canliDurdur()
        canliCG = nil
        canliNS = nil
        canliYakalayici = nil
        sonIz = nil
        ekranSabitlendi = false
        hareketSayaci = 0
        sonAnahtarDizisi = ""
        mevcutBloklar = []
        // Yeni seçim = temiz sayfa: eski seçimde başka pencereden sızan
        // metinlerin çevirileri yeni bölgeye bulaşmasın
        ceviriOnbellek.removeAll()
        katmaniKapat()

        if !CGPreflightScreenCaptureAccess() {
            CGRequestScreenCaptureAccess()
            izinUyar()
            return
        }

        let fareKonumu = NSEvent.mouseLocation
        let ekran = NSScreen.screens.first(where: {
            NSMouseInRect(fareKonumu, $0.frame, false)
        }) ?? NSScreen.main ?? NSScreen.screens[0]

        let pencere = SecimPenceresi(
            contentRect: ekran.frame, styleMask: .borderless,
            backing: .buffered, defer: false)
        pencere.level = .screenSaver
        pencere.backgroundColor = .clear
        pencere.isOpaque = false
        pencere.hasShadow = false
        let gorunum = SecimGorunumu()
        gorunum.tamamlandi = { [weak self] r in self?.secimBitti(r) }
        pencere.contentView = gorunum
        secimPenceresi = pencere
        NSApp.activate(ignoringOtherApps: true)
        pencere.makeKeyAndOrderFront(nil)
        pencere.makeFirstResponder(gorunum)
        NSCursor.crosshair.set()
    }

    private func izinUyar() {
        NSApp.activate(ignoringOtherApps: true)
        let uyari = NSAlert()
        uyari.messageText = "Ekran Kaydı izni gerekli"
        uyari.informativeText = """
        Ekrandaki yazıyı okuyabilmem için Ekran Kaydı izni şart.

        Ayarları Aç'a bas → listede EkranCeviri'yi bul → anahtarı aç → \
        uygulamayı yeniden başlat.
        """
        uyari.addButton(withTitle: "Ayarları Aç")
        uyari.addButton(withTitle: "Vazgeç")
        if uyari.runModal() == .alertFirstButtonReturn {
            NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:"
                + "com.apple.preference.security?Privacy_ScreenCapture")!)
        }
    }

    func secimBitti(_ nsBolge: NSRect?) {
        secimPenceresi?.orderOut(nil)
        secimPenceresi = nil
        NSCursor.arrow.set()
        guard let nsBolge = nsBolge else { return }
        let cgBolge = cgKoordinat(nsBolge)
        let olcek = NSScreen.screens.first(where: {
            NSMouseInRect(NSPoint(x: nsBolge.midX, y: nsBolge.midY),
                          $0.frame, false)
        })?.backingScaleFactor ?? 2
        isSuruyor = true
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.25) {
            self.ilkCeviri(nsBolge: nsBolge, cgBolge: cgBolge, olcek: olcek)
        }
    }

    private func ilkCeviri(nsBolge: NSRect, cgBolge: CGRect, olcek: CGFloat) {
        let ayarlar = self.ayarlar
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.4) {
            if self.isSuruyor {
                self.bekleme = Baloncuk("⏳ Çevriliyor…", orta: NSPoint(
                    x: nsBolge.midX, y: nsBolge.maxY + 30), sureSn: 0)
            }
        }
        isKuyrugu.async {
            NSLog("EC-boru: başladı, ekranKaydı=\(CGPreflightScreenCaptureAccess())")
            let yakalayici = try? sckFiltreKur(cgBolge)
            NSLog("EC-boru: sck filtre=\(yakalayici != nil)")
            // Katman açıkken screencapture yedeği kendi çevirimizi de
            // çeker; bu durumda yalnız SCK (katman hariç) kullanılır
            let yedekSerbest = DispatchQueue.main.sync {
                self.katmanPenceresi == nil }
            guard let goruntu = bolgeGoruntusu(cgBolge, yakalayici: yakalayici,
                                               olcek: olcek,
                                               yedekKullan: yedekSerbest) else {
                NSLog("EC-boru: GÖRÜNTÜ ALINAMADI")
                DispatchQueue.main.async {
                    self.isSuruyor = false
                    self.bekleme?.orderOut(nil); self.bekleme = nil
                    _ = Baloncuk("Ekran görüntüsü alınamadı", orta: NSPoint(
                        x: nsBolge.midX, y: nsBolge.midY), sureSn: 3)
                }
                return
            }
            NSLog("EC-boru: görüntü alındı \(goruntu.width)x\(goruntu.height)")
            var bloklar: [Blok] = []
            var sessizler: [CGRect] = []
            var motorAdi = ""
            let ocrBuyutme = ocrBuyutmeHesapla(olcek: olcek)
            if let satirlar = try? ocrYap(goruntu, diller: ayarlar.ocrDilleri,
                                          buyutme: ocrBuyutme) {
                NSLog("EC-boru: OCR \(satirlar.count) satır")
                (bloklar, sessizler) = bloklaraAyir(satirlar, boyut: cgBolge.size)
                let sonuc = bloklariCevir(bloklar, motor: ayarlar.motor,
                                          ayarlar: ayarlar,
                                          onbellek: self.ceviriOnbellek)
                motorAdi = sonuc.0
                self.ceviriOnbellek = sonuc.1
        self.uretilenleriKaydet(sonuc.1)
                if bloklar.contains(where: { $0.ceviri != nil }) {
                    self.sonAnahtarDizisi = bloklar.map { $0.anahtar }
                        .joined(separator: "\n")
                    self.gecmiseAktar(bloklar)
                }
                self.sonIz = goruntuIzi(goruntu)
                self.ekranSabitlendi = false
            }
            NSLog("EC-boru: \(bloklar.count) blok, motor=\(motorAdi), "
                + "çevrilen=\(bloklar.filter { $0.ceviri != nil }.count)")
            let sabitBloklar = bloklar
            let sabitSessizler = sessizler
            let sabitMotor = motorAdi
            DispatchQueue.main.async {
                self.isSuruyor = false
                self.bekleme?.orderOut(nil); self.bekleme = nil
                guard sabitBloklar.contains(where: { $0.ceviri != nil }) else {
                    let mesaj = sabitBloklar.contains(where: { $0.hedef })
                        ? "Çeviri alınamadı (ağ/motor) — tekrar dene"
                        : "Çevrilecek yazı bulunamadı"
                    _ = Baloncuk(mesaj, orta: NSPoint(
                        x: nsBolge.midX, y: nsBolge.midY), sureSn: 3)
                    return
                }
                self.mevcutBloklar = sabitBloklar
                self.katmaniGoster(
                    nsBolge: nsBolge, bloklar: sabitBloklar,
                    bitmap: NSBitmapImageRep(cgImage: goruntu),
                    motorAdi: sabitMotor)
                self.katmanGorunumu?.sessizKutular = sabitSessizler
                self.canliCG = cgBolge
                self.canliNS = nsBolge
                self.canliOlcek = olcek
                self.canliOcrBuyutme = ocrBuyutme
                self.canliYakalayici = yakalayici
                if self.canliAcik { self.canliBaslat() }
            }
        }
    }

    /// Ürettiğimiz her çevirinin normalize anahtarını kaydeder — zehir
    /// kalkanı bu kümeyle "kendi katmanımızı mı okuduk?" kontrolü yapar.
    func uretilenleriKaydet(_ bellek: [String: String]) {
        for ceviri in bellek.values where !ceviri.isEmpty {
            let a = anahtarla(ceviri)
            uretilenCeviriler.insert(a)
            uretilmisCeviriler.insert(a)   // motor katmanı da bilsin
        }
    }

    /// Yeni görülen mesajları yerel sohbet hafızasına yazar (oturum içinde
    /// tekrar yazılmaz).
    @objc private func gecmisDegistir(_ oge: NSMenuItem) {
        ayarlar.gecmisAcik.toggle()
        ayarlar.kaydet()
        oge.state = ayarlar.gecmisAcik ? .on : .off
    }

    @objc private func gecmisiSil() {
        NSApp.activate(ignoringOtherApps: true)
        let uyari = NSAlert()
        uyari.messageText = "Sohbet geçmişi kalıcı olarak silinsin mi?"
        uyari.addButton(withTitle: "Sil")
        uyari.addButton(withTitle: "Vazgeç")
        if uyari.runModal() == .alertFirstButtonReturn {
            try? FileManager.default.removeItem(at: gecmisURL)
            gecmiseYazilan.removeAll()
            motorEtiketi?.stringValue = "Geçmiş silindi"
        }
    }

    private func gecmiseAktar(_ bloklar: [Blok]) {
        guard ayarlar.gecmisAcik else { return }
        // Kendi ürettiğimiz çeviri metni ASLA "gelen mesaj" olarak
        // kaydedilmez. (Gerçek geçmişte Türkçe çevirilerin 'karsi'
        // mesajı olarak biriktiği tespit edildi — hem hafızayı
        // kirletiyor hem tekrar çeviriye yol açıyordu.)
        for b in bloklar where b.hedef {
            if uretilenCeviriler.contains(b.anahtar) { continue }
            if gecmiseYazilan.insert(b.anahtar).inserted {
                gecmiseYaz(kim: b.benim ? "ben" : "karsi",
                           metin: b.metin, ceviri: b.ceviri)
            }
        }
    }

    // ---- canlı mod

    private func canliBaslat() {
        canliZamanlayici?.invalidate()
        canliZamanlayici = Timer.scheduledTimer(
            withTimeInterval: 0.8, repeats: true) { [weak self] _ in
            self?.canliTur()
        }
    }

    private func canliDurdur() {
        canliZamanlayici?.invalidate()
        canliZamanlayici = nil
    }

    private func canliTur() {
        // BEKÇİ: bir tur 25 sn'yi aştıysa (ağ askıda, SCK yanıtsız) kilidi
        // kır. Bu kilit takılınca canlı mod sessizce ölüyordu.
        if canliMesgul, Date().timeIntervalSince(canliBaslangic) > 25 {
            NSLog("EC-canli: bekçi kilidi kırdı (takılı tur)")
            canliMesgul = false
            ekranSabitlendi = false
            sonIz = nil
            if let cg = canliCG {
                isKuyrugu.async { self.canliYakalayici = try? sckFiltreKur(cg) }
            }
        }
        guard !isSuruyor, !canliMesgul,
              katmanPenceresi != nil, let cg = canliCG else { return }
        let yakalayici = canliYakalayici
        let olcek = canliOlcek
        // UZUN OTURUM SİGORTASI: SCK filtresi bayatlayıp hep aynı kareyi
        // verebiliyor (uyku, Space değişimi) → sistem "değişiklik yok"
        // sanıp uyur, eski çeviri takılı kalır. ~12 sn'de bir tazele.
        turSayaci += 1
        if turSayaci % 15 == 0 {
            isKuyrugu.async {
                self.canliYakalayici = try? sckFiltreKur(cg)
                self.sonIz = nil
                self.ekranSabitlendi = false
            }
        }
        // Tanı kamerası: /tmp/ekranceviri_foto_istek dosyası bırakılırsa
        // katman DAHİL bölgenin fotoğrafını çeker (uzaktan hata ayıklama)
        if FileManager.default.fileExists(atPath: "/tmp/ekranceviri_foto_istek"),
           let ns = self.canliNS {
            try? FileManager.default.removeItem(
                atPath: "/tmp/ekranceviri_foto_istek")
            let tam = cgKoordinat(ns.insetBy(dx: -60, dy: -70))
            let p = Process()
            p.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
            p.arguments = ["-x", "-R\(Int(tam.origin.x)),\(Int(tam.origin.y)),"
                         + "\(Int(tam.width)),\(Int(tam.height))",
                         "/tmp/ekranceviri_foto.png"]
            try? p.run()
        }
        canliMesgul = true
        canliBaslangic = Date()
        isKuyrugu.async { autoreleasepool {
            // autoreleasepool ŞART: her turda CGImage + Vision + Bitmap
            // otomatik-serbest havuza giriyor; arka plan kuyruğunda havuz
            // kendiliğinden boşalmadığı için bellek büyüyordu (6 dk soak
            // testinde 31 → 106 MB ölçüldü).
            defer { DispatchQueue.main.async { self.canliMesgul = false } }
            guard let goruntu = bolgeGoruntusu(cg, yakalayici: yakalayici,
                                               olcek: olcek,
                                               yedekKullan: false) else {
                // SCK çöktü: bu turu atla, filtreyi tazelemeyi dene
                self.canliYakalayici = try? sckFiltreKur(cg)
                self.sonIz = nil
                return
            }
            let iz = goruntuIzi(goruntu)
            let onceki = self.sonIz
            self.sonIz = iz
            guard let onceki = onceki else { return }
            let fark = izFarki(iz, onceki)
            if fark > 0.012 {
                self.ekranSabitlendi = false
                // Yamalar hareket sırasında YERİNDE KALIR (silinirse Almanca
                // görünür + yanıp sönme hissi). AMA: aktif sohbette ekran hiç
                // durulmayabilir (yazıyor animasyonu, art arda mesaj) —
                // çeviri açlığa düşmesin: 3 tur üst üste hareket varsa mevcut
                // kareyle YİNE DE güncelle. En kötü gecikme ~2.5 sn olur.
                self.hareketSayaci += 1
                if self.hareketSayaci >= 3 {
                    self.hareketSayaci = 0
                    self.canliGuncelle(goruntu: goruntu, cgBolge: cg)
                }
                return
            }
            self.hareketSayaci = 0

            // Ekran sabit ve bu sabit ekran için henüz çeviri yapılmadıysa:
            if !self.ekranSabitlendi {
                self.ekranSabitlendi = true
                self.canliGuncelle(goruntu: goruntu, cgBolge: cg)
            }
            // Ekran zaten sabitse tekrar OCR ÇALIŞTIRMA (titreme + CPU)
        } }
    }

    /// isKuyrugu üzerinde: içerik duruldu. Blokları normalize anahtarla
    /// ESKİLERLE EŞLEŞTİR: eşleşen balon ekranda hiç kıpırdamaz; yalnız
    /// gerçekten yeni mesaj çevrilir.
    private func canliGuncelle(goruntu: CGImage, cgBolge: CGRect) {
        let ayarlar = self.ayarlar
        guard let satirlar = try? ocrYap(goruntu, diller: ayarlar.ocrDilleri,
                                         buyutme: self.canliOcrBuyutme)
        else {
            self.ekranSabitlendi = false   // OCR aksadı: sonraki tur dener
            return
        }
        let (yeniBloklar, yeniSessizler) = bloklaraAyir(satirlar,
                                                        boyut: cgBolge.size)
        NSLog("EC-canli: OCR \(satirlar.count) satır → \(yeniBloklar.count) blok")

        // ZEHİR KALKANI: karede kendi ürettiğimiz çevirilerden 2+ görünüyorsa
        // yakalama katmanımızı içeriyor demektir (SCK dışlaması bozulmuş).
        // YALNIZ UZUN metinler sayılır: "Merhaba", "Tamam" gibi kısa çeviriler
        // ekranda GERÇEK mesaj olarak da geçer ve yanlış alarm canlı
        // güncellemeyi tamamen durduruyordu ("kaydırınca çevirmiyor").
        let kirliSayisi = yeniBloklar.filter {
            $0.hedef && $0.anahtar.count >= 12 &&
            self.uretilenCeviriler.contains($0.anahtar)
        }.count
        if kirliSayisi >= 2 {
            NSLog("EC-canli: ZEHİR KALKANI devrede (kirli=\(kirliSayisi))")
            self.canliYakalayici = try? sckFiltreKur(cgBolge)
            self.ekranSabitlendi = false
            return
        }

        let yeniDizi = yeniBloklar.map { $0.anahtar }.joined(separator: "\n")

        func benzermisin(_ a: Blok, _ b: Blok) -> Bool {
            if a.anahtar == b.anahtar { return true }
            let kesen = a.rect.intersection(b.rect)
            if !kesen.isNull && !kesen.isEmpty {
                let iou = kesen.width * kesen.height / (a.rect.width * a.rect.height + b.rect.width * b.rect.height - kesen.width * kesen.height)
                if iou > 0.45 { return true } // Tolerans artırıldı: ufak OCR sarsıntıları eşleşmeyi bozmasın
            }
            return false
        }

        var sayfaKaymis = false
        var eskiler = self.mevcutBloklar
        for yeni in yeniBloklar {
            if let i = eskiler.firstIndex(where: { benzermisin($0, yeni) }) {
                let eski = eskiler.remove(at: i)
                yeni.ceviri = eski.ceviri
                
                // OCR'ın milimetrik oynaması balonu kıpırdatmasın
                if abs(eski.rect.minX - yeni.rect.minX) <= 8,
                   abs(eski.rect.minY - yeni.rect.minY) <= 8,
                   abs(eski.rect.width - yeni.rect.width) <= 12,
                   abs(eski.rect.height - yeni.rect.height) <= 12 {
                    yeni.rect = eski.rect
                } else {
                    // Eşleşti ama konumu 8 pikselden fazla değişti (kaydırma yapılmış)
                    sayfaKaymis = true
                }
                
                // Anahtar değişiminde önbelleği güncelle (IoU ile eşleştiyse eski anahtarı tut veya yenisini ekle)
                if yeni.anahtar != eski.anahtar, let ceviri = eski.ceviri {
                    self.ceviriOnbellek[yeni.anahtar] = ceviri
                }
            } else if let hazir = self.ceviriOnbellek[yeni.anahtar],
                      !hazir.isEmpty {
                // "" işaretli (çevrilememiş) girdiler atanmaz ki eksik
                // sayılsın ve Grok tamamlama devralabilsin
                yeni.ceviri = hazir
            }
        }
        let eksikVar = yeniBloklar.contains {
            $0.hedef && $0.ceviri == nil
        }

        let yeniBitmap = NSBitmapImageRep(cgImage: goruntu)
        let nsBolgeBoyutu = self.canliNS?.size ?? CGSize(width: cgBolge.width, height: cgBolge.height)

        // 1. FAZ — HEMEN boya: eşleşen çeviriler yeni konumlarına anında
        // oturur. Kaydırma sonrası "eski konumda asılı yama + yeni çeviri
        // üst üste" görüntüsünün çözümü budur: ekran her zaman gerçeği
        // gösterir, yalnız gerçekten yeni mesaj motoru bekler.
        self.mevcutBloklar = yeniBloklar
        DispatchQueue.main.async {
            self.katmanGorunumu?.guncelle(
                yeniBloklar: yeniBloklar,
                yeniBitmap: yeniBitmap,
                yeniBoyut: nsBolgeBoyutu,
                yeniSessizler: yeniSessizler
            )
        }

        guard eksikVar else {
            self.sonAnahtarDizisi = yeniDizi
            self.gecmiseAktar(yeniBloklar)
            return
        }
        _ = sayfaKaymis   // bilgi 1. fazda kullanıldı; ayrı dallanma gerekmez

        // 2. FAZ — yalnız eksik (yeni) bloklar motora gider
        NSLog("EC-canli: eksik var, motora gidiyor "
            + "(\(yeniBloklar.filter { $0.hedef && $0.ceviri == nil }.count) blok)")
        let sonuc = bloklariCevir(yeniBloklar, motor: ayarlar.motor,
                                  ayarlar: ayarlar,
                                  onbellek: self.ceviriOnbellek)
        NSLog("EC-canli: motor=\(sonuc.0), kalan eksik="
            + "\(yeniBloklar.filter { $0.hedef && $0.ceviri == nil }.count)")
        self.ceviriOnbellek = sonuc.1
        self.uretilenleriKaydet(sonuc.1)
        let tamam = !yeniBloklar.contains {
            $0.hedef && $0.ceviri == nil
        }
        if tamam {
            self.sonAnahtarDizisi = yeniDizi
            self.gecmiseAktar(yeniBloklar)
        } else {
            // Çeviri eksik kaldı: ekran kıpırdamasa da sonraki turda yeniden
            // dene — "hiç çevirmiyor / eski çeviri takılı kaldı"nın panzehiri
            self.ekranSabitlendi = false
        }
        
        // Çeviri bitince sadece TEK SEFERDE ekrana bas (yanıp sönmeyi önler)
        DispatchQueue.main.async {
            self.katmanGorunumu?.guncelle(
                yeniBloklar: yeniBloklar,
                yeniBitmap: yeniBitmap,
                yeniBoyut: nsBolgeBoyutu,
                yeniSessizler: yeniSessizler
            )
            self.motorEtiketi?.stringValue = sonuc.0
        }
    }

    // ---- katman ve kontrol paneli

    private func katmaniGoster(nsBolge: NSRect, bloklar: [Blok],
                               bitmap: NSBitmapImageRep?, motorAdi: String) {
        let ekran = NSScreen.screens.first(where: {
            NSMouseInRect(NSPoint(x: nsBolge.midX, y: nsBolge.midY),
                          $0.frame, false)
        }) ?? NSScreen.main ?? NSScreen.screens[0]

        // Yama katmanı fareyi TAMAMEN geçirir: sohbet ekranı kullanıcınındır
        let pencere = NSWindow(
            contentRect: nsBolge, styleMask: .borderless,
            backing: .buffered, defer: false)
        pencere.level = .floating
        pencere.backgroundColor = .clear
        pencere.isOpaque = false
        pencere.hasShadow = false
        pencere.ignoresMouseEvents = true
        pencere.hidesOnDeactivate = false

        let gorunum = KatmanGorunumu()
        gorunum.bloklar = bloklar
        gorunum.bitmap = bitmap
        gorunum.bolgeBoyut = nsBolge.size
        pencere.contentView = gorunum
        katmanGorunumu = gorunum
        katmanPenceresi = pencere
        pencere.orderFrontRegardless()

        barPenceresi?.orderOut(nil)
        let bar = barPaneliKur(nsBolge: nsBolge, ekran: ekran,
                               motorAdi: motorAdi)
        barPenceresi = bar
        bar.orderFrontRegardless()
    }

    private func simgeDugme(_ simge: String, _ ipucu: String,
                            _ eylem: Selector) -> NSButton {
        let dugme = NSButton(
            image: NSImage(systemSymbolName: simge,
                           accessibilityDescription: ipucu) ?? NSImage(),
            target: self, action: eylem)
        dugme.isBordered = false
        dugme.contentTintColor = .white
        dugme.toolTip = ipucu
        dugme.setFrameSize(NSSize(width: 28, height: 22))
        return dugme
    }

    private func barPaneliKur(nsBolge: NSRect, ekran: NSScreen,
                              motorAdi: String) -> NSPanel {
        let genislik: CGFloat = min(max(nsBolge.width, 440), 580)
        let yukseklik: CGFloat = 38
        var x = nsBolge.midX - genislik / 2
        x = max(ekran.visibleFrame.minX + 8,
                min(x, ekran.visibleFrame.maxX - genislik - 8))
        var y = nsBolge.maxY + 8
        if y + yukseklik > ekran.visibleFrame.maxY {
            y = nsBolge.minY - yukseklik - 8
        }
        let panel = NSPanel(
            contentRect: NSRect(x: x, y: y, width: genislik, height: yukseklik),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered, defer: false)
        panel.level = .floating
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.isMovableByWindowBackground = true
        panel.hidesOnDeactivate = false

        let efekt = NSVisualEffectView(frame: NSRect(
            x: 0, y: 0, width: genislik, height: yukseklik))
        efekt.material = .hudWindow
        efekt.state = .active
        efekt.blendingMode = .behindWindow
        efekt.wantsLayer = true
        efekt.layer?.cornerRadius = 11
        efekt.layer?.masksToBounds = true
        panel.contentView = efekt

        let etiket = NSTextField(labelWithString: motorAdi)
        etiket.textColor = NSColor(white: 0.85, alpha: 1)
        etiket.font = NSFont.systemFont(ofSize: 11)
        etiket.lineBreakMode = .byTruncatingTail
        etiket.frame = NSRect(x: 14, y: 11, width: 150, height: 16)
        efekt.addSubview(etiket)
        motorEtiketi = etiket

        var dx = genislik - 6
        for (simge, ipucu, eylem) in [
            ("xmark", "Kapat", #selector(katmaniKapatTiklandi)),
            ("doc.on.doc", "Çevirileri kopyala", #selector(kopyala)),
            ("eye", "Orijinali göster/gizle", #selector(orijinalDegistir(_:))),
            ("sparkles", "Grok AI ile yeniden çevir", #selector(aiIleCevir)),
            ("keyboard", "Yazdığımı çevir ⌃⌥C (Türkçe → karşı dil)",
             #selector(yazdigimiCevir)),
            ("text.bubble", "Cevap öner (AI + sohbet hafızası)",
             #selector(cevapOnerTiklandi)),
        ] {
            let dugme = simgeDugme(simge, ipucu, eylem)
            dx -= dugme.frame.width + 4
            dugme.setFrameOrigin(NSPoint(x: dx, y: 8))
            efekt.addSubview(dugme)
        }

        let anahtar = NSSwitch()
        anahtar.controlSize = .mini
        anahtar.state = canliAcik ? .on : .off
        anahtar.target = self
        anahtar.action = #selector(canliAnahtar(_:))
        anahtar.sizeToFit()
        dx -= anahtar.frame.width + 14
        anahtar.setFrameOrigin(NSPoint(x: dx, y: 9))
        efekt.addSubview(anahtar)

        let canliEtiket = NSTextField(labelWithString: "Canlı")
        canliEtiket.textColor = NSColor(white: 0.85, alpha: 1)
        canliEtiket.font = NSFont.systemFont(ofSize: 11)
        canliEtiket.sizeToFit()
        dx -= canliEtiket.frame.width + 5
        canliEtiket.setFrameOrigin(NSPoint(x: dx, y: 11))
        efekt.addSubview(canliEtiket)

        return panel
    }

    @objc private func canliAnahtar(_ anahtar: NSSwitch) {
        canliAcik = anahtar.state == .on
        if canliAcik { canliBaslat() } else { canliDurdur() }
    }

    @objc private func katmaniKapatTiklandi() { katmaniKapat() }

    private func katmaniKapat() {
        canliDurdur()
        katmanPenceresi?.orderOut(nil)
        katmanPenceresi = nil
        barPenceresi?.orderOut(nil)
        barPenceresi = nil
        oneriPaneli?.orderOut(nil)
        oneriPaneli = nil
        katmanGorunumu = nil
    }

    @objc private func kopyala() {
        let metin = mevcutBloklar.compactMap { $0.ceviri }
            .joined(separator: "\n")
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(metin, forType: .string)
        motorEtiketi?.stringValue = "Panoya kopyalandı ✓"
    }

    @objc private func orijinalDegistir(_ dugme: NSButton) {
        guard let g = katmanGorunumu else { return }
        g.orijinalGoster.toggle()
        dugme.image = NSImage(
            systemSymbolName: g.orijinalGoster ? "eye.slash" : "eye",
            accessibilityDescription: "Orijinal") ?? dugme.image
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        g.needsDisplay = true
        CATransaction.commit()
    }

    @objc private func aiIleCevir() {
        guard let g = katmanGorunumu, !isSuruyor else { return }
        guard !ayarlar.grokApiKey.isEmpty else {
            motorEtiketi?.stringValue = "Grok anahtarı yok (config.json)"
            return
        }
        isSuruyor = true
        motorEtiketi?.stringValue = "Grok AI çeviriyor…"
        let bloklar = mevcutBloklar
        let ayarlar = self.ayarlar
        isKuyrugu.async {
            // ✨ her koşulda kaliteli model
            var kaliteAyar = ayarlar
            kaliteAyar.hizOnceligi = false
            let sonuc = bloklariCevir(bloklar, motor: "ai", ayarlar: kaliteAyar,
                                      onbellek: self.ceviriOnbellek, zorla: true)
            self.ceviriOnbellek = sonuc.1
        self.uretilenleriKaydet(sonuc.1)
            DispatchQueue.main.async {
                self.isSuruyor = false
                self.motorEtiketi?.stringValue = sonuc.0
                g.stilOnbelleginiTemizle()
                CATransaction.begin()
                CATransaction.setDisableActions(true)
                g.needsDisplay = true
                CATransaction.commit()
            }
        }
    }

    // ---- cevap önerisi

    @objc private func cevapOnerTiklandi() { cevapOner(farkli: false) }

    private func cevapOner(farkli: Bool) {
        guard !oneriSuruyor else { return }
        guard !ayarlar.grokApiKey.isEmpty else {
            motorEtiketi?.stringValue = "Öneri için Grok anahtarı gerekli"
            return
        }
        // Sohbet dökümü: bloklar yukarıdan aşağıya, taraf etiketiyle
        let dokum = mevcutBloklar
            .sorted { $0.rect.minY < $1.rect.minY }
            .filter { $0.hedef }
            .map { "\($0.benim ? "BEN" : "KARŞI"): \($0.metin)" }
        guard !dokum.isEmpty else { return }
        oneriSuruyor = true
        motorEtiketi?.stringValue = "cevap hazırlanıyor…"
        let ayarlar = self.ayarlar
        isKuyrugu.async {
            let oneriler = (try? grokOneri(dokum: dokum, ayarlar: ayarlar,
                                           farkliOlsun: farkli)) ?? []
            DispatchQueue.main.async {
                self.oneriSuruyor = false
                guard !oneriler.isEmpty, !oneriler[0].0.isEmpty else {
                    self.motorEtiketi?.stringValue = "öneri alınamadı"
                    return
                }
                self.motorEtiketi?.stringValue = "öneriler hazır"
                self.oneriGoster(oneriler: oneriler)
            }
        }
    }

    var sonOneriler: [(String, String)] = []

    private func oneriGoster(oneriler: [(String, String)]) {
        oneriPaneli?.orderOut(nil)
        guard let bar = barPenceresi else { return }
        sonOneriler = oneriler

        let genislik: CGFloat = max(420, bar.frame.width)
        var yukseklik: CGFloat = 52 // Butonlar için alt boşluk
        
        let cevapNit: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: 13, weight: .medium)]
        let turkceNit: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: 11)]

        for (cevap, turkce) in oneriler {
            let cevapOlcu = (cevap as NSString).boundingRect(
                with: NSSize(width: genislik - 80, height: 400),
                options: [.usesLineFragmentOrigin, .usesFontLeading], attributes: cevapNit)
            let turkceYuksek: CGFloat = turkce.isEmpty ? 0
                : (turkce as NSString).boundingRect(
                    with: NSSize(width: genislik - 80, height: 200),
                    options: [.usesLineFragmentOrigin, .usesFontLeading],
                    attributes: turkceNit).height + 6
            yukseklik += cevapOlcu.height + turkceYuksek + 20
        }

        var y = bar.frame.minY - yukseklik - 6
        if let ekran = bar.screen, y < ekran.visibleFrame.minY {
            y = bar.frame.maxY + 6
        }
        let panel = NSPanel(
            contentRect: NSRect(x: bar.frame.minX, y: y,
                                width: genislik, height: yukseklik),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered, defer: false)
        panel.level = .floating
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.isMovableByWindowBackground = true
        panel.hidesOnDeactivate = false

        let efekt = NSVisualEffectView(frame: NSRect(
            x: 0, y: 0, width: genislik, height: yukseklik))
        efekt.material = .hudWindow
        efekt.state = .active
        efekt.blendingMode = .behindWindow
        efekt.wantsLayer = true
        efekt.layer?.cornerRadius = 11
        efekt.layer?.masksToBounds = true
        panel.contentView = efekt

        var ustY = yukseklik - 12
        for (i, (cevap, turkce)) in oneriler.enumerated() {
            let cevapOlcu = (cevap as NSString).boundingRect(
                with: NSSize(width: genislik - 80, height: 400),
                options: [.usesLineFragmentOrigin, .usesFontLeading], attributes: cevapNit)
            let turkceYuksek: CGFloat = turkce.isEmpty ? 0
                : (turkce as NSString).boundingRect(
                    with: NSSize(width: genislik - 80, height: 200),
                    options: [.usesLineFragmentOrigin, .usesFontLeading],
                    attributes: turkceNit).height + 6
                    
            let oYukseklik = cevapOlcu.height + turkceYuksek + 20
            ustY -= oYukseklik
            
            let cevapAlani = NSTextField(wrappingLabelWithString: cevap)
            cevapAlani.font = NSFont.systemFont(ofSize: 13, weight: .medium)
            cevapAlani.textColor = .white
            cevapAlani.isSelectable = true
            cevapAlani.frame = NSRect(x: 14, y: ustY + turkceYuksek + 10, width: genislik - 80, height: cevapOlcu.height)
            efekt.addSubview(cevapAlani)
            
            if !turkce.isEmpty {
                let turkceAlani = NSTextField(wrappingLabelWithString: "(\(turkce))")
                turkceAlani.font = NSFont.systemFont(ofSize: 11)
                turkceAlani.textColor = NSColor(white: 0.7, alpha: 1)
                turkceAlani.frame = NSRect(x: 14, y: ustY + 8, width: genislik - 80, height: turkceYuksek - 2)
                efekt.addSubview(turkceAlani)
            }
            
            let kopyaButon = NSButton(title: "Kopyala", target: self, action: #selector(tekOneriKopyala(_:)))
            kopyaButon.bezelStyle = .recessed
            kopyaButon.showsBorderOnlyWhileMouseInside = true
            kopyaButon.controlSize = .small
            kopyaButon.font = NSFont.systemFont(ofSize: 11)
            kopyaButon.tag = i
            kopyaButon.setButtonType(.momentaryPushIn)
            kopyaButon.frame = NSRect(x: genislik - 64, y: ustY + (oYukseklik / 2) - 10, width: 54, height: 20)
            efekt.addSubview(kopyaButon)
            
            if i < oneriler.count - 1 {
                let ayirici = NSBox(frame: NSRect(x: 14, y: ustY, width: genislik - 28, height: 1))
                ayirici.boxType = .separator
                efekt.addSubview(ayirici)
            }
        }

        var dx: CGFloat = genislik - 10
        for (baslik, eylem) in [
            ("Kapat", #selector(oneriKapat)),
            ("Yenile", #selector(oneriYenile))
        ] {
            let dugme = NSButton(title: baslik, target: self, action: eylem)
            dugme.bezelStyle = .rounded
            dugme.controlSize = .small
            dugme.font = NSFont.systemFont(ofSize: 11)
            dugme.sizeToFit()
            dx -= dugme.frame.width + 6
            dugme.setFrameOrigin(NSPoint(x: dx, y: 8))
            efekt.addSubview(dugme)
        }

        oneriPaneli = panel
        panel.orderFrontRegardless()
    }

    @objc private func tekOneriKopyala(_ gonderici: NSButton) {
        let index = gonderici.tag
        guard index >= 0 && index < sonOneriler.count else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(sonOneriler[index].0, forType: .string)
        motorEtiketi?.stringValue = "Öneri \(index + 1) panoya kopyalandı ✓"
    }

    @objc private func oneriYenile() {
        oneriPaneli?.orderOut(nil)
        oneriPaneli = nil
        cevapOner(farkli: true)
    }

    @objc private func oneriKapat() {
        oneriPaneli?.orderOut(nil)
        oneriPaneli = nil
    }
}
