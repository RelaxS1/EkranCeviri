using EkranCeviri.Cekirdek;

namespace EkranCeviri.Ceviri;

/// <summary>
/// Çeviri düzenleyicisi: hafıza → motor sırasını yönetir, motor seçer,
/// sonucu kalite kapılarından geçirir ve yalnız GÜVENİLİR çıktıyı kalıcı
/// hafızaya yazar.
///
/// Sıra neden böyle: kullanıcı aynı kişiyle her gün konuşuyor; aynı
/// cümleler tekrar ediyor. Hafıza önce sorulunca tekrarlar ANINDA geliyor
/// ve kaliteli (yavaş) model kullanmak ücretsiz hale geliyor.
/// </summary>
public sealed class Cevirmen
{
    private readonly Hafiza _hafiza;
    private readonly GrokMotor _grok;
    private readonly BingMotor _bing;
    private readonly GoogleMotor _google;
    private readonly Func<Ayarlar> _ayar;

    public Cevirmen(Func<Ayarlar> ayarSaglayici, Hafiza hafiza)
    {
        _ayar = ayarSaglayici;
        _hafiza = hafiza;
        _grok = new GrokMotor(Ag.Istemci, ayarSaglayici);
        _bing = new BingMotor(Ag.Istemci);
        _google = new GoogleMotor(Ag.Istemci);
    }

    public Hafiza Hafiza => _hafiza;

    /// <summary>Grok kullanılabilir mi? (arayüz uyarısı için)</summary>
    public bool GrokHazir => AnahtarKasasi.BicimGecerli(_ayar().GrokApiKey);

    /// <summary>
    /// Bloklardan çevirisi eksik olanları doldurur. Blokların
    /// <see cref="Blok.Ceviri"/> alanı YERİNDE güncellenir.
    /// </summary>
    /// <returns>Panelde gösterilecek motor adı.</returns>
    public async Task<string> BloklariCevirAsync(IReadOnlyList<Blok> bloklar,
                                                 CeviriBaglam baglam,
                                                 CancellationToken iptal)
    {
        // 1. HAFIZA — ağa çıkmadan önce her zaman burası
        var eksik = new List<Blok>();
        foreach (var b in bloklar)
        {
            if (!b.Hedef || b.Ceviri is { Length: > 0 }) continue;
            var hazir = _hafiza.Bul(b.Anahtar);
            if (hazir is { Length: > 0 }) b.Ceviri = hazir;
            else eksik.Add(b);
        }

        if (eksik.Count == 0)
            return baglam.LehceKisa == "Almanca"
                ? "Hafıza" : $"Hafıza · {baglam.LehceKisa}";

        // 2. MOTOR
        var ayar = _ayar();
        var metinler = eksik.Select(b => b.Metin).ToList();
        CeviriSonuc? sonuc = null;

        if (ayar.Motor == "ai")
        {
            if (!GrokHazir)
            {
                // SESSİZ DÜŞÜŞ YOK: kullanıcı yapay zekâ seçtiyse ve anahtar
                // yoksa bunu BİLMELİ. Yine de çeviri gelsin diye makineye
                // düşüyoruz ama panelde uyarı görünüyor.
                Gunluk.Yaz("motor=ai ama anahtar yok → makine motoruna düşüldü");
                sonuc = await MakineyleAsync(metinler, baglam, iptal);
                YerlestirVeKaydet(eksik, sonuc);
                return "⚠ Anahtar yok → " + sonuc.MotorAdi;
            }
            sonuc = await _grok.CevirAsync(metinler, baglam, iptal);
            // Grok tamamen başarısızsa (ağ koptu, kota bitti) kullanıcıyı
            // çevirisiz bırakmaktansa makineye düş.
            if (sonuc.Ceviriler.All(c => c is null or ""))
            {
                Gunluk.Yaz("Grok hiç sonuç vermedi → makine motoruna düşüldü");
                sonuc = await MakineyleAsync(metinler, baglam, iptal);
            }
        }
        else
        {
            sonuc = await MakineyleAsync(metinler, baglam, iptal);
        }

        YerlestirVeKaydet(eksik, sonuc);
        return sonuc.MotorAdi;
    }

