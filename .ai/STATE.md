# EkranCeviri — Durum

## Gerçekler
- Ana ürün: `EkranCeviri.swift` + `Sinama.swift` + `main.swift` (Swift/AppKit,
  menü çubuğu uygulaması). `ekran_ceviri.py` legacy prototip.
- Derleme/kurulum: `./uygulama_yap.sh` (testleri koşar, anahtar sızıntısını
  engeller, ad-hoc imzalar, /Applications'a kurar).
- Testler: `./testleri_calistir.sh` (ağsız, 20 test).
- Gizli QA: `EkranCeviri --gizli-sinama` → ekrana hiçbir şey çıkmadan
  bellekte sahte sohbet çizer, gerçek OCR+çeviri+katman render'ı yapar,
  `/tmp/qa_gizli_*.png` üretir ve konsola çeviri raporu basar.
- Görünür QA: `--sinama` (pencere açar; kullanıcı bilgisayarı kullanırken
  KULLANMA).

## Invariantlar
- Keychain ASLA otomatik okunmaz (ad-hoc imzada parola diyaloğu çıkarıyor).
  Anahtar `~/Library/Application Support/EkranCeviri/anahtar` (0600).
- Makine motorlarına (Bing/Google) giden metin önce `lehceyiStandartlastir`
  ile standart Almancaya çevrilir; Grok'a HAM metin gider.
- `motor == "ai"` iken YALNIZ Grok kullanılır (sessiz düşüş yok).
- Çizimde ölçüm ve çizim aynı yerleşim motorunu kullanır
  (`boundingRect` ↔ `NSAttributedString.draw(with:options:)`).
- Ağ çağrılarında sonsuz bekleme yok; canlı turda 25 sn bekçi kilidi kırar.

## Aktif görev
Kararlılık + çeviri kalitesi denetimi (tamamlandı, kanıt: `.ai/EVIDENCE.md`).

## Kararlar
- Zürih lehçesi için ücretsiz motorlar tek başına yetersiz → sözlük
  ön-normalizasyonu + kaynak dil zorlama; en iyi kalite Grok.
- Yamalar kırpma yerine büyür; saat damgası için balon altında pay bırakılır.
