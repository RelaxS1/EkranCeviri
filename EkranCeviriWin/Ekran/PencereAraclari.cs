using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;

namespace EkranCeviri.Ekran;

/// <summary>
/// Pencere konumlandırma. WPF'in <c>Left</c>/<c>Top</c> özellikleri
/// cihazdan bağımsız birimdedir (DIU); farklı ölçekli iki ekran varsa
/// pencere yanlış monitöre veya kaydırılmış konuma düşüyor. Fiziksel
/// piksel her ekranda tek anlamlıdır — bu yüzden konumlandırmayı Win32
/// ile yapıyoruz.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PencereAraclari
{
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                            int X, int Y, int cx, int cy,
                                            uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    /// <summary>Pencereyi FİZİKSEL piksel dikdörtgenine oturtur.</summary>
    public static void Konumlandir(IntPtr pencere, Rect fizikselKutu)
    {
        if (pencere == IntPtr.Zero) return;
        SetWindowPos(pencere, IntPtr.Zero,
                     (int)Math.Round(fizikselKutu.X),
                     (int)Math.Round(fizikselKutu.Y),
                     (int)Math.Round(fizikselKutu.Width),
                     (int)Math.Round(fizikselKutu.Height),
                     SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>Tüm ekranları kapsayan sanal masaüstü, fiziksel pikselde.
    /// Çok ekranlı kurulumda sol/üst NEGATİF olabilir — bu yüzden
    /// genişlik/yüksekliği tek başına kullanmak yetmez.</summary>
    public static Rect TumMasaustu() => new(
        GetSystemMetrics(SM_XVIRTUALSCREEN),
        GetSystemMetrics(SM_YVIRTUALSCREEN),
        Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN)),
        Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN)));
}
