# Durum — Ekran Çeviri (Windows)

**Ne:** Ekranda seçilen sohbet alanını okuyup çeviriyi orijinal balonların
üstüne yazan Windows tepsi uygulaması. C# / .NET 8 / WPF.
Almanca lehçeleri (Almanya, Avusturya, İsviçre) → Türkçe (hedef dil seçilebilir).

**Depo:** github.com/RelaxS1/EkranCeviri — yalnız Windows sürümü.
macOS sürümü `6cdc4f0` öncesi commit'lerde ve `/Users/sami/Desktop/Claude/EkranCeviriMac`'te.

## AKTİF GÖREV — macOS iyileştirmelerinin Windows'a taşınması (2026-09-04)

**Tamamlanma sözleşmesi (RELEASE / HIGH):**
- Kullanıcı sonucu: Windows kullanıcısı v1.2.0'ı indirdiğinde Mac'teki
  davranışı alır: yarım kalan çeviri yok (canlıda boş çeviri kalıcılaşmaz),
  hızlı-önce/kalite-sonra, hareket kararı (küçük balon kaçmaz), ücretli
  tekrar tavanı, giden mesaj kapısı (ham+biçimli, ret nedeni görünür),
  bar düğmeleri (✕ 📋 👁 ✨ ⌨️ 💬), cevap önerisi, sohbet hafızası,
  menü yapısı, arıza/teşhis günlüğü.
- Başarı ölçütü: `dotnet build` iki csproj 0 hata (macOS çapraz);
  `Testler/Saf` macOS'ta EXIT=0; CI (gerçek Windows) test adımı yeşil ve
  `.exe` üretildi; `v1.2.0` Releases'ta.
- Negatif yol: giden kapısı IBAN/URL/sayı kaybı/Türkçe kalıntıda reddeder;
  QA/sınama modu ayar dosyasına yazmaz; motor "ai" iken makine motoruna
  düşülmez; anahtarsız ai modunda uyarı + ücretsiz motor.
- Kanıt: `.ai/EVIDENCE.md` (komut + exit + CI çalışma numarası).
- Geri alma: dal `windows-evrim` → `main`e yalnız CI yeşilse birleşir;
  önceki sürüm etiketi `v1.1.0` ve `.exe`'si Releases'ta kalır.

**Faz durumu:** A (saf katman) TAMAM, commit `8a5c6b5`. B (motorlar),
C (akış), D (arayüz) yazılıyor. E (belgeler/yayın kapısı) bu tur: README,
SECURITY, CONTRIBUTING, yayinla.sh, windows.yml, bu dosya.
Sözleşme: `.ai/PORT-SOZLESMESI.md` (faz/dosya sahipliği, imzalar, Mac satırları).

**Taşınan (Mac → Windows):** kalite kapıları (`Kalite`: sayı korunumu,
kalıntı, yankı, (b) sınıfı, `ZarfaGuvenli`), giden kapısı ham+biçimli
(`GidenKapisi`), `TekrarDefteri`/`BosNobetDefteri`, `KaliteTuru` (8 sn yaş
kapısı, 20 sn zaman aşımı, derinlik 40), `Hareket.Karar` (hücre sayısı),
`ArizaGunlugu`/`Teshis`, `Ayarlar.Dogrula` + sınama kilidi, `Diller`,
`Geometri.BarKonumu`, `DurumMetni`; B/C/D: yanıt çözücü, oturum önbelleği,
sohbet geçmişi (JSONL), iki şeritli canlı döngü, bar düğmeleri, öneri paneli, menü.
**Taşınmayan (Windows'ta karşılığı yok / gereksiz):** SCStream akışı (BitBlt
tek kare kalır), hardened runtime/TCC izinleri, Keychain (DPAPI var),
`--gizli-sinama` QA sahne düzeneği ve tohum sözlüğü, Erişilebilirlik izinsiz
yedek yol (`SendInput` izin istemez), `bulutOnay` ilk-kullanım onayı
(anahtar penceresi zaten kullanıcı eylemi), GIZLILIK.md (README "Gizlilik"
bölümü + SECURITY.md karşılar).

## Mimari

