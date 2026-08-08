# Güvenlik

## Açık bildirimi

Bir güvenlik açığı bulursan **herkese açık issue açma**. GitHub'da
**Security → Report a vulnerability** üzerinden özel bildirim gönder.

## Uygulamanın güvenlik duruşu

**API anahtarı.** Depoda, `.exe` içinde veya ayar dosyasında hiçbir
anahtar gömülü değildir. Kullanıcının anahtarı yalnız kendi
bilgisayarında, Windows DPAPI ile (`DataProtectionScope.CurrentUser` +
ek entropi) şifrelenmiş olarak `%APPDATA%\EkranCeviri\anahtar.bin`
dosyasında tutulur. Dosya kopyalansa bile başka bir kullanıcı hesabı veya
başka bir makine çözemez. Arayüzde daima maskeli gösterilir
(`xai-abcd…wxyz`); giriş alanı `PasswordBox`'tır.
`Ayarlar.Kaydet()` `grok_api_key` alanını **her zaman boş yazar** —
anahtarın ayar dosyasına düşmesi kod yoluyla imkânsızdır.

**Ekran verisi.** Yazı tanıma tamamen cihazda çalışır
(`Windows.Media.Ocr`). Ekran görüntüsü diske yazılmaz ve hiçbir sunucuya
gönderilmez. Ağa çıkan tek şey çıkarılmış metindir.

**Günlük.** `%APPDATA%\EkranCeviri\gunluk.txt` dosyasına sohbet metni,
çeviri içeriği veya anahtar **yazılmaz** — yalnız olay ve hata bilgisi.

**İstem enjeksiyonu.** Ekrandan okunan metin modele **veri** olarak, ayrı
ve etiketli alanlarda (`<sohbet>`, `<gecmis>`, `<tarz_ornekleri>`)
verilir; sistem istemine asla girmez. Metnin içindeki "önceki talimatları
yok say" türü ifadeler talimat sayılmaz. Bu önemlidir çünkü "Yazdığımı
Çevir" çıktısı kullanıcı okumadan mesaj kutusuna yapıştırılır. Model
çıktısı ayrıca kalite kapılarından geçer: rakam, saat ve fiyat korunumu
doğrulanmazsa çeviri reddedilir ve hiçbir şey yapıştırılmaz.

**Veri kaybı koruması.** Kısayol önce `Ctrl+A` gönderir. Yanlış pencerede
tetiklenirse kullanıcının belgesini yok edebilirdi, bu yüzden:
ön plandaki uygulama bilinen bir mesajlaşma uygulaması değilse işlem
yapılmaz; seçilen metin 1200 karakteri veya 15 satırı aşarsa iptal edilir;
kullanıcının panosu korunur ve geri yüklenir.

**Yetki.** Uygulama yönetici hakkı istemez (`asInvoker`). Ekran okuma ve
tuş gönderme normal kullanıcı hakkıyla yapılır.

**Kod imzası.** Sürümler imzasızdır; Windows SmartScreen "bilinmeyen
yayımcı" uyarısı verir. `.exe` GitHub Actions'ta, bu depodaki kaynaktan,
herkesin görebildiği bir iş akışıyla üretilir.

## Kapsam dışı

- Kullanıcının kendi API anahtarını üçüncü kişilerle paylaşması
- Windows'un kendi güvenlik mekanizmalarındaki açıklar
- Çeviri sağlayıcılarının (xAI, Bing, Google) altyapısı
