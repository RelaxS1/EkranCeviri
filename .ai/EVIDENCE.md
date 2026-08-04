# Kanıt — EkranCeviri kararlılık + çeviri kalitesi denetimi (2026-07-28)

Result: PASS

## Commands
| komut | exit |
|---|---|
| `./testleri_calistir.sh` | 0 (20/20 test geçti) |
| `./uygulama_yap.sh` | 0 (test + derleme + imza + kurulum) |
| `/Applications/EkranCeviri.app/Contents/MacOS/EkranCeviri --gizli-sinama` | 0 (5 tur) |
| `swiftc -swift-version 5 -O EkranCeviri.swift Sinama.swift main.swift` | 0 |

## Real flows
- Uçtan uca gizli QA (ekrana hiçbir pencere çıkmadan): sahte WhatsApp
  sohbeti bellekte çizildi → gerçek Vision OCR → blok gruplama → Grok
  çevirisi → katman render'ı → PNG. 5 tur: ilk çeviri, yeni mesaj,
  kaydırma, iki yeni mesaj, zor senaryo.
- Sonuç: her turda `çevrilen == hedef` (4/4, 5/5, 5/5, 6/6, 5/5);
  önbellek 13 kayıt → eski mesajlar tekrar motora gitmedi.
- Görsel: `/tmp/qa_gizli_*.png` — Almanca görünmüyor, saat damgaları tam,
  kırpma/taşma/iç içe geçme yok, emojiler korunmuş.
- Pencereli QA (`--sinama`) ile canlı mod ayrıca doğrulandı: yeni mesaj
  geldiğinde log `EC-canli: eksik var (1 blok) → motor=Grok AI, kalan
  eksik=0`.

## Negative / failure paths
- Grok anahtarı yokken: sessiz düşüş yerine panelde `⚠︎ Grok anahtarı yok
  → Bing` (canlı doğrulandı).
- Ham lehçe Bing'e verildiğinde: çeviri yapılmıyor veya ters anlam
  (ölçüldü: "bim Bahnhof" → "istasyonun yarısı geldi"); normalizasyondan
  sonra doğru ("Yarın şehre geliyor musun?").
- Ekran görüntüsü alınamazsa: `EC-boru: GÖRÜNTÜ ALINAMADI` + kullanıcıya
  baloncuk.
- Ağ askıda kalırsa: `httpGetir` zaman aşımı + görev iptali; canlı turda
  25 sn bekçi kilidi kırar (kod yolu mevcut, log satırı hazır).

## Applicable gates
- Unit/regresyon: `./testleri_calistir.sh` 20/20.
- Derleme kapısı: `uygulama_yap.sh` testler geçmeden paket üretmiyor.
- Gizli bilgi kapısı: config'te gerçek anahtar varsa paketleme durur.
- Görsel kapı: gizli QA PNG'leri elle incelendi (5 tur).

## Remaining risk / single external action
- Grok çıktısı modele bağlı; lehçede nadiren serbest yorum yapıyor.
- Anahtarsız ücretsiz uç noktalar (Bing/Google) IP engeline takılabilir.
- Ad-hoc imza: her yeniden derlemede TCC/Keychain kimliği değişir; Ekran
  Kaydı izni yeniden onay isteyebilir.
- Tek dış işlem: sohbete yapıştırılan iki xAI anahtarı sızmış sayılmalı;
  xAI konsolundan iptal edilip yenisi menüden girilmeli.

## 2026-07-29 — Çok lehçeli İsviçre Almancası + kalite kapıları

Komutlar ve sonuçlar:
- `./testleri_calistir.sh` → 31/31 ✓ (lehçe algılama, kalite kapıları,
  normalizasyon, blok gruplama regresyonları dahil)
