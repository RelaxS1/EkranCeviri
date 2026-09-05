using System.Runtime.Versioning;
using System.Threading;
using System.Windows;
using EkranCeviri.Cekirdek;

namespace EkranCeviri;

/// <summary>
/// Giriş noktası. XAML yerine düz C#: tek dosyalık .exe dağıtımında
/// yükleyecek kaynak azaldıkça hata yüzeyi de azalıyor.
/// </summary>
public static class Program
{
    private static Mutex? _tekOrnek;

    [STAThread]
    [SupportedOSPlatform("windows10.0.19041.0")]
    public static int Main()
    {
        // TEK ÖRNEK: iki kopya çalışırsa ikisi de aynı kısayolu almaya
        // çalışıp biri sessizce ölüyor ve kullanıcı "çalışmıyor" sanıyor.
        _tekOrnek = new Mutex(true, @"Local\EkranCeviri.TekOrnek", out bool ilk);
        if (!ilk)
        {
            MessageBox.Show(
                "Ekran Çeviri zaten çalışıyor. Simgesi saatin yanındaki "
                + "gizli simgeler alanında (küçük ok) olabilir.",
                "Ekran Çeviri", MessageBoxButton.OK, MessageBoxImage.Information);
            return 0;
        }

        // --tani BAYRAĞI (GİZLİLİK): arıza günlüğüne mesaj METNİ yalnız bu
        // bayrakla girer; bayrak okunmazsa TaniModu hep false kalır ve teşhis
        // istense de metin yazılmaz. Ayarlar.cs'teki --sinama ile aynı biçim.
        ArizaGunlugu.TaniModu = Environment.GetCommandLineArgs()
            .Contains("--tani", StringComparer.Ordinal);

        var uygulama = new Application
        {
            // Pencere yok = uygulama kapanmasın: bu bir tepsi uygulaması,
            // tüm pencereler kapanınca çıkmamalı.
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        Yonetici? yonetici = null;

        // Yakalanmamış istisna uygulamayı SESSİZCE öldürmemeli: kullanıcı
        // ne olduğunu bilmeli ve günlükte iz kalmalı.
        uygulama.DispatcherUnhandledException += (_, e) =>
        {
            Gunluk.Hata("yakalanmamış", e.Exception);
            MessageBox.Show(
                "Beklenmeyen bir hata oldu ama uygulama çalışmaya devam "
                + "ediyor.\n\nAyrıntı için: Gelişmiş → Günlük dosyasını aç\n\n"
                + e.Exception.Message,
                "Ekran Çeviri", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Gunluk.Hata("alanDışı", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Gunluk.Hata("gözlenmemişGörev", e.Exception);
            e.SetObserved();
        };

        try
        {
            Gunluk.Yaz("=== Ekran Çeviri başladı ===");
            yonetici = new Yonetici(uygulama.Dispatcher);
            yonetici.Basla();
            return uygulama.Run();
        }
        catch (Exception e)
        {
            Gunluk.Hata("başlangıç", e);
            MessageBox.Show(
                "Uygulama başlatılamadı:\n\n" + e.Message,
                "Ekran Çeviri", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
        finally
        {
            yonetici?.Dispose();
            _tekOrnek?.ReleaseMutex();
            _tekOrnek?.Dispose();
        }
    }
}
