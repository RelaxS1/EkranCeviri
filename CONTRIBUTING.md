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
dotnet build EkranCeviri.csproj -c Debug --nologo         # 0 hata; nullability uyarıları hatadır
dotnet build Testler/Testler.csproj -c Debug --nologo
dotnet run --project Testler/Saf/Saf.csproj -c Debug --nologo   # her yerde koşar (macOS dâhil)
dotnet run --project Testler/Testler.csproj -c Debug --nologo   # yalnız Windows'ta koşar
dotnet publish EkranCeviri.csproj -c Release                     # tek dosyalık .exe
```

İki test koşucusu var:

- **`Testler/Saf`** — WPF ve Windows API'sine dokunmayan üretim dosyalarını
  (`Ceviri/Kalite.cs`, `Ceviri/GidenKapisi.cs`, `Ceviri/Defterler.cs`,
  `Ekran/Hareket.cs`, `Cekirdek/ArizaGunlugu.cs`, …) doğrudan derler ve
  **macOS/Linux'ta gerçekten koşar**. Çıktısı `TÜM SAF TESTLER GEÇTİ ✅
  (N test)` ve çıkış kodu 0 olmalı. Windows'suz geliştirirken tek kanıt
  yolu budur.
- **`Testler/Testler.csproj`** — WPF/DPAPI/OCR'a dokunan testler; saf
  testleri de çağırır. macOS'tan çapraz derlenir ama çalıştırılamaz; CI'da
  Windows'ta koşar.

CI her push'ta gerçek bir Windows makinesinde derler, iki koşucuyu da
koşar. **Testler geçmezse .exe üretilmez.** Kapıyı atlatma; kırılan bir
şey varsa düzelt.

## Kurallar

- **Anahtar, jeton veya kişisel veri commit'leme.** `Ayarlar.Kaydet()`
  `grok_api_key`'i her zaman boş yazar — bunu değiştirme. `yayinla.sh`
  çalışma ağacını ve git geçmişini `xai-` deseni için tarar.
- Kod, tanımlayıcılar ve kullanıcıya görünen metinler **Türkçe**.
- Yorumlar **neden**i açıklar, **ne**yi değil. Bu koddaki eşiklerin
  çoğu bir hatanın izidir; yorumu silme, taşı.
- **Testler üretim fonksiyonlarını çağırır**, kopyalarını değil.
  Kopyalanmış mantık üzerinde geçen test hiçbir şey kanıtlamaz.
- **Her yeni saf fonksiyon `Testler/Saf/Saf.csproj`'un `Compile`
  listesine eklenir** ve testi `Testler/SafTestler*.cs` içine yazılır.
  "Saf" demek: `System.Windows`, `Windows.*`, `ProtectedData` ve
  `Gunluk`'a bağımlı değil; dosya/ağ I/O'su varsa testten geçici dizine
  yönlendirilebilir (`ArizaGunlugu.Dizin`, `Teshis.Dizin` gibi). Saf
  koşucuda derlenmeyen bir fonksiyonun testi yalnız CI'da koşar — bunu
  gerekçesiyle yorumla.
- Üretim kodunda `Console` yok (WinExe); günlük `Gunluk.Yaz/Hata`.
- Çeviri çıktısına dokunan bir değişiklik yapıyorsan rakam/saat/fiyat
  korunum ve giden kapısı testlerini genişlet — o metin doğrudan
  karşıdakine gidiyor.
- Kaynak sızıntısına dikkat: GDI DC/bitmap ve `LockBits` mutlaka serbest
  bırakılmalı. Saniyede bir yakalayan bir uygulamada sızıntı birkaç
  saatte uygulamayı öldürür.
- Ekrandan okunan metni **sistem istemine koyma** — yalnız kullanıcı
  mesajında, etiketli veri olarak ve `Kalite.ZarfaGuvenli`den geçirerek.
- Paylaşılan durum (`Blok.Ceviri`, defterler, önbellek) iki iş
  parçacığından okunur: `lock` ya da `Concurrent*`. Arayüze dokunan her
  şey `Dispatcher` üzerinden.
- Belgeye giren her sayı koddan ya da o turda üretilen ölçümden gelir;
  ölçülmemiş şey "ölçülmedi" diye yazılır.

## Neye katkı iyi gelir

- Yeni lehçeler ve sözlük genişletmeleri (Avusturya bölgeleri, İsviçre
  kantonları) — `Ceviri/Lehce.cs`
- Yazı tanıma doğruluğu: farklı temalar, koyu mod, küçük yazı tipleri —
  `Ekran/OcrOkuyucu.cs`
- Balon algılama eşikleri: farklı sohbet uygulamaları —
  `Ekran/Bloklayici.cs`
- Hareket kararı eşikleri (`Ekran/Hareket.cs`): farklı animasyonlar,
  GIF/çıkartma yoğun sohbetler
- Yeni çeviri motoru bağlayıcıları — `Ceviri/MakineMotorlari.cs`

Bir eşik değiştiriyorsan o davranışı kilitleyen bir test ekle: saf bir
eşikse `Testler/SafTestler*.cs`, Windows'a bağlıysa `Testler/Program.cs`.
