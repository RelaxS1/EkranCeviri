// Temel doğrulama testleri — ağ YOK, tümü belirlenimci.
import AppKit
import Carbon.HIToolbox

var hata = 0
func esit<T: Equatable>(_ ad: String, _ a: T, _ b: T) {
    if a == b { print("  ✓ \(ad)") }
    else { print("  ✗ \(ad): \(a) ≠ \(b)"); hata += 1 }
}
func dogru(_ ad: String, _ k: Bool) { esit(ad, k, true) }

print("Metin:")
esit("anahtarla (soru işareti korunur — anlamı değiştirir)",
     anahtarla("Hoi! Wie gahts?"), "hoiwiegahts?")
esit("düz cümle ayrı anahtar", anahtarla("Hoi! Wie gahts"), "hoiwiegahts")
dogru("soru ve düz cümle FARKLI anahtar",
      anahtarla("Chunnsch morn?") != anahtarla("Chunnsch morn"))
dogru("karisikMi yarı karışımı yakalar",
      karisikMi("bin nur drei tag in rom gewesen",
                "bin nur 3 tag in rom gsi kaldım"))
dogru("karisikMi temiz çeviriyi geçirir",
      !karisikMi("wie geht es dir heute", "bugün nasılsın"))
esit("gidenFormatla", gidenFormatla("Merhaba, Nasılsın?!"), "Merhaba nasılsın")
esit("kisayolMetni", kisayolMetni(8, controlKey | optionKey), "⌃⌥C")
esit("tusAdi F5", tusAdi(96), "F5")
esit("çit temizleme", citCizgileriniAt("```json\n[\"a\"]\n```"), "[\"a\"]")


print("Lehçe normalizasyonu:")
esit("chunnsch→kommst",
     lehceyiStandartlastir("Chunnsch du morn au id Stadt?"),
     "Kommst du morgen auch in die Stadt?")
esit("saat kalıbı (açık saat + nicht wahr)",
     lehceyiStandartlastir("So am halbi sächsi bim Bahnhof, gäll"),
     "So am fünf uhr dreissig beim Bahnhof, nicht wahr")
esit("selamlama", lehceyiStandartlastir("Hoi! Wie gahts dir hüt?"),
     "Hallo! Wie geht es dir heute?")
dogru("standart Almanca bozulmaz",
      lehceyiStandartlastir("Guten Morgen, wie geht es dir?")
        == "Guten Morgen, wie geht es dir?")
dogru("emoji ve noktalama korunur",
      lehceyiStandartlastir("Hoi 😊, bis spöter!") == "Hallo 😊, bis später!")

esit("emoji korunur",
     emojileriKoru(kaynak: "Was machsch grad? 😊", ceviri: "Ne yapıyorsun?"),
     "Ne yapıyorsun? 😊")
esit("çeviride varsa tekrar eklenmez",
     emojileriKoru(kaynak: "Hoi 😊", ceviri: "Merhaba 😊"), "Merhaba 😊")

esit("sevgi kalıbı",
     lehceyiStandartlastir("Ich ha di gärn, weisch das?"),
     "Ich habe dich gern, weisst du das?")
esit("hafta sonu", lehceyiStandartlastir("Hesch zit am wuchenänd?"),
     "Hast du zeit am wochenende?")

print("Bulanık önbellek (OCR titremesi):")
let bellekOrnek = ["wurschmerchurzenmemomache": "bana kısa bir not yaz",
                   "hoiwiegahtsdirhut": "selam bugün nasılsın"]
esit("OCR titremesi yakalanır",
     bulanikBul("wurschmerchurzenmemomake", bellekOrnek) ?? "yok",
     "bana kısa bir not yaz")
dogru("farklı mesaj eşleşmez",
      bulanikBul("chaschmirestotischicke", bellekOrnek) == nil)
dogru("kısa metinde bulanık kapalı",
      bulanikBul("hoiwie", bellekOrnek) == nil)
