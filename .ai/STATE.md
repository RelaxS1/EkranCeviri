# Durum — Ekran Çeviri (Windows)

**Ne:** Ekranda seçilen sohbet alanını okuyup çeviriyi orijinal balonların
üstüne yazan Windows tepsi uygulaması. C# / .NET 8 / WPF.
Almanca lehçeleri (Almanya, Avusturya, İsviçre) → Türkçe.

**Depo:** github.com/RelaxS1/EkranCeviri — yalnız Windows sürümü.
macOS sürümü `6cdc4f0` öncesi commit'lerde duruyor (silindi, geçmişte var).

## Mimari

| Katman | Dosya |
|---|---|
| Giriş, tek örnek kilidi, istisna kancaları | `Program.cs` |
| Beyin: bölge, canlı döngü, kısayol, bekçiler | `Yonetici.cs` |
| Ayarlar + DPAPI anahtar kasası | `Cekirdek/Ayarlar.cs` |
| Model tipleri, motor arayüzü | `Cekirdek/Modeller.cs` |
| Yakalama, OCR, balon gruplama, renk örnekleme | `Ekran/` |
| Lehçe, kalite kapıları, hafıza, motorlar | `Ceviri/` |
| Katman, bölge seçici, tepsi, ayar/anahtar pencereleri | `Arayuz/` |
| Global kısayol, SendInput, pano | `Kisayol/` |
| Ağsız birim testleri (üretim fonksiyonlarını çağırır) | `Testler/` |

## Doğrulama durumu

- **CI (gerçek Windows, `windows-latest`): 54/54 test GEÇTİ**, çalışma
  31247640890. `.exe` üretildi ve v1.0.0 sürümüne eklendi (74 MB).
- macOS'tan çapraz derleme uyarısız geçiyor (`EnableWindowsTargeting`).
- Saf mantık testleri (44) macOS'ta da üretim kaynaklarına karşı koşuyor.
- **Gerçek makinede kullanıcı denemesi HENÜZ YAPILMADI** — ekran
  yakalama, yama konumları, balon renk örneklemesi ve kısayolun gerçek
  sohbet uygulamalarındaki davranışı görülmedi. İlk geri bildirimde
  ayar gerekmesi bekleniyor.

## Açık kapılar / sonraki adımlar

- Kod imzası yok → SmartScreen "bilinmeyen yayımcı" uyarısı veriyor.
- Yalnız `win-x64`; ARM Windows için ayrı yayın gerekir.
- `Ekran/Bloklayici.cs` eşikleri WhatsApp Desktop'a göre ayarlandı;
  Telegram/Discord'da yeniden bakılması gerekebilir.
- Windows.Graphics.Capture'a geçiş: BitBlt bazı GPU-hızlandırmalı
  pencerelerde eksik yakalayabilir (henüz gözlenmedi).

## Değişmezler (bozulursa kullanıcı zarar görür)

1. Anahtar ayar dosyasına/pakete/depoya **asla** yazılmaz.
2. Giden mesajda rakam/saat/fiyat korunumu — geçilmezse yapıştırma iptal.
3. Ekrandan okunan metin sistem istemine girmez.
4. Kısayol bilinmeyen uygulamada ve uzun seçimde çalışmaz (veri kaybı).
5. Katman `WDA_EXCLUDEFROMCAPTURE` ile yakalama dışında (kendi çevirimizi
   tekrar çevirmemek için).
6. Testler üretim fonksiyonlarını çağırır; CI test kapısı atlanamaz.
7. GDI DC/bitmap ve LockBits daima serbest bırakılır.
