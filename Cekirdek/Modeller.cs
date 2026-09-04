using System.Windows;
using EkranCeviri.Ceviri;

namespace EkranCeviri.Cekirdek;

/// <summary>OCR'ın okuduğu tek satır. Kutu, YAKALANAN GÖRÜNTÜNÜN piksel
/// koordinatındadır (sol-üst orijin), ekran koordinatı değil.</summary>
public sealed class OcrSatir
{
    public required string Metin { get; init; }
    public required Rect Kutu { get; init; }
    public double Guven { get; init; } = 1.0;
}

/// <summary>
/// Ekrandaki bir sohbet balonu. Aynı anda hem iş kuyruğundan hem arayüz
/// kuyruğundan okunup yazılıyor — <see cref="Ceviri"/> KİLİTLİDİR.
/// macOS sürümünde bu kilit olmadığı için uygulama çöküyordu.
/// </summary>
public sealed class Blok
{
    private readonly object _kilit = new();
    private string? _ceviri;

    public Blok(string metin, Rect kutu, bool benim)
    {
        Metin = metin;
        Kutu = kutu;
        Benim = benim;
        // Anahtar init'te BİR KEZ hesaplanır. Tembel hesaplama iki kuyruktan
        // aynı anda tetiklenince veri yarışı oluyordu (macOS'ta ölçüldü).
        Anahtar = Kalite.Anahtarla(metin);
    }

    public string Metin { get; }
    public Rect Kutu { get; set; }

    /// <summary>Sağ hizalı = kullanıcının kendi mesajı.</summary>
    public bool Benim { get; }

    /// <summary>Çevrilecek mi? (karşı tarafın mesajı ve anlamlı uzunlukta)</summary>
    public bool Hedef { get; set; }

    /// <summary>Noktalama/boşluk arındırılmış eşleştirme anahtarı.</summary>
    public string Anahtar { get; }

    public string? Ceviri
    {
        get { lock (_kilit) return _ceviri; }
        set { lock (_kilit) _ceviri = value; }
    }

    /// <summary>Aynı Metin/Kutu/Benim/Hedef/Ceviri ile YENİ nesne. Kalite
    /// turu KOPYA bloklarla çalışır: macOS'ta paylaşılan Blok'a iki kuyruktan
    /// yazmak çöktürüyordu.</summary>
    public Blok Kopya() => new(Metin, Kutu, Benim) { Hedef = Hedef, Ceviri = Ceviri };
}

/// <summary>Önceki konuşmanın yapılı satırı: kim yazdı, ne yazdı, çevirisi
/// (varsa). Dize listesi ("ben: …") modele rol ayrımını belirsiz veriyordu.</summary>
public sealed record KonusmaSatiri(bool Benim, string Metin, string? Ceviri);

/// <summary>Motora verilen bağlam. Sohbete özgü her şey burada.</summary>
public sealed class CeviriBaglam
{
    public required Ayarlar Ayar { get; init; }

    /// <summary>Modele verilen tam ad: "Zürih İsviçre Almancası (Züridütsch)"</summary>
    public string LehceAd { get; init; } = "Almanca";

    /// <summary>Panelde gösterilen kısa etiket: "Züridütsch"</summary>
    public string LehceKisa { get; init; } = "Almanca";

    /// <summary>Üslup taklidi için karşı tarafın ekrandaki mesajları.
    /// GÜVENİLMEZ VERİ — sistem istemine ASLA girmez.</summary>
    public IReadOnlyList<string> TarzOrnekleri { get; init; } = [];

    /// <summary>Tekil/çoğul ve gönderme hatalarını önleyen önceki konuşma.
    /// GÜVENİLMEZ VERİ.</summary>
    public IReadOnlyList<string> OncekiKonusma { get; init; } = [];

    /// <summary>Aynı konuşmanın yapılı hâli; motorlar bunu tercih eder.
    /// Dize listesi geriye uyum için kalır. GÜVENİLMEZ VERİ.</summary>
    public IReadOnlyList<KonusmaSatiri> OncekiKonusmaYapili { get; init; } = [];
}

/// <summary>Bir çeviri turunun sonucu.</summary>
public sealed class CeviriSonuc
{
    /// <summary>Girişle AYNI uzunlukta. Çevrilemeyen öğe null.</summary>
    public required IReadOnlyList<string?> Ceviriler { get; init; }

    /// <summary>Panelde gösterilecek motor adı: "Grok · Züridütsch"</summary>
    public required string MotorAdi { get; init; }

    /// <summary>Kalıcı hafızaya yalnız GÜVENİLİR motor çıktısı yazılır
    /// (makine motorları hafızayı zehirliyordu).</summary>
    public bool HafizayaYazilabilir { get; init; }
}

/// <summary>Çeviri motoru. Her motor kendi hatasını yutar ve null döndürür;
/// istisna FIRLATMAZ — bir motorun çökmesi turu bitirmemeli.</summary>
public interface IMotor
{
    string Ad { get; }

    /// <summary>Anahtar gerektiriyor mu? (arayüzde uyarı için)</summary>
    bool AnahtarGerekir { get; }

    Task<CeviriSonuc> CevirAsync(IReadOnlyList<string> metinler,
                                 CeviriBaglam baglam,
                                 CancellationToken iptal);
}