dogru("mesafe erken çıkış", !mesafeAzMi("abcdefghij", "zzzzzzzzzz", enFazla: 2))

print("Hiza koruması (model indeksi güvenilmez):")
func hizaTesti(_ json: String, _ n: Int) -> [String] {
    var liste = [String](repeating: "", count: n)
    guard let d = try? JSONSerialization.jsonObject(with: Data(json.utf8))
            as? [String: Any],
          let kayitlar = d["ceviriler"] as? [[String: Any]] else { return liste }
    let ceviriler = kayitlar.map { ($0["ceviri"] as? String) ?? "" }
    if ceviriler.count == n { return ceviriler }
    let indeksler = kayitlar.compactMap { $0["indeks"] as? Int }
    let kaydir = (indeksler.max() ?? 0) >= n ? 1 : 0
    for (i2, kayit) in kayitlar.enumerated() {
        let ham = (kayit["indeks"] as? Int) ?? (i2 + kaydir)
        let i = ham - kaydir
        if i >= 0 && i < n { liste[i] = (kayit["ceviri"] as? String) ?? "" }
    }
    return liste
}
esit("1-tabanlı indeks kaymaz",
     hizaTesti("{\"ceviriler\":[{\"indeks\":1,\"ceviri\":\"bir\"},{\"indeks\":2,\"ceviri\":\"iki\"}]}", 2),
     ["bir", "iki"])
esit("eksik öğe doğru yere",
     hizaTesti("{\"ceviriler\":[{\"indeks\":2,\"ceviri\":\"iki\"}]}", 3),
     ["", "", "iki"])

print("Durum makinesi (takılma kurtarma):")
let dTest = UygulamaDelege()
dogru("boştayken hazır", dTest.cevirmeyeHazir())
dTest.isSuruyor = true; dTest.isBaslangic = Date()
dogru("taze iş korunur", !dTest.cevirmeyeHazir())
dTest.isBaslangic = Date().addingTimeInterval(-20)
dogru("takılı iş otomatik sıfırlanır", dTest.cevirmeyeHazir())
dogru("bayrak temizlendi", !dTest.isSuruyor)
dTest.gidenSuruyor = true
dTest.gidenBaslangic = Date().addingTimeInterval(-40)
dogru("giden kilidi kırılır", dTest.gidenHazir())
dogru("giden bayrağı temizlendi", !dTest.gidenSuruyor)

print("Kayma telafisi (yeni mesaj gelince):")
var izA = [Float](repeating: 0, count: 256)
for i in 60..<70 { izA[i] = 200 }
for i in 120..<130 { izA[i] = 180 }
var izB = [Float](repeating: 0, count: 256)   // 12 satır YUKARI kaymış
for i in 48..<58 { izB[i] = 200 }
for i in 108..<118 { izB[i] = 180 }
let (kayma, benzerlik) = dikeyKayma(izA, izB)
esit("kayma miktarı doğru ölçüldü", kayma, -12)
dogru("benzerlik yüksek", benzerlik > 0.55)
let (k2, b2) = dikeyKayma(izA, izA)
esit("kayma yoksa 0", k2, 0)
dogru("aynı kare tam benzer", b2 > 0.9)
var gurultu = [Float](repeating: 0, count: 256)
for i in 0..<256 { gurultu[i] = Float((i * 37) % 255) }
let (_, b3) = dikeyKayma(izA, gurultu)
dogru("alakasız içerikte benzerlik düşük", b3 < 0.55)

print("Hafıza hijyeni:")
dogru("çok kısa anahtar (<4) kaydedilmez", { let h = CeviriHafizasi.paylasilan
    h.yaz("ok", "tamam", dil: "tr"); return h.ara("ok", dil: "tr") == nil }())
dogru("kısa metinde bulanık eşleşme YOK (yanlış çeviri riski)",
      bulanikBul("selamnasilsin", ["selamnasilsn": "merhaba"]) == nil)
