# Katkı

## Kurulum

```bash
git clone https://github.com/KULLANICI/EkranCeviri.git
cd EkranCeviri
./kur.sh
```

Gereken: macOS 13+, Xcode Command Line Tools.

## Değişiklik yaparken

```bash
./testleri_calistir.sh     # ağsız birim testler — her değişiklikten sonra
./uygulama_yap.sh          # test + derle + imzala + kur + yayın kapısı
```

`uygulama_yap.sh` yalnız testler geçerse derler ve yalnız yayın kapısı
(`--gizli-dongu 3`) geçerse kurar. Kapıyı atlatma; kırılan bir şey varsa
düzelt.

## Sınama düzeneği

Testler ekranda pencere açmaz — arayüz gerektiren senaryolar sahte sahne
(`SinamaSahnesi`) üzerinden koşar:

```bash
/Applications/EkranCeviri.app/Contents/MacOS/EkranCeviri --gizli-sinama
/Applications/EkranCeviri.app/Contents/MacOS/EkranCeviri --gizli-dongu 20
/Applications/EkranCeviri.app/Contents/MacOS/EkranCeviri --gizli-soak 10
```

Testler üretim fonksiyonlarını çağırır, kopyalarını değil. Yeni bir davranış
eklerken testi de üretim fonksiyonuna bağla — kopyalanmış mantık üzerinde
geçen test, hiçbir şey kanıtlamaz.

## Kurallar

- **Anahtar, jeton veya kişisel veri commit'leme.** `config.json` içindeki
  `grok_api_key` daima boş kalır.
- Kullanıcıya görünen metinler Türkçe; kod ve tanımlayıcılar da Türkçe
  (mevcut üslupla uyumlu kal).
- Çeviri çıktısına dokunan bir değişiklik yapıyorsan rakam/saat/fiyat
  korunum testlerini genişlet.
- Ekranda pencere açan, odağı çalan veya kullanıcının işini bölen bir test
  yazma.

## Neye katkı iyi gelir

- Yeni lehçeler ve sözlük genişletmeleri (Avusturya bölgeleri, İsviçre kantonları)
- Yazı tanıma doğruluğu (farklı arayüz temaları, koyu mod, küçük yazı tipleri)
- Yeni çeviri motoru bağlayıcıları
- Windows sürümü — README'deki [Windows](README.md#windows) bölümüne bak;
  çeviri mantığı taşınabilir, sistem katmanı sıfırdan yazılmalı
