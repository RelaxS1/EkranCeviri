# Ekran Çeviri — Windows

Ekranda bir sohbet alanı seç; yazışma **balonların üstüne Türkçe** yazılsın.
Google Lens'in yaptığını WhatsApp sohbetinde, canlı olarak yapar.

Almanya, Avusturya ve İsviçre'de konuşulan Almanca **lehçelerini** anlamak
için yapıldı: Hochdeutsch, Bavyera, Kuzey Almanya, Zürih, Bern, Basel,
Doğu İsviçre, Wallis. Hangi lehçenin konuşulduğunu kendi bulur.

## Kurulum

1. **[EkranCeviri.exe indir](../../releases/latest)**
2. Çift tıkla — **kurulum yok, .NET kurmana gerek yok**
3. Windows "bilinmeyen yayımcı" uyarısı verirse:
   **Ek bilgi** → **Yine de çalıştır**
   *(Uygulama imzasız olduğu için çıkıyor — kaynak kodun tamamı bu depoda)*

Uygulama saatin yanındaki **tepsi simgesine** yerleşir (yeşil "ç"). Simge
görünmüyorsa saatin solundaki **küçük oka** tıkla, gizli simgeler orada.

**Gereken:** Windows 10 sürüm 2004 (build 19041) veya üstü.

## Yapay zekâ anahtarı (API key)

Uygulama **kendi anahtarınla** çalışır — içinde gömülü anahtar yoktur.

**İlk açılışta sorulur.** Sonradan girmek ya da değiştirmek için:
> tepsi simgesine **sağ tıkla** → **Gelişmiş** → **Yapay Zekâ Anahtarı…**