dogru("Türkçe kaynak hafızaya yazılmaz (kendi çıktımız)",
      { let h = CeviriHafizasi.paylasilan
        h.yaz("bugunnasilsinbebegim", "bugün nasılsın bebeğim", dil: "tr",
              kaynakMetin: "bugün nasılsın bebeğim")
        return h.ara("bugunnasilsinbebegim", dil: "tr") == nil }())
dogru("rakamlar farklıysa bulanık eşleşmez",
      bulanikBul("dominachat30dakika55frank",
                 ["dominachat60dakika85frank": "60 dakika 85 frank"]) == nil)
dogru("rakamlar aynıysa eşleşir",
      bulanikBul("dominachat30dakika55frank",
                 ["dominachat30dakika55frnak": "30 dakika 55 frank"]) != nil)

print("Blok eşleştirme güvenliği:")
func blokYap(_ t: String, _ y: CGFloat) -> Blok {
    Blok(OCRSatiri(metin: t, rect: CGRect(x: 10, y: y, width: 200, height: 18)))
}
// Aynı konumda AMA farklı metin: eski çeviri yapışMAMALI
let eskiB = blokYap("Chunnsch du morn au id Stadt?", 100)
let yeniB = blokYap("Ich ha kei zit für das hüt", 100)
func benzerMi(_ a: Blok, _ b: Blok) -> Bool {
    if a.anahtar == b.anahtar { return true }
    let kesen = a.rect.intersection(b.rect)
    guard !kesen.isNull, !kesen.isEmpty else { return false }
    let birlesim = a.rect.width * a.rect.height + b.rect.width * b.rect.height
                 - kesen.width * kesen.height
    guard birlesim > 0 else { return false }
    guard kesen.width * kesen.height / birlesim > 0.45 else { return false }
    let uzunluk = max(a.anahtar.count, b.anahtar.count)
    guard uzunluk > 0, abs(a.anahtar.count - b.anahtar.count) <= 3 else { return false }
    return mesafeAzMi(a.anahtar, b.anahtar, enFazla: max(1, min(3, uzunluk / 10)))
}
dogru("aynı konum + farklı metin eşleşmez (çeviri yanlış mesaja yapışmaz)",
      !benzerMi(eskiB, yeniB))
let ocrTitrek = blokYap("Chunnsch du morn au id Stadl?", 102)
dogru("OCR titremesi hâlâ eşleşir", benzerMi(eskiB, ocrTitrek))

print("OCR saat damgası (fiyat/tarih korunmalı):")
func saatBulunanlar(_ s: String) -> [String] {
    let ns = NSRange(s.startIndex..<s.endIndex, in: s)
    return (icSaatDeseni?.matches(in: s, options: [], range: ns) ?? [])
        .compactMap { Range($0.range, in: s).map { r in String(s[r]) } }
}
dogru("gerçek saat yakalanır", saatBulunanlar("Bis 14:32 dann") == ["14:32"])
dogru("fiyat silinmez (12.50)", saatBulunanlar("das kostet 12.50 franken").isEmpty)
dogru("tarih silinmez (12.05)", saatBulunanlar("am 12.05 hämmer termin").isEmpty)
dogru("aralık silinmez (10-15)", saatBulunanlar("in 10-15 minute").isEmpty)
dogru("geçersiz saat yakalanmaz (25:99)", saatBulunanlar("kod 25:99 test").isEmpty)

print("Kalite kapıları:")
dogru("Almanca kalıntı yakalanır", almancaKalintiVar("Ich mues no chli çalışmak"))
dogru("temiz Türkçe geçer", !almancaKalintiVar("Yarın şehre geliyor musun"))
dogru("Türkçe sızıntı yakalanır", turkceKalintiVar("Okey bis spöter canım"))
dogru("temiz lehçe geçer",
      !turkceKalintiVar("Okey bis spöter ich mues no chli schaffe"))

