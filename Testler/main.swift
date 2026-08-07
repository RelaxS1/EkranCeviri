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
esit("saat bozulmaz", gidenFormatla("Treffe mer am 17:30"), "Treffe mer am 17:30")
esit("ondalık bozulmaz", gidenFormatla("Das sind 1,5 stund"), "Das sind 1,5 stund")
esit("fiyat bozulmaz", gidenFormatla("Kostet 150.- franken"), "Kostet 150.- franken")
esit("tarih bozulmaz", gidenFormatla("Am 12.05 treffe"), "Am 12.05 treffe")
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

esit("bileşik emoji bozulmaz",
     emojileriKoru(kaynak: "seni seviyorum ❤️", ceviri: "ich liebe dich"),
     "ich liebe dich ❤️")
esit("boş çeviriye emoji eklenmez",
     emojileriKoru(kaynak: "hallo 😊", ceviri: ""), "")
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

print("Hiza koruması — ÜRETİM fonksiyonu (kopya değil):")
esit("1-tabanlı indeks kaymaz",
     cevirileriYerlestir([["indeks": 1, "ceviri": "bir"],
                          ["indeks": 2, "ceviri": "iki"]], adet: 2),
     ["bir", "iki"])
esit("karışık sıra indeksle düzelir",
     cevirileriYerlestir([["indeks": 2, "ceviri": "üç"],
                          ["indeks": 0, "ceviri": "bir"],
                          ["indeks": 1, "ceviri": "iki"]], adet: 3),
     ["bir", "iki", "üç"])
esit("eksik öğe doğru yere",
     cevirileriYerlestir([["indeks": 2, "ceviri": "iki"]], adet: 3),
     ["", "", "iki"])
esit("indeks yoksa dizi sırası",
     cevirileriYerlestir([["ceviri": "a"], ["ceviri": "b"]], adet: 2),
     ["a", "b"])

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

print("Blok eşleştirme — ÜRETİM fonksiyonu (kopya değil):")
func blokYap(_ t: String, _ yk: CGFloat) -> Blok {
    Blok(OCRSatiri(metin: t, rect: CGRect(x: 10, y: yk, width: 200, height: 18)))
}
dogru("aynı konum + farklı metin eşleşmez (çeviri yanlış mesaja yapışmaz)",
      !bloklarEslesirMi(blokYap("Chunnsch du morn au id Stadt?", 100),
                        blokYap("Ich ha kei zit für das hüt", 100)))
dogru("OCR titremesi hâlâ eşleşir",
      bloklarEslesirMi(blokYap("Chunnsch du morn au id Stadt?", 100),
                       blokYap("Chunnsch du morn au id Stadl?", 102)))
dogru("aynı metin kaymış olsa da eşleşir (mesaj yukarı kaydı)",
      bloklarEslesirMi(blokYap("Hoi wie gahts", 100),
                       blokYap("Hoi wie gahts", 400)))
dogru("uzak konum + farklı metin eşleşmez",
      !bloklarEslesirMi(blokYap("Hoi wie gahts dir hüt", 100),
                        blokYap("Ich mues no chli schaffe", 400)))

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
dogru("tek gündelik kelime yanlış lehçeye kaymaz",
      lehceyiAlgila(["Ich hab das nich gesehen, was machst du?"]).kisa
        == "Hochdeutsch")
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
