using System.Net;
using System.Net.Http;

namespace EkranCeviri.Cekirdek;

/// <summary>
/// Paylaşılan HTTP istemcisi. macOS sürümünde <c>URLSession.shared</c>
/// bilgisayar uykudan kalktıktan sonra bayat bağlantı havuzu yüzünden
/// "ağ hatası" üretiyordu; çözüm kendi oturumumuzu kurmaktı. Aynı dert
/// .NET'te <c>PooledConnectionLifetime</c> ile çözülür: bağlantılar
/// belirli aralıkla tazelenir, DNS ve uyku sonrası ölü soketler takılmaz.
/// </summary>
public static class Ag
{
    public static HttpClient Istemci { get; } = Kur();

    private static HttpClient Kur()
    {
        var tasiyici = new SocketsHttpHandler
        {
            // Uyku sonrası ölü bağlantı sorununun asıl çözümü
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            AutomaticDecompression = DecompressionMethods.All,
            MaxConnectionsPerServer = 8,
            // Kimlik bilgisi taşımıyoruz; çerez de istemiyoruz
            UseCookies = false,
        };

        var istemci = new HttpClient(tasiyici)
        {
            // Tek tek isteklerde ayrıca CancellationToken kullanılıyor;
            // bu üst sınır sadece son emniyet.
            Timeout = TimeSpan.FromSeconds(90),
        };
        istemci.DefaultRequestHeaders.Add("User-Agent", "EkranCeviri/1.0");
        return istemci;
    }

    /// <summary>Geçici hata mı? (yeniden denemeye değer) Yalnız 429/5xx.
    /// Zaman aşımı ve iptal BURADAN GEÇMEZ: zaman aşımı zaten 30 sn'lik bir
    /// bekleyişin sonucudur (3 deneme = 95 sn kilit, macOS kara-delik
    /// deneyinde ölçüldü).</summary>
    public static bool GeciciHata(HttpStatusCode kod) =>
        kod == HttpStatusCode.TooManyRequests || (int)kod >= 500;

    /// <summary>Kalıcı hata mı? (yeniden denemek anlamsız — anahtar yanlış)</summary>
    public static bool KaliciHata(HttpStatusCode kod) =>
        kod is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            or HttpStatusCode.PaymentRequired;

    /// <summary>Retry-After tavanı: sunucu dakikalar isterse canlı kuyruğu
    /// o kadar tutmayız.</summary>
    private static readonly TimeSpan RetryAfterTavani = TimeSpan.FromSeconds(30);

    /// <summary>Sunucunun önerdiği bekleme (Retry-After, saniye ya da tarih),
    /// 30 sn ile sınırlı. macOS'ta 429'da sabit 0,6/1,8 sn ile geri dönmek
    /// sunucunun istediğinden erkendi ve ikinci deneme de 429 alıyordu.</summary>
    public static TimeSpan? RetryAfter(HttpResponseMessage yanit)
    {
        var ra = yanit.Headers.RetryAfter;
        if (ra is null) return null;
        if (ra.Delta is { } d && d > TimeSpan.Zero)
            return d > RetryAfterTavani ? RetryAfterTavani : d;
        if (ra.Date is { } t)
        {
            var fark = t - DateTimeOffset.UtcNow;
            if (fark > TimeSpan.Zero)
                return fark > RetryAfterTavani ? RetryAfterTavani : fark;
        }
        return null;
    }

    /// <summary>Geçici hatada bekleme: sunucu önerisi varsa o, yoksa 0,6 sn
    /// sonra 1,8 sn (ölçülen: ikinci deneme çoğunlukla yetiyor).</summary>
    public static TimeSpan Bekleme(int deneme, TimeSpan? onerilen) =>
        onerilen ?? TimeSpan.FromSeconds(deneme == 0 ? 0.6 : 1.8);
}