    /// <summary>Bing → Google sırası. Bing daha iyi Almanca yapıyor;
    /// başarısız olan satırlar Google'a devrediliyor.</summary>
    private async Task<CeviriSonuc> MakineyleAsync(IReadOnlyList<string> metinler,
                                                   CeviriBaglam baglam,
                                                   CancellationToken iptal)
    {
        var bing = await _bing.CevirAsync(metinler, baglam, iptal);
        var eksikIndeks = new List<int>();
        for (int i = 0; i < metinler.Count; i++)
            if (string.IsNullOrEmpty(bing.Ceviriler[i])) eksikIndeks.Add(i);

        if (eksikIndeks.Count == 0) return bing;

        var kalanlar = eksikIndeks.Select(i => metinler[i]).ToList();
        var google = await _google.CevirAsync(kalanlar, baglam, iptal);

        var birlesik = bing.Ceviriler.ToArray();
        for (int j = 0; j < eksikIndeks.Count; j++)
            if (j < google.Ceviriler.Count)
                birlesik[eksikIndeks[j]] = google.Ceviriler[j];

        var hicBing = bing.Ceviriler.All(c => c is null or "");
        return new CeviriSonuc
        {
            Ceviriler = birlesik,
            MotorAdi = hicBing ? google.MotorAdi : bing.MotorAdi,
            // Makine çıktısı kalıcı hafızayı ZEHİRLİYOR — asla yazılmaz.
            HafizayaYazilabilir = false,
        };
    }

    private void YerlestirVeKaydet(IReadOnlyList<Blok> eksik, CeviriSonuc sonuc)
    {
        for (int i = 0; i < eksik.Count && i < sonuc.Ceviriler.Count; i++)
        {
            var ceviri = sonuc.Ceviriler[i];
            if (string.IsNullOrWhiteSpace(ceviri)) continue;
            eksik[i].Ceviri = ceviri;
            if (sonuc.HafizayaYazilabilir)
                _hafiza.Ata(eksik[i].Anahtar, ceviri);
        }
        if (sonuc.HafizayaYazilabilir) _hafiza.Kaydet();
    }

    /// <summary>
    /// Kullanıcının Türkçe yazdığını karşı tarafın lehçesine çevirir.
    /// Bu metin DOĞRUDAN müşteriye gidiyor: kalite kapıları GrokMotor
    /// içinde uygulanır, geçemezse null döner ve hiçbir şey yapıştırılmaz.
    /// </summary>
    public async Task<string?> GidenCevirAsync(string turkce, CeviriBaglam baglam,
                                               CancellationToken iptal)
    {
        if (string.IsNullOrWhiteSpace(turkce)) return null;
        var ayar = _ayar();

        if (ayar.GidenMotor == "grok")
        {
            if (!GrokHazir)
            {
                Gunluk.Yaz("giden: anahtar yok");
                return null;   // üst katman kullanıcıyı uyarır
            }
            return await _grok.GidenCevirAsync(turkce, baglam, iptal);
        }

        // Makine motoruyla giden mesaj: lehçe üretemez, yalnız standart
        // Almanca. Kullanıcı bunu bilerek seçtiyse yapılır.
        var sonuc = await _bing.CevirAsync([turkce], new CeviriBaglam
        {
            Ayar = ayar,
            LehceAd = baglam.LehceAd,
            LehceKisa = baglam.LehceKisa,
        }, iptal);
        var cikti = sonuc.Ceviriler.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(cikti)) return null;

        // Rakam koruma kapısı makine yolunda da geçerli: saat/fiyat bozulursa
        // ticari zarar. Kapı geçilmezse mesaj YAPIŞTIRILMAZ.
        if (!Kalite.RakamlarKorundu(turkce, cikti))
        {
            Gunluk.Yaz("giden: rakam kapısı geçilmedi, iptal");
            return null;
        }
        cikti = Kalite.EmojileriKoru(turkce, cikti);
        return ayar.GidenKarakter ? Kalite.GidenFormatla(cikti) : cikti;
    }
}
