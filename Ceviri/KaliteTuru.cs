namespace EkranCeviri.Ceviri;

/// <summary>
/// KALİTE TURU GERİ BASINCI (ürün kararı #1). Hızlı model ekrana basıldıktan
/// sonra kalite modeli arka planda düzeltir. macOS'ta her çağrı yeni bir iş
/// açıyordu: uçuşta-tek-iş bayrağı, birleştirme ve derinlik tavanı yoktu;
/// kuyruk uzayınca sonuç ekranda karşılığı kalmamış bloklara geliyordu.
/// </summary>
public static class KaliteTuru
{
    /// <summary>Bekleyen blok tavanı: bunun üstü en eskiden atılır.</summary>
    public const int DerinlikTavani = 40;

    /// <summary>YAŞ KAPISI: hızlı çeviri ekrana çizildikten sonra bu kadar
    /// zaman geçmişse kalite sonucu YALNIZ hafızaya yazılır, ekran
    /// DEĞİŞTİRİLMEZ. Kullanıcı o balonu çoktan okudu; gözünün önünde
    /// değişmesi ürünü "kararsız" hissettirir — okunmuş metni oynatmıyoruz.</summary>
    public static readonly TimeSpan YasKapisi = TimeSpan.FromSeconds(8);

    /// <summary>Kalite isteği kendi zaman aşımını taşır: arka plan işi
    /// etkileşimli 30 sn tavanıyla kesiliyordu (ölçülen 30061 ms tam olarak
    /// tavanın kendisi — para harcandı, sonuç yok). Yaş kapısından BÜYÜK
    /// olmalı, yoksa kapı hiç kapanmaz.</summary>
    public static readonly TimeSpan ZamanAsimi = TimeSpan.FromSeconds(20);

    /// <summary>Kalite sonucu ekrana yazılmalı mı? Ölçülen şey balonun
    /// EKRANDA DURDUĞU toplam süredir: kuyrukta bekleme + ağ çağrısının
    /// kendisi. Yalnız kuyruk beklemesini ölçmek kapıyı işlevsiz bırakır —
    /// kuyruk boşken başlayan 20 sn'lik bir çağrı "taze" sayılır ve ekran
    /// yine 20 sn sonra oynar. Bu ayrımı test kilitler.</summary>
    public static bool EkranaYazilsinMi(DateTime dogum, DateTime simdi) =>
        simdi - dogum <= YasKapisi;
}
