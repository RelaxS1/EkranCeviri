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
        pencere = NSWindow(
            contentRect: NSRect(x: 220, y: 200, width: 540, height: 470),
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
    }

    func kaydir(_ d: CGFloat) {
        gorunum.kaydirmaY += d
        gorunum.needsDisplay = true
    }
}

