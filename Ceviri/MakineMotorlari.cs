using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EkranCeviri.Cekirdek;

namespace EkranCeviri.Ceviri;

/// <summary>
/// Ücretsiz motorlar için ortak temel.
///
/// KRİTİK FARK: bu motorlara giden metne ÖNCE
/// <see cref="Lehce.Standartlastir"/> uygulanır. Ham lehçeyi ya hiç
/// çeviremiyorlar ya da TERS anlam üretiyorlar ("bim Bahnhof" →
/// "istasyonun yarısı geldi"). Grok'a ham metin gider — o lehçeyi biliyor.
///
/// Çıktıları kalıcı hafızaya YAZILMAZ: makine çevirisi hafızayı zehirliyor
/// ve yanlış çeviri kalıcı hale geliyordu.
///
/// İKİ YÜZ: <see cref="CevirHamAsync"/>/<see cref="DuzCevirAsync"/> Mac
/// <c>bingCevir/googleCevir</c> gibi hata FIRLATIR (düzenleyici arıza
/// günlüğüne SEBEP yazar); <see cref="IMotor"/> yüzü hatayı yutar.
/// </summary>
public abstract class MakineMotoru : IMotor
{
    protected readonly HttpClient Istemci;
    protected MakineMotoru(HttpClient ag) => Istemci = ag;

    public abstract string Ad { get; }
    public bool AnahtarGerekir => false;

    /// <summary>Girişle aynı uzunlukta ham çıktı; hata fırlatır.</summary>
    protected abstract Task<string?[]> HamCevirAsync(IReadOnlyList<string> metinler,
                                                     string hedef, string kaynak,
                                                     CancellationToken iptal,
                                                     int deneme);

    /// <summary>IMotor yüzü: hatayı yutar, kalite kapılı sonuç döndürür.</summary>
    public Task<CeviriSonuc> CevirAsync(IReadOnlyList<string> metinler,
                                        CeviriBaglam baglam,
                                        CancellationToken iptal) =>
        CevirAsync(metinler, baglam, iptal, 3);

