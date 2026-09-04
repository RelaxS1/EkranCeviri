using System.Text.RegularExpressions;
using System.Windows;
using EkranCeviri.Cekirdek;
using EkranCeviri.Ceviri;

namespace EkranCeviri.Ekran;

/// <summary>
/// OCR satırlarını sohbet BALONLARINA gruplar. Bu dosyadaki her eşik bir
/// hatanın izidir: ayrı balonların birleşmesi, tek balonun bölünmesi,
/// tarih ayracının çevrilmesi, fiyatın saat sanılıp silinmesi.
/// </summary>
public static partial class Bloklayici
{
    /// <summary>
    /// YALNIZ gerçek saat damgası: iki nokta üst üste + geçerli saat.
    /// Eski gevşek desen (<c>\d{1,2}[:.-]\d{2}</c>) fiyatı (12.50), tarihi
    /// (12.05) ve süre aralığını (10-15) da saat sanıp METİNDEN SİLİYORDU —
    /// "das kostet 12.50 franken" çevirisinde fiyat kayboluyordu.
    /// </summary>
    [GeneratedRegex(@"(?<![\d.,:])([01]?\d|2[0-3]):[0-5]\d(?![\d.,:])")]
    private static partial Regex SaatDeseni();

    /// <summary>Sağ/sol kararı için orta çizgi. Tam %50 değil: WhatsApp
    /// balonları asimetrik yerleşiyor.</summary>
    private const double OrtaCizgi = 0.52;

    public static (List<Blok> Bloklar, List<Rect> Sessizler) Ayir(
        IReadOnlyList<OcrSatir> satirlar, Size boyut)
    {
        var sessizler = new List<Rect>();
        var yerel = new List<(string Metin, Rect Kutu)>(satirlar.Count);

        foreach (var s in satirlar)
        {
            // Saat damgası hiçbir bloğa girmez: çeviriye karışırsa model
            // "14:32" gibi bir zamanı cümlenin parçası sanıyor.
            var temiz = SaatDeseni().Replace(s.Metin, " ").Trim();
            if (!temiz.Any(char.IsLetter))
            {
                // Harfsiz kutu (yalnız saat, tik işareti, emoji) çevrilmez
                // ama YAMA DA ÇİZİLMEZ — üstünü kapatmak bilgi siliyor.
                sessizler.Add(s.Kutu);
                continue;
            }
            yerel.Add((temiz, s.Kutu));
        }

        // Okuma sırası: yukarıdan aşağı, aynı hizada soldan sağa
        yerel.Sort((a, b) =>
        {
            double fark = a.Kutu.Y - b.Kutu.Y;
            if (Math.Abs(fark) < 3) return a.Kutu.X.CompareTo(b.Kutu.X);
            return fark < 0 ? -1 : 1;
        });

        var gruplar = new List<Grup>();
        foreach (var satir in yerel)
        {
            bool satirSol = (satir.Kutu.X + satir.Kutu.Width / 2)
                            < boyut.Width * OrtaCizgi;
            bool eklendi = false;

            // Yalnız son 6 gruba bak: sohbet listesi uzadıkça hepsini
            // taramak canlı modda gereksiz pahalı.
            for (int i = gruplar.Count - 1; i >= Math.Max(0, gruplar.Count - 6); i--)
            {
                var grup = gruplar[i];
                bool grupSol = (grup.Kutu.X + grup.Kutu.Width / 2)
                               < boyut.Width * OrtaCizgi;
                // FARKLI TARAFTAKİ satırlar ASLA aynı bloğa girmez:
                // benim mesajımla karşınınki birleşirse çeviri de karışır.
                if (satirSol != grupSol) continue;

                var son = grup.SonKutu;
                double ortYukseklik = (son.Height + satir.Kutu.Height) / 2;
                double dikeyBosluk = satir.Kutu.Y - son.Bottom;

                // Üstte: hafif örtüşmeye izin (OCR kutuları taşabiliyor).
                // Altta: balon İÇİ satır aralığı ortalama yüksekliğin
                // %50'sini geçmez. Daha gevşek eşik AYRI balonları tek
                // bloğa yapıştırıyordu.
                if (dikeyBosluk <= -son.Height * 0.3) continue;
                if (dikeyBosluk >= ortYukseklik * 0.5) continue;

                // Aynı balonun satırları aynı sol kenardan başlar
                if (Math.Abs(satir.Kutu.X - grup.IlkKutu.X) >= boyut.Width * 0.10)
                    continue;

                grup.Ekle(satir.Metin, satir.Kutu);
                eklendi = true;
                break;
            }
            if (!eklendi) gruplar.Add(new Grup(satir.Metin, satir.Kutu));
        }

        var bloklar = new List<Blok>(gruplar.Count);
        foreach (var g in gruplar)
        {
            bool benim = (g.Kutu.X + g.Kutu.Width / 2) > boyut.Width * OrtaCizgi;
            var blok = new Blok(g.Metin, g.Kutu, benim);

            // Sohbet balonları ya sola ya sağa yaslıdır. ORTALANMIŞ bloklar
            // (tarih çipi, uçtan uca şifreleme bildirimi, sistem mesajı)
            // sohbet değildir: çevrilmez, olduğu gibi görünür.
            bool solda = g.Kutu.X < boyut.Width * 0.22;
            bool sagda = g.Kutu.Right > boyut.Width * 0.78;
            // Yalnız DAR ve ortalanmış bloklar sistem öğesidir. Geniş blok
            // her zaman çevrilir: dar bölge seçiminde geniş balon
            // yanlışlıkla "ortalanmış" görünüp çevirisiz kalıyordu.
            bool atla = !solda && !sagda && g.Kutu.Width < boyut.Width * 0.5;

            // Hedef = atlanmayan, anlamlı uzunluktaki mesaj. KENDİ mesajlarımız
            // da çevrilir (Mac davranışı): kullanıcı kendi yazdığı Almancayı
            // da Türkçe görmek istiyor. Türkçe yazdığı mesajlar
            // Kalite.CevrilecekSeyYokMu ile motora GİTMEZ.
            blok.Hedef = !atla && blok.Anahtar.Length >= 2;
            bloklar.Add(blok);
        }
        return (bloklar, sessizler);
    }

