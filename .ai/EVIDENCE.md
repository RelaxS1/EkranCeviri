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