| Katman | Dosya |
|---|---|
| Giriş, tek örnek kilidi, istisna kancaları, `--tani` | `Program.cs` |
| Beyin: bölge, canlı döngü (iki şerit), kısayol, bekçiler | `Yonetici.cs`, `Yonetici.Arayuz.cs` (D) |
| Ayarlar + doğrulama + DPAPI anahtar kasası | `Cekirdek/Ayarlar.cs` |
| Model tipleri, motor arayüzü | `Cekirdek/Modeller.cs` |
| Arıza günlüğü, teşhis, diller, durum metni, geometri | `Cekirdek/ArizaGunlugu.cs`, `Teshis.cs`, `Diller.cs`, `DurumMetni.cs`, `Geometri.cs` |
| Yakalama, OCR, balon gruplama, hareket kararı | `Ekran/` (`Hareket.cs` saf) |
| Lehçe, kalite kapıları, giden kapısı, defterler, hafıza, motorlar | `Ceviri/` (`Kalite`, `GidenKapisi`, `Defterler`, `KaliteTuru`; B: `YanitCozucu`, `OturumOnbellegi`, `SohbetGecmisi`) |
| Katman, bölge seçici, tepsi, bar, öneri paneli, ayar/anahtar pencereleri | `Arayuz/` |
| Global kısayol, SendInput, pano, kısayol metni | `Kisayol/` |
| Windows testleri (CI'da koşar) | `Testler/Program.cs`, `Testler.csproj` |
| Saf test koşucusu (macOS'ta da koşar) | `Testler/Saf/Saf.csproj`, `SafTestlerA..D.cs`, `SafYardimci.cs` |

## Doğrulama komutları (her turda YENİDEN üret, kopyalama)

| Ne | Komut |
|---|---|
| Çapraz derleme | `~/.dotnet/dotnet build EkranCeviri.csproj -c Debug --nologo 2>&1 \| grep -E "Hata\|Uyarı" \| sort -u` → `0 Hata` |
| Test projesi derleme | aynı komut `Testler/Testler.csproj` ile |
| Saf testler | `~/.dotnet/dotnet run --project Testler/Saf/Saf.csproj -c Debug --nologo` → `TÜM SAF TESTLER GEÇTİ ✅ (N test)`, `EXIT=0` |
| Yayın kapısı | `bash -n yayinla.sh`; negatif: geçici depoda sahte `xai-`+40 karakter → `✗ DURDURULDU`, EXIT=1 |
| CI | `gh run watch` — gerçek Windows: Derle → Testleri koş → Saf testleri koş → publish |

**Ölçümü KOPYALAMA kuralı:** bu dosyaya ve belgelere giren her sayı (test
sayısı, çağrı sayısı, süre) o turda yukarıdaki komutlarla ya da koddaki
sabitten üretilir. Ölçülmemiş şey "ölçülmedi" diye yazılır. Mac'teki
ölçümler (78 çağrı/17 metin, 30061 ms) Mac'e aittir; Windows'ta aynı
davranış kodla taşındı ama **Windows'ta ölçülmedi**.

## Doğrulama durumu (2026-09-04, Faz E turu)

- Saf koşucu macOS'ta: `TÜM SAF TESTLER GEÇTİ ✅ (159 test)`, EXIT=0 (Faz A).
- Yayın kapısı negatif kontrolü: geçici depoda çalışma ağacı → DURDU (maskeli
  `xai-4IqiDc…(GIZLENDI)`), yalnız geçmişte → DURDU (commit:dosya), main dışı
  dal → DURDU. Gerçek depoda `git grep` deseni boş.
- CI Faz A+E ile HENÜZ koşmadı (dal `windows-evrim`, main'e push yok);
  önceki kayıt: v1.0.0 için 54/54 test, çalışma 31247640890.
- **Gerçek makinede kullanıcı denemesi HENÜZ YAPILMADI** — liste: `.ai/EL-TESTI.md`.
## Açık kapılar / sonraki adımlar

- B/C/D bittiğinde: iki csproj 0 hata, saf koşucu, `windows-evrim` → main,
  CI yeşil, `./yayinla.sh 1.2.0` (csproj sürümünü betik yazar).
- Kod imzası yok → SmartScreen uyarısı. Yalnız `win-x64`.
- `Ekran/Bloklayici.cs` eşikleri WhatsApp Desktop'a göre; Telegram/Discord'da
  yeniden bakılabilir. BitBlt bazı GPU pencerelerinde eksik yakalayabilir.

## Değişmezler (bozulursa kullanıcı zarar görür)

1. Anahtar ayar dosyasına/pakete/depoya **asla** yazılmaz; yayın kapısı geçmişi tarar.
2. Giden mesaj kapısı HAM + BİÇİMLİ çıktıya; geçilmezse yapıştırma yok, neden gösterilir.
3. Ekrandan okunan metin sistem istemine girmez; zarf sınırları `ZarfaGuvenli`.
4. Kısayol bilinmeyen uygulamada ve uzun seçimde çalışmaz (veri kaybı).
5. Katman `WDA_EXCLUDEFROMCAPTURE` ile yakalama dışında (kendi çevirimizi tekrar çevirmemek).
6. Testler üretim fonksiyonlarını çağırır; her saf fonksiyon `Saf.csproj`'da; CI kapısı atlanamaz.
7. GDI DC/bitmap ve LockBits daima serbest bırakılır.
8. Motor "ai" iken makine motoruna düşülmez; motor "ücretsiz" iken xAI'ye hiçbir metin gitmez.
9. Arıza/teşhis günlüğü metin taşımaz (yalnız `--tani`); sınama modu ayar yazmaz.
10. Kalite sonucu 8 sn'den yaşlı balonu oynatmaz; boş çeviri başarı sayılmaz.