    /// <summary>
    /// İki karedeki bloklar AYNI mesaj mı?
    ///
    /// KIRMIZI ÇİZGİ: yalnız konuma (IoU) bakmak YASAK. Yeni mesaj gelip
    /// liste kayınca eski çeviri BAŞKA mesaja yapışıyor ve kalıcı hafızaya
    /// yazılıyordu — kullanıcının "yanlış anlaşılabilecek çeviri"
    /// şikayetinin kök nedeni buydu. Metin benzerliği ŞART.
    /// </summary>
    public static bool Eslesirler(Blok a, Blok b)
    {
        if (a.Anahtar == b.Anahtar) return true;

        var kesen = Rect.Intersect(a.Kutu, b.Kutu);
        if (kesen.IsEmpty) return false;
        double birlesim = a.Kutu.Width * a.Kutu.Height
                        + b.Kutu.Width * b.Kutu.Height
                        - kesen.Width * kesen.Height;
        if (birlesim <= 0) return false;
        if (kesen.Width * kesen.Height / birlesim <= 0.45) return false;

        int uzunluk = Math.Max(a.Anahtar.Length, b.Anahtar.Length);
        if (uzunluk == 0) return false;
        if (Math.Abs(a.Anahtar.Length - b.Anahtar.Length) > 3) return false;

        return Kalite.MesafeAzMi(a.Anahtar, b.Anahtar,
                                 Math.Max(1, Math.Min(3, uzunluk / 10)));
    }

    private sealed class Grup
    {
        private readonly List<string> _satirlar = [];

        public Grup(string metin, Rect kutu)
        {
            _satirlar.Add(metin);
            IlkKutu = kutu;
            SonKutu = kutu;
            Kutu = kutu;
        }

        public Rect IlkKutu { get; }
        public Rect SonKutu { get; private set; }
        public Rect Kutu { get; private set; }
        public string Metin => string.Join(" ", _satirlar);

        public void Ekle(string metin, Rect kutu)
        {
            _satirlar.Add(metin);
            SonKutu = kutu;
            Kutu = Rect.Union(Kutu, kutu);
        }
    }
}
