namespace EkranCeviri.Ekran;

/// <summary>
/// Canlı OCR tetikleme kararının durumu. Saf: yakalama yapmaz, iz alır.
///
/// GERÇEK EKRAN TESTİNDE ÖĞRENİLEN: karar ortalama farka dayanırsa KÜÇÜK bir
/// yeni balon (bölgenin %3'ü) eşiğin altında kalıyor ve mesaj hiç
/// OCR'lanmıyor; eski 12 sn'lik kör tazeleme bunu telafi ediyormuş. Bu
/// yüzden içerik değişimi ve sabitlik HÜCRE SAYISIYLA ölçülür: 3 hücre
/// (~50×45 px) ≈ kısa bir kelime; imleç yanıp sönmesi (1 hücre, küçük
/// fark) eşiğin altında kalır. GIF çıkartma tek başına saniyede bir OCR
/// üretiyordu: sürekli değişen hücreler animasyon maskesine alınır.
/// </summary>
public sealed class HareketDurumu
{
    /// <summary>Son OCR yapılan karenin izi — "içerik değişti mi" bununla ölçülür.</summary>
    public byte[]? SonOcrIzi;

    /// <summary>Hücre kaç ARDIŞIK turda değişti (animasyon = sürekli değişen hücre).</summary>
    public byte[] Sayaclar = [];

    public double SonOcrZamani;

    /// <summary>Ağ hatası art arda gelince OCR bu zamana kadar yasak (pil koruması).</summary>
    public double OcrYasakBitis;
}

/// <param name="Fark">Önceki tura göre ortalama fark (kaydırma tespiti bunu kullanır).</param>
/// <param name="Sabit">Önceki tura göre anlamlı hücre değişimi yok (animasyon hücreleri hariç).</param>
/// <param name="IcerikDegisti">Animasyon maskesi DIŞINDA, son OCR'a göre içerik değişti mi.</param>
/// <param name="AnimasyonOrani">Animasyon sayılan hücre oranı (teşhis).</param>
/// <param name="OcrYap">Bu turda OCR yapılmalı mı (tek karar noktası).</param>
public readonly record struct HareketKarari(double Fark, bool Sabit, bool IcerikDegisti,
                                            double AnimasyonOrani, bool OcrYap);

public static class Hareket
{
    /// <summary>
    /// Eşikler ölçüldü: hücre başına 12/255 gri farkı imleç yanıp sönmesinin
    /// (~4/255) üstünde, yazı/emoji değişiminin (>30/255) altındadır.
    /// <paramref name="simdi"/> saniye cinsindendir (Stopwatch tabanlı);
    /// <paramref name="durum"/> yerinde güncellenir.
    /// </summary>
    public static HareketKarari Karar(byte[] iz, byte[]? onceki, HareketDurumu durum,
                                      double simdi, int hucreEsik = 12,
                                      int hucreSayisiEsik = 3, double hareketEsik = 0.012,
                                      double zorlaAralik = 8)
    {
        int n = iz.Length;
        if (durum.Sayaclar.Length != n) durum.Sayaclar = new byte[n];

        // 1) Önceki tura göre fark + ardışık değişim sayaçları. Maske, BU TUR
        //    güncellenmeden ÖNCEKİ sayaçlara göre okunur (animasyon hücreleri
        //    "hareket" sayılmaz).
        long toplam = 0;
        int degisenOnceki = 0;
        if (onceki is not null && onceki.Length == n)
        {
            for (int i = 0; i < n; i++)
            {
                int d = Math.Abs(iz[i] - onceki[i]);
                toplam += d;
                bool maskeli = durum.Sayaclar[i] >= 3;
                if (d > hucreEsik)
                {
                    durum.Sayaclar[i] = (byte)Math.Min(durum.Sayaclar[i] + 1, 20);
                    if (!maskeli) degisenOnceki++;
                }
                else
                {
                    durum.Sayaclar[i] = 0;
                }
            }
        }
        double fark = onceki is null ? 0 : toplam / (double)Math.Max(1, n) / 255.0;

        // 2) Animasyon maskesi: 3+ ardışık turda değişen hücre
        int maskeSayisi = 0;
        for (int i = 0; i < n; i++) if (durum.Sayaclar[i] >= 3) maskeSayisi++;
        double animasyonOrani = maskeSayisi / (double)Math.Max(1, n);
        // Maske çok büyükse (video oynuyor) güvenilmez → tüm hücreler sayılır
        bool maskeGuvenilir = animasyonOrani <= 0.35;

        // 3) İçerik değişimi: maske dışındaki hücrelerde son OCR'a göre DEĞİŞEN
        //    HÜCRE SAYISI (ortalama değil — küçük balon ortalamada kayboluyor).
        bool icerikDegisti;
        var s = durum.SonOcrIzi;
        if (s is not null && s.Length == n)
        {
            int degisenSonOcr = 0;
            for (int i = 0; i < n; i++)
            {
                if (maskeGuvenilir && durum.Sayaclar[i] >= 3) continue;
                if (Math.Abs(iz[i] - s[i]) > hucreEsik) degisenSonOcr++;
            }
            icerikDegisti = degisenSonOcr >= hucreSayisiEsik;
        }
        else
        {
            icerikDegisti = true;      // hiç OCR yapılmadı
        }

        // 4) Sabitlik: ortalama fark küçük VE anlamlı hücre değişimi yok
        bool sabit = fark <= hareketEsik && degisenOnceki < hucreSayisiEsik;

        // 5) Tek karar: içerik değiştiyse ve (ekran sabitse VEYA sürekli hareket
        //    altında en az zorlaAralik geçtiyse) OCR yap. Yasak süresinde asla.
        bool zorlaZamani = simdi - durum.SonOcrZamani >= zorlaAralik;
        bool ocrYap = icerikDegisti && simdi >= durum.OcrYasakBitis
                      && (sabit || zorlaZamani);
        return new HareketKarari(fark, sabit, icerikDegisti, animasyonOrani, ocrYap);
    }

    /// <summary>İki izin ortalama farkı (0..1). Boyut uyuşmazsa 1 = "tamamen
    /// farklı" (yeniden OCR güvenli taraftır).</summary>
    public static double IzFarki(byte[] a, byte[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 1;
        long toplam = 0;
        for (int i = 0; i < a.Length; i++) toplam += Math.Abs(a[i] - b[i]);
        return toplam / (double)a.Length / 255.0;
    }
}
