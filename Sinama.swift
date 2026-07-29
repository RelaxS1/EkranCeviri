import AppKit
import ScreenCaptureKit

// MARK: - Sınama sahnesi (QA düzeneği, --sinama)

final class SahteSohbet: NSView {
    struct Mesaj { let metin: String; let benim: Bool }
    var mesajlar: [Mesaj] = []
    var kaydirmaY: CGFloat = 0
    override var isFlipped: Bool { true }

    override func draw(_ kirli: NSRect) {
        NSColor(red: 0.055, green: 0.08, blue: 0.09, alpha: 1).setFill()
        bounds.fill()
        var y: CGFloat = 16 - kaydirmaY
        let cip = "HEUTE" as NSString
        let cipN: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: 11),
            .foregroundColor: NSColor(white: 0.75, alpha: 1)]
        let co = cip.size(withAttributes: cipN)
        let cr = NSRect(x: bounds.midX - co.width / 2 - 10, y: y,
                        width: co.width + 20, height: co.height + 8)
        NSColor(white: 0.16, alpha: 1).setFill()
        NSBezierPath(roundedRect: cr, xRadius: 6, yRadius: 6).fill()
        cip.draw(at: NSPoint(x: cr.minX + 10, y: cr.minY + 4),
                 withAttributes: cipN)
        y = cr.maxY + 14
        for m in mesajlar { y = balonCiz(m, y) + 12 }
    }

    private func balonCiz(_ m: Mesaj, _ y: CGFloat) -> CGFloat {
        let f = NSFont.systemFont(ofSize: 14)
        let maxW = bounds.width * 0.62
        let olcu = (m.metin as NSString).boundingRect(
            with: NSSize(width: maxW, height: 999),
            options: [.usesLineFragmentOrigin], attributes: [.font: f])
        let bw = olcu.width + 26, bh = olcu.height + 26
        let bx: CGFloat = m.benim ? bounds.width - bw - 14 : 14
        (m.benim ? NSColor(red: 0, green: 0.36, blue: 0.29, alpha: 1)
                 : NSColor(red: 0.15, green: 0.18, blue: 0.19, alpha: 1)).setFill()
        NSBezierPath(roundedRect: NSRect(x: bx, y: y, width: bw, height: bh),
                     xRadius: 9, yRadius: 9).fill()
        (m.metin as NSString).draw(
            in: NSRect(x: bx + 13, y: y + 6, width: maxW, height: olcu.height + 4),
            withAttributes: [.font: f, .foregroundColor: NSColor.white])
        ("21:04" as NSString).draw(
            at: NSPoint(x: bx + bw - 36, y: y + bh - 15),
            withAttributes: [.font: NSFont.systemFont(ofSize: 9),
                             .foregroundColor: NSColor(white: 0.6, alpha: 1)])
        return y + bh
    }
}

final class SinamaSahnesi {
    let pencere: NSWindow
    let gorunum: SahteSohbet

    init() {
        // ANA ekranda konumlan: ikincil ekranda AppKit çizimi kısıyor
        // (QA'da doğrulandı: pencere X=-1175'teyken hiç repaint olmadı)
        let ana = (NSScreen.main ?? NSScreen.screens[0]).visibleFrame
        pencere = NSWindow(
            contentRect: NSRect(x: ana.midX - 270, y: ana.midY - 235,
                                width: 540, height: 470),
            styleMask: [.titled], backing: .buffered, defer: false)
        pencere.title = "Sahte Sohbet (sınama)"
        gorunum = SahteSohbet()
        gorunum.mesajlar = [
            .init(metin: "Hoi! Wie gahts dir hüt?", benim: false),
            .init(metin: "Chunnsch du morn au id Stadt?", benim: false),
            .init(metin: "Jaa gärn! Um welchi Zit träffe mer eus?", benim: true),
            .init(metin: "So am halbi sächsi bim Bahnhof, gäll", benim: false),
        ]
        pencere.contentView = gorunum
        pencere.orderFrontRegardless()
        sinamaIstisnaPencere = CGWindowID(pencere.windowNumber)
    }

