# Ekran Çeviri

Ekranda bir bölge seç — oradaki yazışma **balonların üstüne, kendi dilinde**
yazılsın. Google Lens'in yaptığını WhatsApp sohbetinde, canlı olarak yapar.

Almanya, Avusturya ve İsviçre'de konuşulan Almanca **lehçelerini** anlamak için
yapıldı: Hochdeutsch, Bavyera, Kuzey Almanya, Zürih, Bern, Basel, Doğu İsviçre,
Wallis. Hangi lehçenin konuşulduğunu kendi bulur.

> **İki sürüm var.** Bu klasör macOS sürümüdür.
> Windows kullanıyorsan → **[EkranCeviriWin](EkranCeviriWin/README.md)**
> (kurulum gerektirmeyen tek dosyalık .exe:
> [Releases](../../releases/latest))

## Ne yapar

- **Bölgeyi Çevir** — ekranda bir alan seç, çeviriler balonların üstüne yapışır
- **Canlı mod** — yeni mesaj geldiğinde otomatik çevirir; sohbet kaydığında
  çeviriler içerikle birlikte kayar
- **Yazdığımı Çevir** — mesaj kutusuna Türkçe yaz, kısayola bas: karşındakinin
  konuştuğu **lehçede** yazılmış hâliyle değişir
- **Cevap Öner** — sohbetin gidişatına göre cevap önerir (isteğe bağlı)
- Sohbet ekranı senin kalır: çeviri katmanı tıklamayı geçirir, kaydırabilir,
  yazabilirsin

## Kurulum

Gereken: **macOS 13+**, Xcode Command Line Tools (`xcode-select --install`).

```bash
git clone https://github.com/RelaxS1/EkranCeviri.git
cd EkranCeviri
./kur.sh
```

`kur.sh` imza sertifikasını oluşturur, uygulamayı derler ve
`/Applications/EkranCeviri.app` olarak kurar.

### İki izin (bir kez)

Sistem Ayarları → Gizlilik ve Güvenlik:

| İzin | Ne için |
|---|---|
| **Ekran Kaydı** | Seçtiğin bölgedeki yazıyı okumak (zorunlu) |
| **Erişilebilirlik** | "Yazdığımı Çevir" için (isteğe bağlı) |

İzinleri verdikten sonra uygulamayı bir kez kapatıp aç.

## Çeviri motoru

Uygulama **kendi API anahtarınla** çalışır — hiçbir anahtar gömülü gelmez.

| Motor | Kalite | Ücret |
|---|---|---|
| **xAI Grok** (önerilir) | Lehçeleri gerçekten anlar | Kendi anahtarın, kullandığın kadar |
| Bing / Google (yedek) | Standart Almanca iyi, **lehçede zayıf** | Ücretsiz |

Anahtar almak: [console.x.ai](https://console.x.ai) → API Keys → Create API Key.
İlk açılışta sorulur; sonra menü → Gelişmiş → Yapay Zekâ Anahtarı.

Anahtarın **yalnız senin bilgisayarında**, sana özel (0600) bir dosyada durur:
`~/Library/Application Support/EkranCeviri/anahtar`

## Gizlilik

- **Yazı tanıma tamamen cihazında** çalışır; ekran görüntüsü hiçbir yere
  gönderilmez, diske kaydedilmez
- Çeviri motoruna **yalnız çıkarılan metin** gider ve ilk seferinde izin istenir
- Sohbet hafızası yereldir; menüden kapatılabilir ve silinebilir
- Ekrandan okunan metin modele **veri** olarak verilir, talimat olarak değil
  (prompt injection koruması)

## Kullanım

1. Menü çubuğundaki **💬ç** ikonuna tıkla → **Bölgeyi Çevir**
2. Sohbetin olduğu alanı sürükleyerek seç
3. Çeviriler yerine oturur; panelden canlı modu, dili ve kimliği ayarlayabilirsin

**Yazdığımı Çevir:** mesaj kutusuna Türkçe yaz → kısayola bas (varsayılan ⌃⌥C,
menüden değiştirilebilir) → mesaj karşındakinin lehçesinde yazılmış hâliyle
değişir. Fiyat ve saatler rakam olarak korunur.

## Ayarlar

Menüde günlük kullanılanlar üstte: **Bölgeyi Çevir · Yazdığımı Çevir ·
Kim yazıyor · Bana çevir**. Geri kalanı **Gelişmiş** altında: çeviri motoru,
karşı tarafın dili, kısayol, API anahtarı, yazım üslubu, hafıza.

Ayar dosyası: `~/Library/Application Support/EkranCeviri/config.json`

## Windows

Windows sürümü **[EkranCeviriWin/](EkranCeviriWin/README.md)** klasöründe,
ayrı bir uygulama olarak yazıldı (C# / .NET 8 / WPF). Kurulum gerektirmeyen
tek dosyalık .exe: **[Releases](../../releases/latest)**.

Aynı uygulamanın "taşınmış" hâli değil — sistem katmanının tamamı sıfırdan
yazıldı, çünkü macOS sürümünün üç temel parçası da Apple'a özgü:

| Parça | macOS | Windows |
|---|---|---|
| Ekran yakalama | ScreenCaptureKit | GDI BitBlt |
| Yazı tanıma | Apple Vision | Windows.Media.Ocr |
| Şeffaf katman | AppKit NSPanel | WPF katmanlı pencere |
| Katman yakalanmasın | SCContentFilter dışlaması | `WDA_EXCLUDEFROMCAPTURE` |
| Global kısayol | Carbon HotKey | `RegisterHotKey` |
| Tuş gönderme | CGEvent (Erişilebilirlik izni) | `SendInput` (izin gerekmez) |
| Anahtar saklama | Dosya (0600) + Anahtar Zinciri | DPAPI ile şifreli dosya |

**Taşınan** kısım çeviri zekâsıdır ve birebir aynıdır: lehçe algılama,
lehçe→standart Almanca sözlüğü, Grok istemleri, rakam/fiyat koruma kapıları,
çeviri hafızası, blok eşleştirme kuralları.

## Geliştirme

```bash
./testleri_calistir.sh          # ağsız birim testler
./uygulama_yap.sh               # test + derle + imzala + kur (yayın kapılı)
```

Hata ayıklama modları (ekrana pencere açmaz):

```bash
/Applications/EkranCeviri.app/Contents/MacOS/EkranCeviri --gizli-sinama
```

`--gizli-dongu <n>` tekrarlı çevir/kapat döngüsünü, `--gizli-soak <dk>` uzun
süre dayanıklılığını sınar.

`ekran_ceviri.py` eski Python prototipidir; kullanılmıyor.

## Lisans

MIT — bkz. [LICENSE](LICENSE). Katkı için [CONTRIBUTING.md](CONTRIBUTING.md),
güvenlik bildirimi için [SECURITY.md](SECURITY.md).
