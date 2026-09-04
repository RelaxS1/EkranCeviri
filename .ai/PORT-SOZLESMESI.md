# EkranCeviri — macOS → Windows PORT SÖZLEŞMESİ (2026-09-04)

Bu belge port ajanlarının TEK sözleşmesidir. Her ajan yalnız kendi dosyalarına
dokunur, aşağıdaki imzaları BİREBİR uygular, derlemeyi sıfır hatayla bitirir.

## Depolar ve komutlar

- Windows deposu (hedef): `/Users/sami/Desktop/Claude/EkranCeviri` — dal `windows-evrim`.
  C#/.NET 8/WPF, macOS'tan çapraz DERLENİR ama ÇALIŞTIRILAMAZ. Gerçek doğrulama
  GitHub Actions'ta (Windows). Kaynak yapısı: `Cekirdek/`, `Ceviri/`, `Ekran/`,
  `Arayuz/`, `Kisayol/`, `Yonetici.cs`, `Program.cs`, `Testler/`.
- macOS kaynağı (KAYNAK — davranış birebir buradan taşınır, Swift):
  `/Users/sami/Desktop/Claude/EkranCeviriMac/` → `Cekirdek.swift`, `Lehce.swift`,
  `Motorlar.swift`, `Giden.swift`, `Akis.swift`, `Ekran.swift`, `Katman.swift`,
  `Uygulama.swift`, `Testler/main.swift` (1094 satır test — TAŞINACAK testler burada).
- Derleme (ZORUNLU, her ajan bitirmeden önce, ÇIKTIDA "0 Error" olmalı):
  `~/.dotnet/dotnet build /Users/sami/Desktop/Claude/EkranCeviri/EkranCeviri.csproj -c Debug --nologo 2>&1 | tail -5`
  `~/.dotnet/dotnet build /Users/sami/Desktop/Claude/EkranCeviri/Testler/Testler.csproj -c Debug --nologo 2>&1 | tail -5`
  Nullability uyarıları HATADIR (csproj: `WarningsAsErrors CS8600;CS8602;CS8603;CS8618`).
- Saf testler macOS'ta GERÇEKTEN koşar (A fazı bu koşucuyu kurar):
  `~/.dotnet/dotnet run --project /Users/sami/Desktop/Claude/EkranCeviri/Testler/Saf/Saf.csproj -c Debug --nologo`
  Çıktı `TÜM SAF TESTLER GEÇTİ ✅` + EXIT=0 olmalı.
- Git: HİÇBİR ajan commit atmaz. API anahtarı, kullanıcı verisi, `xai-` deseni ASLA yazılmaz.

## Kod kuralları (CONTRIBUTING.md ile aynı)

- Tanımlayıcılar, yorumlar, kullanıcıya görünen metinler TÜRKÇE.
- Yorum NEDENİ açıklar (bu koddaki her eşik bir hatanın izidir). Mac yorumlarındaki
  gerekçeyi TAŞI, kısalt ama silme.
- Testler ÜRETİM fonksiyonlarını çağırır; kopya mantık test edilmez.
- `Console` kullanma (WinExe). Günlük: `Gunluk.Yaz/Hata`.
- Eşzamanlılık: `Blok.Ceviri` kilitli; paylaşılan sözlükler `lock` ya da Concurrent*.
- Arayüz iş parçacığına dokunan her şey `Dispatcher` üzerinden.

## Faz planı ve DOSYA SAHİPLİĞİ (çakışma yasak)

