using System.Text.Json;

namespace EkranCeviri.Ceviri;

/// <summary>
/// Grok yanıtını çözen SAF katman (HttpClient yok, günlük yok) — Motorlar.swift
/// 371-426 ve grokCevir çözümleme dalları. Ayrı dosya olmasının nedeni test:
/// hiza koruması ve kesik yanıt kurtarma ÜRETİM fonksiyonu olarak test edilir,
/// kopyası değil.
/// </summary>
public static class YanitCozucu
{
    /// <summary>Model bazen JSON'u ``` çitleri arasında döndürüyor.</summary>
    public static string CitleriAt(string s)
    {
        var t = s.Trim();
        if (!t.StartsWith("```", StringComparison.Ordinal)) return t;
        int ilkSatirSonu = t.IndexOf('\n');
        if (ilkSatirSonu < 0)
            return t.Replace("```json", "").Replace("```", "").Trim();
        t = t[(ilkSatirSonu + 1)..];
        int son = t.LastIndexOf("```", StringComparison.Ordinal);
        return (son >= 0 ? t[..son] : t).Trim();
    }

    /// <summary>
    /// Modelin döndürdüğü kayıtları doğru sıraya yerleştirir.
    /// SIRALAMA MODELE EMANET EDİLMEZ: model indeksi bazen 1'den başlatıyor ve
    /// TÜM çeviriler bir kayıyordu (QA'da yakalandı: 1. mesajın çevirisi 2.
    /// mesaja yazıldı). Kural: indeksler geçerli bir PERMÜTASYON (0 ya da 1
    /// tabanlı) ise indeksle yerleştir; sayı tutuyorsa dizi sırası; aksi hâlde
    /// indeks 0-tabanına normalize edilerek yerleştirilir (0 indeksi görülen
    /// dizi 0-tabanlı sayılır; fazla/aralık dışı kayıt düşer). Boş yuva null.
    /// </summary>
    public static string?[] CevirileriYerlestir(
        IReadOnlyList<(int? Indeks, string? Ceviri)> kayitlar, int adet)
    {
        var liste = new string?[Math.Max(0, adet)];
        if (adet <= 0) return liste;
        var indeksler = kayitlar.Where(k => k.Indeks.HasValue)
                                .Select(k => k.Indeks!.Value).ToList();
        var kume = new HashSet<int>(indeksler);
        bool sifirTabanli = kume.SetEquals(Enumerable.Range(0, adet));
        bool birTabanli = kume.SetEquals(Enumerable.Range(1, adet));

        if (indeksler.Count == kayitlar.Count && kayitlar.Count == adet
            && (sifirTabanli || birTabanli))
        {
            int kaydir = birTabanli ? 1 : 0;
            for (int j = 0; j < kayitlar.Count; j++)
            {
                int i = indeksler[j] - kaydir;
                if (i < 0 || i >= adet) continue;
                liste[i] = kayitlar[j].Ceviri;
            }
        }
        else if (kayitlar.Count == adet)
        {
            for (int i = 0; i < adet; i++) liste[i] = kayitlar[i].Ceviri;
        }
        else
        {
            // Mac Motorlar.swift 417 yalnız "max >= adet" ile 1-tabanlı sayar;
            // model adet'ten FAZLA kayıt döndürünce (0,1,2 / adet=2) 0-tabanlı
            // dizi bir kaydırılıp yanlış mesaja yanlış çeviri gidiyordu.
            // BİLİNÇLİ SAPMA: 0 indeksi varsa dizi 0-tabanlıdır, kaydırma yok
            // (1-tabanlı dizide 0 asla bulunmaz; fazla kayıt sondan düşer).
            int kaydir = indeksler.Count > 0 && indeksler.Max() >= adet
                && !kume.Contains(0) ? 1 : 0;
            for (int n = 0; n < kayitlar.Count; n++)
            {
                int ham = kayitlar[n].Indeks ?? (n + kaydir);
                int i = ham - kaydir;
                if (i < 0 || i >= adet) continue;
                liste[i] = kayitlar[n].Ceviri;
            }
        }
        return liste;
    }

