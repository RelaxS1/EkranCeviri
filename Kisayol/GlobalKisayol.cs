using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Interop;
using EkranCeviri.Cekirdek;

namespace EkranCeviri.Kisayol;

/// <summary>
/// Global kısayol (varsayılan Ctrl+Alt+C). Uygulama arka plandayken de
/// çalışır — macOS'ta Carbon <c>RegisterEventHotKey</c> ile yapılan işin
/// Windows karşılığı, ama çok daha basit: izin gerekmez.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GlobalKisayol : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int KimlikNo = 0xEC01;
    /// <summary>Tuş basılı tutulunca klavye tekrarı ikinci/üçüncü WM_HOTKEY
    /// üretiyordu; ayrıca Alt hâlâ basılıyken enjekte edilen Ctrl+C kayıtlı
    /// Ctrl+Alt+C ile eşleşip kendi kısayolumuzu tetikleyebiliyordu.
    /// Ayarlar.Dogrula modu 0x000F'e kırptığı için bit burada eklenir.</summary>
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id,
                                              uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _kaynak;
    private Action? _geriCagri;
    private bool _kayitli;

    /// <summary>
    /// Kısayolu kaydeder. Zaten başka bir uygulama almışsa <c>false</c>
    /// döner — SESSİZCE BAŞARISIZ OLMAZ, üst katman kullanıcıya söyler.
    /// (Ctrl+Alt+C'yi alan uygulamalar sık; kullanıcı "çalışmıyor" diye
    /// düşünmemeli.)
    /// </summary>
    public bool Kaydet(uint mod, uint tus, Action geriCagri)
    {
        Kaldir();
        _geriCagri = geriCagri;
        try
        {
            // Yalnız-mesaj penceresi: görünmez, görev çubuğunda yok,
            // yalnızca WM_HOTKEY almak için var.
            var parametre = new HwndSourceParameters("EkranCeviriKisayol")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0,
                // HWND_MESSAGE = -3
                ParentWindow = new IntPtr(-3),
            };
            _kaynak = new HwndSource(parametre);
            _kaynak.AddHook(Kanca);

            _kayitli = RegisterHotKey(_kaynak.Handle, KimlikNo, mod | MOD_NOREPEAT, tus);
            if (!_kayitli)
            {
                Gunluk.Yaz("kısayol alınamadı: hata "
                         + Marshal.GetLastWin32Error());
                Kaldir();
            }
            return _kayitli;
        }
        catch (Exception e)
        {
            Gunluk.Hata("kısayolKaydet", e);
            Kaldir();
            return false;
        }
    }

    private IntPtr Kanca(IntPtr hwnd, int mesaj, IntPtr wParam, IntPtr lParam,
                         ref bool islendi)
    {
        if (mesaj != WM_HOTKEY || wParam.ToInt32() != KimlikNo) return IntPtr.Zero;
        islendi = true;
        try
        {
            // Geri çağrı ARAYÜZ iş parçacığında çalışır: içinden pencere
            // açılabiliyor ve arayüz durumu okunuyor.
            _geriCagri?.Invoke();
        }
        catch (Exception e)
        {
            Gunluk.Hata("kısayolGeriÇağrı", e);
        }
        return IntPtr.Zero;
    }

    public void Kaldir()
    {
        try
        {
            if (_kayitli && _kaynak is not null)
                UnregisterHotKey(_kaynak.Handle, KimlikNo);
        }
        catch (Exception e) { Gunluk.Hata("kısayolKaldır", e); }
        _kayitli = false;

        if (_kaynak is not null)
        {
            _kaynak.RemoveHook(Kanca);
            _kaynak.Dispose();
            _kaynak = null;
        }
    }

    public void Dispose() => Kaldir();
}
