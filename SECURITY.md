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

**Ücretsiz motor gerçekten ücretsizdir.** Çeviri motoru "Ücretsiz çeviri"
seçiliyken **hiçbir metin xAI'ye gitmez** — "Yazdığımı Çevir" ve ekran
çevirisi dâhil. Giden motor tercihi yalnız ana motor "Yapay zekâ" iken
geçerlidir (`Ayarlar.EtkinGidenMotor`, `GidenKapisi.MotorSecimi`). Bu,
kullanıcının görmediği bir menü seçeneği yüzünden yazdığı her mesajın
ücretli motora gitmesini kapatan koddur.

**Ekran verisi.** Yazı tanıma tamamen cihazda çalışır
(`Windows.Media.Ocr`). Ekran görüntüsü diske yazılmaz ve hiçbir sunucuya
gönderilmez. Ağa çıkan tek şey çıkarılmış metindir. Modele giden şeylerin
tam listesi README "Gizlilik" bölümündedir.

**Günlükler.** `%APPDATA%\EkranCeviri\gunluk.txt` dosyasına sohbet metni,
çeviri içeriği veya anahtar **yazılmaz** — yalnız olay ve hata bilgisi.
Arıza günlüğü (`ariza.log`) başarısız her çeviri için tek satır tutar:
zaman, aşama, motor, sebep, süre, metnin **uzunluğu** ve sha256'sının ilk
8 hanesi. Metnin kendisi **yazılmaz**; yalnız `--tani` komut satırı
bayrağıyla açılan tanı modunda eklenir (`ArizaGunlugu.Satir`). Teşhis
günlüğü (`teshis.log`) 60 saniyede bir sayaç ve süre özeti yazar, metin
taşımaz.

**İstem enjeksiyonu.** Ekrandan okunan metin modele **veri** olarak, ayrı
ve etiketli alanlarda (`<sohbet>`, `<gecmis>`, `<tarz_ornekleri>`,
`<cevrilecek>`) verilir; sistem istemine asla girmez. Metnin içindeki
"önceki talimatları yok say" türü ifadeler talimat sayılmaz. Güvenilmez
metin zarfa konmadan önce `Kalite.ZarfaGuvenli` ile zarf sınırını taklit
eden diziler (`</cevrilecek>` gibi) etkisizleştirilir; sohbete gömülü bir
etiket zarfı erken kapatıp modele yönerge geçiremez.

**Giden çıktı kapısı.** "Yazdığımı Çevir" çıktısı kullanıcı okumadan mesaj
kutusuna yapıştırıldığı için `GidenKapisi.Kapi` **hem ham model yanıtına
hem biçimlendirilmiş çıktıya** uygulanır (biçimleme noktalamayı sildiği
için `www.kotu.com` biçimden sonra `wwwkotucom` oluyor ve yalnız biçimli
çıktı denetlenirken URL katmanı fiilen ölüydü). Kapı sırasıyla şunları
reddeder: rakam dizisi birebir korunmamış; kaynakta olmayan URL / IBAN /
e-posta / telefon eklenmiş; çıktı `max(80, kaynak×3)` karakterden uzun;
çıktıda Türkçe kalıntı var. Kapı reddederse **hiçbir şey yapıştırılmaz**,
mesaj değişmez ve ret nedeni kullanıcıya bildirimle gösterilir. Bu kapı
Grok ve ücretsiz motor yollarının ikisinde de zorunludur.

**Veri kaybı koruması.** Kısayol önce `Ctrl+A` gönderir. Yanlış pencerede
tetiklenirse kullanıcının belgesini yok edebilirdi, bu yüzden:
ön plandaki uygulama bilinen bir mesajlaşma uygulaması değilse işlem
yapılmaz; seçilen metin 1200 karakteri veya 15 satırı aşarsa iptal edilir;
kullanıcının panosu korunur ve geri yüklenir.

**Sınama modu.** `--sinama` / `--gizli-sinama` ile açılan uygulama
ayarları diske **yazmaz** (`Ayarlar.Kaydet`); bir QA koşusu kullanıcının
gerçek motor/anahtar ayarını bozamaz.

**Yetki.** Uygulama yönetici hakkı istemez (`asInvoker`). Ekran okuma ve
tuş gönderme normal kullanıcı hakkıyla yapılır.

**Kod imzası.** Sürümler imzasızdır; Windows SmartScreen "bilinmeyen
yayımcı" uyarısı verir. `.exe` GitHub Actions'ta, bu depodaki kaynaktan,
herkesin görebildiği bir iş akışıyla üretilir.

**Yayın kapısı.** `yayinla.sh` yalnız `main` dalından çalışır; çalışma
ağacını ve **git geçmişinin tamamını** `xai-` anahtar deseni için tarar.
Tarama sonucu çıkış koduna değil çıktının varlığına bakılarak okunur
(boru + `grep -q` kalıbı SIGPIPE yüzünden sır varken "temiz" diyordu);
bulgu basılırken anahtar maskelenir.

## Kapsam dışı

- Kullanıcının kendi API anahtarını üçüncü kişilerle paylaşması
- Windows'un kendi güvenlik mekanizmalarındaki açıklar
- Çeviri sağlayıcılarının (xAI, Bing, Google) altyapısı
