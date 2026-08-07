# Ekran Çeviri — Windows

Ekranda bir sohbet alanı seç; yazışma **balonların üstüne Türkçe** yazılsın.
Almanya, Avusturya ve İsviçre lehçelerini anlar.

macOS sürümünün Windows karşılığı. Aynı çeviri zekâsı (lehçe algılama,
sözlükler, kalite kapıları) taşındı; ekran, arayüz ve kısayol katmanı
Windows'a göre sıfırdan yazıldı.

## Kurulum — arkadaşların için

1. [Releases](../../releases/latest) sayfasından **EkranCeviri.exe** indir
2. Çift tıkla. **Kurulum yok, .NET kurmana gerek yok.**
3. Windows "bilinmeyen yayımcı" uyarısı verirse:
   **Ek bilgi** → **Yine de çalıştır**

Uygulama saatin yanındaki tepsi simgesine yerleşir. Simgeye **çift tıkla**
→ sohbet alanını seç.

**Gereken:** Windows 10 sürüm 2004 (build 19041) veya üstü.

### Almanca yazı tanıma

Uygulama açılışta kontrol eder ve eksikse söyler. Eklemek için:
Ayarlar → Saat ve dil → Dil ve bölge → **Dil ekle** → Almanca →
kurarken **"Temel yazma"** kutusunu işaretle (yazı tanıma o pakette).

Eklemezsen uygulama yine çalışır, sadece Almanca'ya özgü harflerde
(ä, ö, ü, ß) daha çok hata yapar.

## Ne yapar

- **Bölgeyi Çevir** — alan seç, çeviriler balonların üstüne yapışır
- **Canlı mod** — yeni mesaj gelince otomatik çevirir; sohbet kaydığında
  çeviriler içerikle birlikte kayar
- **Yazdığımı Çevir** (Ctrl+Alt+C) — mesaj kutusuna Türkçe yaz, kısayola
  bas: metin karşındakinin **lehçesinde** yazılmış hâliyle değişir
- Sohbet ekranı senin kalır: çeviri katmanı tıklamayı geçirir

## Çeviri motoru

| Motor | Kalite | Ücret |
|---|---|---|
| **xAI Grok** (önerilir) | Lehçeleri gerçekten anlar | Kendi anahtarın |
| Bing / Google (yedek) | Standart Almanca iyi, **lehçede zayıf** | Ücretsiz |

Anahtar: [console.x.ai](https://console.x.ai) → API Keys → Create API Key.
İlk açılışta sorulur; sonra tepsi menüsü → Gelişmiş → Yapay Zekâ Anahtarı.

Anahtarın **Windows'un kendi şifrelemesiyle** (DPAPI) saklanır:
`%APPDATA%\EkranCeviri\anahtar.bin`. Dosya kopyalansa bile başka kullanıcı
veya başka bilgisayar çözemez. Ayar dosyasına ya da uygulamanın içine
**asla** yazılmaz.

## Gizlilik

- **Yazı tanıma tamamen bilgisayarında** (Windows.Media.Ocr) — ekran
  görüntüsü hiçbir yere gitmez, diske yazılmaz
- Çeviri motoruna **yalnız okunan metin** gider
- Sohbet hafızası yerel; menüden silinebilir
- Günlük dosyasına **sohbet içeriği yazılmaz**, yalnız olay ve hata
- Ekrandan okunan metin modele **veri** olarak verilir, talimat olarak
  değil (prompt injection koruması)

### Kısayol neden her pencerede çalışmıyor

"Yazdığımı Çevir" önce Ctrl+A ile mesaj kutusunu seçer. Yanlış bir
pencerede tetiklenirse **belgeni silebilirdi**. Bu yüzden:

- Yalnız bilinen mesajlaşma uygulamalarında ve tarayıcıda çalışır
- Seçilen metin 1200 karakteri veya 15 satırı aşarsa işlem **iptal edilir**
  (o bir mesaj değil, belgedir)
- Panondaki eski içerik korunur ve işlem sonunda geri konur

## Windows'a özgü tasarım kararları

| Konu | Karar | Neden |
|---|---|---|
| Ekran yakalama | GDI BitBlt | Sohbet saniyede bir yakalanıyor; Windows.Graphics.Capture'ın karmaşıklığı gereksiz |
| Katman yakalanmasın | `WDA_EXCLUDEFROMCAPTURE` | Kendi çevirimizi okursak onu tekrar çeviririz (macOS'ta sonsuz döngüye girmişti) |
| Tıklama geçişi | `WS_EX_TRANSPARENT\|LAYERED\|NOACTIVATE` | Sohbet ekranı kullanıcının kalmalı |
| Konumlandırma | Win32, fiziksel piksel | WPF'in DIU'su farklı ölçekli iki ekranda yanlış yere düşüyor |
| DPI | Manifest'te PerMonitorV2 | Süreç başlarken kurulmalı; %150 ölçekte yamalar balonun yanına düşüyordu |
| Anahtar | DPAPI | macOS'taki 0600 dosyadan daha güçlü |
| Dağıtım | Tek dosya, self-contained | Arkadaşlar indirip çift tıklasın; .NET kurulumu istemesin |

## Geliştirme

```bash
dotnet build EkranCeviri.csproj
dotnet run --project Testler/Testler.csproj
dotnet publish EkranCeviri.csproj -c Release
```

macOS veya Linux'tan da derlenir (`EnableWindowsTargeting` açık) ama
**çalıştırılamaz**. Her push'ta GitHub Actions gerçek bir Windows
makinesinde derleyip testleri koşuyor — asıl doğrulama orada.

Sürüm çıkarmak için `v1.0.0` gibi bir etiket at: CI, .exe'yi Releases
sayfasına koyar.

`Testler` üretim fonksiyonlarını çağırır, kopyalarını değil. Yeni bir
davranış eklerken testi de üretim fonksiyonuna bağla.
