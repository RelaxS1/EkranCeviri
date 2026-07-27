# Ekran Çeviri.app

Menü çubuğunda yaşayan bir macOS uygulaması. Bölge seç; içindeki yazışma
tanınır (Apple Vision OCR), çevrilir ve **orijinalin üstüne aynı konumda,
aynı balon rengiyle** yazılır — sanki karşındaki kişi senin dilinde
yazıyormuş gibi.

Kurulu konum: `/Applications/EkranCeviri.app` (kendi kendine yeter; bu
klasöre bağımlı değildir).

## Kullanım

1. Uygulamayı aç (Spotlight → "EkranCeviri"). Dock'ta görünmez, menü
   çubuğuna **ç** balonu ikonu olarak yerleşir.
2. İkona tıkla → **Bölgeyi Çevir** (veya global kısayol **⌃⌥T**).
3. Ekran kararır, çevrilecek bölgeyi sürükleyerek seç.
4. Çeviri orijinal yazının üstüne yama olarak çizilir.

Overlay üzerindeki düğmeler:

| Düğme | İşlev |
|---|---|
| AI ile çevir | Aynı metni Grok ile yeniden çevirir (ücretsiz çeviri kötüyse) |
| Orijinal | Çeviriyi gizle/göster (alttaki gerçek ekranı görürsün) |
| Kopyala | Tüm çevirileri panoya kopyalar |
| ✕ / Esc | Kapatır |

Menüden **çeviri motoru** (Otomatik / Grok AI / Hızlı) ve **hedef dil**
seçilebilir; seçimler kalıcıdır.

## Çeviri motoru mantığı

- **Otomatik** (varsayılan): Metinde Zürih lehçesi belirteci (isch, nöd,
  chli, gäll, hesch…) bulunursa **Grok AI**, yoksa **ücretsiz Google**
  (anahtarsız `clients5` uç noktası, tüm bloklar tek istekte).
- Google İsviçre Almancasını anlamıyor (test edildi, saçmalıyor) — lehçe
  algılanınca otomatik Grok'a gidilir. Grok hata verirse ücretsize düşer.

## İzinler

| İzin | Ne için | Nerede |
|---|---|---|
| Ekran Kaydı | Bölgeyi yakalamak (zorunlu) | Sistem Ayarları → Gizlilik ve Güvenlik → Ekran Kaydı → EkranCeviri |
| Giriş İzleme | ⌃⌥T kısayolu (isteğe bağlı; menü izinsiz de çalışır) | … → Giriş İzleme → EkranCeviri |

İlk açılışta Ekran Kaydı izni otomatik sorulur; verdikten sonra uygulamayı
kapatıp yeniden aç.

## Ayarlar

`~/Library/Application Support/EkranCeviri/config.json`:

| Alan | Anlam |
|---|---|
| `hedef_dil` | Çeviri hedefi (`tr`, `en`, `de`…) |
| `motor` | `auto` / `ai` / `hizli` |
| `grok_api_key` | xAI API anahtarı |
| `grok_model` | Varsayılan `grok-3` (xAI şu an grok-4.3'e yönlendiriyor) |
| `ocr_dilleri` | Vision'a verilen tanıma dilleri |

## Geliştirme (bu klasör)

```bash
./calistir.sh                # menü çubuğu modunda çalıştır (venv'den)
./calistir.sh --sec          # tek seferlik: hemen seç, bitince çık
./calistir.sh --sec --ai     # tek seferlik, Grok zorla
venv/bin/python3 ekran_ceviri.py --test        # GUI'siz öz test
venv/bin/python3 ekran_ceviri.py --test --test-ai  # + Grok testi
./uygulama_yap.sh            # .app'i yeniden derle ve kur
```

Kod değişince `./uygulama_yap.sh` çalıştırman yeterli — ikonu, paketleri
ve imzayı tazeleyip `/Applications`'a kurar, eski kopyayı kapatır.

Alfred'den tetiklemek istersen (isteğe bağlı):
`/Applications/EkranCeviri.app/Contents/MacOS/EkranCeviri --sec`

## Paketleme notları (önemli, tekrar yaşanmasın)

- **Venv olduğu gibi .app'e gömülemez**: `venv/bin/python3` paket dışına
  sembolik bağdır; imzalı pakette dış bağ yasak, Launch Services uygulamayı
  *sessizce* başlatmaz. Çözüm: yalnız `site-packages` kopyalanır, sistem
  Python'u `PYTHONPATH` ile kullanılır.
- **Script-tabanlı .app'i LS, Rosetta (x86_64) altında başlatabiliyor**:
  PyQt6 arm64 olduğundan `ImportError: incompatible architecture` çöküşü
  olur. Çözüm: başlatıcıda `arch -arm64` + Info.plist'te
  `LSRequiresNativeExecution`.
- Hata ayıklama: uygulamanın stdout/stderr'i
  `~/Library/Logs/EkranCeviri.log` dosyasına akar.

## Sınırlar

- Ücretsiz Google çevirisi resmi olmayan uç nokta kullanır; kapanırsa kod
  otomatik yedek uç noktaya düşer.
- Emojiler OCR'da tanınmaz; yama emojinin üstünü kapatabilir.
- Çok ekran varsa seçim, imlecin bulunduğu ekranda açılır.
