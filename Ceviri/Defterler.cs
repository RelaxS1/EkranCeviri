namespace EkranCeviri.Ceviri;

/// <summary>
/// ÜCRETLİ TEKRARIN TEK DURAĞI — anahtar başına TEKRAR TAVANI + (b) işareti.
///
/// <see cref="BosNobetDefteri"/> yalnız CANLI turu frenliyordu; ücretli
/// KALİTE turu bu defteri hiç görmüyordu ve aynı metni her sabit karede
/// yeniden gönderiyordu. Ölçülen sonuç: 17 benzersiz metin için 78 ücretli
/// çağrı.
///
/// İki tür kayıt tutar:
///  - degismez: (b) sınıfı ya da tavana varmış anahtar. KALICI: oturum
///    boyunca bir daha ücretli çağrı almaz.
///  - deneme: (a) sınıfı sayaç; tavana varınca anahtar degismez'e geçer.
/// İki iş parçacığından erişilir (canlı tur + kalite turu): kilitli.
/// </summary>
public sealed class TekrarDefteri
{
    public static TekrarDefteri Paylasilan { get; } = new();

    /// <summary>Aynı anahtar için EN FAZLA kaç ücretli deneme?
    /// 2 seçildi: bu yola yalnız motorun YANIT VERDİĞİ durum girer (gerçek
    /// ağ arızası bir üstteki dalda bos-yanit/ag-hatasi yazar ve buraya hiç
    /// gelmez). Bir metin iki farklı model yapılandırmasında da değişmeden
    /// döndüyse sonuç belirlenimlidir; ölçülen 61 israf çağrısı üçüncü ve
    /// sonraki denemelerdir.</summary>
    public const int Tavan = 2;

    /// <summary>Sınırsız büyümesin: aşınca en eski çeyrek atılır.</summary>
    private const int Sinir = 4_000;

    private readonly object _kilit = new();
    private readonly HashSet<string> _degismez = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _deneme = new(StringComparer.Ordinal);
    private readonly List<string> _sira = [];

    /// <summary>(b) ya da vazgeçme: kalıcı işaret.</summary>
    public void DegismezIsaretle(string anahtar)
    {
        lock (_kilit)
        {
            _deneme.Remove(anahtar);
            if (!_degismez.Add(anahtar)) return;
            _sira.Add(anahtar);
            if (_sira.Count <= Sinir) return;
            int at = Sinir / 4;
            for (int i = 0; i < at; i++) _degismez.Remove(_sira[i]);
            _sira.RemoveRange(0, at);
        }
    }

    public bool DegismezMi(string anahtar)
    {
        lock (_kilit) return _degismez.Contains(anahtar);
    }

    /// <summary>Bu anahtar için ücretli çağrı YAPILMAMALI mı?</summary>
    public bool VazgecildiMi(string anahtar)
    {
        lock (_kilit)
        {
            if (_degismez.Contains(anahtar)) return true;
            return _deneme.TryGetValue(anahtar, out var n) && n >= Tavan;
        }
    }

    /// <summary>Bir (a) denemesi işlenir; GÜNCEL deneme sayısını döndürür.</summary>
    public int Denendi(string anahtar)
    {
        lock (_kilit)
        {
            int n = (_deneme.TryGetValue(anahtar, out var e) ? e : 0) + 1;
            _deneme[anahtar] = n;
            return n;
        }
    }

    /// <summary>Gerçek çeviri geldi: sayaç sıfırlanır (işaret kaldırılmaz —
    /// (b) işareti zaten gerçek çeviri gelmediği için konmuştur).</summary>
    public void Basarili(string anahtar)
    {
        lock (_kilit) _deneme.Remove(anahtar);
    }

    /// <summary>KULLANICI AÇIKÇA İSTEDİ (✨ "daha iyi çevir", "yeniden çevir"):
    /// bütçe yeniden açılır. Tavan otomatik turları frenlemek içindir;
    /// düğmeye basan kullanıcıya "hiçbir şey olmuyor" dedirtmemeli.</summary>
    public void TekrarAc(string anahtar)
    {
        lock (_kilit)
        {
            _degismez.Remove(anahtar);
            _deneme.Remove(anahtar);
        }
    }

    public void Sifirla()
    {
        lock (_kilit)
        {
            _degismez.Clear();
            _deneme.Clear();
            _sira.Clear();
        }
    }

    public int Izlenen
    {
        get { lock (_kilit) return _degismez.Count + _deneme.Count; }
    }
}

/// <summary>
/// ÇEVRİLEMEYEN MESAJLARIN SÜRELİ DEFTERİ (TTL + deneme sayacı).
///
/// Eskiden çevrilemeyen mesaj oturum önbelleğine "" yazılıyordu ve "" bir
/// PROTOKOLDÜ: başarı hesabı "çeviri null mu"ya baktığı için boş çeviri
/// BAŞARI sayılıyor, pesPeseHata sıfırlanıyordu. Tek kurtarma kapısı
/// (pesPeseHata >= 3) sağlıklı ağda hiç açılmadığından mesaj sonsuza kadar
/// kaynak dilde kalıyordu. Artık başarısızlık burada tutulur: TTL dolunca
/// tur açılır, anahtar başına deneme hakkı boşuna para yakmayı engeller,
/// uzun aradan sonra hak yenilenir (ağ saatlerce kapalı kalabilir).
/// </summary>
public sealed class BosNobetDefteri
{
    private struct Kayit { public int Deneme; public DateTime Son; }