dogru("Hochdeutsch örnekleri standart", gidenOrnekler("Hochdeutsch").contains("ich hab zeit"))
dogru("Bayrisch örnekleri Bavyera", gidenOrnekler("Bayrisch").contains("i hob zeit"))
dogru("Bern örnekleri Bern", gidenOrnekler("Bärndütsch").contains("i ha ziit"))

print("Alman modu ve kimlik:")
var av = Ayarlar()
dogru("varsayılan Alman modu", av.dilModu == "alman")
dogru("varsayılan kimlik kadın→erkek",
      av.benCinsiyet == "kadin" && av.karsiCinsiyet == "erkek")
dogru("varsayılan +18 açık", av.yetiskin)
dogru("Alman modu Almanya+İsviçre kapsar",
      modTanimi(av).contains("Almanya") && modTanimi(av).contains("İsviçre"))
av.dilModu = "isvicre"
dogru("İsviçre modu yalnız CH", !modTanimi(av).contains("Bavyera"))
dogru("kimlik cümlesi kadın-erkek",
      kimlikTanimi(Ayarlar()).contains("kadın")
      && kimlikTanimi(Ayarlar()).contains("erkek"))
var ay = Ayarlar(); ay.benCinsiyet = "yok"; ay.karsiCinsiyet = "yok"
dogru("kimlik belirtilmezse boş", kimlikTanimi(ay).isEmpty)

print("Lehçe algılama:")
esit("Zürih", lehceyiAlgila(["Chunnsch du morn au id Stadt?",
                             "Ich ha nöd chli Zit hüt"]).kisa, "Züridütsch")
esit("Bern", lehceyiAlgila(["Itz mues i gäng wärche u de chum i"]).kisa,
     "Bärndütsch")
esit("Basel", lehceyiAlgila(["Nit vyl zyt hüt, ych nimm s Drämmli"]).kisa,
     "Baseldytsch")
esit("standart Almanca", lehceyiAlgila(["Guten Morgen, wie geht es dir?"]).kisa,
     "Hochdeutsch")
esit("Bavyera/Avusturya",
     lehceyiAlgila(["Servus, hob i ned gsehn oida"]).kisa, "Bayrisch")
esit("Kuzey Almanya",
     lehceyiAlgila(["Moin, wat machst du nich so?"]).kisa, "Norddeutsch")
dogru("belirsiz İsviçre Almancası",
      lehceyiAlgila(["merci vilmal isch guet"]).kisa.contains("İsviçre"))

print("Blok gruplama (regresyon):")
func nb(_ x: CGFloat, _ y: CGFloat, _ w: CGFloat, _ h: CGFloat) -> CGRect {
    CGRect(x: x / 400, y: 1 - (y + h) / 300, width: w / 400, height: h / 300)
}
let satirlar: [(String, CGRect)] = [
    ("hallo wie geht", nb(20, 20, 120, 16)),
    ("es dir heute",   nb(20, 40, 100, 16)),
    ("neue nachricht", nb(20, 90, 120, 16)),
    ("tamam gelirim",  nb(260, 130, 120, 16)),
    ("14:32",          nb(150, 170, 40, 12)),
    ("Bugün",          nb(180, 200, 44, 14)),
]
let (bloklar, sessizler) = bloklaraAyir(satirlar,
                                        boyut: CGSize(width: 400, height: 300))
esit("sessiz kutu", sessizler.count, 1)
esit("hedef blok", bloklar.filter { $0.hedef }.count, 3)
dogru("iki satır tek balonda", bloklar.contains { $0.satirlar.count == 2 })
dogru("ayrı balonlar birleşmedi",
      bloklar.filter { !$0.benim && $0.hedef }.count == 2)
dogru("sağ balon benim",
      bloklar.first { $0.metin.hasPrefix("tamam") }?.benim == true)
dogru("ortalanmış dar blok atlandı",
      bloklar.first { $0.metin == "Bugün" }?.atla == true)

print(hata == 0 ? "\nTÜM TESTLER GEÇTİ ✅" : "\n\(hata) TEST BAŞARISIZ ❌")
exit(hata == 0 ? 0 : 1)
