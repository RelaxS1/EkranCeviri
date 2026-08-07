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
/// </summary>
public abstract class MakineMotoru : IMotor
{
    protected readonly HttpClient Istemci;
    protected MakineMotoru(HttpClient ag) => Istemci = ag;

    public abstract string Ad { get; }
    public bool AnahtarGerekir => false;

    protected abstract Task<string?[]?> HamCevirAsync(IReadOnlyList<string> metinler,
                                                      string hedef, string kaynak,
                                                      CancellationToken iptal);

    public async Task<CeviriSonuc> CevirAsync(IReadOnlyList<string> metinler,
                                              CeviriBaglam baglam,
                                              CancellationToken iptal)
    {
        var bos = new CeviriSonuc
        {
            Ceviriler = new string?[metinler.Count],
            MotorAdi = $"{Ad} (yanıt yok)",
            HafizayaYazilabilir = false,
        };
        if (metinler.Count == 0) return bos;

        try
        {
            var hazir = metinler.Select(Lehce.Standartlastir).ToList();
            // Kaynak dili ZORLA: otomatik algılama lehçeyi bazen Malayca
            // sanıp metni hiç çevirmiyordu (canlı testte doğrulandı).
            var ham = await HamCevirAsync(hazir, baglam.Ayar.HedefDil,
                                          baglam.Ayar.KaynakDilKodu, iptal)
                            .ConfigureAwait(false);
            if (ham is null) return bos;

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
        catch (Exception e)
        {
            Gunluk.Hata($"{Ad}Cevir", e);
            return bos;
        }
    }

    /// <summary>Geçici hatalarda 3 deneme. Bu motorlar ücretsiz olduğu için
    /// sık 429 veriyor; beklemeden tekrar denemek işe yaramıyor.</summary>
    protected async Task<string?> GonderAsync(Func<HttpRequestMessage> istekKur,
                                              CancellationToken iptal)
    {
        double[] bekleme = [0.6, 1.8];
        for (int deneme = 0; deneme < 3; deneme++)
        {
            iptal.ThrowIfCancellationRequested();
            try
            {
                using var istek = istekKur();
                using var zamanAsimi = CancellationTokenSource
                    .CreateLinkedTokenSource(iptal);
                zamanAsimi.CancelAfter(TimeSpan.FromSeconds(20));

                using var yanit = await Istemci.SendAsync(istek, zamanAsimi.Token)
                                          .ConfigureAwait(false);
                if (yanit.IsSuccessStatusCode)
                    return await yanit.Content.ReadAsStringAsync(iptal)
                                      .ConfigureAwait(false);

                Gunluk.Yaz($"{Ad} HTTP {(int)yanit.StatusCode}");
                if (!Ag.GeciciHata(yanit.StatusCode)) return null;
            }
            catch (OperationCanceledException) when (iptal.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                Gunluk.Hata($"{Ad} deneme {deneme + 1}", e);
            }

            if (deneme < bekleme.Length)
                await Task.Delay(TimeSpan.FromSeconds(bekleme[deneme]), iptal)
                          .ConfigureAwait(false);
        }
        return null;
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

    private async Task<string?> JetonAlAsync(CancellationToken iptal)
    {
        await JetonKilidi.WaitAsync(iptal).ConfigureAwait(false);
        try
        {
            if (_jeton is not null
                && DateTime.UtcNow - _jetonZamani < TimeSpan.FromSeconds(480))
                return _jeton;

            var metin = await GonderAsync(() =>
            {
                var i = new HttpRequestMessage(HttpMethod.Get,
                    "https://edge.microsoft.com/translate/auth");
                i.Headers.UserAgent.ParseAdd("Mozilla/5.0");
                return i;
            }, iptal).ConfigureAwait(false);

            // Jeton bir JWT: 100 karakterden kısaysa hata sayfası gelmiştir
            if (metin is null || metin.Length < 100) return null;
            _jeton = metin.Trim();
            _jetonZamani = DateTime.UtcNow;
            return _jeton;
        }
        finally
        {
            JetonKilidi.Release();
        }
    }

    protected override async Task<string?[]?> HamCevirAsync(
        IReadOnlyList<string> metinler, string hedef, string kaynak,
        CancellationToken iptal)
    {
        var jeton = await JetonAlAsync(iptal).ConfigureAwait(false);
        if (jeton is null) return null;

        var kaynakEk = string.IsNullOrEmpty(kaynak) ? "" : $"&from={kaynak}";
        var adres = "https://api-edge.cognitive.microsofttranslator.com/"
                  + $"translate?api-version=3.0&to={hedef}{kaynakEk}";
        var yuk = new JsonArray(metinler.Select(m =>
            (JsonNode)new JsonObject { ["Text"] = m }).ToArray()).ToJsonString();

        var metin = await GonderAsync(() =>
        {
            var i = new HttpRequestMessage(HttpMethod.Post, adres);
            i.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
            i.Content = new StringContent(yuk, Encoding.UTF8, "application/json");
            return i;
        }, iptal).ConfigureAwait(false);
        if (metin is null) return null;

        try
        {
            using var belge = JsonDocument.Parse(metin);
            var sonuc = new string?[metinler.Count];
            int i2 = 0;
            foreach (var oge in belge.RootElement.EnumerateArray())
            {
                if (i2 >= sonuc.Length) break;
                if (oge.TryGetProperty("translations", out var c)
                    && c.GetArrayLength() > 0
                    && c[0].TryGetProperty("text", out var t))
                    sonuc[i2] = t.GetString();
                i2++;
            }
            return sonuc;
        }
        catch (Exception e)
        {
            Gunluk.Hata("bingÇöz", e);
            return null;
        }
    }
}

/// <summary>Google'ın anahtarsız clients5 uç noktası — tüm metinler TEK
/// istekte gider.</summary>
public sealed class GoogleMotor(HttpClient ag) : MakineMotoru(ag)
{
    public override string Ad => "Google";

    protected override async Task<string?[]?> HamCevirAsync(
        IReadOnlyList<string> metinler, string hedef, string kaynak,
        CancellationToken iptal)
    {
        var sl = string.IsNullOrEmpty(kaynak) ? "auto" : kaynak;
        var adres = "https://clients5.google.com/translate_a/t"
                  + $"?client=dict-chrome-ex&sl={sl}&tl={hedef}";
        var govde = string.Join("&", metinler.Select(
            m => "q=" + Uri.EscapeDataString(m)));

        var metin = await GonderAsync(() =>
        {
            var i = new HttpRequestMessage(HttpMethod.Post, adres);
            i.Headers.UserAgent.ParseAdd("Mozilla/5.0");
            i.Content = new StringContent(govde, Encoding.UTF8,
                                          "application/x-www-form-urlencoded");
            return i;
        }, iptal).ConfigureAwait(false);
        if (metin is null) return null;

        try
        {
            using var belge = JsonDocument.Parse(metin);
            var kok = belge.RootElement;
            if (kok.ValueKind != JsonValueKind.Array) return null;

            var sonuc = new string?[metinler.Count];
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
        catch (Exception e)
        {
            Gunluk.Hata("googleÇöz", e);
            return null;
        }
    }
}