    func bolgeNS() -> NSRect {
        let r = gorunum.convert(gorunum.bounds, to: nil)
        return pencere.convertToScreen(r)
    }

    func mesajEkle(_ metin: String, benim: Bool) {
        gorunum.mesajlar.append(.init(metin: metin, benim: benim))
        gorunum.needsDisplay = true
        gorunum.display()          // senkron çizim: throttle'a takılmasın
        NSLog("EC-sinama: mesaj sayısı=\(gorunum.mesajlar.count)")
    }

    /// Dayanıklılık testi: uzun süre boyunca düzenli mesaj + kaydırma.
    func soakBaslat(mesajlar: [String], aralik: Double, adet: Int) {
        for i in 0..<adet {
            let t = Double(i) * aralik + 8
            DispatchQueue.main.asyncAfter(deadline: .now() + t) { [self] in
                mesajEkle(mesajlar[i % mesajlar.count], benim: i % 3 == 2)
                if i % 4 == 3 { kaydir(90) }
                NSLog("EC-soak: tur \(i + 1)/\(adet) (t+\(Int(t))sn)")
            }
        }
    }

    func kaydir(_ d: CGFloat) {
        gorunum.kaydirmaY += d
        gorunum.needsDisplay = true
        gorunum.display()
        NSLog("EC-sinama: kaydırma=\(gorunum.kaydirmaY)")
    }
}



// MARK: - Gizli (ekran dışı) QA düzeneği: hiçbir pencere açılmaz
// Sahte sohbet BELLEKTE çizilir, gerçek OCR + çeviri + katman render'ı
// dosyaya yazılır. Kullanıcının ekranına hiçbir şey çıkmaz.

final class GizliSinama {
    /// NOT: hem SahteSohbet hem KatmanGorunumu isFlipped=true. Ekran dışı
    /// NSImage bağlamı varsayılan olarak TERS (sol-alt orijin) olduğundan
    /// çizim mutlaka flipped:true bağlamda yapılmalı; yoksa yamalar dikey
    /// aynalanır ve gerçekte olmayan bir hizasızlık görünür (QA'da yaşandı).
    static func sahneGorunumu(_ mesajlar: [SahteSohbet.Mesaj],
                              _ kaydirma: CGFloat,
                              _ boyut: NSSize) -> SahteSohbet {
        let g = SahteSohbet(frame: NSRect(origin: .zero, size: boyut))
        g.mesajlar = mesajlar
        g.kaydirmaY = kaydirma
        return g
    }

    static func sahneGoruntusu(mesajlar: [SahteSohbet.Mesaj],
                               kaydirma: CGFloat,
                               boyut: NSSize) -> NSImage {
        let g = sahneGorunumu(mesajlar, kaydirma, boyut)
        return NSImage(size: boyut, flipped: true) { _ in
            g.draw(g.bounds)
            return true
        }
    }

    static func cgResim(_ img: NSImage) -> CGImage? {
        var r = NSRect(origin: .zero, size: img.size)
        return img.cgImage(forProposedRect: &r, context: nil, hints: nil)
    }

