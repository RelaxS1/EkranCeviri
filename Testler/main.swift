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
esit("anahtarla", anahtarla("Hoi! Wie gahts?"), "hoiwiegahts")
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