- `./uygulama_yap.sh` → testler geçti, imzalandı, /Applications'a kuruldu
- `--gizli-sinama` (ekrana pencere açmadan, gerçek OCR+Grok+render):
  5 aşama, motor="Grok · Züridütsch", çevrilen 4/4, 5/5, 5/5, 6/6, 5/5;
  önbellek 13 kayıt (tekrar çeviri yok). PNG'ler /tmp/qa_gizli_*.png
- Üretim `grokCevir` ile 3 lehçe testi: algılama 3/3 doğru
  (Züridütsch/Bärndütsch/Baseldytsch), gelen çeviriler doğru.
- Üretim `girdiCevir` ile giden test: her lehçe kendi yazımını üretti
  (ZH "ich mues no chli schaffe" / BE "i ga o chli wärche" /
  BS "y mues au no e bitz schaffe"), Türkçe sızıntısı yok.

Negatif yol: Türkçe sızıntılı ilk yanıt → kalite kapısı düzeltme turu
tetikledi ve temiz çıktı üretti (önce/sonra karşılaştırıldı).

Kalan risk: OCR'ın ağır bozduğu kısa mesajlarda (ör. "danke babe biz heiss
machsch au trffe?") anlam tahmini oynak. Uzun dayanıklılık (soak) testi
kullanıcı isteğiyle ertelendi.

## 2026-08-02 — "Kapatıp tekrar Bölgeyi Çevir çalışmıyor" kök nedeni

KÖK NEDEN: arka plan iş kuyruğu (isKuyrugu) içinden `DispatchQueue.main.sync`
çağrılıyordu. Ana iş parçacığı bir onay/uyarı penceresiyle meşgulken SERİ
kuyruk kilitleniyor, `isSuruyor` bayrağı sonsuza dek açık kalıyor ve
`cevirBaslat()` başında `if isSuruyor { return }` ile SESSİZCE dönüyordu →
menüdeki "Bölgeyi Çevir" ölü görünüyordu.

DÜZELTMELER:
1. İş kuyruğunda main.sync YOK (ilkCeviri ve yazdigimiCevir): gereken değerler
   ana iş parçacığında, iş başlamadan önce okunuyor.
2. `bulutOnayAl` arka plandan çağrılırsa BLOKLAMADAN false döner; soru ana
   iş parçacığında asenkron sorulur.
3. `cevirmeyeHazir()` / `gidenHazir()`: 15/30 sn'den eski takılı işi otomatik
   iptal eder; asla sessizce dönmez, kullanıcıya durum bildirilir.
