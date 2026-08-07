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

    /// <summary>Geçici hata mı? (yeniden denemeye değer)</summary>
    public static bool GeciciHata(HttpStatusCode kod) =>
        kod == HttpStatusCode.TooManyRequests || (int)kod >= 500;

    /// <summary>Kalıcı hata mı? (yeniden denemek anlamsız — anahtar yanlış)</summary>
    public static bool KaliciHata(HttpStatusCode kod) =>
        kod is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            or HttpStatusCode.PaymentRequired;
}
