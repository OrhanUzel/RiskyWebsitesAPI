namespace RiskyWebsitesAPI.Models;

// API’nin kullanıcıya döndüğü standart cevap modeli.
public class RiskCheckResponse
{
    // Site riskli listelerde bulundu mu?
    public bool IsRisky { get; set; }

    // Türkçe mesaj: Kullanıcıya durumla ilgili açıklayıcı metin.
    public string? Message { get; set; }

    // Kontrol edilen (normalize edilmiş) domain/host.
    public string? CheckedDomain { get; set; }

    // Varsa bulunduğu kaynak dosyaların kısa anahtarları (aa, ab, ac).
    public string[] FoundInFiles { get; set; } = Array.Empty<string>();
}