    /// Bir turu uçtan uca koşar ve birleşik PNG yazar.
    @discardableResult
    static func tur(_ ad: String, mesajlar: [SahteSohbet.Mesaj],
                    kaydirma: CGFloat, ayarlar: Ayarlar,
                    onbellek: inout [String: String]) -> String {
        let boyut = NSSize(width: 540, height: 470)
        let sahne = sahneGoruntusu(mesajlar: mesajlar, kaydirma: kaydirma,
                                   boyut: boyut)
        guard let cg = cgResim(sahne) else { return "\(ad): görüntü yok" }
        guard let satirlar = try? ocrYap(cg, diller: ayarlar.ocrDilleri) else {
            return "\(ad): OCR başarısız"
        }
        let (bloklar, sessizler) = bloklaraAyir(satirlar, boyut: boyut)
        let sonuc = bloklariCevir(bloklar, motor: ayarlar.motor,
                                  ayarlar: ayarlar, onbellek: onbellek)
        onbellek = sonuc.1

        // Katmanı ekran dışında render et
        let katman = KatmanGorunumu(frame: NSRect(origin: .zero, size: boyut))
        katman.bloklar = bloklar
        katman.sessizKutular = sessizler
        katman.bitmap = NSBitmapImageRep(cgImage: cg)
        katman.bolgeBoyut = boyut

        let sahneG = sahneGorunumu(mesajlar, kaydirma, boyut)
        let birlesik = NSImage(size: boyut, flipped: true) { _ in
            sahneG.draw(sahneG.bounds)
            katman.draw(katman.bounds)
            return true
        }
        if let b = cgResim(birlesik),
           let veri = NSBitmapImageRep(cgImage: b)
               .representation(using: .png, properties: [:]) {
            try? veri.write(to: URL(fileURLWithPath: "/tmp/qa_gizli_\(ad).png"))
        }
        let cevrilen = bloklar.filter { $0.ceviri != nil && !$0.ceviri!.isEmpty }
        var rapor = "\(ad): \(bloklar.count) blok, motor=\(sonuc.0), "
                  + "çevrilen=\(cevrilen.count)/\(bloklar.filter { $0.hedef }.count)\n"
        for b in bloklar where b.hedef {
            rapor += "   • \(b.metin)\n     → \(b.ceviri ?? "‼️ ÇEVRİLMEDİ")\n"
        }
        return rapor
    }

    static func calistir(ayarlar: Ayarlar) {
        var onbellek: [String: String] = [:]
        var mesajlar: [SahteSohbet.Mesaj] = [
            .init(metin: "Hoi! Wie gahts dir hüt?", benim: false),
            .init(metin: "Chunnsch du morn au id Stadt?", benim: false),
            .init(metin: "Jaa gärn! Um welchi Zit träffe mer eus?", benim: true),
            .init(metin: "So am halbi sächsi bim Bahnhof, gäll", benim: false),
        ]
        print(tur("1_ilk", mesajlar: mesajlar, kaydirma: 0,
                  ayarlar: ayarlar, onbellek: &onbellek))
        mesajlar.append(.init(metin: "Was machsch grad? 😊", benim: false))
        print(tur("2_yenimesaj", mesajlar: mesajlar, kaydirma: 0,
                  ayarlar: ayarlar, onbellek: &onbellek))
        mesajlar.append(.init(metin: "Muss hüt no chli schaffe, bis spöter!",
                              benim: false))
        print(tur("3_kaydirma", mesajlar: mesajlar, kaydirma: 70,
                  ayarlar: ayarlar, onbellek: &onbellek))
        mesajlar.append(.init(metin: "Ich ha di gärn, weisch das? 😘",
                              benim: false))
        mesajlar.append(.init(metin: "Hesch zit am wuchenänd?", benim: false))
        print(tur("4_ikimesaj", mesajlar: mesajlar, kaydirma: 140,
                  ayarlar: ayarlar, onbellek: &onbellek))
        // ZOR SENARYO: gerçek ekran görüntülerinden alınan bozuk OCR,
        // fiyat, emoji, uzun paragraf, karışık dil, çok kısa mesaj
        var zor: [SahteSohbet.Mesaj] = [
            .init(metin: "danke babe biz heiss machsch au trffe?", benim: false),
            .init(metin: "Dominachat 30 dk 55.- / 60 dk 85.-", benim: true),
            .init(metin: "Okey 👍", benim: false),
            .init(metin: "Ich ha di ganzi wuche gschaffet und bi mega müed, "
                       + "aber am samschtig hätti zit zum uns träffe wenn du "
                       + "au chasch, susch mached mers nöchsti wuche gäll",
                  benim: false),
            .init(metin: "sorry i was busy, schriib dir spöter ok?", benim: true),
        ]
        print(tur("5_zor", mesajlar: zor, kaydirma: 0,
                  ayarlar: ayarlar, onbellek: &onbellek))
        print("ÖNBELLEK: \(onbellek.count) kayıt (tekrar çeviri yok)")
    }
}