    /// <summary><paramref name="deneme"/>: kullanıcının beklediği yollarda 3;
    /// canlı turda 1 (tur zaten yeniden dener, 3 × 20 sn zinciri canlı
    /// kuyruğu dakikalarca tutuyordu).</summary>
    public async Task<CeviriSonuc> CevirAsync(IReadOnlyList<string> metinler,
                                              CeviriBaglam baglam,
                                              CancellationToken iptal,
                                              int deneme)
    {
        var bos = new CeviriSonuc
        {
            Ceviriler = new string?[metinler.Count],
            MotorAdi = $"{Ad} (yanıt yok)",
            HafizayaYazilabilir = false,
        };
        if (metinler.Count == 0) return bos;

        string?[] ham;
        try
        {
            ham = await CevirHamAsync(metinler, baglam, iptal, deneme).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (iptal.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            Gunluk.Hata($"{Ad}Cevir", e);
            return bos;
        }

        var sonuc = new string?[metinler.Count];
        for (int i = 0; i < metinler.Count; i++)
        {
            var c = i < ham.Length ? ham[i] : null;
            if (string.IsNullOrWhiteSpace(c)) continue;

            // Kalite kapıları: bozuk çıktıyı ekrana basmaktansa boş
            // bırak — orijinal görünür ve Grok devralabilir.
            if (Kalite.KarisikMi(metinler[i], c)) continue;
            if (Kalite.HicCevrilmemis(metinler[i], c)) continue;

            sonuc[i] = Kalite.EmojileriKoru(metinler[i], c);
        }
        return new CeviriSonuc
        {
            Ceviriler = sonuc,
            MotorAdi = Ad,
            HafizayaYazilabilir = false,
        };
    }

    /// <summary>Standartlaştırılmış metin, ayarlardaki yön (kaynak → hedef),
    /// KAPISIZ ham çıktı. Hata fırlatır. Yankı/karışıklık kararı
    /// düzenleyicide verilir (ikinci ücretsiz motor turu için).</summary>
    public Task<string?[]> CevirHamAsync(IReadOnlyList<string> metinler,
                                         CeviriBaglam baglam,
                                         CancellationToken iptal,
                                         int deneme = 3) =>
        DuzCevirAsync(metinler.Select(Lehce.Standartlastir).ToList(),
                      baglam.Ayar.HedefDil, baglam.Ayar.KaynakDilKodu, iptal, deneme);

    /// <summary>Verilen yönde, metne DOKUNMADAN çevirir (giden mesaj:
    /// tr → karşı dil; Türkçe metne lehçe standartlaştırması uygulanmaz —
    /// "au", "no" gibi heceler Almanca sanılıp bozuluyordu). Hata fırlatır.</summary>
    public Task<string?[]> DuzCevirAsync(IReadOnlyList<string> metinler,
                                         string hedef, string kaynak,
                                         CancellationToken iptal,
                                         int deneme = 3) =>
        metinler.Count == 0
            ? Task.FromResult(Array.Empty<string?>())
            : HamCevirAsync(metinler, hedef, kaynak, iptal, Math.Max(1, deneme));

    /// <summary>
    /// Geçici hatalarda (429/5xx, bağlantı kopması) <paramref name="deneme"/>
    /// kez dener; ZAMAN AŞIMI ve kalıcı hata YENİDEN DENENMEZ (zaman aşımı
    /// zaten 20 sn'lik bekleyişin sonucu). Bu motorlar ücretsiz olduğu için
    /// sık 429 veriyor; Retry-After varsa ona uyulur. Son hata FIRLATILIR.
    /// </summary>
    protected async Task<string> GonderAsync(Func<HttpRequestMessage> istekKur,
                                             CancellationToken iptal,
                                             int deneme)
    {
        int toplam = Math.Max(1, deneme);
        for (int d = 0; d < toplam; d++)
        {
            iptal.ThrowIfCancellationRequested();
            HttpResponseMessage yanit;
            string govde;
            try
            {
                using var istek = istekKur();
                using var zamanAsimi = CancellationTokenSource
                    .CreateLinkedTokenSource(iptal);
                zamanAsimi.CancelAfter(TimeSpan.FromSeconds(20));
                yanit = await Istemci.SendAsync(istek, zamanAsimi.Token)
                                     .ConfigureAwait(false);
                try
                {
                    govde = await yanit.Content.ReadAsStringAsync(zamanAsimi.Token)
                                       .ConfigureAwait(false);
                }
                catch
                {
                    // Gövde okuma hatasında yanıt aşağıdaki using'e ulaşamıyor
                    // ve bağlantı/akış sızıyordu (GrokMotor ile aynı desen).
                    yanit.Dispose();
                    throw;
                }
            }
            catch (OperationCanceledException) when (iptal.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                // Zaman aşımı: yeniden denenmez (3 × 20 sn canlı kuyruğu kilitler).
                throw new ZamanAsimiHatasi($"{Ad} isteği zaman aşımına uğradı");
            }
            catch (HttpRequestException e)
            {
                // Bağlantı kopması / DNS: bir daha dene, sonuncuysa fırlat.
                Gunluk.Hata($"{Ad} deneme {d + 1}", e);
                if (d == toplam - 1) throw;
                await Task.Delay(Ag.Bekleme(d, null), iptal).ConfigureAwait(false);
                continue;
            }

            using (yanit)
            {
                if (yanit.IsSuccessStatusCode) return govde;
                Gunluk.Yaz($"{Ad} HTTP {(int)yanit.StatusCode}");
                var hata = new HttpRequestException($"{Ad} HTTP {(int)yanit.StatusCode}",
                                                    null, yanit.StatusCode);
                if (!Ag.GeciciHata(yanit.StatusCode) || d == toplam - 1) throw hata;
                var bekle = Ag.Bekleme(d, Ag.RetryAfter(yanit));
                await Task.Delay(bekle, iptal).ConfigureAwait(false);
            }
        }
        throw new HttpRequestException($"{Ad} yanıt vermedi");
    }
}

/// <summary>
/// Bing (Microsoft Edge) anahtarsız uç noktası. Almanca→Türkçe'de
/// Google'dan tutarlı olduğu ölçüldü; bu yüzden ilk sırada.
/// Jeton ~10 dk geçerli olduğundan önbelleklenir.
/// </summary>
public sealed class BingMotor(HttpClient ag) : MakineMotoru(ag)
{
    private static readonly SemaphoreSlim JetonKilidi = new(1, 1);
    private static string? _jeton;
    private static DateTime _jetonZamani = DateTime.MinValue;

    public override string Ad => "Bing";

    /// <summary>Jeton ve önbellekten mi geldiği. Jeton alınamazsa fırlatır.</summary>
    private async Task<(string Jeton, bool Onbellekten)> JetonAlAsync(CancellationToken iptal,
                                                                     int deneme)
    {
        await JetonKilidi.WaitAsync(iptal).ConfigureAwait(false);
        try
        {
            if (_jeton is not null
                && DateTime.UtcNow - _jetonZamani < TimeSpan.FromSeconds(480))
                return (_jeton, true);

            var metin = await GonderAsync(() =>
            {
                var i = new HttpRequestMessage(HttpMethod.Get,
                    "https://edge.microsoft.com/translate/auth");
                i.Headers.UserAgent.ParseAdd("Mozilla/5.0");
                return i;
            }, iptal, deneme).ConfigureAwait(false);

            // Jeton bir JWT: 100 karakterden kısaysa hata sayfası gelmiştir.
            // FormatException: arıza günlüğünde "bos-yanit" (Mac "bing" alanı).
            if (metin.Length < 100) throw new FormatException("Bing jetonu alınamadı");
            _jeton = metin.Trim();
            _jetonZamani = DateTime.UtcNow;
            return (_jeton, false);
        }
        finally
        {
            JetonKilidi.Release();
        }
    }

    private static void JetonSifirla()
    {
        JetonKilidi.Wait();
        try { _jeton = null; _jetonZamani = DateTime.MinValue; }
        finally { JetonKilidi.Release(); }
    }

