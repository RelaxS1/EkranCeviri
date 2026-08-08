# Katkı

## Kurulum

```bash
git clone https://github.com/RelaxS1/EkranCeviri.git
cd EkranCeviri
dotnet build EkranCeviri.csproj
```

Gereken: .NET 8 SDK. Windows'ta çalıştırılır; macOS/Linux'tan yalnız
derlenir (`EnableWindowsTargeting` açık).

## Değişiklik yaparken

```bash
dotnet run --project Testler/Testler.csproj    # her değişiklikten sonra
dotnet publish EkranCeviri.csproj -c Release   # tek dosyalık .exe
```

CI her push'ta gerçek bir Windows makinesinde derler ve testleri koşar.
**Testler geçmezse .exe üretilmez.** Kapıyı atlatma; kırılan bir şey
varsa düzelt.

## Kurallar

- **Anahtar, jeton veya kişisel veri commit'leme.** `Ayarlar.Kaydet()`
  `grok_api_key`'i her zaman boş yazar — bunu değiştirme.
- Kod, tanımlayıcılar ve kullanıcıya görünen metinler **Türkçe**.
- Yorumlar **neden**i açıklar, **ne**yi değil. Bu koddaki eşiklerin
  çoğu bir hatanın izidir; yorumu silme, taşı.
- **Testler üretim fonksiyonlarını çağırır**, kopyalarını değil.
  Kopyalanmış mantık üzerinde geçen test hiçbir şey kanıtlamaz.
- Çeviri çıktısına dokunan bir değişiklik yapıyorsan rakam/saat/fiyat
  korunum testlerini genişlet — o metin doğrudan müşteriye gidiyor.
- Kaynak sızıntısına dikkat: GDI DC/bitmap ve `LockBits` mutlaka serbest
  bırakılmalı. Saniyede bir yakalayan bir uygulamada sızıntı birkaç
  saatte uygulamayı öldürür.
- Ekrandan okunan metni **sistem istemine koyma** — yalnız kullanıcı
  mesajında, etiketli veri olarak.

## Neye katkı iyi gelir

- Yeni lehçeler ve sözlük genişletmeleri (Avusturya bölgeleri, İsviçre
  kantonları) — `Ceviri/Lehce.cs`
- Yazı tanıma doğruluğu: farklı temalar, koyu mod, küçük yazı tipleri —
  `Ekran/OcrOkuyucu.cs`
- Balon algılama eşikleri: farklı sohbet uygulamaları —
  `Ekran/Bloklayici.cs`
- Yeni çeviri motoru bağlayıcıları — `Ceviri/MakineMotorlari.cs`

Bir eşik değiştiriyorsan `Testler/Program.cs` içine o davranışı kilitleyen
bir test ekle.
