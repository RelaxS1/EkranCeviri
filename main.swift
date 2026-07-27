import AppKit

// MARK: - Giriş

let uygulama = NSApplication.shared
uygulama.setActivationPolicy(.accessory)
let delege = UygulamaDelege()
uygulama.delegate = delege
uygulama.run()