Anahtar almak: [console.x.ai](https://console.x.ai) → hesap aç →
**API Keys** → **Create API Key** → çıkan `xai-...` metnini kopyala.

| Motor | Lehçe kalitesi | Ücret |
|---|---|---|
| **xAI Grok** (anahtarla) | Lehçeleri gerçekten anlar | Kullandığın kadar |
| Bing / Google (yedek) | Standart Almanca iyi, **lehçede zayıf** | Ücretsiz |

**Anahtarsız da çalışır** — ücretsiz motorlara düşer. Ama karşındaki
İsviçre veya Bavyera lehçesiyle yazıyorsa ücretsiz motorlar sık sık ters
anlam üretiyor; anahtar girmen önerilir.

Anahtar penceresinde:
- Yazdığın anahtar **noktalarla maskelenir**, ekranda hiç görünmez
- Kayıtlı anahtar yalnız `xai-abcd…wxyz` biçiminde özetlenir
- **Anahtarı sil** düğmesi hem dosyayı hem kaydı temizler
- `xai-` ile başlamayan metin kaydedilmez (yanlış yapıştırma sessizce
  bütün çevirileri bozuyordu)

Anahtarın **Windows'un kendi şifrelemesiyle** (DPAPI) saklanır:
`%APPDATA%\EkranCeviri\anahtar.bin`. Dosya kopyalansa bile başka bir
kullanıcı veya başka bir bilgisayar çözemez. Ayar dosyasına ya da
uygulamanın içine **asla** yazılmaz.

### Almanca yazı tanıma

Uygulama açılışta kontrol eder, eksikse söyler. Eklemek için:
Ayarlar → Saat ve dil → Dil ve bölge → **Dil ekle** → Almanca →
kurarken **"Temel yazma"** kutusunu işaretle (yazı tanıma o pakette).

Eklemezsen uygulama yine çalışır, sadece Almanca'ya özgü harflerde
(ä, ö, ü, ß) daha çok hata yapar.

## Kullanım

1. Tepsi simgesine **çift tıkla** (ya da sağ tıkla → Bölgeyi Çevir)
2. Sohbetin olduğu alanı sürükleyerek seç
3. Çeviriler balonların üstüne oturur

- **Canlı mod** — yeni mesaj gelince otomatik çevirir; sohbet kaydığında
  çeviriler içerikle birlikte kayar, mevcut çeviriler yanıp sönmez
- **Yazdığımı Çevir** (`Ctrl+Alt+C`) — mesaj kutusuna Türkçe yaz, kısayola
  bas: metin karşındakinin **lehçesinde** yazılmış hâliyle değişir.
  Fiyat ve saatler rakam olarak korunur.
- Sohbet ekranı senin kalır: çeviri katmanı tıklamayı geçirir,
  kaydırabilir, yazabilirsin

Ayarlar (motor, karşı tarafın dili, kimlik, kısayol, yazım üslubu):
tepsi simgesine sağ tıkla → **Gelişmiş** → **Ayarlar**.

## Gizlilik

- **Yazı tanıma tamamen bilgisayarında** (Windows.Media.Ocr) — ekran
  görüntüsü hiçbir yere gitmez, diske yazılmaz
- Çeviri motoruna **yalnız okunan metin** gider
- Sohbet hafızası yerel; menüden silinebilir
- Günlük dosyasına **sohbet içeriği yazılmaz**, yalnız olay ve hata
- Ekrandan okunan metin modele **veri** olarak verilir, talimat olarak
  değil (prompt injection koruması)

### Kısayol neden her pencerede çalışmıyor

"Yazdığımı Çevir" önce `Ctrl+A` ile mesaj kutusunu seçer. Yanlış bir
pencerede tetiklenirse **belgeni silebilirdi**. Bu yüzden:

- Yalnız bilinen mesajlaşma uygulamalarında ve tarayıcıda çalışır
- Seçilen metin 1200 karakteri veya 15 satırı aşarsa işlem **iptal edilir**
  (o bir mesaj değil, belgedir)
- Panondaki eski içerik korunur ve işlem sonunda geri konur

## Tasarım kararları

| Konu | Karar | Neden |
|---|---|---|
| Ekran yakalama | GDI BitBlt | Sohbet saniyede bir yakalanıyor; Windows.Graphics.Capture'ın karmaşıklığı gereksiz |
| Katman yakalanmasın | `WDA_EXCLUDEFROMCAPTURE` | Kendi çevirimizi okursak onu tekrar çeviririz (sonsuz döngü) |
| Tıklama geçişi | `WS_EX_TRANSPARENT\|LAYERED\|NOACTIVATE` | Sohbet ekranı kullanıcının kalmalı |
| Konumlandırma | Win32, fiziksel piksel | WPF'in DIU'su farklı ölçekli iki ekranda yanlış yere düşüyor |
| DPI | Manifest'te PerMonitorV2 | Süreç başlarken kurulmalı; %150 ölçekte yamalar balonun yanına düşüyordu |
| Anahtar | DPAPI | Düz dosyadan güçlü; başka makinede çözülemez |
| Dağıtım | Tek dosya, self-contained | İndir, çift tıkla; .NET kurulumu isteme |

Yazı tanıma cihazda çalıştığı, motorlar kalite kapılarından geçtiği ve
lehçe sözlüğü elle yazıldığı için çeviri kalitesi sabit bir çeviri
sitesinden belirgin yüksek — özellikle lehçelerde.

## Geliştirme

```bash
dotnet build EkranCeviri.csproj
dotnet run --project Testler/Testler.csproj
dotnet publish EkranCeviri.csproj -c Release
```

macOS veya Linux'tan da derlenir (`EnableWindowsTargeting` açık) ama
**çalıştırılamaz**. Her push'ta GitHub Actions gerçek bir Windows
makinesinde derleyip testleri koşuyor — asıl doğrulama orada, ve
**testler geçmezse .exe üretilmiyor**.

Sürüm çıkarmak: `./yayinla.sh 1.1.0` — etiketi atar, CI .exe'yi
[Releases](../../releases/latest) sayfasına koyar.

`Testler` üretim fonksiyonlarını çağırır, kopyalarını değil. Yeni bir
davranış eklerken testi de üretim fonksiyonuna bağla — kopyalanmış mantık
üzerinde geçen test hiçbir şey kanıtlamaz.

Bu uygulama bir macOS sürümünden yola çıkarak yazıldı; çeviri zekâsı
(lehçe algılama, sözlükler, istemler, kalite kapıları) oradan taşındı,
sistem katmanının tamamı Windows'a göre sıfırdan yazıldı. macOS kaynağı
git geçmişinde duruyor.

## Lisans

MIT — bkz. [LICENSE](LICENSE). Katkı için [CONTRIBUTING.md](CONTRIBUTING.md),
güvenlik bildirimi için [SECURITY.md](SECURITY.md).
