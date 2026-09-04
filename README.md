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
anlam üretiyor; anahtar girmen önerilir. Motor "Yapay zekâ" seçiliyken
anahtar yoksa çubukta `⚠︎ Grok anahtarı yok → Bing` uyarısı görünür; motor
"Yapay zekâ" iken ücretsiz motora **sessizce düşülmez** (yanlış çeviri
hafızaya yazılıp "Grok kötü çeviriyor" yanılgısı doğuruyordu).

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
3. Çeviriler balonların üstüne oturur; bölgenin üstünde (sığmazsa altında)
   bir **çeviri çubuğu** belirir

Sohbet ekranı senin kalır: çeviri katmanı tıklamayı geçirir, kaydırabilir,
yazabilirsin. Kendi yazdığın Almanca mesajlar da çevrilir; Türkçe yazdıkların
motora gitmez.

### Çeviri çubuğu

Solda durum etiketi (hata varsa turuncu), sağda düğmeler:

| Düğme | Ne yapar |
|---|---|
| ✕ | Çeviri katmanını kapatır |
| 📋 | Ekrandaki çevirileri satır satır panoya kopyalar |
| 👁 | Orijinal metni göster / gizle |
| ✨ | Ekrandaki her şeyi **Grok kalite modeliyle yeniden** çevirir; tekrar tavanını da açar |
| ⌨️ | **Yazdığımı Çevir** (kısayolla aynı) — Türkçe → karşı dil |
| 💬 | **Cevap öner** — sohbete uygun 3 cevap |
| Canlı | Canlı modu aç/kapat |

**Canlı mod** — yeni mesaj gelince otomatik çevirir; sohbet kaydığında
çeviriler içerikle birlikte kayar, mevcut çeviriler yanıp sönmez. Çubukta
"⏳ çevriliyor…" ağ beklediğini gösterir; ağ giderse çevrilemeyen mesajlar
bağlantı gelince otomatik denenir.

**Yazdığımı Çevir** (`Ctrl+Alt+C` ya da ⌨️) — mesaj kutusuna Türkçe yaz,
kısayola bas: metin karşındakinin **lehçesinde** yazılmış hâliyle değişir.
Fiyat ve saatler rakam olarak korunur. Çıktı bir kapıdan geçer (sayılar,
bağlantı/hesap/numara, uzunluk, Türkçe kalıntı); kapı reddederse **mesajın
değişmez** ve nedeni bildirimle söylenir ("Mesajın DEĞİŞMEDİ — …").

**Cevap öner** (💬) — ekrandaki sohbeti okur, karşı dilde 3 cevap önerir;
her önerinin altında Türkçe anlamı, yanında **Kopyala**. Sohbet hafızası
açıksa senin eski mesajlarından üslup örnekleri ve benzer geçmiş konuşmalar
da modele verilir (istemi sen yazarsın, sen yapıştırırsın — otomatik
gönderim yok). Grok anahtarı gerektirir.

**Önce hızlı göster, sonra kaliteyle düzelt** (Gelişmiş, varsayılan açık):
hızlı model çeviriyi anında yazar; kalite modeli arka planda aynı blokları
yeniden çevirir. Kalite sonucu, balon ekrana çizildikten sonra **en fazla
8 saniye** içinde geldiyse ekranda düzeltilir; daha geç gelirse ekran
oynatılmaz, sonuç **yalnız hafızaya** yazılır (okunmuş metnin gözünün
önünde değişmesi ürünü kararsız hissettiriyordu). Kalite isteğinin kendi
zaman aşımı 20 sn'dir. Zaten düzgün çevrilmiş kısa metinlerde (25
karakterden az), çevrilecek bir şey olmayan bloklarda (yalnız sayı, saat,
bağlantı, emoji ya da zaten Türkçe) ve tekrar tavanına varmış metinlerde
ücretli kalite çağrısı hiç yapılmaz.

### Menü (tepsi simgesine sağ tıkla)

- **Bölgeyi Çevir · Yazdığımı Çevir · Çeviriyi Kapat**
- **Canlı çeviri** (işaretli)
- **Kim yazıyor ▸** Ben / Karşımdaki → Kadın / Erkek / Belirtme
  (çeviride hitap ve ekler buna göre)
- **Bana çevir ▸** hedef dil (Türkçe, İngilizce, Almanca, Fransızca,
  İspanyolca, İtalyanca, Rusça, Arapça, Portekizce)
