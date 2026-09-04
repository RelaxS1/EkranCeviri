using EkranCeviri.Cekirdek;
using EkranCeviri.Kisayol;
using static EkranCeviri.Testler.SafYardimci;

namespace EkranCeviri.Testler;

/// <summary>
/// FAZ D (arayüz) saf testleri: kısayol metni, durum rengi kuralı, bar
/// konumu sınır durumları. Arayüz sınıfları WPF ister; burada yalnız onların
/// dayandığı SAF kararlar sabitlenir — bir kırmızı, barın yanlış yere
/// düşmesi ya da ipucunun yalan söylemesi demektir.
/// </summary>
public static class SafTestlerD
{
    public static void Kos()
    {
        KisayolMetniTestleri();
        DurumMetniEkTestleri();
        BarKonumuSinirTestleri();
    }

    // ---------------- kısayol metni (Mac tusAdi/kisayolMetni) ----------------

    private static void KisayolMetniTestleri()
    {
        Baslik("KisayolMetni.Metin");
        Esit("Ctrl + Alt + C", KisayolMetni.Metin(KisayolMetni.ModCtrl | KisayolMetni.ModAlt, 0x43),
             "varsayılan Ctrl+Alt+C");
        Esit("Ctrl + F", KisayolMetni.Metin(KisayolMetni.ModCtrl, 0x46), "Ctrl+F");
        Esit("Shift + Win + X", KisayolMetni.Metin(KisayolMetni.ModShift | KisayolMetni.ModWin, 0x58),
             "Shift+Win+X — sıra Ctrl, Alt, Shift, Win");
        Esit("Ctrl + Alt + Shift + Win + 7",
             KisayolMetni.Metin(0x0F, 0x37), "dört değiştirici + rakam");
        Esit("Boşluk", KisayolMetni.Metin(0, 0x20), "değiştiricisiz özel tuş adı");
        Esit("F1", KisayolMetni.TusAdi(0x70), "F1");
        Esit("F12", KisayolMetni.TusAdi(0x7B), "F12");
        Esit("Enter", KisayolMetni.TusAdi(0x0D), "Enter");
        Esit("Sayısal 5", KisayolMetni.TusAdi(0x65), "NumPad5");
        Esit("Tuş#0xFF", KisayolMetni.TusAdi(0xFF), "bilinmeyen kod boş görünmez");
        // Ayarlar.Dogrula tuş kodunu 1..255'e kıstırır: aralığın tamamı ad üretmeli
        var bosVar = false;
        for (uint t = 1; t <= 255; t++)
            if (string.IsNullOrWhiteSpace(KisayolMetni.TusAdi(t))) bosVar = true;
        Dogru(!bosVar, "1..255 her sanal tuş için boş olmayan ad");
    }

    // ---------------- durum metni rengi (Mac DurumEtiketi.hataMi) ----------------

    private static void DurumMetniEkTestleri()
    {
        Baslik("DurumMetni.HataMi — bar etiketleri");
        Dogru(DurumMetni.HataMi("Grok anahtarı yok (Gelişmiş → Yapay Zekâ Anahtarı)"),
              "anahtar yok → hata (turuncu)");
        Dogru(DurumMetni.HataMi("Öneri için Grok anahtarı gerekli"), "gerekli → hata");
        Dogru(DurumMetni.HataMi("Grok yanıt vermedi (40 sn) — tekrar dene"), "yanıt vermedi → hata");
        // "kopyalanamadı" iz listesinde YOK: hata rengi için ⚠ öne konur
        Dogru(!DurumMetni.HataMi("Panoya kopyalanamadı"), "kopyalanamadı tek başına iz değil");
        Dogru(DurumMetni.HataMi("⚠ Panoya kopyalanamadı — tekrar dene"), "⚠ ile hata rengi");
        Dogru(!DurumMetni.HataMi("Panoya kopyalandı ✓"), "kopyalandı → bilgi");
        Dogru(!DurumMetni.HataMi("öneriler hazır"), "öneriler hazır → bilgi");
        Dogru(!DurumMetni.HataMi("cevap hazırlanıyor…"), "hazırlanıyor → bilgi");
        Dogru(!DurumMetni.HataMi("Grok · Züridütsch"), "motor etiketi → bilgi");
        Dogru(!DurumMetni.HataMi("Hafızadan ✓"), "hafızadan → bilgi");
        Dogru(DurumMetni.HataMi("⚠︎ Grok anahtarı yok → Bing"), "⚠ işareti → hata");
    }

    // ---------------- bar konumu sınır durumları (DIU dönüşümü ÖNCESİ, fiziksel) ----------------

    private static void BarKonumuSinirTestleri()
    {
        Baslik("Geometri.BarKonumu — sınır durumları");
        var ekran = new Kutu(0, 0, 1920, 1040);   // görev çubuğu hariç çalışma alanı
        const double g = 460 * 1.5, y = 38 * 1.5; // %150 ölçekli ekranda fiziksel bar

        // Bölge ekranın tepesine yapışık: üstte yer yok → alta düşer
        var (x1, y1) = Geometri.BarKonumu(new Kutu(600, 0, 500, 400), ekran, g, y);
        Esit(408.0, y1, "tepeye yapışık bölge: bar altta (bolge.Alt + 8)");
        Esit(850 - g / 2, x1, "x bölgeye ortalanır");

        // Bölge ekranın tamamını kaplıyor: ne üst ne alt sığar → görünür alanın
        // içine kıstırılır (bar ekran DIŞINA taşımaz)
        var (_, y2) = Geometri.BarKonumu(new Kutu(0, 0, 1920, 1040), ekran, g, y);
        Dogru(y2 >= ekran.Y + 8 && y2 + y <= ekran.Alt - 8, "tam ekran bölge: bar görünür alanda");

        // Bölge çok solda: x sola kıstırılır (8 pay)
        var (x3, _) = Geometri.BarKonumu(new Kutu(0, 300, 200, 200), ekran, g, y);
        Esit(8.0, x3, "sol kenar: x = 8");

        // Bölge çok sağda: x sağa kıstırılır
        var (x4, _) = Geometri.BarKonumu(new Kutu(1800, 300, 120, 200), ekran, g, y);
        Esit(ekran.Sag - g - 8, x4, "sağ kenar: x = sağ - genişlik - 8");

        // Bardan dar bölge: bar yine bölgeye ortalanır, taşma yok
        var (x5, y5) = Geometri.BarKonumu(new Kutu(900, 500, 100, 100), ekran, g, y);
        Esit(950 - g / 2, x5, "dar bölge: bar bölge merkezine ortalanır");
        Esit(500 - y - 8, y5, "dar bölge: bar üstte");

        // Çok ekran: negatif orijinli sol monitör — kıstırma o ekranın kutusuna göre
        var sol = new Kutu(-1920, 0, 1920, 1040);
        var (x6, y6) = Geometri.BarKonumu(new Kutu(-1900, 20, 400, 300), sol, g, y);
        Esit(-1920 + 8, x6, "negatif orijin: x sol kenara kıstırılır");
        Esit(328.0, y6, "negatif orijin: üst sığmaz → alt (20+300+8)");
    }
}
