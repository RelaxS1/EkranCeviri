using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using EkranCeviri.Cekirdek;

namespace EkranCeviri.Kisayol;

/// <summary>
/// Klavye taklidi ve pano. macOS'ta bu iş Erişilebilirlik izni istiyordu;
/// Windows'ta izin GEREKMEZ (yalnız hedef uygulama yönetici olarak
/// çalışıyorsa UIPI engeller).
///
/// VERİ KAYBI KORUMASI: Ctrl+A tüm belgeyi seçer. Yanlış uygulamada
/// tetiklenirse kullanıcının belgesini YOK EDER. Bu yüzden hem uygulama
/// denetimi hem uzunluk sınırı var.
/// </summary>
[SupportedOSPlatform("windows")]
public static class Klavye
{
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_A = 0x41;
    private const ushort VK_C = 0x43;
    private const ushort VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const int INPUT_KEYBOARD = 1;

    /// <summary>Bu sınırı aşan seçim mesaj kutusu değil BELGEDİR:
    /// işlemi iptal et. (macOS'ta bu koruma konulana kadar yanlış
    /// pencerede tetiklenen kısayol veri kaybı riski taşıyordu.)</summary>
    private const int EnFazlaKarakter = 1200;
    private const int EnFazlaSatir = 15;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputBirlesim U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputBirlesim
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        // Birleşim boyutu en büyük üyeye göre belirlenir; MOUSEINPUT
        // (x64'te 32 bayt) yer tutucusu olmadan SendInput boyut hatası
        // verip sessizce hiçbir şey yapmıyor.
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd, out uint lpdwProcessId);

    private static void TusGonder(ushort tus, bool birak)
    {
        var girdi = new INPUT[1];
        girdi[0].type = INPUT_KEYBOARD;
        girdi[0].U.ki = new KEYBDINPUT
        {
            wVk = tus,
            dwFlags = birak ? KEYEVENTF_KEYUP : 0,
        };
        if (SendInput(1, girdi, Marshal.SizeOf<INPUT>()) == 0)
            Gunluk.Yaz($"SendInput başarısız: {Marshal.GetLastWin32Error()}");
    }

    private static async Task KisayolGonderAsync(ushort tus)
    {
        TusGonder(VK_CONTROL, false);
        TusGonder(tus, false);
        await Task.Delay(25).ConfigureAwait(false);
        TusGonder(tus, true);
        TusGonder(VK_CONTROL, true);
    }

    /// <summary>Ön plandaki uygulamanın exe adı (uzantısız, küçük harf).</summary>
    public static string OnPlandakiUygulama()
    {
        try
        {
            var pencere = GetForegroundWindow();
            if (pencere == IntPtr.Zero) return "";
            GetWindowThreadProcessId(pencere, out uint pid);
            if (pid == 0) return "";
            using var islem = Process.GetProcessById((int)pid);
            return islem.ProcessName.ToLowerInvariant();
        }
        catch (Exception e)
        {
            Gunluk.Hata("önPlandakiUygulama", e);
            return "";
        }
    }

    private static readonly string[] Mesajlasma =
    [
        "whatsapp", "telegram", "discord", "ferdium", "signal", "slack",
        "teams", "thunderbird", "franz", "rambox", "element", "viber",
        "messenger", "skype", "wechat", "line", "threema", "session",
        // Web istemcileri: web.whatsapp.com tarayıcıda açılıyor
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "arc",
    ];

    public static bool MesajlasmaUygulamasiMi(string exe)
    {
        if (string.IsNullOrEmpty(exe)) return false;
        return Mesajlasma.Any(m => exe.Contains(m, StringComparison.Ordinal));
    }

    /// <summary>
    /// Mesaj kutusundaki metni alır (Ctrl+A, Ctrl+C). Kullanıcının eski
    /// panosu korunur ve işlem başarısızsa geri konur.
    /// Uzunluk sınırını aşan seçim BELGEDİR: null döner, hiçbir şey yapılmaz.
    /// </summary>
    public static async Task<string?> SecKopyalaAsync(CancellationToken iptal)
    {
        var eskiPano = await PanoOkuAsync().ConfigureAwait(false);
        try
        {
            // Panoyu BOŞALT: kopyalama başarısız olursa eski içeriği
            // "kullanıcının yazdığı metin" sanıp çevirmemeliyiz.
            await PanoYazAsync("").ConfigureAwait(false);

            await KisayolGonderAsync(VK_A).ConfigureAwait(false);
            await Task.Delay(40, iptal).ConfigureAwait(false);
            await KisayolGonderAsync(VK_C).ConfigureAwait(false);

            // Panonun DEĞİŞMESİNİ bekle. Sabit uyku hem yavaş hem güvensiz:
            // yavaş makinede erken okuyup boş dönüyordu.
            string? metin = null;
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(50, iptal).ConfigureAwait(false);
                metin = await PanoOkuAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(metin)) break;
            }

            if (string.IsNullOrWhiteSpace(metin))
            {
                await PanoYazAsync(eskiPano ?? "").ConfigureAwait(false);
                return null;
            }

            if (metin.Length > EnFazlaKarakter
                || metin.Count(c => c == '\n') > EnFazlaSatir)
            {
                // Belge seçilmiş: dokunma, eski panoyu geri koy, iptal et.
                Gunluk.Yaz($"giden: seçim çok uzun ({metin.Length} karakter) "
                         + "— veri kaybı koruması devrede");
                await PanoYazAsync(eskiPano ?? "").ConfigureAwait(false);
                return null;
            }
            return metin;
        }
        catch (Exception e)
        {
            Gunluk.Hata("seçKopyala", e);
            await PanoYazAsync(eskiPano ?? "").ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Metni yapıştırır ve kullanıcının eski panosunu geri yükler.
    /// Geri yükleme GECİKMELİ: hemen yaparsak Ctrl+V boş yapıştırıyor.</summary>
    public static async Task YapistirAsync(string metin, CancellationToken iptal)
    {
        var eskiPano = await PanoOkuAsync().ConfigureAwait(false);
        await PanoYazAsync(metin).ConfigureAwait(false);
        await Task.Delay(60, iptal).ConfigureAwait(false);
        await KisayolGonderAsync(VK_V).ConfigureAwait(false);

        if (string.IsNullOrEmpty(eskiPano)) return;
        // Arka planda geri yükle: kullanıcıyı bekletme
        _ = Task.Run(async () =>
        {
            await Task.Delay(800).ConfigureAwait(false);
            await PanoYazAsync(eskiPano).ConfigureAwait(false);
        });
    }

    // ---- pano: STA iş parçacığı ister ve BAŞKA uygulama kilitlemiş olabilir

    private static Task<string?> PanoOkuAsync() =>
        StaCalistir<string?>(() =>
            Clipboard.ContainsText() ? Clipboard.GetText() : null);

    private static Task PanoYazAsync(string metin) =>
        StaCalistir<object?>(() =>
        {
            if (string.IsNullOrEmpty(metin)) Clipboard.Clear();
            else Clipboard.SetText(metin);
            return null;
        });

    /// <summary>
    /// Pano işlemini STA iş parçacığında ve 3 denemeyle çalıştırır.
    /// Windows'ta panoyu tek seferde tek işlem açabilir; başka bir uygulama
    /// (Office, tarayıcı) o an tutuyorsa çağrı hata veriyor.
    /// </summary>
    private static Task<T> StaCalistir<T>(Func<T> is_)
    {
        var kaynak = new TaskCompletionSource<T>();
        var iparcacigi = new Thread(() =>
        {
            for (int deneme = 0; deneme < 3; deneme++)
            {
                try { kaynak.TrySetResult(is_()); return; }
                catch (Exception e)
                {
                    if (deneme == 2)
                    {
                        Gunluk.Hata("pano", e);
                        kaynak.TrySetResult(default!);
                        return;
                    }
                    Thread.Sleep(60);
                }
            }
        });
        iparcacigi.SetApartmentState(ApartmentState.STA);
        iparcacigi.IsBackground = true;
        iparcacigi.Start();
        return kaynak.Task;
    }
}