- **Gelişmiş ▸** karşı tarafın dili (Alman modu — Almanya + İsviçre /
  yalnız İsviçre / otomatik) · çeviri motoru · yetişkin içerik · hız
  önceliği · önce hızlı göster · Yazdığımı Çevir hızlı modelle · emoji ·
  kısayol · yazım üslubu · yapay zekâ anahtarı · ayarlar · **sohbet
  hafızası** (aç/kapat) · kayıtlı çevirileri sil · sohbet geçmişini sil ·
  günlük / arıza günlüğü dosyasını aç · yazı tanıma dilleri
- **Çık**

### Sohbet hafızası

Açıkken (varsayılan açık) ekrandan okunan mesajlar ve çevirileri yerel bir
JSONL dosyasına yazılır: `%APPDATA%\EkranCeviri\sohbet_gecmisi.jsonl`.
Yalnız "Cevap öner" için üslup ve bağlam örneği olarak kullanılır. Menüden
**kapatılır** ve **silinir**; hiçbir yere yüklenmez.

### Arıza ve teşhis günlükleri

- `%APPDATA%\EkranCeviri\ariza.log` — başarısız her çeviri tek satır:
  `zaman | aşama | motor | sebep | süre | uzunluk=N | karma=xxxxxxxx`.
  Mesaj metni **yazılmaz**; `karma` sha256'nın ilk 8 hanesidir ("aynı
  mesaj yine mi düştü?" sorusu için). Sebepler küçük bir kümeden gelir:
  `kalite-kapisi-reddetti`, `zaman-asimi`, `iptal`, `ag-hatasi`,
  `bos-yanit`, `motor-cevirmedi`, `anahtar-yok`, `hiza-cakismasi`,
  `cevrilecek-sey-yok`, `bilinmeyen`. Yalnız `--tani` bayrağıyla açılırsa
  metin de eklenir. 2 MB'ı geçince `.1`e döndürülür.