4. Sağlık bekçisi (5 sn'de bir): 40 sn'yi aşan iş/giden/canlı bayraklarını
   sıfırlar, sahipsiz canlı zamanlayıcıyı durdurur.
5. Eşzamanlılık: KilitliSozluk/KilitliKume, Blok.ceviri NSLock, anahtar
   init'te (lazy yarışı kalktı).

KANITLAR (komutlar ve sonuçlar):
- `./testleri_calistir.sh` → tüm testler geçti (durum makinesi testleri dahil)
- `--gizli-dongu 20` → 20/20 başarılı, bayraklar her turda temiz
- `--gizli-dongu 12` (son derleme) → 12/12, 0 sorun
- `--gizli-sinama` → 5 aşama, çevrilmeyen mesaj yok, önbellek çalışıyor
- Eşzamanlılık saldırısı (8 kuyruk × 3000 tur + eşzamanlı okuyucu) → çökme yok
- Negatif yol (arayüzsüz): takılı iş 20 sn sonra sıfırlanıyor, giden kilidi
  40 sn'de kırılıyor, onay kapısı arka planda 3 sn içinde bloklamadan dönüyor

KALAN RİSK: Erişilebilirlik izni kullanıcı tarafından bir kez verilmeli
(kalıcı imza kimliği sayesinde artık derlemeler arasında korunuyor).

## 2026-08-02 (2) — Acımasız kalite denetimi (32 kritik/yüksek bulgu)

KÖK NEDENLER ve DÜZELTMELER:
1. KRİTİK — Canlı modda bloklar yalnız KONUM örtüşmesiyle (IoU>0.45)
   eşleşiyordu; metin doğrulaması yoktu. Yeni mesaj gelip liste kayınca
   ESKİ çeviri BAŞKA mesaja yapışıyor, üstelik kalıcı hafızaya yazılıyordu
   → "yanlış anlaşılabilecek çeviri". Artık metin benzerliği (düzenleme
   mesafesi) şart; farklı anahtara çeviri kopyalama yalnız ≤2 karakterlik
   OCR titremesinde yapılıyor.
2. KRİTİK — Saat damgası temizleyicisi `\d{1,2}[:.-]\d{2}` fiyatı (12.50),
   tarihi (12.05) ve süre aralığını (10-15) METİNDEN SİLİYORDU. Gerçek saat
   desenine daraltıldı. Ölçüm: 5 fiyat/tarih/süre mesajında sayı kaybı 0/5.
3. KRİTİK — Model doğru sayıda ama SIRASI BOZUK kayıt döndürdüğünde `indeks`
   yok sayılıyordu → çeviriler takas oluyordu. Artık indeksler geçerli
   permütasyon ise indeksle yerleştiriliyor.
4. KRİTİK — ScreenCaptureKit çağrılarında zaman aşımı yoktu; yanıt gelmezse
   iş kuyruğu KALICI kilitleniyordu. 8 sn zaman aşımı eklendi.
5. KRİTİK — Kalıcı hafıza kirlenmişti (uygulamanın kendi Türkçe çıktısı,
   1 harflik anahtarlar, ücretsiz motor çöpü). Sürüm 2 göçü eski dosyayı
   siliyor; artık yalnız Grok üretimi + zehir kalkanından geçmiş kayıtlar
   yazılıyor; kısa anahtarda bulanık eşleşme kapalı; rakamlar eşleşmezse
   bulanık eşleşme yok; en iyi aday seçiliyor (sözlük sırası rastgeleliği).
6. YÜKSEK — `anahtarla` soru işaretini siliyordu: soru/düz cümle aynı
   anahtara düşüp yanlış çeviri paylaşıyordu. Soru işareti anahtara katıldı.
7. YÜKSEK — Boş çeviriye emoji eklenip " 😂" hafızaya yazılıyordu.
8. YÜKSEK — Motor/hedef dil değişince ekrandaki çeviriler tazelenmiyordu.
9. YÜKSEK — Giden çeviri ve öneri hep HIZLI modeli kullanıyordu.
10. YÜKSEK — canliYakalayici/sonIz/sonIzdusum kilitsizdi (veri yarışı).
11. GİZLİLİK — /tmp tanı arka kapısı yalnız --tani ile açılıyor; sohbet
    geçmişi dosyası 0600.
12. YENİ ÖZELLİK — İçerik kaydığında yamalar OCR beklemeden içerikle
    birlikte kayıyor (satır izdüşümü korelasyonu); güvenilir kayma yoksa
    yamalar GİZLENİYOR ("eski çeviri 2-3 sn yanlış yerde" çözümü).
13. Sohbet bağlamı modele geçiriliyor: önbellek yüzünden tek başına giden
    yeni mesaj artık önceki konuşmayı görüyor (tekil/çoğul, gönderme
    hataları düzeldi — ölçüldü).

KANITLAR:
- `./testleri_calistir.sh` → tümü geçti (yeni: kayma telafisi, hafıza
  hijyeni, blok eşleştirme güvenliği, saat deseni, soru işareti)
- `--gizli-sinama` → 5 aşama, çevrilmeyen mesaj yok
- `--gizli-dongu 15` → 15/15, bayraklar temiz
- Eşzamanlılık saldırısı (8 kuyruk × 3000 tur) → çökme yok
- Fiyat/tarih/süre testi → sayı kaybı 0/5
- Bağlamlı vs bağlamsız çeviri karşılaştırması → tekil/çoğul hataları düzeldi
