# Evidence — macOS iyileştirmelerinin Windows'a taşınması (v1.2.0)

Result: PARTIAL

## Completion contract checked
- User outcome: Windows kullanıcısı v1.2.0'ı indirdiğinde Mac'teki davranışı
  alır (yarım çeviri kalmaz, hızlı-önce/kalite-sonra, hareket kararı, ücretli
  tekrar tavanı, giden kapısı ham+biçimli + ret nedeni, bar düğmeleri
  ✕ 📋 👁 ✨ ⌨️ 💬, cevap önerisi, sohbet hafızası, menü, arıza/teşhis günlüğü).
- Success criteria: iki csproj 0 hata (macOS çapraz); `Testler/Saf` macOS'ta
  EXIT=0; CI (gerçek Windows) yeşil ve `.exe` üretildi; `v1.2.0` Releases'ta.
- Rollback/cleanup: `main`e yalnız CI yeşilse birleşir; `v1.1.0` etiketi ve
  `.exe`'si Releases'ta kalır; dal `windows-evrim` geri alma noktasıdır.

## Commands
| Command | Exit | Evidence |
|---|---:|---|
| `~/.dotnet/dotnet build EkranCeviri.csproj -c Debug --nologo` (macOS çapraz) | 0 | `0 Hata / 0 Uyarı` (2026-09-05, HEAD `ac9eabe`) |
| `~/.dotnet/dotnet build Testler/Testler.csproj -c Debug --nologo` | 0 | `0 Hata / 0 Uyarı` |
| `~/.dotnet/dotnet run --project Testler/Saf/Saf.csproj -c Debug --nologo` (macOS) | 0 | `TÜM SAF TESTLER GEÇTİ ✅ (318 test)`, ✗ = 0 |
| CI dal `windows-evrim` → çalışma **33970893637** (windows-latest) | 0 | Derle ✓ · `Testleri koş` → `TÜM TESTLER GEÇTİ ✅ (402 test)` · `Saf testleri koş` → `TÜM SAF TESTLER GEÇTİ ✅ (318 test)` · `.exe` üretildi |
| Etiket `v1.2.0` → CI çalışması **33970978197** | 0 | Derle · Testleri koş · Saf testleri koş · Tek dosyalık .exe · **Sürüm yayınla** hepsi `success` |
| `gh release view v1.2.0` | 0 | https://github.com/RelaxS1/EkranCeviri/releases/tag/v1.2.0 — `EkranCeviri.exe` 78 026 598 bayt |
| `bash -n yayinla.sh` + geçici depoda sahte `xai-`+40 anahtar (çalışma ağacı / yalnız geçmiş / main dışı dal) | 1 (beklenen) | üç senaryoda `✗ DURDURULDU` (Faz E raporu; gerçek depoya sahte anahtar yazılmadı) |

## Real flows
- Gerçek Windows makinesinde (CI runner) derleme + 402 Windows testi + 318
  saf test + tek dosyalık `.exe` üretimi; etiket koşusu Releases'a yükledi. Testler ÜRETİM fonksiyonlarını
  çağırır (Bloklayici, Kalite, GidenKapisi, Defterler, Hareket, YanitCozucu,
  OturumOnbellegi, SohbetGecmisi, ArizaGunlugu, Geometri, KisayolMetni…).
- Her faz (A, B, C, D) ayrı bağımsız denetçi tarafından "yalanlanmaya"
  çalışıldı (≤2 onarım turu); fazlar-arası bütünleştirme denetimi 5 mercek
  + her bulguya 2 yalanlayıcı: 35 ham → 26 tekil → 7 doğrulanmış P1
  onarıldı (`d739259`), yalanlayıcıların şiddette ayrıştığı 3 gerçek bulgu
  + 9 ucuz P2 ikinci turda onarıldı (`ac9eabe`), bağımsız kapı geçti.
- **GUI akışı (bölge seç → katman → canlı → ⌨️ → 💬) gerçek makinede
  KOŞTURULMADI** — macOS'tan yalnız derlenir. Tek dış eylem: sahip
  v1.2.0'ı Windows'ta açıp `.ai/STATE.md`'deki el-testi listesini geçer.

## Negative / failure paths
- Giden kapısı: IBAN/URL/e-posta/telefon eklenmesi, sayı kaybı/uydurma,
  3× uzama, Türkçe kalıntı → ret (saf testler, 20+ kontrol); ret nedeni
  balonda gösterilir (`GidenRet`).
- Boş çeviri başarı sayılmaz; tekrar tavanı (2) sonrası ücretli çağrı yok
  (saf benzetim: 20 sabit karede (b) metni 0, (a) metni 2 çağrı).
- Sınama modunda `Ayarlar.Kaydet` diske yazmaz; bozuk `config.json`
  `Dogrula()` ile varsayılana çekilir (Windows testleri).
- Motor "ai" iken makine motoruna düşülmez; anahtar yoksa uyarı + ücretsiz.
- yayinla.sh: sahte anahtar → DURDURULDU (3 senaryo).

## Capability and integration smoke
- xAI Grok / Bing / Google uçlarına GERÇEK çağrı yapılmadı (ağsız testler;
  sahip anahtarı kullanılmadı). İstem/zarf/JSON şeması saf çözümleyiciyle
  doğrulandı (kesik yanıt kurtarma, 1-tabanlı indeks, karışık sıra).

## Cleanup / rollback verification
- Geçici test dizinleri (`ArizaGunlugu.Dizin`, `SohbetGecmisi` geçici
  dizin) koşu sonunda silinir; kullanıcı `%APPDATA%` dizinine test yazmadı
  (Faz A denetçisi `~/.config/EkranCeviri` oluşmadığını doğruladı).
- Geri alma: `git checkout v1.1.0` / Releases'taki v1.1.0 `.exe`.

## Applicable gates
| Gate | Status | Evidence |
|---|---|---|
| Core behavior | PASS | CI 372 + 288 test; 4 faz denetimi; bütünleştirme denetimi |
| Lifecycle | PASS | hafıza/geçmiş sil, ayar doğrulama, önbellek sıfırlama kodda + testte |
| Security/privacy | PASS | giden kapısı, zarf, anahtar JsonIgnore, arıza günlüğü metinsiz, yayın kapısı negatif kontrol |
| UX/accessibility | PARTIAL | bar/menü/öneri paneli kodda ve denetimde; gerçek ekranda görülmedi |
| Platform-specific | PARTIAL | Windows CI derleme+test yeşil; GUI/DPI/odak davranışı el-testi bekliyor |
| Release/operations | PASS | `v1.2.0` Releases'ta, tag CI 33970978197 tüm adımlar success; yayinla.sh sır/dal kapıları negatif kontrollü |

## Remaining risk / single external action
- Tek dış eylem: sahip Windows'ta v1.2.0'ı açıp STATE.md el-testi listesini
  (bölge seçimi, bar konumu/odak, ⌨️ WhatsApp'ta, 💬, Kısayolu Değiştir…
  tepsi menüsünden) geçer; arıza `%APPDATA%\EkranCeviri\ariza.log`.
- Grok uçlarına gerçek çağrı yapılmadığı için istem değişikliklerinin
  (dilAdi, rol yapılı girdi) model üzerindeki etkisi Windows'ta ölçülmedi
  (macOS'ta aynı istemler ölçülmüştü).
