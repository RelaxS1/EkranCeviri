using System.IO;
using System.Text;

namespace EkranCeviri.Cekirdek;

/// <summary>
/// Basit dosya günlüğü. Arkadaşın bilgisayarında bir şey ters giderse
/// "günlüğü gönder" diyebilelim diye var.
/// GÜNLÜĞE ASLA: API anahtarı, sohbet metni, çeviri içeriği yazılmaz —
/// yalnız olay ve hata bilgisi. Sohbet mahrem.
/// </summary>
public static class Gunluk
{
    private static readonly object Kilit = new();
    private static string Yol => Path.Combine(Ayarlar.DestekDizini, "gunluk.txt");
    private const long EnFazlaBayt = 512 * 1024;

    public static void Yaz(string mesaj)
    {
        try
        {
            lock (Kilit)
            {
                Directory.CreateDirectory(Ayarlar.DestekDizini);
                // Dosya şişerse baştan başla: aylarca açık kalan uygulamada
                // günlük diski doldurmasın.
                if (File.Exists(Yol) && new FileInfo(Yol).Length > EnFazlaBayt)
                    File.Delete(Yol);
                File.AppendAllText(
                    Yol,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {mesaj}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Günlük yazamamak uygulamayı durdurmaz.
        }
    }

    public static void Hata(string yer, Exception e) =>
        Yaz($"HATA [{yer}] {e.GetType().Name}: {e.Message}");
}
