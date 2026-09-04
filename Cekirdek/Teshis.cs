using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace EkranCeviri.Cekirdek;

/// <summary>
/// Sayaç + süre ölçümü + ana iş parçacığı bekçisi. 60 sn'de bir tek satırlık
/// özet dosyaya düşer: kullanıcının "donma" dediği şeyin somut kaydı.
/// WPF'e bağımlı DEĞİL — ana kuyruk <see cref="Baslat"/>'a delegeyle
/// verilir; saf koşucuda da derlenir. Gunluk'a bağımlı değil (aynı sebep).
/// </summary>
public sealed class Teshis
{
    public static Teshis Paylasilan { get; } = new();

    /// <summary>Teşhis dosyası dizini; varsayılan %APPDATA%\EkranCeviri.
    /// Testler geçici dizine yönlendirir — kullanıcının gerçek izine yazmasın.</summary>
    public static string Dizin { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EkranCeviri");

    public const string DosyaAdi = "teshis.log";
    private const long TavanBayt = 2_000_000;
    public static string DosyaYolu => Path.Combine(Dizin, DosyaAdi);

    /// <summary>Aşama etiketi hiç sıfırlanmıyordu: saatler önce biten "ilk
    /// çeviri" bugünkü TAKILMA satırına yazılıyordu. Bayat etiket "boşta"ya döner.</summary>
    public static readonly TimeSpan AsamaTazelik = TimeSpan.FromSeconds(30);

    private readonly object _kilit = new();
    private string _asama = "başlangıç";
    private DateTime _asamaZamani = DateTime.MinValue;

    private readonly Dictionary<string, int> _sayac = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _sureMs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _enUzunMs = new(StringComparer.Ordinal);
    private readonly List<(double Ms, string Asama)> _takilmalar = [];

    private Thread? _bekci;
    private Timer? _zamanlayici;
    private volatile bool _acik;

    public static string AsamaEtiketi(string ad, DateTime baslangic, DateTime simdi) =>
        simdi - baslangic <= AsamaTazelik ? ad : "boşta";

    /// <summary>Uygulamanın o an ne yaptığı — takılma kaydına düşer.</summary>
    public string Asama
    {
        get
        {
            lock (_kilit) return AsamaEtiketi(_asama, _asamaZamani, DateTime.UtcNow);
        }
        set
        {
            lock (_kilit) { _asama = value; _asamaZamani = DateTime.UtcNow; }
        }
    }

    /// <summary>Bir işi ölçer ve sayar. Sonucu aynen döndürür.</summary>
    public T Olc<T>(string ad, Func<T> is_)
    {
        var t0 = Stopwatch.GetTimestamp();
        try { return is_(); }
        finally { Kaydet(ad, Stopwatch.GetElapsedTime(t0).TotalMilliseconds); }
    }

    public async Task<T> OlcAsync<T>(string ad, Func<Task<T>> is_)
    {
        var t0 = Stopwatch.GetTimestamp();
        try { return await is_().ConfigureAwait(false); }
        finally { Kaydet(ad, Stopwatch.GetElapsedTime(t0).TotalMilliseconds); }
    }

    private void Kaydet(string ad, double ms)
    {
        lock (_kilit)
        {
            _sayac[ad] = (_sayac.TryGetValue(ad, out var n) ? n : 0) + 1;
            _sureMs[ad] = (_sureMs.TryGetValue(ad, out var t) ? t : 0) + ms;
            _enUzunMs[ad] = Math.Max(_enUzunMs.TryGetValue(ad, out var e) ? e : 0, ms);
        }
    }

    public void Say(string ad)
    {
        lock (_kilit) _sayac[ad] = (_sayac.TryGetValue(ad, out var n) ? n : 0) + 1;
    }

    /// <summary>Sayacın o anki değeri (yalnız okuma). 60 sn'lik özet satırı bu
    /// sözlüğün TÜM anahtarlarını basar; test bunu buradan doğrular.</summary>
    public int SayacDegeri(string ad)
    {
        lock (_kilit) return _sayac.TryGetValue(ad, out var n) ? n : 0;
    }

    /// <summary>
    /// Ana iş parçacığı bekçisi: 250 ms'de bir ping; yanıt 200 ms'i aşarsa
    /// takılma (o anki aşama etiketiyle). 60 sn'de bir özet satırı.
    /// <paramref name="anaKuyrugaGonder"/> verilen işi arayüz kuyruğuna
    /// atar (WPF: Dispatcher.BeginInvoke) — bu sınıf WPF'i tanımaz.
    /// </summary>
    public void Baslat(Action<Action> anaKuyrugaGonder)
    {
        if (_acik) return;
        _acik = true;

        var t = new Thread(() =>
        {
            while (_acik)
            {
                var gonderim = Stopwatch.GetTimestamp();
                using var sema = new SemaphoreSlim(0, 1);
                try
                {
                    anaKuyrugaGonder(() =>
                    {
                        double gecikme = Stopwatch.GetElapsedTime(gonderim).TotalMilliseconds;
                        if (gecikme > 200)
                        {
                            lock (_kilit)
                            {
                                _takilmalar.Add((gecikme,
                                    AsamaEtiketi(_asama, _asamaZamani, DateTime.UtcNow)));
                                if (_takilmalar.Count > 200) _takilmalar.RemoveRange(0, 100);
                            }
                        }
                        try { sema.Release(); } catch (ObjectDisposedException) { }
                    });
                    // 3 sn içinde yanıt yoksa uygulama ciddi donmuş demektir;
                    // döngüyü kilitlemeden devam et (kayıt yukarıda düşer).
                    sema.Wait(TimeSpan.FromSeconds(3));
                }
                catch
                {
                    // Kuyruk kapanmış olabilir (uygulama çıkıyor): bekçi durur.
                }
                Thread.Sleep(250);
            }
        })
        {
            Name = "ekranceviri.teshis",
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
        };
        t.Start();
        _bekci = t;

        _zamanlayici = new Timer(_ => Yaz(), null,
                                 TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    public void Durdur()
    {
        _acik = false;
        _zamanlayici?.Dispose();
        _zamanlayici = null;
        Yaz();
    }

    /// <summary>60 sn'de bir tek satır: sayaçlar, ortalama/en uzun süreler,
    /// takılmalar. KALP ATIŞI: sayaç boşken satır yazılmıyordu; 2 saatlik
    /// sessizlik "kullanıcı çalışmadı" ile "uygulama dondu" arasında ayrım
    /// bırakmıyordu. Boş kova da tek satır ("boşta") yazar.</summary>
    private void Yaz()
    {
        Dictionary<string, int> s;
        Dictionary<string, double> m, e;
        List<(double Ms, string Asama)> t;
        lock (_kilit)
        {
            s = new Dictionary<string, int>(_sayac, StringComparer.Ordinal);
            m = new Dictionary<string, double>(_sureMs, StringComparer.Ordinal);
            e = new Dictionary<string, double>(_enUzunMs, StringComparer.Ordinal);
            t = [.. _takilmalar];
            _sayac.Clear(); _sureMs.Clear(); _enUzunMs.Clear(); _takilmalar.Clear();
        }

        var parcalar = new List<string>();
        if (s.Count == 0 && t.Count == 0) parcalar.Add("boşta");
        foreach (var ad in s.Keys.Order(StringComparer.Ordinal))
        {
            int n = s[ad];
            if (m.TryGetValue(ad, out var toplam) && n > 0)
                parcalar.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0}={1}(ort {2:0}ms, en {3:0}ms)", ad, n, toplam / n,
                    e.TryGetValue(ad, out var en) ? en : 0));
            else
                parcalar.Add($"{ad}={n}");
        }
        if (t.Count > 0)
        {
            var enKotu = t[0];
            foreach (var k in t) if (k.Ms > enKotu.Ms) enKotu = k;
            parcalar.Add(string.Format(CultureInfo.InvariantCulture,
                "takilma={0}(en {1:0}ms @{2})", t.Count, enKotu.Ms, enKotu.Asama));
        }

        var satir = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    + " " + string.Join(" · ", parcalar) + Environment.NewLine;
        try
        {
            Directory.CreateDirectory(Dizin);
            var yol = DosyaYolu;
            // Şişmesin: aylarca açık kalıyor. Tek nesil (.1) döndürülür.
            if (File.Exists(yol) && new FileInfo(yol).Length > TavanBayt)
                File.Move(yol, yol + ".1", overwrite: true);
            File.AppendAllText(yol, satir, Encoding.UTF8);
        }
        catch
        {
            // Teşhis yazamamak uygulamayı durdurmaz.
        }
    }
}
