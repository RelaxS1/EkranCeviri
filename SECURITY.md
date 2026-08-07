# Güvenlik

## Açık bildirimi

Bir güvenlik açığı bulursan **herkese açık issue açma**. GitHub'da
**Security → Report a vulnerability** üzerinden özel bildirim gönder.

## Uygulamanın güvenlik duruşu

**API anahtarı.** Depoda, uygulama paketinde veya ikilide hiçbir anahtar gömülü
değildir. Kullanıcının anahtarı yalnız kendi bilgisayarında,
`~/Library/Application Support/EkranCeviri/anahtar` dosyasında, `0600` izinle
tutulur. Arayüzde daima maskeli gösterilir.

**Ekran verisi.** Yazı tanıma tamamen cihazda (Apple Vision) çalışır. Ekran
görüntüsü diske yazılmaz ve hiçbir sunucuya gönderilmez. Ağa çıkan tek şey
çıkarılmış metindir; bu da ilk kullanımda açık onaya bağlıdır.

**İstem enjeksiyonu.** Ekrandan okunan metin modele **veri** olarak, ayrı ve
etiketli bir alanda verilir. Metnin içindeki "önceki talimatları yok say" türü
ifadeler talimat sayılmaz. Model çıktısı ayrıca kalite kapılarından geçer:
rakam, saat ve fiyat korunumu doğrulanmazsa çeviri reddedilir.

**Sistem izinleri.** Uygulama yalnız iki izin ister: Ekran Kaydı (zorunlu) ve
Erişilebilirlik ("Yazdığımı Çevir" için, isteğe bağlı). Erişilebilirlik izni
verilmezse o özellik kapanır, uygulamanın geri kalanı çalışır.

**Kod imzası.** Yerel kurulum, makinede üretilen kendinden imzalı bir sertifika
kullanır; böylece her derlemede izinler sıfırlanmaz. Derleme betiği ad-hoc
imzayı reddeder.

## Kapsam dışı

- Kullanıcının kendi API anahtarını üçüncü kişilerle paylaşması
- macOS'un kendi izin mekanizmalarındaki açıklar
- Çeviri sağlayıcılarının (xAI, Bing, Google) altyapısı