| Faz | Sahip olduğu dosyalar |
|---|---|
| A (temel, TEK ajan, önce) | `Cekirdek/Ayarlar.cs`, `Cekirdek/Modeller.cs`, `Cekirdek/ArizaGunlugu.cs`(yeni), `Cekirdek/Teshis.cs`(yeni), `Cekirdek/Diller.cs`(yeni), `Cekirdek/DurumMetni.cs`(yeni), `Cekirdek/Geometri.cs`(yeni), `Ceviri/Kalite.cs`, `Ceviri/Lehce.cs`, `Ceviri/Defterler.cs`(yeni), `Ceviri/GidenKapisi.cs`(yeni), `Ceviri/KaliteTuru.cs`(yeni), `Ekran/Hareket.cs`(yeni), `Ekran/Bloklayici.cs` (yalnız `Hedef` kuralı), `Yonetici.cs` (YALNIZ aşağıdaki 5 yardımcı üyeyi ekler), `Testler/Program.cs`, `Testler/Testler.csproj`, `Testler/SafTestlerA.cs`(yeni), `Testler/SafTestlerB.cs`/`C`/`D` (BOŞ iskelet), `Testler/Saf/Saf.csproj`+`Testler/Saf/Main.cs`(yeni) |
| B (motorlar, A'dan sonra) | `Ceviri/GrokMotor.cs`, `Ceviri/YanitCozucu.cs`(yeni), `Ceviri/SohbetGecmisi.cs`(yeni), `Ceviri/Cevirmen.cs`, `Ceviri/OturumOnbellegi.cs`(yeni), `Ceviri/Hafiza.cs`, `Ceviri/MakineMotorlari.cs`, `Cekirdek/Ag.cs`, `Testler/SafTestlerB.cs`, `Testler/Saf/Saf.csproj` (yalnız yeni saf dosyaları Compile listesine ekler) |
| C (akış, B'den sonra, D ile PARALEL) | `Yonetici.cs`, `Ekran/PencereAraclari.cs`, `Testler/SafTestlerC.cs` |
| D (arayüz, B'den sonra, C ile PARALEL) | `Arayuz/KontrolCubugu.cs`, `Arayuz/OneriPenceresi.cs`(yeni), `Arayuz/TepsiSimgesi.cs`, `Arayuz/AyarPenceresi.cs`, `Kisayol/KisayolMetni.cs`(yeni), `Yonetici.Arayuz.cs`(yeni, `partial class Yonetici`), `Testler/SafTestlerD.cs` |
| E (belgeler, B'den sonra, C/D ile PARALEL) | `README.md`, `SECURITY.md`, `CONTRIBUTING.md`, `yayinla.sh`, `.github/workflows/windows.yml`, `.ai/STATE.md`(yeni) |

Faz 0'da (zaten yapıldı): `Yonetici` `partial` oldu; `KatmanGorunumu.OrijinalGoster`
ve `YamalariAta(yamalar, ofsetiKoru)`; `KatmanPenceresi.MetniTazele(bloklar, boyaci)`;
`Yakalama.GoruntuIzi(Bitmap) -> byte[]` ve `Yakalama.IzFarki(byte[], byte[])`.
Bunlara DOKUNMA, kullan.

---

## FAZ A — TEMEL (saf yardımcılar, ayarlar, defterler, hareket kararı, test koşucusu)

Kaynak: `Cekirdek.swift` (146-533), `Giden.swift` (108-138, 302-372, 541-896),
`Lehce.swift` (emojileriKoru zaten var), `Ekran.swift` (655-756), `Katman.swift`
(13-160 Teshis, 1101-1123 DurumEtiketi), `Akis.swift` (938-962 kalite sabitleri,
1128-1136 barPanelKonumu), `Testler/main.swift`.

### A1. `Cekirdek/Diller.cs` (yeni, saf)
```csharp
public static class Diller {
    public static readonly (string Kod, string Ad)[] Adlar = {("tr","Türkçe"),("en","İngilizce"),("de","Almanca"),("fr","Fransızca"),("es","İspanyolca"),("it","İtalyanca"),("ru","Rusça"),("ar","Arapça"),("pt","Portekizce")};
    public static string Ad(string kod);          // bilinmiyorsa kodun kendisi
    public static bool Gecerli(string kod);
}
```

### A2. `Cekirdek/Ayarlar.cs`
- Yeni alanlar: `public bool KaliteSonra { get; set; } = true;` (hızlı-önce/kalite-sonra),
  `public bool GidenHizOnceligi { get; set; }` (⌨️ hızlı model), `public string GidenKarakterMetni { get; set; } = "";`
  (giden mesaj için ayrı üslup metni; boşsa `Kisilik` kullanılır). MEVCUT `GidenKarakter` (bool,
  noktalama biçimi) AYNEN KALIR — JSON anahtarı değişmez.
- `GidenMotor` değer kümesi `"grok" | "bing"`; dosyadan `"hizli"` gelirse `"bing"`e çevir.
- `public void Dogrula()` — `Yukle()` sonunda çağrılır, diske DOKUNMAZ (Mac `dogrula()` 461-499):
  HedefDil ∈ Diller yoksa "tr"; Motor ∈ {ai,hizli} yoksa "ai"; GidenMotor ∈ {grok,bing} yoksa "grok";
  DilModu ∈ {alman,isvicre,otomatik} yoksa "alman"; Ben/KarsiCinsiyet ∈ {kadin,erkek,yok} ("" → "yok");
  KaynakDilKodu `^[a-z]{2}(-[A-Z]{2})?$` yoksa "de"; OcrDili boş/geçersizse "de";
  KisayolTus 1..255 yoksa 0x43; `KisayolMod &= 0x000F`, 0 ise 0x0003;
  GrokModel/GrokModelKalite yalnız `[A-Za-z0-9._-]`, 1..64 karakter, yoksa varsayılan;
  Kisilik ve GidenKarakterMetni ≤ 2000 karakter; KaynakDilAdi ≤ 120.
- `public static bool SinamaModuAktif { get; }` = komut satırında `--sinama` ya da `--gizli-sinama` var.
  `Kaydet()` bu hâlde diske YAZMAZ ve `Gunluk.Yaz("ayar: sınama modu — diske yazılmadı")` der
  (Mac'te QA koşusu kullanıcının gerçek ayarını bozuyordu; motor ai→hizli düşüyordu).
- `public string EtkinGidenMotor => Motor == "ai" ? GidenMotor : "bing";`
  (sahip kararı #7: ücretsiz motor seçen kullanıcı ⌨️ ile xAI'ye ÇIKMAZ.)

### A3. `Cekirdek/Modeller.cs`
- `Blok`: `public Blok Kopya()` (aynı Metin/Kutu/Benim/Hedef/Ceviri; kalite turu KOPYA bloklarla
  çalışır — Mac'te paylaşılan Blok'a iki kuyruktan yazmak çöktürüyordu).
- Yeni: `public sealed record KonusmaSatiri(bool Benim, string Metin, string? Ceviri);`
- `CeviriBaglam`: `public IReadOnlyList<KonusmaSatiri> OncekiKonusmaYapili { get; init; } = [];`
  (mevcut `OncekiKonusma` dize listesi KALIR — geriye uyum; B yapılı olanı tercih eder).
- `CeviriSonuc` değişmez.

### A4. `Ekran/Bloklayici.cs` — yalnız `Hedef` kuralı
`blok.Hedef = !atla && blok.Anahtar.Length >= 2;` — KENDİ mesajlarımız da çevrilir
(Mac davranışı; kullanıcı kendi yazdığı Almancayı da Türkçe görmek istiyor; Türkçe yazdığı
mesajlar `Kalite.CevrilecekSeyYokMu` ile motora GİTMEZ). `Testler/Program.cs`teki
"sağ balon benim" testini `benimki is { Benim: true, Hedef: true }` olarak güncelle; yorumla açıkla.

### A5. `Ceviri/Kalite.cs` — eklenen/değişen SAF fonksiyonlar (Mac imzaları)
```csharp
public static Dictionary<char,int> RakamCoklugu(string s);           // Giden.swift 809
public static List<string> RakamObekleri(string s);                  // 816: "saat 17:30 · 150.-" → ["17","30","150"]
public static string? SayilarKorunduMu(string kaynak, string cikti); // 848-867: null=OK, değilse Türkçe neden ("kayıp rakam: 5 · uydurulan rakam: 7" | "sayı değişti: 12 çıktıda böyle geçmiyor")
public static bool RakamlarKorundu(string kaynak, string cikti) => SayilarKorunduMu(kaynak, cikti) is null; // mevcut çağıranlar/testler için
public static bool YankiMi(string ceviri, string kaynak, string makineMetni); // Motorlar.swift 735-740
public static bool HicCevrilmemis(string kaynak, string ceviri);     // Mac 552-557: k.Length<6 → false; k==c → true; MesafeAzMi(k,c,max(1,k.Length/10))
public static bool AlmancaKalintiVar(string ceviri);                 // Mac 707-714: küme = Mac `almancaIsaretler` ∪ mevcut Windows kümesi; kural: kelime≥2 ve (kalıntı≥2 || (kalıntı≥1 && kalıntı*3 >= kelimeSayısı))
public static bool TurkceKalintiVar(string s);                       // Mac 718-731 kümesi ∪ mevcut Windows kümesi; "ne" ve "saat" kelimelerini ÇIKAR (Almanca "ne?" ve "Saat" ile çakışıyor); ı/İ/ş/ğ harfi tek başına yeter
public static bool CevrilecekSeyYokMu(string kaynak, string hedefDil = "tr"); // Giden.swift 563-602 (b sınıfı, KASITLI DAR)
public const int KaliteNuansEsigi = 25;
public static bool KaliteAdayiMi(string anahtar, string kaynak, string? ceviri, string hedefDil = "tr"); // 689-705; anahtar boş değilse TekrarDefteri.Paylasilan.VazgecildiMi(anahtar) → false
public static string ZarfaGuvenli(string metin);                     // 108-138: yalnız </?\s*(tarz_ornekleri|cevrilecek|gecmis|sohbet)\b[^>]*> eşleşmelerinde < → ‹ , > → ›
```
Mevcut `Anahtarla, KarisikMi, EmojileriKoru, GidenFormatla, MesafeAzMi, Rakamlari` AYNEN kalır.
`MesafeAzMi`: a veya b boşken çökmesin (Mac 242-245 koruması).

### A6. `Ceviri/GidenKapisi.cs` (yeni, saf) — Giden.swift 772-896
```csharp
public sealed class GidenRet : Exception { public string Neden { get; } public GidenRet(string neden) : base(neden) {...} }
public static class GidenKapisi {
    public static HashSet<string> YonlendiriciIzler(string s);  // 783-800 desenleri: URL/alan adı; IBAN; e-posta (ignorecase); telefon \+?\d[\d\s().-]{6,}\d — eşleşme küçük harf + boşluksuz
    public static GidenRet? GuvenliMi(string kaynak, string cikti, bool uzunlukDenetle = true); // 871-888 sırası: sayılar → yeni iz → uzunluk (>max(80, kaynak*3)) → Türkçe kalıntı
    public static GidenRet? Kapi(string kaynak, string ham, string bicimli); // 832-837: önce ham (uzunlukDenetle:false), sonra biçimli
    public static string MotorSecimi(string motor, string gidenMotor) => motor == "ai" ? gidenMotor : "bing";
}
```

### A7. `Ceviri/Defterler.cs` (yeni, saf) — Giden.swift 302-372 ve 604-687
```csharp
public sealed class TekrarDefteri {   // ücretli tekrar tavanı + kalıcı (b) işareti
    public static TekrarDefteri Paylasilan { get; } = new();
    public const int Tavan = 2;  // sınır 4000, en eski çeyrek atılır
    public void DegismezIsaretle(string anahtar); public bool DegismezMi(string anahtar);
    public bool VazgecildiMi(string anahtar); public int Denendi(string anahtar);
    public void Basarili(string anahtar); public void TekrarAc(string anahtar); public void Sifirla(); public int Izlenen { get; }
}
public sealed class BosNobetDefteri {  // çevrilemeyen mesajın süreli defteri
    public static readonly TimeSpan TurTtl = TimeSpan.FromSeconds(90); public const int EnFazlaDeneme = 4; public static readonly TimeSpan HakYenileme = TimeSpan.FromSeconds(900); // sınır 2000
    public bool TurAcikMi(int pesPeseHata, DateTime? simdi = null); public void TurBasladi(DateTime? simdi = null);
    public bool HakVarMi(string anahtar, DateTime? simdi = null); public void Denendi(string anahtar, DateTime? simdi = null);
    public void Basarili(string anahtar); public void Sifirla(); public int Izlenen { get; }
}
```
Tümü `lock` ile korunur (iki iş parçacığından erişilir).

### A8. `Ceviri/KaliteTuru.cs` (yeni, saf) — Akis.swift 938-962
```csharp
public static class KaliteTuru {
    public const int DerinlikTavani = 40;
    public static readonly TimeSpan YasKapisi = TimeSpan.FromSeconds(8);   // geçince ekran DEĞİŞMEZ, yalnız hafızaya
    public static readonly TimeSpan ZamanAsimi = TimeSpan.FromSeconds(20); // kalite isteği ayrı zaman aşımı
    public static bool EkranaYazilsinMi(DateTime dogum, DateTime simdi) => simdi - dogum <= YasKapisi;
}
```

### A9. `Ekran/Hareket.cs` (yeni, saf) — Ekran.swift 655-756 BİREBİR
```csharp
public sealed class HareketDurumu { public byte[]? SonOcrIzi; public byte[] Sayaclar = []; public double SonOcrZamani; public double OcrYasakBitis; }
public readonly record struct HareketKarari(double Fark, bool Sabit, bool IcerikDegisti, double AnimasyonOrani, bool OcrYap);
public static class Hareket {
    public static HareketKarari Karar(byte[] iz, byte[]? onceki, HareketDurumu durum, double simdi, int hucreEsik = 12, int hucreSayisiEsik = 3, double hareketEsik = 0.012, double zorlaAralik = 8);
    public static double IzFarki(byte[] a, byte[] b);
}
```
`simdi` saniye cinsinden (Stopwatch tabanlı). Swift'teki `durum` inout → sınıf yerinde güncellenir.

### A10. `Cekirdek/ArizaGunlugu.cs` (yeni, saf — WPF/Windows API YOK) — Cekirdek.swift 206-294
```csharp
public static class ArizaGunlugu {
    public static string Dizin { get; set; }  // varsayılan %APPDATA%\EkranCeviri (Environment.SpecialFolder.ApplicationData) — Ayarlar'a BAĞIMLI OLMA (saf koşucu için)
    public const string DosyaAdi = "ariza.log"; public const long TavanBayt = 2_000_000; public static string DosyaYolu { get; }
    public static bool TaniModu { get; set; }  // Program.cs `--tani` ile açar; yalnız o zaman metin yazılır
    public static string MetinKarmasi(string metin);     // sha256 ilk 8 hex, küçük harf
    public static string Satir(DateTime zaman, string asama, string motor, string sebep, int sureMs, string metin, bool taniModu); // "yyyy-MM-dd HH:mm:ss | asama | motor | sebep | 1234ms | uzunluk=N | karma=xxxxxxxx" (+ " | metin=…" yalnız taniModu)
    public static void Yaz(string asama, string motor, string sebep, int sureMs, string metin); // boş asama/sebep → yazma; Teshis.Paylasilan.Say("ariza-"+sebep); 2 MB'ı geçince .1'e döndür; lock altında append
    public static string Sebep(Exception e);  // GidenRet→"kalite-kapisi-reddetti"; OperationCanceledException→"iptal" (TimeoutException/İptalNedeni zaman aşımı ise "zaman-asimi"); HttpRequestException→"ag-hatasi"; JsonException/FormatException→"bos-yanit"; diğer→"bilinmeyen"
    public static void Bekle() {}  // senkron; imza uyumu için
}
```
Zaman aşımı ayrımı için `public sealed class ZamanAsimiHatasi : OperationCanceledException` tanımla (B kullanır).

### A11. `Cekirdek/Teshis.cs` (yeni; WPF'e bağımlı DEĞİL — ana kuyruk bir delegeyle verilir) — Katman.swift 13-160
```csharp
public sealed class Teshis {
    public static Teshis Paylasilan { get; } = new();
    public static readonly TimeSpan AsamaTazelik = TimeSpan.FromSeconds(30);
    public string Asama { get; set; }   // 30 sn'den eskiyse "boşta" döner
    public T Olc<T>(string ad, Func<T> is_);  public async Task<T> OlcAsync<T>(string ad, Func<Task<T>> is_);
    public void Say(string ad); public int SayacDegeri(string ad);
    public void Baslat(Action<Action> anaKuyrugaGonder); // 250 ms'de bir ping; yanıt >200 ms ise takılma (asama etiketiyle); 60 sn'de bir tek satır dosya
    public void Durdur();
    public static string Dizin { get; set; } // varsayılan %APPDATA%\EkranCeviri; dosya "teshis.log", 2 MB'da .1
}
```
60 sn satırı: `sayac=N(ort Xms, en Yms)` parçaları + `takilma=N(en Zms @asama)`; boş kovada "boşta" (kalp atışı).

### A12. `Cekirdek/DurumMetni.cs` (yeni, saf) — Katman.swift 1104-1111
`public static class DurumMetni { public static readonly string[] HataIzleri = {"hata","yok","alınamadı","vermedi","yakalanamıyor","reddedildi","başarısız","gerekli","⚠"}; public static bool HataMi(string metin); }`

### A13. `Cekirdek/Geometri.cs` (yeni, saf) — Akis.swift 1128-1136 (y ekseni Windows'ta AŞAĞI büyür)
```csharp
public readonly record struct Kutu(double X, double Y, double Genislik, double Yukseklik) { double Sag; double Alt; double OrtaX; }
public static class Geometri {
    /// Bar sol-üst köşesi: x ortalanır ve görünür alana kıstırılır; y = bölgenin ÜSTÜ (bolge.Y - yukseklik - 8), sığmazsa ALTI (bolge.Alt + 8); iki eksende de 8 pay ile kıstırılır.
    public static (double X, double Y) BarKonumu(Kutu bolge, Kutu gorunur, double genislik, double yukseklik);
}
```

### A14. `Ceviri/Lehce.cs` — yalnız `GidenOrnek`
Gövdeleri Mac `gidenOrnekler` (Giden.swift 737-770) ile DEĞİŞTİR (ölçülmüş örnekler; Windows'taki
"ich has dr gseh gfehlt" uydurmaydı). Ostschwyz/Rheinisch için Mac'te örnek yok → Windows'takini bırak.

### A15. `Yonetici.cs` — YALNIZ şu üyeleri ekle (C ve D bunlara güvenir; C sonra geliştirir)
```csharp
internal CeviriBaglam BaglamKur(IReadOnlyList<Blok> bloklar);   // private → internal (OncekiKonusmaYapili'yi de doldur)
internal List<Blok> MevcutBloklarKopya();                        // lock altında kopya
internal void MotorEtiketiAyarla(string metin);                  // Dispatcher ile _cubuk.MotorAdi
internal void GeriBildir(string metin);                          // bar varsa etiket, yoksa _tepsi.Bilgi("Ekran Çeviri", metin) — Mac geriBildir
internal void EkrandakiCevirileriTazele();                       // hedef blokların Ceviri'sini null yap, _cevirmen.BloklariCevirAsync ile arka planda yeniden çevir, bitince Dispatcher'da _katman.MetniTazele + etiket. Motor/dil değişince çağrılır.
internal KatmanPenceresi? Katman => _katman; internal KontrolCubugu? Cubuk => _cubuk;
```
Başka hiçbir şeyi değiştirme (canlı döngü C'nin işi).

### A16. Testler
- `Testler/Testler.csproj`: `<Compile Remove="Saf/**" />` ekle.
- `Testler/Saf/Saf.csproj` (yeni): `net8.0`, `OutputType Exe`, `Nullable enable`, `ImplicitUsings enable`,
  `<Compile Include="../../Ceviri/Kalite.cs;../../Ceviri/Lehce.cs;../../Ceviri/Defterler.cs;../../Ceviri/GidenKapisi.cs;../../Ceviri/KaliteTuru.cs;../../Ekran/Hareket.cs;../../Cekirdek/ArizaGunlugu.cs;../../Cekirdek/Teshis.cs;../../Cekirdek/Diller.cs;../../Cekirdek/DurumMetni.cs;../../Cekirdek/Geometri.cs;../SafTestlerA.cs;../SafTestlerB.cs;../SafTestlerC.cs;../SafTestlerD.cs;../SafYardimci.cs" />`
  ve `Testler/Saf/Main.cs`: `SafYardimci.Sifirla(); SafTestlerA.Kos(); SafTestlerB.Kos(); SafTestlerC.Kos(); SafTestlerD.Kos(); return SafYardimci.Bitir("SAF");` — bu koşucu macOS'ta GERÇEKTEN koşar (`dotnet run`), CI'da da koşacak.
- `Testler/SafYardimci.cs` (yeni): `public static class SafYardimci { static int _gecen,_kalan; public static void Baslik(string); public static void Dogru(bool, string); public static void Esit<T>(T beklenen, T gelen, string ad); public static void Sifirla(); public static int Bitir(string etiket); }` — çıktı biçimi mevcut Program.cs'teki gibi (`  ✓ ad` / `  ✗ ad`), sonda `TÜM SAF TESTLER GEÇTİ ✅ (N test)` ya da `BAŞARISIZ ❌`.
- `Testler/SafTestlerA.cs`: `public static class SafTestlerA { public static void Kos() {...} }` — Mac `Testler/main.swift`ten TAŞI: 570-596 (giden kapısı sayı kümesi), 175-198 (giden kapı IBAN/URL/uzunluk/telefon), 164-169 (kalıntı), 200-245 (yankı + (b) sınıfı), 247-319 (TekrarDefteri + ücretli çağrı sayımı benzetimi), 483-567 (hareket kararı — 11 senaryo, `hkare/yeniMesaj/animasyon/imlec/kaydir` yardımcılarıyla), 744-777 (kalite yaş kapısı), 855-898 (ArizaGunlugu.Satir/MetinKarmasi/Sebep; dosya kısmı için `ArizaGunlugu.Dizin` geçici dizine alınır), DurumMetni.HataMi (5 örnek), Geometri.BarKonumu (üst sığar / üst sığmaz alta düşer / x sol-sağ kıstırma / tam yükseklik → yine görünür alanda), BosNobetDefteri (TTL, 4 deneme, 900 sn yenileme — yeni yaz), ZarfaGuvenli (`</cevrilecek>` dönüşür, "3 < 5" ve "->" bozulmaz), Diller.Ad.
- `Testler/SafTestlerB.cs`, `C`, `D`: `public static class SafTestlerX { public static void Kos() { } }` iskelet.
- `Testler/Program.cs`: mevcut testler kalır (Bloklayici "Hedef" beklentisi güncellenir), `Main` başında `SafTestlerA.Kos(); SafTestlerB.Kos(); SafTestlerC.Kos(); SafTestlerD.Kos();` çağrılır ve sayaçlar `SafYardimci` üzerinden birleştirilir; Ayarlar.Dogrula testleri (Mac 598-656) buraya (Windows'a özel, ProtectedData yüzünden saf koşucuda derlenmez).
- KOŞ ve KANIT: saf koşucu macOS'ta EXIT=0; iki csproj 0 hata.

---

## FAZ B — MOTORLAR (Grok, ücretsiz motorlar, düzenleyici, oturum önbelleği, sohbet geçmişi)

Kaynak: `Motorlar.swift` (tamamı), `Giden.swift` (1-107 geçmiş, 140-196 grokOneri, 898-1051 girdiCevir), `Akis.swift` 301-315 (uretilenleriKaydet).

### B1. `Ceviri/YanitCozucu.cs` (yeni, SAF — HttpClient yok)
```csharp
public static class YanitCozucu {
    public static string CitleriAt(string s);
    public static string?[] CevirileriYerlestir(IReadOnlyList<(int? Indeks, string? Ceviri)> kayitlar, int adet); // Motorlar.swift 400-426 (0/1 tabanlı permütasyon, aksi hâlde dizi sırası)
    public static string?[]? KesikJsondanKurtar(string icerik, int adet);   // 371-398: yarım JSON'dan tam {…"ceviri":…} nesnelerini ayıkla
    public static string?[]? Coz(string icerik, int adet);                  // GrokMotor.Coz'un taşınmış hâli + düz dize dizisi yedeği + kesik yanıt kurtarma
}
```
GrokMotor bunları çağırır. Testler B'de.

### B2. `Ceviri/SohbetGecmisi.cs` (yeni) — Giden.swift 1-106
JSONL dosyası `%APPDATA%\EkranCeviri\sohbet_gecmisi.jsonl`; `public sealed record GecmisKaydi(long T, string Kim, string Metin, string Ceviri);`
`public static class SohbetGecmisi { static string Yol; static void Yaz(string kim, string metin, string? ceviri); static List<GecmisKaydi> Oku(int son = 500); static void Kirp(); /* >5 MB → en eski yarı atılır */ static void Sil(); static List<string> UslupOrnekleri(IReadOnlyList<GecmisKaydi> k, int adet = 12); static List<string> BenzerGecmis(IReadOnlyList<GecmisKaydi> k, string sorgu, int adet = 3); }`
`UslupOrnekleri`/`BenzerGecmis` SAF (liste üzerinde) — Saf.csproj'a ekle (dosya I/O olan üyeler `System.IO` ile derlenir, saf koşucuda çağrılmaz).

### B3. `Ceviri/OturumOnbellegi.cs` (yeni, saf) — Motorlar.swift `KilitliSozluk` (648-726)
`public sealed class OturumOnbellegi { public (Dictionary<string,string> Kopya, int Nesil) Kopyala(); public bool Birlestir(IReadOnlyDictionary<string,string> yeni, int nesil); public string? this[string k] { get; set; } public void Sil(string k); public void Temizle(); public int Nesil { get; } public int Sayi { get; } }` — tavan 3000, aşınca EN ESKİ (ekleme sırası) 1000 atılır; `Temizle` nesli ilerletir; eski nesille `Birlestir` false döner ve yazmaz.

### B4. `Ceviri/Hafiza.cs`
- `public void Ata(string anahtar, string ceviri, string kaynakMetin = "")` — Türkçe denetimi `kaynakMetin` boş değilse ONUN üzerinde (Mac 466-467).
- `public void Guncelle(string anahtar, string ceviri, string kaynakMetin = "") => Ata(...)` (✨ eski kaydı da düzeltir).
- `public void UretilenIsaretle(string ceviriAnahtari)` (zehir kalkanı kümesine ekle; `UretilenMi` mevcut).
- Sınır 3000 → kalsın. `Kaydet` aynen.

### B5. `Ceviri/MakineMotorlari.cs`
`CevirAsync(metinler, baglam, iptal, int deneme = 3)` — canlı turda 1 deneme. Motorlara STANDARTLAŞTIRILMIŞ metin gider (mevcut davranış korunur).

### B6. `Ceviri/GrokMotor.cs`
- `public Task<CeviriSonuc> CevirAsync(IReadOnlyList<string> metinler, CeviriBaglam baglam, CancellationToken iptal, IReadOnlyList<bool>? roller = null, bool canli = false, bool hizli = false, TimeSpan? zamanAsimi = null)`
  - model = `(ayar.HizOnceligi || hizli) ? GrokModel : GrokModelKalite`
  - deneme = canli ? 1 : 3; istek başına zaman aşımı = zamanAsimi ?? 30 sn. ZAMAN AŞIMI YENİDEN DENENMEZ (Mac 37-52: 3×30 sn canlı kuyruğu kilitliyordu); yalnız 429/5xx/ağ kopması denenir. Zaman aşımında `ZamanAsimiHatasi` fırlat (üst katman ArizaGunlugu için); çağıran yakalar.
  - Girdi: `cevrilecek` = roller varsa `[{rol:"ben"|"karsi", metin}]` yoksa dize dizisi; `onceki_konusma` = `OncekiKonusmaYapili` varsa `[{rol, metin, ceviri}]` (son 10) yoksa mevcut dize listesi.
  - İstem: hedef dil sabit "Türkçe" DEĞİL — `Diller.Ad(ayar.HedefDil)` (Mac `dilAdi`); kural 5'e "(rol: ben = kullanıcı, karsi = karşı taraf)" ekle (Mac 502-503).
  - Yanıt: `YanitCozucu.Coz`. Sonra EKSİKSİZLİK (a) + HİZA (b) denetimi (Motorlar.swift 867-925): (a) boş/`AlmancaKalintiVar`/`HicCevrilmemis` → `yeniden`; (b) iki FARKLI kaynağa aynı çeviri → `hizaCakisan`. `canli` ise tekil düzeltme YOK, yalnız hizaCakisan satırlar `null` (+ `ArizaGunlugu.Yaz(asama, "grok-hizli|grok-kalite", "hiza-cakismasi", …)`); değilse `yeniden` listesinin ilk 6'sı TEK TEK yeniden çevrilir (mevcut `EksikleriTamamlaAsync` bu listeyle).
  - `EmojileriKoru` sonra uygulanır. `MotorAdi = "Grok"`.
- `public Task<string> GidenCevirAsync(string turkce, CeviriBaglam b, CancellationToken iptal)` — Giden.swift 922-1051 BİREBİR:
  model = `(HizOnceligi || GidenHizOnceligi) ? GrokModel : GrokModelKalite`; karakter = `GidenKarakterMetni` boşsa `Kisilik`; tarz örnekleri ve `<cevrilecek>` içeriği `Kalite.ZarfaGuvenli` ile; sayı düzeltme turu `SayilarKorunduMu` ile (obek listesi mesaja yazılır); Türkçe kalıntı turu; SONDA ZORUNLU `GidenKapisi.Kapi(turkce, ham: yanit, bicimli)` — `bicimli = ayar.GidenKarakter ? Kalite.GidenFormatla(yanit) : yanit.Trim()`; kapı reddederse `throw ret` (GidenRet). Boş yanıt → `throw new GidenRet("Grok yanıt vermedi")`. Bu metot artık `null` DÖNMEZ, fırlatır.
- `public Task<List<(string Cevap, string Turkce)>> OneriAsync(IReadOnlyList<string> dokum, CeviriBaglam b, bool farkliOlsun, CancellationToken iptal)` — Giden.swift 140-196: geçmiş `ayar.GecmisAcik` ise `SohbetGecmisi.Oku()`; sistem istemi + `<gecmis tur="uslup">`/`<gecmis tur="benzer">`/`<sohbet>` zarfları (ZarfaGuvenli); sıcaklık 0.8; JSON `{"oneriler":[{"cevap","turkce"}×3]}`; JSON gelmezse `[(icerik, "")]`.
- `IstekAsync` imzası: `(mesajlar, sicaklik, sema, model, iptal, int deneme, TimeSpan zamanAsimi)`.

### B7. `Ceviri/Cevirmen.cs` — Motorlar.swift `bloklariCevir` (745-1122) BİREBİR + Windows uyarlaması
```csharp
public sealed record CeviriSecenekleri(bool Zorla = false, bool KaliciYaz = true, bool Canli = false, bool Hizli = false, TimeSpan? ZamanAsimi = null, string Asama = "ceviri");
public OturumOnbellegi Onbellek { get; }
public Task<string> BloklariCevirAsync(IReadOnlyList<Blok> bloklar, CeviriBaglam baglam, CancellationToken iptal, CeviriSecenekleri? secenek = null); // MEVCUT 3 parametreli çağrılar derlenmeye DEVAM ETMELİ
public void Sifirla();   // yeni bölge: Onbellek.Temizle() + TekrarDefteri.Paylasilan.Sifirla()
public Task<string> GidenCevirAsync(string turkce, CeviriBaglam baglam, CancellationToken iptal); // GidenRet fırlatır; bing yolunda da GidenKapisi.Kapi uygulanır (Giden.swift 906-921)
public Task<List<(string Cevap, string Turkce)>> OneriAsync(IReadOnlyList<string> dokum, CeviriBaglam b, bool farkli, CancellationToken iptal);
```
`BloklariCevirAsync` algoritması (Mac satırlarıyla):
1. `hedefler = bloklar.Where(Hedef)`; boşsa "çevrilecek yazı yok".
2. ÖN ELEME (765-785): `Kalite.CevrilecekSeyYokMu` olanları `TekrarDefteri.DegismezIsaretle` + ArizaGunlugu "on-eleme/cevrilecek-sey-yok" (yalnız ilk işaretlemede); değişmez blokların çevirisi = kendi metni.
3. `hedefler2` = zehir (`Hafiza.UretilenMi(anahtar)`) ve `VazgecildiMi` olmayanlar (786-795).
4. `!Zorla` ise: oturum önbelleği (birebir + bulanık `Hafiza`'daki BulanikBul mantığıyla — Cevirmen içinde `BulanikBul(anahtar, sozluk)` statik saf kopyası değil, `Hafiza`'dakini `internal static` yapıp paylaş) → kalıcı `Hafiza.Bul` (796-810).
5. `eksikler` (811-812). Etiket "Hafızadan ✓" (815).
6. `grokIzinli = Motor=="ai" && anahtar geçerli` (824). Motor ai ama anahtar yoksa `m="hizli"`, `AnahtarUyarisi=true`, ArizaGunlugu "anahtar-yok" (834-842).
7. ai: `_grok.CevirAsync(metinler, baglam, iptal, roller: eksikler.Benim, canli, hizli, zamanAsimi)`; bağlam = ekrandaki ZATEN çevrilmiş bloklar (852-861) → `OncekiKonusmaYapili`. Etiket `"Grok · {LehceKisa}"`. **ai modunda makine motoruna DÜŞÜŞ YOK** (sahip kararı: "Grok kötü çeviriyor" yanılgısı + hafıza zehirlenmesi); başarısızlıkta "çeviri hatası (ağ?)" + ArizaGunlugu sebep.
8. hizli: Bing → Google (deneme canli?1:3), yankı satırları ikinci motorla bir kez daha (969-993). Etiket `"Bing (ücretsiz)"`/`"Google (ücretsiz)"`, anahtar uyarısı varsa `"⚠︎ Grok anahtarı yok → Bing"`.
9. Yerleştirme (994-1053): `EmojileriKoru`; gerçek çeviri ise `TekrarDefteri.Basarili`, belleğe yaz, grok ise `grokAnahtarlari`; değilse (b) → değişmez + orijinal göster; (a) → `Denendi ≥ Tavan` ise değişmez, değilse ArizaGunlugu "motor-cevirmedi" ve bellek `""`.
10. GROK TAMAMLAMA (1056-1089): `!Canli && grokIzinli` ise `""` artıkları Grok'a; etiket `"Grok (artıklar)"`/`+Grok`.
11. `KaliciYaz` ise yalnız `grokAnahtarlari` ∧ zehir değil → `Hafiza.Ata(anahtar, c, kaynakMetin)` ; sonunda `Hafiza.Kaydet()` (kısıtlı).
12. Bar dürüstlüğü (1106-1117): "✓ değişiklik gerekmedi" / " · değişiklik gerekmedi".
13. Bloklara yaz (1120), belleği `Onbellek.Birlestir(bellek, nesil)` ile birleştir (nesil başta alınır), `UretilenleriKaydet(bellek)` (Akis.swift 303-315: `Anahtarla(ceviri) == kaynakAnahtar` ise atla) → `Hafiza.UretilenIsaretle`.
`Teshis.Paylasilan.Olc(secenek.Hizli ? "grok-hizli" : "ceviri")` ile ölç.

### B8. `Testler/SafTestlerB.cs`
Mac 84-99 (CevirileriYerlestir), KesikJsondanKurtar (3 örnek: yarım JSON'dan 2/3 kurtarma, tam JSON, hiç nesne yok → null), 682-723 (OturumOnbellegi nesil + 8 iş parçacığı 2400 yazım + tavan), 811-818 (`GidenKapisi.MotorSecimi`), `SohbetGecmisi.UslupOrnekleri` (tekrar eden "ben" mesajı bir kez, en yeni önce, 12 sınırı) ve `BenzerGecmis` (kelime kesişimi puanı, izleyen ilk "ben" cevabı, en fazla 3). `Saf.csproj` Compile listesine `../../Ceviri/YanitCozucu.cs;../../Ceviri/OturumOnbellegi.cs;../../Ceviri/SohbetGecmisi.cs` ekle (System.Text.Json saf .NET'te var).

---

## FAZ C — AKIŞ (canlı döngü iki şerit, hareket kararı, kalite-sonra, odak iadesi, geçmiş)

Kaynak: `Akis.swift` 1-440 (ilkCeviri, uretilenleriKaydet, gecmiseAktar, canliBaslat), 440-1081 (canliTur, canliGuncelle, canliCeviriyiKuyrugaAl, kaliteyleDuzelt), `Uygulama.swift` 934-1010 (yazdigimiCevir). Dosya: `Yonetici.cs` (A'nın eklediği 5 üye KALIR, geliştirilebilir), `Ekran/PencereAraclari.cs`.

Windows'a özgü: yakalama `Yakalama.BolgeYakala` (değişmez), OCR `OcrOkuyucu`, epoch'lar mevcut. Yeni durum alanları: `HareketDurumu _hareket`, `BosNobetDefteri _bosDefter`, `CancellationTokenSource _canliIptal` (YeniCanliEpoch her çağrıda eskisini iptal edip yenisini kurar), `_canliCeviriSuruyor/_canliCeviriBekleyen/_canliCeviriBaslangic`, `DateTime _geriCekilmeBitis`, kalite kuyruğu durumu (`_kaliteBekleyen: Dictionary<string,(Blok blok, DateTime dogum)>`, `_kaliteUcusta`), `IntPtr _oncekiPencere`, `HashSet<string> _gecmiseYazilan`, `Stopwatch _saat`.

C1. **İki şerit** (Mac P0 kök nedeni): `CanliTurAsync` = yakalama → iz/izdüşüm → kaydırma telafisi → `Hareket.Karar` → uygunsa OCR + eşleştirme + 1. faz boyama → `_canliMesgul=false`. AĞ İŞİ AYRI: `CanliCeviriyiKuyrugaAl(...)` tek uçuşlu arka plan görevi (uçuşta varsa `_canliCeviriBekleyen=true`); bitince başarı = BOŞ OLMAYAN çeviri (Akis 864-897: BosNobetDefteri + pesPeseHata + 60/120 sn geri çekilme → `_geriCekilmeBitis`, `_hareket.OcrYasakBitis` bunu okur), tur hâlâ güncel ve `!bekleyen` ve `_mevcutBloklar` aynı listeyse TEK repaint + etiket; `hizliYol && !kareHareketli` ise `KaliteyleDuzelt(motordan)`. Ağ işi `CancellationToken` ile (epoch iptali ≤250 ms'de düşürür); 45 sn bekçi.
C2. **Eşleştirme** (Akis 704-778): BOŞ çeviri miras alınmaz; eşleşmeyen blok için oturum önbelleği (`_cevirmen.Onbellek[anahtar]`): doluysa al, `""` ise motor ai iken `bosNobetciDene && HakVarMi` ise `Sil` (eksik say) değilse `""` bırak.
C3. **Zehir kalkanı** mevcut (`Hafiza.UretilenMi`), uzun anahtar ≥12, 2+ → `_ekranSabitlendi=false` dön.
C4. **İlk çeviri** (Akis 162-299): `hizliYol = KaliteSonra && !HizOnceligi && Motor=="ai" && anahtar var` → `Hizli:true, KaliciYaz:false`; sonra `KaliteyleDuzelt(motordan)`; `_hareket.SonOcrIzi` tohumla; `GecmiseAktar`; etiketler; Türkçe metin var ama çeviri yoksa "Çeviri alınamadı — bağlantı gelince otomatik denenecek" + `_pesPeseHata=1`.
C5. **KaliteyleDuzelt** (Akis 933-1081): `Kalite.KaliteAdayiMi(anahtar, metin, ceviri, HedefDil)` ön eleme; KOPYA bloklar; `_kaliteBekleyen` birleştirme, derinlik `KaliteTuru.DerinlikTavani`; tek uçuşlu worker (`Task.Run` döngüsü); her parti `_cevirmen.BloklariCevirAsync(kopyalar, baglam, iptal, new(Zorla:true, KaliciYaz:true, Canli:true, ZamanAsimi: KaliteTuru.ZamanAsimi, Asama:"kalite"))` (motor ai, `HizOnceligi=false` kopya ayar); `KaliteTuru.EkranaYazilsinMi(dogum, now)` geçenler Dispatcher'da `_mevcutBloklar`a anahtarla uygulanır (`Anahtarla` eşitse atla), `_katman.MetniTazele(bloklar, boyaci)` (`Guncelle` DEĞİL — kaydırma telafisini sıfırlar), etiket "Grok ✓ kalite"; geçmeyenler yalnız hafızaya (Teshis "kalite-yasli").
C6. **Odak iadesi** (Akis 107-145): `BolgeCevirBaslat` öncesi `_oncekiPencere = PencereAraclari.OnPlandakiPencere()`; seçim (iptal dahil) bitince `PencereAraclari.OnePlanaGetir(_oncekiPencere)`. `PencereAraclari`ya `GetForegroundWindow/SetForegroundWindow` P/Invoke ekle.
C7. **Giden** (`GidenCevirAsync`): `_cevirmen.GidenCevirAsync` artık `GidenRet` fırlatır → `_tepsi.Bilgi("Çeviri yapılamadı", "Mesajın DEĞİŞMEDİ — " + ret.Neden)`; anahtar denetimi `Ayar.EtkinGidenMotor == "grok"` ile; `Teshis.Asama = "giden çeviri"`; zaman aşımı 25 sn kalır.
C8. **Geçmiş** (Akis 338-355): `GecmiseAktar(bloklar)` — `GecmisAcik` ise, zehir değilse, boş çeviri değilse, `_gecmiseYazilan`da yoksa `SohbetGecmisi.Yaz(kim, metin, ceviri)`; 20 sn'de bir `SohbetGecmisi.Kirp()` + `_hafiza.Kaydet()` zamanlayıcısı (Cekirdek.swift kaydet zamanlayıcısı).
C9. **Bekçiler** (Akis 457-479): tur 25 sn, ağ işi 45 sn → bayrak sıfırla + `YeniCanliEpoch`. `SaglikDenetimi` 40 sn kuralları kalır.
C10. **CanliDurdur**: zamanlayıcı + CTS iptal + `_bosDefter.Sifirla()` + `TekrarDefteri.Paylasilan.Sifirla()` + `YeniCanliEpoch`. `BolgeCevirBaslat`: `_cevirmen.Sifirla()`, `_hareket = new()`.
C11. `Basla()`: `Teshis.Paylasilan.Baslat(a => _arayuz.BeginInvoke(a))`; `Dispose`: `Teshis.Durdur()`, `SohbetGecmisi.Kirp()`.
C12. Etiketler: ağ işi başlarken "⏳ çevriliyor…"; SCK/yakalama 5 üst üste başarısızlıkta "Ekran yakalanamıyor — yeniden dene" (üstel geri çekilme 2→60 sn).
C13. `Testler/SafTestlerC.cs`: saf bir şey yoksa BOŞ kalabilir; ama `Hareket.Karar` ile "kaydırma + yeni mesaj" senaryosunu ve `BosNobetDefteri.TurAcikMi` mantığını en az 6 kontrolle sabitle.

---

## FAZ D — ARAYÜZ (bar düğmeleri, öneri paneli, menü, ayar penceresi)

Kaynak: `Akis.swift` 1138-1513 (bar, kopyala, orijinal, ✨, cevap önerisi, öneri paneli), `Uygulama.swift` 1204-1385 (menü), `Katman.swift` 1101-1123 (DurumEtiketi).

D1. `Kisayol/KisayolMetni.cs` (yeni): `public static class KisayolMetni { public static string Metin(uint mod, uint tus); }` — AyarPenceresi'ndeki `KisayolMetni`yi buraya taşı ("Ctrl + Alt + C").
D2. `Arayuz/KontrolCubugu.cs`: genişlik `clamp(bölge DIU genişliği, 460, 580)`, yükseklik 38; konum `Geometri.BarKonumu` (fiziksel→DIU dönüşümü ölçekle) → `PencereyiKonumlandir`; sol: durum etiketi (kalan genişliğin tamamı, `DurumMetni.HataMi` ise turuncu `#FFA973`, değilse `#D9D9D9`, `ToolTip` = tam metin); sağ→sol düğmeler (Segoe MDL2/Unicode simge, 28×22, kenarlıksız, ipucu): `✕ Kapat` → `KatmaniKapat()`, `📋 Çevirileri kopyala` → `CevirileriKopyala()`, `👁 Orijinali göster/gizle` → `OrijinalDegistir()` (simge değişir), `✨ Grok AI ile yeniden çevir` → `KaliteyleYenidenCevir()`, `⌨️ Yazdığımı çevir {KisayolMetni} (Türkçe → karşı dil)` → `GidenCevirBaslat()`, `💬 Cevap öner (AI + sohbet hafızası)` → `CevapOnerAsync(false)`; sonra `Canlı` ToggleSwitch benzeri CheckBox. `MotorAdi` set edildiğinde renk/ipucu güncellenir. Pencere yakalamadan gizli (`KatmandanGizle`) ve odak ÇALMAZ (`ShowActivated=false`, `WS_EX_NOACTIVATE|TOOLWINDOW` — `Yakalama.TiklamayiGecir` DEĞİL, tıklanabilir kalmalı; yalnız NOACTIVATE/TOOLWINDOW stilini `PencereAraclari` ya da yerel P/Invoke ile uygula).
D3. `Arayuz/OneriPenceresi.cs` (yeni): bar altında (sığmazsa üstünde), genişlik = bar genişliği (min 420), koyu yarı saydam yuvarlak; her öneri: `Cevap` (13pt, beyaz, seçilebilir TextBox ReadOnly) + `(Türkçe anlam)` (11pt gri) + sağda `Kopyala` (panoya cevap); altta `Yenile` (→ `CevapOnerAsync(true)`) ve `Kapat`; Topmost, ShowActivated=false, yakalamadan gizli. `Goster(List<(string,string)>)`.
D4. `Yonetici.Arayuz.cs` (yeni, `public sealed partial class Yonetici`):
```csharp
public void CevirileriKopyala();          // boş olmayan çevirileri satır satır panoya; GeriBildir("Panoya kopyalandı ✓")
public bool OrijinalDegistir();           // _katman.Gorunum.OrijinalGoster toggle; yeni değeri döner
public void KaliteyleYenidenCevir();      // Akis 1283-1334: anahtar yoksa etiket; TekrarDefteri.TekrarAc(her blok); _cevirmen.BloklariCevirAsync(mevcut, baglam, iptal, new(Zorla:true, KaliciYaz:true)) kalite modeliyle (Hizli:false; ayar kopyasında HizOnceligi=false → BaglamKur'a kopya ayar geçir); bitince Hafiza.Guncelle(anahtar, c, metin) her blok için; Dispatcher'da _katman.MetniTazele + etiket; _isSuruyor/_isBaslangic bekçisi
public Task CevapOnerAsync(bool farkli);  // Akis 1340-1377: dokum = _mevcutBloklar (Y'ye göre sıralı, Hedef) "BEN: …"/"KARŞI: …"; anahtar yoksa etiket "Öneri için Grok anahtarı gerekli"; etiket "cevap hazırlanıyor…"; _cevirmen.OneriAsync; sonuç → OneriPenceresi.Goster; "öneriler hazır"/"öneri alınamadı"; tek uçuş (_oneriSuruyor), 40 sn bekçi
public void AyarDegistir(Action<Ayarlar> degisiklik); // uygula + Kaydet + (kısayol değiştiyse KisayolKur) + motor/dil/mod/kimlik değiştiyse EkrandakiCevirileriTazele()
public void HafizayiSil();   // onay MessageBox → _hafiza.Temizle(); GeriBildir
public void GecmisiSil();    // onay → SohbetGecmisi.Sil(); GeriBildir
public void KisayolDegistir(); // AyarPenceresi'ndeki yakalama penceresini tek başına aç → AyarDegistir
public void UslupDuzenle();    // "Nasıl Yazayım (üslup)…": çok satırlı metin penceresi → Kisilik (ve GidenKarakterMetni) → AyarDegistir
```
`KisayolKur()` mevcut `private` → `internal` yap (Yonetici.cs'te C sahibi; D YALNIZ `Yonetici.Arayuz.cs` yazar. `KisayolKur` erişimi için C'ye not: `internal void KisayolKur()`; D kendi dosyasında `KisayolKur()` çağırır — C ile aynı anda çalışıyorsanız ve derleme kırılırsa `_kisayol.Kaydet(_ayar.KisayolMod, _ayar.KisayolTus, GidenCevirBaslat)` doğrudan çağır).
D5. `Arayuz/TepsiSimgesi.cs` menüsü — Mac 1204-1385 BİREBİR yapı: `Bölgeyi Çevir` · `Yazdığımı Çevir  (Ctrl + Alt + C)` · `Çeviriyi Kapat` · ─ · `Canlı çeviri` (işaretli) · `Kim yazıyor ▸ Ben ▸ Kadın/Erkek/Belirtme`, `Karşımdaki ▸ …` · `Bana çevir ▸ Diller.Adlar` · ─ · `Gelişmiş ▸` [`Karşı tarafın dili ▸ Alman modu — Almanya + İsviçre (önerilen) / Yalnız İsviçre Almancası / Otomatik (her dil)`, `Çeviri motoru ▸ Yapay zekâ — en iyi kalite (önerilen) / Ücretsiz çeviri`, `Yetişkin içerik (sansürsüz)`✓, `Hız önceliği (daha hızlı, biraz düşük kalite)`✓, `Önce hızlı göster, sonra kaliteyle düzelt`✓, `Yazdığımı Çevir hızlı modelle (lehçe kalitesi düşebilir)`✓, `Emoji ekleyebilsin`✓, ─, `Kısayolu Değiştir…`, `Nasıl Yazayım (üslup)…`, `Yapay Zekâ Anahtarı…`, `Ayarlar…`, ─, `Sohbet hafızası`✓, `Kayıtlı Çevirileri Sil…`, `Sohbet Geçmişini Sil…`, ─, `Günlük dosyasını aç`, `Arıza günlüğünü aç`, `Yazı tanıma dilleri…`] · ─ · `Çık`. `Opening` olayında: `Çeviriyi Kapat` yalnız katman açıkken etkin; `Bölgeyi Çevir` iş sürerken devre dışı; işaretler ayardan tazelenir. Tüm ayar eylemleri `AyarDegistir` ile.
D6. `Arayuz/AyarPenceresi.cs`: `Bana çevir` (Diller) combo, `Önce hızlı göster, sonra kaliteyle düzelt`, `Yazdığımı Çevir hızlı modelle` kutuları; Kısayol metni `KisayolMetni.Metin`.
D7. `Testler/SafTestlerD.cs`: `KisayolMetni.Metin` (Ctrl+Alt+C, Ctrl+F, Shift+Win+X), `DurumMetni.HataMi` ek örnekler, `Geometri.BarKonumu` DIU dönüşümü öncesi sınır durumları — en az 8 kontrol. `Saf.csproj`'a `../../Kisayol/KisayolMetni.cs` ekle (yalnız `KeyInterop` WPF'e bağımlıysa tuş adını KENDİ tablonla üret — Mac `tusAdi` gibi — saf kalsın).

---

## FAZ E — BELGELER VE YAYIN KAPISI

E1. `yayinla.sh`: geçmiş sır kapısı SIGPIPE ile fail-open (Mac KARARLAR #1): `git log --all -p | grep -q` yerine
`if git rev-list --all | xargs git grep -I -l -E 'xai-[A-Za-z0-9]{20,}' -- 2>/dev/null | head -1 | grep -q .; then` DEĞİL (yine boru) — doğru kalıp: sonucu değişkene al: `BULGU=$(git rev-list --all | xargs git grep -I -l -E 'xai-[A-Za-z0-9]{20,}' 2>/dev/null || true); [ -n "$BULGU" ] && { echo "✗ DURDURULDU: git GEÇMİŞİNDE API anahtarı var"; exit 1; }`. Çalışma ağacı kapısında bulguları MASKELE (anahtarın tamamını basma). Ayrıca dal kapısı: yalnız `main`den yayın.
E2. `.github/workflows/windows.yml`: test adımından sonra `dotnet run --project Testler/Saf/Saf.csproj -c Debug --nologo` adımı; sürüm notlarını yeni özelliklerle güncelle (bar düğmeleri, cevap önerisi, kalite-sonra, arıza günlüğü, sohbet hafızası).
E3. `README.md`: Kullanım bölümüne bar düğmeleri (✕ 📋 👁 ✨ ⌨️ 💬), "Cevap Öner", menü yapısı (Kim yazıyor, Bana çevir, Gelişmiş), `Önce hızlı göster, sonra kaliteyle düzelt` açıklaması (hızlı model anında, kalite modeli ≤8 sn içinde düzeltir; sonrası yalnız hafızaya), sohbet hafızası (yerel JSONL, menüden kapat/sil), arıza günlüğü (`%APPDATA%\EkranCeviri\ariza.log`, metin YAZILMAZ: uzunluk + sha256 ön eki), teşhis günlüğü. Gizlilik bölümü: modele giden şeyler listesi (okunan metin + kimlik/karakter tanımı + geçmiş açıksa üslup örnekleri). Tasarım kararları tablosuna: iki şerit canlı döngü, hareket kararı (hücre sayısı), ücretli tekrar tavanı (2), kalite yaş kapısı (8 sn), giden kapısı ham+biçimli. Sürüm 1.2.0.
E4. `SECURITY.md`: giden kapısı artık HAM ve BİÇİMLİ çıktıya birlikte uygulanır; ret nedeni kullanıcıya gösterilir; arıza günlüğü metin taşımaz; ücretsiz motor seçiliyken xAI'ye hiçbir metin gitmez.
E5. `CONTRIBUTING.md`: saf test koşucusu (`Testler/Saf`) ve "her yeni saf fonksiyon Saf.csproj'a eklenir" kuralı; faz/dosya sahipliği değil, sadece komutlar.
E6. `.ai/STATE.md` (yeni): bu portun özeti, taşınan/taşınmayan (SCStream, hardened runtime, Keychain, QA sinama rig'i, izinsizYedekYol) listesi, doğrulama komutları, "ölçümü kopyalama" kuralı; CI'nın gerçek Windows doğrulaması olduğu.
Belgelerde hiçbir sayı uydurulmaz; kod okunarak yazılır.