    /// <summary>
    /// KESİK YANIT KURTARMA. Model max_tokens'a takıldığında JSON yarım
    /// kalıyor, katı çözümleme başarısız oluyor ve TÜM parti sessizce
    /// düşüyordu. Yarım metinde TAM gelen <c>{… "ceviri": "…"}</c> nesneleri
    /// ayıklanır; hizalanamayan satırlar boş kalır ve tamamlama turu onları
    /// alır. Hiç nesne yoksa null.
    /// </summary>
    public static string?[]? KesikJsondanKurtar(string icerik, int adet)
    {
        if (adet <= 0) return null;
        var kayitlar = new List<(int? Indeks, string? Ceviri)>();
        var yigin = new Stack<int>();
        bool dizgide = false, kacis = false;
        for (int i = 0; i < icerik.Length; i++)
        {
            char k = icerik[i];
            if (kacis) { kacis = false; continue; }
            if (dizgide)
            {
                if (k == '\\') kacis = true;
                else if (k == '"') dizgide = false;
                continue;
            }
            switch (k)
            {
                case '"': dizgide = true; break;
                case '{': yigin.Push(i); break;
                case '}':
                    if (yigin.Count == 0) break;
                    int b = yigin.Pop();
                    var kayit = NesneyiCoz(icerik.AsSpan(b, i - b + 1));
                    if (kayit is not null) kayitlar.Add(kayit.Value);
                    break;
            }
        }
        if (kayitlar.Count == 0) return null;
        return CevirileriYerlestir(kayitlar, adet);
    }

    /// <summary>Tek bir JSON nesnesi: "ceviri" dize değilse kayıt değildir
    /// (dış zarf nesnesi ya da başka bir alan).</summary>
    private static (int? Indeks, string? Ceviri)? NesneyiCoz(ReadOnlySpan<char> metin)
    {
        try
        {
            using var belge = JsonDocument.Parse(metin.ToString());
            var kok = belge.RootElement;
            if (kok.ValueKind != JsonValueKind.Object) return null;
            if (!kok.TryGetProperty("ceviri", out var c)
                || c.ValueKind != JsonValueKind.String) return null;
            int? indeks = kok.TryGetProperty("indeks", out var ix)
                          && ix.ValueKind == JsonValueKind.Number
                          && ix.TryGetInt32(out var n) ? n : null;
            return (indeks, c.GetString());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Grok çeviri yanıtını çözer: <c>{"ceviriler":[…]}</c> ana yol; düz dize
    /// dizisi ya da nesne dizisi yedek yol; hiçbiri değilse kesik yanıt
    /// kurtarma. Çözülemezse null — çağıran "bos-yanit" sayar.
    /// </summary>
    public static string?[]? Coz(string icerik, int adet)
    {
        if (adet <= 0) return [];
        var temiz = CitleriAt(icerik);
        JsonDocument? belge = null;
        try { belge = JsonDocument.Parse(temiz); }
        catch (JsonException) { /* aşağıda kesik yanıt kurtarma denenir */ }

        if (belge is not null)
        {
            using (belge)
            {
                var kok = belge.RootElement;
                JsonElement dizi;
                bool diziVar;
                if (kok.ValueKind == JsonValueKind.Object)
                    diziVar = kok.TryGetProperty("ceviriler", out dizi)
                          && dizi.ValueKind == JsonValueKind.Array;
                else if (kok.ValueKind == JsonValueKind.Array) { dizi = kok; diziVar = true; }
                else { dizi = default; diziVar = false; }

                if (diziVar)
                {
                    var ogeler = dizi.EnumerateArray().ToList();
                    // Düz dize dizisi (yedek yol): model şemayı atlayıp
                    // yalnız çevirileri döndürdüğünde sıra dizi sırasıdır.
                    if (ogeler.Count > 0 && ogeler.All(o => o.ValueKind == JsonValueKind.String))
                    {
                        var duz = new string?[adet];
                        for (int i = 0; i < ogeler.Count && i < adet; i++)
                            duz[i] = ogeler[i].GetString();
                        return duz;
                    }
                    var kayitlar = new List<(int? Indeks, string? Ceviri)>(ogeler.Count);
                    foreach (var o in ogeler)
                    {
                        if (o.ValueKind == JsonValueKind.String)
                        {
                            kayitlar.Add((null, o.GetString()));
                            continue;
                        }
                        if (o.ValueKind != JsonValueKind.Object) { kayitlar.Add((null, null)); continue; }
                        int? indeks = o.TryGetProperty("indeks", out var ix)
                                      && ix.ValueKind == JsonValueKind.Number
                                      && ix.TryGetInt32(out var n) ? n : null;
                        string? ceviri = o.TryGetProperty("ceviri", out var c)
                                         && c.ValueKind == JsonValueKind.String
                            ? c.GetString() : null;
                        kayitlar.Add((indeks, ceviri));
                    }
                    return CevirileriYerlestir(kayitlar, adet);
                }
            }
        }

        var kurtarilan = KesikJsondanKurtar(temiz, adet);
        if (kurtarilan is not null && kurtarilan.Any(s => !string.IsNullOrEmpty(s)))
            return kurtarilan;
        return null;
    }

    /// <summary>Dolu satır sayısı (kesik yanıt günlük satırı için).</summary>
    public static int DoluSayisi(IReadOnlyList<string?> liste)
    {
        int n = 0;
        foreach (var s in liste) if (!string.IsNullOrEmpty(s)) n++;
        return n;
    }
}