    /// <summary>İki yeniden-deneme turu arasındaki en az süre.</summary>
    public static readonly TimeSpan TurTtl = TimeSpan.FromSeconds(90);

    /// <summary>Bir anahtar için art arda en fazla deneme.</summary>
    public const int EnFazlaDeneme = 4;

    /// <summary>Bu kadar sessizlikten sonra anahtarın deneme hakkı yenilenir.</summary>
    public static readonly TimeSpan HakYenileme = TimeSpan.FromSeconds(900);

    private const int Sinir = 2_000;

    private readonly object _kilit = new();
    private readonly Dictionary<string, Kayit> _kayitlar = new(StringComparer.Ordinal);
    private DateTime _sonTur = DateTime.MinValue;

    private static DateTime Simdi(DateTime? s) => s ?? DateTime.UtcNow;

    /// <summary>Bu turda çevrilemeyen girdiler yeniden denensin mi?
    /// pesPeseHata TEK besleyici olamaz: sağlıklı ağda hiç 3'e ulaşmıyor.</summary>
    public bool TurAcikMi(int pesPeseHata, DateTime? simdi = null)
    {
        lock (_kilit)
            return pesPeseHata >= 3 || Simdi(simdi) - _sonTur >= TurTtl;
    }

    public void TurBasladi(DateTime? simdi = null)
    {
        lock (_kilit) _sonTur = Simdi(simdi);
    }

    public bool HakVarMi(string anahtar, DateTime? simdi = null)
    {
        lock (_kilit)
        {
            if (!_kayitlar.TryGetValue(anahtar, out var k)) return true;
            if (Simdi(simdi) - k.Son >= HakYenileme) return true;
            return k.Deneme < EnFazlaDeneme;
        }
    }

    public void Denendi(string anahtar, DateTime? simdi = null)
    {
        lock (_kilit)
        {
            var an = Simdi(simdi);
            var k = _kayitlar.TryGetValue(anahtar, out var e)
                ? e : new Kayit { Deneme = 0, Son = DateTime.MinValue };
            if (an - k.Son >= HakYenileme) k.Deneme = 0;
            k.Deneme += 1;
            k.Son = an;
            _kayitlar[anahtar] = k;
            if (_kayitlar.Count <= Sinir) return;
            // Sınırsız büyümesin: en eski yarısı atılır (uygulama günlerce açık).
            var atilacak = _kayitlar.OrderBy(p => p.Value.Son)
                                    .Take(Sinir / 2)
                                    .Select(p => p.Key).ToList();
            foreach (var a in atilacak) _kayitlar.Remove(a);
        }
    }

    public void Basarili(string anahtar)
    {
        lock (_kilit) _kayitlar.Remove(anahtar);
    }

    public void Sifirla()
    {
        lock (_kilit)
        {
            _kayitlar.Clear();
            _sonTur = DateTime.MinValue;
        }
    }

    public int Izlenen
    {
        get { lock (_kilit) return _kayitlar.Count; }
    }
}

/// <summary>
/// TAVANLI, KİLİTLİ KÜME (ekleme sıralı). "Geçmişe yazıldı" işaretleri gibi
/// oturum boyu büyüyen kümeler için: kullanıcı uygulamayı günlerce kapatmıyor,
/// tavansız HashSet sınırsız büyüyordu. Tavan aşılınca EN ESKİ (ilk eklenen)
/// dörtte biri atılır — TekrarDefteri/Hafiza ile aynı "çeyrek at" deseni,
/// ama anahtar sırası değil EKLEME sırası: en eski mesajlar zaten ekrandan
/// düşmüştür, yeniden kaydedilme olasılığı en düşük olanlar onlardır.
/// İki iş parçacığından erişilir: kilitli.
/// </summary>
public sealed class KilitliKume
{
    public const int VarsayilanTavan = 8_000;

    private readonly object _kilit = new();
    private readonly HashSet<string> _kume = new(StringComparer.Ordinal);
    private readonly Queue<string> _sira = new();
    private readonly int _tavan;

    public KilitliKume(int tavan = VarsayilanTavan)
    {
        if (tavan < 4) throw new ArgumentOutOfRangeException(nameof(tavan));
        _tavan = tavan;
    }

    /// <summary>Yeni eklendiyse true (HashSet.Add sözleşmesi). Tavan aşılınca
    /// ekleme sırasıyla en eski çeyrek atılır.</summary>
    public bool Ekle(string anahtar)
    {
        lock (_kilit)
        {
            if (!_kume.Add(anahtar)) return false;
            _sira.Enqueue(anahtar);
            if (_kume.Count > _tavan)
            {
                int atilacak = _tavan / 4;
                while (atilacak-- > 0 && _sira.Count > 0)
                    _kume.Remove(_sira.Dequeue());
            }
            return true;
        }
    }

    public bool Icerir(string anahtar)
    {
        lock (_kilit) return _kume.Contains(anahtar);
    }

    public void Temizle()
    {
        lock (_kilit)
        {
            _kume.Clear();
            _sira.Clear();
        }
    }

    public int Sayi
    {
        get { lock (_kilit) return _kume.Count; }
    }
}