    protected override async Task<string?[]> HamCevirAsync(
        IReadOnlyList<string> metinler, string hedef, string kaynak,
        CancellationToken iptal, int deneme)
    {
        // 401/403 = jeton bayatladı. Geçersizleştirilmediği için ücretsiz
        // zincir jeton TTL'i (8 dk) boyunca çökük kalıyordu. Yalnız
        // ÖNBELLEKTEN gelen jetonda bir kez tazelenip yeniden denenir
        // (taze jeton da 401 alırsa hata olduğu gibi çıkar).
        for (int tur = 0; ; tur++)
        {
            var (jeton, onbellekten) = await JetonAlAsync(iptal, deneme).ConfigureAwait(false);
            try
            {
                return await CevirJetonlaAsync(metinler, hedef, kaynak, jeton, iptal, deneme)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException e) when (tur == 0 && onbellekten
                && e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                JetonSifirla();
                Gunluk.Yaz($"Bing jeton reddedildi ({(int)e.StatusCode!}) → tazelenip yeniden deneniyor");
            }
        }
    }

    private async Task<string?[]> CevirJetonlaAsync(IReadOnlyList<string> metinler,
                                                    string hedef, string kaynak,
                                                    string jeton,
                                                    CancellationToken iptal, int deneme)
    {
        // Dil kodları URL'ye KODLANARAK girer: config'ten gelen değer ham
        // eklenince bozuk kodda istek çöküyordu (Mac denetim bulgusu).
        var kaynakEk = string.IsNullOrEmpty(kaynak)
            ? "" : "&from=" + Uri.EscapeDataString(kaynak);
        var adres = "https://api-edge.cognitive.microsofttranslator.com/"
                  + $"translate?api-version=3.0&to={Uri.EscapeDataString(hedef)}{kaynakEk}";
        var yuk = new JsonArray(metinler.Select(m =>
            (JsonNode)new JsonObject { ["Text"] = m }).ToArray()).ToJsonString();

        var metin = await GonderAsync(() =>
        {
            var i = new HttpRequestMessage(HttpMethod.Post, adres);
            i.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
            i.Content = new StringContent(yuk, Encoding.UTF8, "application/json");
            return i;
        }, iptal, deneme).ConfigureAwait(false);

        using var belge = JsonDocument.Parse(metin);   // JsonException → "bos-yanit"
        if (belge.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("Bing yanıtı çözülemedi");
        var sonuc = new string?[metinler.Count];
        int i2 = 0;
        foreach (var oge in belge.RootElement.EnumerateArray())
        {
            if (i2 >= sonuc.Length) break;
            if (oge.TryGetProperty("translations", out var c)
                && c.ValueKind == JsonValueKind.Array
                && c.GetArrayLength() > 0
                && c[0].TryGetProperty("text", out var t))
                sonuc[i2] = t.GetString();
            i2++;
        }
        return sonuc;
    }
}

/// <summary>Google'ın anahtarsız clients5 uç noktası — tüm metinler TEK
/// istekte gider.</summary>
public sealed class GoogleMotor(HttpClient ag) : MakineMotoru(ag)
{
    public override string Ad => "Google";

    protected override async Task<string?[]> HamCevirAsync(
        IReadOnlyList<string> metinler, string hedef, string kaynak,
        CancellationToken iptal, int deneme)
    {
        var sl = string.IsNullOrEmpty(kaynak) ? "auto" : kaynak;
        var adres = "https://clients5.google.com/translate_a/t"
                  + $"?client=dict-chrome-ex&sl={Uri.EscapeDataString(sl)}"
                  + $"&tl={Uri.EscapeDataString(hedef)}";
        var govde = string.Join("&", metinler.Select(
            m => "q=" + Uri.EscapeDataString(m)));

        var metin = await GonderAsync(() =>
        {
            var i = new HttpRequestMessage(HttpMethod.Post, adres);
            i.Headers.UserAgent.ParseAdd("Mozilla/5.0");
            i.Content = new StringContent(govde, Encoding.UTF8,
                                          "application/x-www-form-urlencoded");
            return i;
        }, iptal, deneme).ConfigureAwait(false);

        using var belge = JsonDocument.Parse(metin);   // JsonException → "bos-yanit"
        var kok = belge.RootElement;
        var sonuc = new string?[metinler.Count];
        // Tek öğeli istekte uç nokta düz dizge de döndürebiliyor.
        if (metinler.Count == 1 && kok.ValueKind == JsonValueKind.String)
        {
            sonuc[0] = kok.GetString();
            return sonuc;
        }
        if (kok.ValueKind != JsonValueKind.Array)
            throw new JsonException("Google yanıtı çözülemedi");

        int i2 = 0;
        foreach (var oge in kok.EnumerateArray())
        {
            if (i2 >= sonuc.Length) break;
            // Yanıt ya düz dize ya da [çeviri, algılanan_dil] ikilisi
            sonuc[i2] = oge.ValueKind switch
            {
                JsonValueKind.String => oge.GetString(),
                JsonValueKind.Array when oge.GetArrayLength() > 0
                    && oge[0].ValueKind == JsonValueKind.String
                    => oge[0].GetString(),
                _ => null,
            };
            i2++;
        }
        return sonuc;
    }
}
