namespace EkranCeviri.Ceviri;

/// <summary>
/// OTURUM ÖNBELLEĞİ — Motorlar.swift <c>KilitliSozluk</c>. İki iş
/// parçacığından (canlı tur + kalite turu) aynı anda erişilir; macOS'ta
/// kilitsiz sözlük bellek bozulmasıyla ÇÖKÜYORDU. Saf: I/O yok.
///
/// EKLEME SIRASI tutulur: yaşlandırma en eskiyi atar (sırasız sözlükte
/// "son 2000'i tut" RASTGELE siliyordu — "az önce çevrilen mesaj kayboldu").
///
/// NESİL: <see cref="Temizle"/> her çağrıda ilerletir. Arka planda biten
/// bir iş başlangıcındaki nesille birleştirmeye gelir; nesil eskiyse yazamaz.
/// (Eski "kopya al / toptan yaz" deseni, kullanıcı yeni bölge seçtikten
/// sonra biten işin eski çevirilerini geri getiriyordu.)
/// </summary>
public sealed class OturumOnbellegi
{
    /// <summary>Toplam tavan ve kırpma hedefi — bloklariCevir'deki 3000/2000 aynen.</summary>
    private const int Tavan = 3000;
    private const int Hedef = 2000;

    private readonly object _kilit = new();
    private readonly Dictionary<string, string> _d = new(StringComparer.Ordinal);
    private readonly List<string> _sira = [];
    private int _nesil;

    /// <summary>Kopya ve nesil TEK kilit altında: ikisi ayrı okunursa arada
    /// Temizle girebilir ve nesil kopyayla uyuşmaz.</summary>
    public (Dictionary<string, string> Kopya, int Nesil) Kopyala()
    {
        lock (_kilit)
            return (new Dictionary<string, string>(_d, StringComparer.Ordinal), _nesil);
    }

    public int Nesil { get { lock (_kilit) return _nesil; } }

    public int Sayi { get { lock (_kilit) return _d.Count; } }

    /// <summary>İşin başındaki nesil hâlâ güncelse yeni girdileri BİRLEŞTİRİR
    /// (var olan anahtarın değeri güncellenir, yeni anahtar sıraya girer).
    /// Döner: yazıldı mı.</summary>
    public bool Birlestir(IReadOnlyDictionary<string, string> yeni, int nesil)
    {
        lock (_kilit)
        {
            if (nesil != _nesil) return false;
            foreach (var (k, v) in yeni)
            {
                if (!_d.ContainsKey(k)) _sira.Add(k);
                _d[k] = v;
            }
            KirpKilitli();
            return true;
        }
    }

    public string? this[string k]
    {
        get { lock (_kilit) return _d.TryGetValue(k, out var v) ? v : null; }
        set
        {
            lock (_kilit)
            {
                if (value is not null)
                {
                    if (!_d.ContainsKey(k)) _sira.Add(k);
                    _d[k] = value;
                    KirpKilitli();
                }
                else if (_d.Remove(k))
                {
                    _sira.Remove(k);
                }
            }
        }
    }

    public void Sil(string k) => this[k] = null;

    /// <summary>Boşaltır ve nesli ilerletir: eski nesille gelen birleştirme
    /// artık reddedilir.</summary>
    public void Temizle()
    {
        lock (_kilit)
        {
            _d.Clear();
            _sira.Clear();
            _nesil++;
        }
    }

    private void KirpKilitli()
    {
        if (_d.Count <= Tavan) return;
        int at = _d.Count - Hedef;
        int n = Math.Min(at, _sira.Count);
        for (int i = 0; i < n; i++) _d.Remove(_sira[i]);
        _sira.RemoveRange(0, n);
    }
}
