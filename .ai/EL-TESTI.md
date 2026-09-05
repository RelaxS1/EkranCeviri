# Windows el-testi listesi — v1.2.0 (macOS'tan doğrulanamayanlar)

Bu maddeler macOS çapraz derlemede ÇALIŞTIRILAMAZ; CI yalnız derleme + birim
testleri koşar. Sahip gerçek Windows'ta bir kez geçmeli. Arıza olursa
`%APPDATA%\EkranCeviri\ariza.log` ve `teshis.log` ile gel.

## Temel akış
- [ ] Tepsi simgesi çift tık → bölge seç → çeviriler balonlara oturuyor; bar
      bölgenin üstünde (sığmazsa altında), görev çubuğunun üstüne düşmüyor.
- [ ] Seçim bitince (iptal dahil) klavye odağı WhatsApp'a geri dönüyor.
- [ ] Canlı: yeni mesaj gelince çevriliyor; kaydırınca yamalar birlikte
      kayıyor; ağ beklerken bar "⏳ çevriliyor…" yazıyor ve kaydırma devam ediyor.
- [ ] Küçük tek kelimelik yeni balon (bölgenin %3'ü) de çevriliyor.
- [ ] Kalite-sonra: hızlı çeviri anında, kalite düzeltmesi ≤8 sn'de geliyor;
      8 sn sonra gelen düzeltme ekranı OYNATMIYOR.

## Bar düğmeleri
- [ ] ✕ kapatır (öneri paneli de kapanır) · 📋 panoya çeviriler · 👁 orijinal
      göster/gizle · ✨ kaliteyle yeniden çevir (bar "Grok ✓ kalite") ·
      ⌨️ ipucu gerçek kısayolu yazıyor · 💬 3 öneri + Türkçe anlam + Kopyala/Yenile/Kapat.
- [ ] Motor "Ücretsiz çeviri" iken ✨ ve 💬 "Ücretsiz motor seçili…" diyor, xAI'ye çıkmıyor.

## Yazdığımı Çevir (⌨️ / Ctrl+Alt+C)
- [ ] WhatsApp mesaj kutusunda Türkçe yaz → kısayol → metin lehçeye çevrilmiş
      hâliyle DEĞİŞİYOR (Ctrl+Alt fiziksel basılıyken de çalışıyor).
- [ ] Sonrasında kullanıcının ASIL panosu geri geliyor (kendi Türkçe metni değil).
- [ ] Kapı reddi: "150 frank" yazıp modelin harfle yazması → "Mesajın
      DEĞİŞMEDİ — Sayılar korunmadı…" bildirimi; mesaj kutusu bozulmamış.
- [ ] Bilinmeyen uygulamada (Not Defteri) kısayol → durduruluyor.

## Menü ve ayarlar
- [ ] Tepsi → Kısayolu Değiştir… (sahipsiz pencere) açılıyor, tuş alıyor,
      anında kapanmıyor, "Beklenmeyen hata" kutusu ÇIKMIYOR.
- [ ] Kim yazıyor / Bana çevir / Gelişmiş öğeleri işaretleri koruyor; dil
      değiştirince ekrandaki çeviriler yeniden çevriliyor, bar ipucu güncel.
- [ ] Çeviriyi Kapat katman kapalıyken devre dışı; Bölgeyi Çevir iş sürerken devre dışı.
- [ ] Sohbet geçmişini sil → dosya siliniyor; hafıza açıkken ekrandakiler yeniden yazılıyor (belgelenmiş).
- [ ] Tepsi → Çık: çeviriden 1-4 sn sonra çıkınca son çeviri `hafiza.json`'da (yeniden açınca "Hafızadan ✓").

## Kaynak ve dayanıklılık
- [ ] Görev Yöneticisi → Ayrıntılar → "GDI Nesneleri": canlı mod 2 saat açıkken sabit kalıyor.
- [ ] Wi-Fi kapalıyken canlı: 3 hatadan sonra 60/120 sn bekleme, `ariza.log`
      satırları metinsiz (`uzunluk=N | karma=…`); ağ gelince kendiliğinden toparlıyor.
- [ ] `teshis.log` 60 sn'de bir satır yazıyor; boşta "boşta".
- [ ] `EkranCeviri.exe --tani` ile `ariza.log` satırlarına `metin=` ekleniyor; bayraksız eklenmiyor.