- `%APPDATA%\EkranCeviri\teshis.log` — 60 saniyede bir tek satır: sayaçlar,
  ortalama/en uzun süreler, arayüz takılmaları (200 ms'den uzun) ve o
  andaki aşama. Boşta bile "boşta" yazar (kalp atışı: "kullanıcı
  çalışmadı" ile "uygulama dondu" ayrılsın).
- `%APPDATA%\EkranCeviri\gunluk.txt` — olay ve hata; içerik yok.

İkisi de menüden açılır: **Gelişmiş → Arıza günlüğünü aç**.

## Gizlilik

- **Yazı tanıma tamamen bilgisayarında** (Windows.Media.Ocr) — ekran
  görüntüsü hiçbir yere gitmez, diske yazılmaz
- Modele giden şeyler, eksiksiz liste:
  - ekrandan okunan mesaj metinleri (ve canlıda bağlam olarak ekrandaki
    önceki mesajlar + çevirileri)
  - "Yazdığımı Çevir"de senin yazdığın Türkçe metin ve ekrandaki karşı
    taraf mesajları (tarz örneği olarak)
  - **kendi yazdığın kimlik/üslup metni** — "Kim yazıyor" ve "Nasıl Yazayım
    (üslup)" alanları (`kisilik`; Yazdığımı Çevir için ayrı üslup metni
    varsa o). Buraya yazdığın ad, meslek gibi her şey Grok sistem
    isteminin parçası olur; istemiyorsan boş bırak
  - sohbet hafızası açıksa, "Cevap öner"de geçmişten üslup örnekleri ve
    benzer konuşmalar
- Bunlar yalnız **Grok** yoluna gider; Bing/Google yalnız düz metin alır.
  Motor "Ücretsiz çeviri" seçiliyken **hiçbir metin xAI'ye gitmez**,
  Yazdığımı Çevir dâhil
- Sohbet hafızası ve çeviri hafızası yerel; menüden silinebilir
- Günlük dosyalarına **sohbet içeriği yazılmaz** (yukarıdaki bölüm)
- Ekrandan okunan metin modele **veri** olarak verilir, talimat olarak
  değil (prompt injection koruması); zarf etiketlerini taklit eden
  diziler etkisizleştirilir

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
| Canlı döngü | İki şerit: yakalama/OCR turu ile ağ işi ayrı | Ağ beklerken kaydırma telafisi ve yeni balon algılama duruyordu; ağ işi tek uçuşlu, epoch iptaliyle düşer |
| Hareket kararı | Hücre sayısıyla (3 hücre ≈ kısa kelime, hücre başına 12/255 gri farkı) | Ortalama fark küçük yeni balonu (bölgenin %3'ü) kaçırıyordu; imleç yanıp sönmesi (~4/255) eşiğin altında kalır; sürekli değişen hücreler (GIF) animasyon maskesine alınır |
| Ücretli tekrar tavanı | Aynı metin için en fazla 2 ücretli deneme, sonra "değişmez" | macOS sürümünde ölçüldü (17 benzersiz metin için 78 ücretli çağrı); aynı mantık Windows'a taşındı, Windows'ta ölçülmedi; iki model yapılandırmasında da aynı dönen sonuç belirlenimcidir |
| Kalite yaş kapısı | 8 sn (ekranda durma süresi = kuyruk + ağ) | Okunmuş balonun gözün önünde değişmesi ürünü "kararsız" hissettirir; geç gelen sonuç yalnız hafızaya |
| Giden kapısı | Ham + biçimli çıktıya birlikte | Biçimleme noktalamayı silince URL/e-posta deseni eşleşmiyordu; kapının bağlantı katmanı fiilen ölüydü |
| Boş çeviri | Başarı sayılmaz; süreli nöbet defteri (90 sn tur, anahtar başına 4 deneme, 900 sn'de hak yenileme) | Boş çeviri "başarı" sayılınca mesaj sonsuza kadar kaynak dilde kalıyordu |
| Ücretsiz motor | Motor "ücretsiz" iken Yazdığımı Çevir de Bing'e | Kullanıcı görmediği bir seçenek yüzünden her mesajını xAI'ye gönderiyordu |

Yazı tanıma cihazda çalıştığı, motorlar kalite kapılarından geçtiği ve
lehçe sözlüğü elle yazıldığı için çeviri kalitesi sabit bir çeviri
sitesinden belirgin yüksek — özellikle lehçelerde.

## Geliştirme

```bash
dotnet build EkranCeviri.csproj -c Debug --nologo
dotnet build Testler/Testler.csproj -c Debug --nologo
dotnet run --project Testler/Saf/Saf.csproj -c Debug --nologo   # macOS/Linux'ta da koşar
dotnet run --project Testler/Testler.csproj -c Debug --nologo   # yalnız Windows
dotnet publish EkranCeviri.csproj -c Release
```

macOS veya Linux'tan da derlenir (`EnableWindowsTargeting` açık) ama
**çalıştırılamaz**; nullability uyarıları hatadır. Saf test koşucusu
(`Testler/Saf`) WPF'e dokunmayan üretim dosyalarını doğrudan derler ve her
yerde koşar. Her push'ta GitHub Actions gerçek bir Windows makinesinde
derleyip iki koşucuyu da koşuyor — asıl doğrulama orada, ve **testler
geçmezse .exe üretilmiyor**.

Sürüm çıkarmak: `./yayinla.sh 1.2.0` — yalnız `main` dalından çalışır,
çalışma ağacını ve git geçmişini anahtar deseni için tarar, etiketi atar;
CI .exe'yi [Releases](../../releases/latest) sayfasına koyar.

`Testler` üretim fonksiyonlarını çağırır, kopyalarını değil. Yeni bir
davranış eklerken testi de üretim fonksiyonuna bağla — kopyalanmış mantık
üzerinde geçen test hiçbir şey kanıtlamaz. Ayrıntı: [CONTRIBUTING.md](CONTRIBUTING.md).

Bu uygulama bir macOS sürümünden yola çıkarak yazıldı; çeviri zekâsı
(lehçe algılama, sözlükler, istemler, kalite kapıları, defterler, hareket
kararı, arıza/teşhis günlüğü) oradan taşındı, sistem katmanının tamamı
Windows'a göre sıfırdan yazıldı. Taşınmayanlar ve doğrulama durumu
[.ai/STATE.md](.ai/STATE.md) içinde.

## Lisans

MIT — bkz. [LICENSE](LICENSE). Katkı için [CONTRIBUTING.md](CONTRIBUTING.md),
güvenlik bildirimi için [SECURITY.md](SECURITY.md).