// MARK: - Gizli dayanıklılık testi (--gizli-soak <dakika>)
// Ekrana hiçbir şey çıkarmadan, gerçek boru hattını (OCR + çeviri + render)
// ve gerçek SCK yakalama yolunu dakikalarca yorar; bellek ve başarı oranı
// raporlanır. Kullanıcı bilgisayarı kullanırken güvenle çalışır.

enum GizliSoak {
    static func bellekMB() -> Double {
        var bilgi = mach_task_basic_info()
        var sayi = mach_msg_type_number_t(
            MemoryLayout<mach_task_basic_info>.size / MemoryLayout<natural_t>.size)
        let sonuc = withUnsafeMutablePointer(to: &bilgi) {
            $0.withMemoryRebound(to: integer_t.self, capacity: Int(sayi)) {
                task_info(mach_task_self_, task_flavor_t(MACH_TASK_BASIC_INFO),
                          $0, &sayi)
            }
        }
        return sonuc == KERN_SUCCESS
            ? Double(bilgi.resident_size) / 1_048_576 : -1
    }

    static func calistir(ayarlar: Ayarlar, dakika: Double) {
        let bitis = Date().addingTimeInterval(dakika * 60)
        let mesajlar = [
            "Hesch hüt scho öppis vor?",
            "Ich mues no schnäll id Migros",
            "Ja klar, chum vorbi wenn d wottsch 😊",
            "Wie lang bisch no da?",
            "Merci vilmal für alles!",
            "Chasch mir es foti schicke?",
            "Bin grad am schaffe, schriib spöter",
            "Was machsch am wuchenänd?",
            "Servus, wia gehts da heid?",
            "Moin, wat machst du so?",
        ]
        var onbellek: [String: String] = [:]
        var tur = 0, basarili = 0, basarisiz = 0
        let baslangicBellek = bellekMB()
        // Gerçek yakalama yolu da yorulsun (küçük bir bölge, ekranda iz yok)
        let bolge = CGRect(x: 0, y: 0, width: 320, height: 200)
        let yakalayici = try? sckFiltreKur(bolge)
        print("GİZLİ DAYANIKLILIK: \(dakika) dk, başlangıç bellek "
            + "\(String(format: "%.1f", baslangicBellek)) MB")

        while Date() < bitis { autoreleasepool {
            tur += 1
            // a) gerçek ekran yakalama + OCR (yakalama yolunu yorar)
            if let g = bolgeGoruntusu(bolge, yakalayici: yakalayici, olcek: 2,
                                      yedekKullan: false) {
                _ = try? ocrYap(g, diller: ayarlar.ocrDilleri)
            }
            // b) çeviri boru hattı: her turda 1 YENİ + 2 eski mesaj
            let yeni = "\(mesajlar[tur % mesajlar.count]) (\(tur))"
            var bloklar: [Blok] = []
            for (i, metin) in [yeni, mesajlar[0], mesajlar[1]].enumerated() {
                let b = Blok(OCRSatiri(metin: metin,
                    rect: CGRect(x: 10, y: CGFloat(i) * 30, width: 220, height: 18)))
                bloklar.append(b)
            }
            let sonuc = bloklariCevir(bloklar, motor: ayarlar.motor,
                                      ayarlar: ayarlar, onbellek: onbellek)
            onbellek = sonuc.1
            if bloklar.allSatisfy({ !($0.ceviri ?? "").isEmpty }) { basarili += 1 }
            else { basarisiz += 1 }
            if tur % 5 == 0 {
                print("  tur \(tur): başarı \(basarili) hata \(basarisiz) "
                    + "· bellek \(String(format: "%.1f", bellekMB())) MB "
                    + "· önbellek \(onbellek.count) kayıt · motor \(sonuc.0)")
            }
            Thread.sleep(forTimeInterval: 8)
        } }
        let bitisBellek = bellekMB()
        print("SONUÇ: \(tur) tur · başarı \(basarili) · hata \(basarisiz) "
            + "· bellek \(String(format: "%.1f", baslangicBellek)) → "
            + "\(String(format: "%.1f", bitisBellek)) MB "
            + "(artış \(String(format: "%.1f", bitisBellek - baslangicBellek)) MB)")
    }
}
