using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;
using RiskyWebsitesAPI.Security;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace RiskyWebsitesAPI.Services
{
    // Yerel domain listesi konfigürasyonu
    public class LocalDomainListConfig
    {
        public string Key { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }

    // Bu servis, GitHub üzerindeki üç txt dosyasını indirir ve bellekte tutar.
    // İstek geldiğinde verilen domainin (host) bu listelerde olup olmadığını kontrol eder.
    public class RiskDomainService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly CircuitBreakerService _circuitBreaker;
        private readonly MemoryProtectionService _memoryProtection;
        private readonly ILogger<RiskDomainService> _logger;
        private readonly IConfiguration _configuration;

        // GitHub üzerindeki üç txt dosyasının ham (raw) URL'leri.
        // Not: Varsayılan "main" dalı kullanılmıştır.
        private static readonly (string key, string url)[] SourceFiles = new[]
        {
            ("aa", "https://raw.githubusercontent.com/romainmarcoux/malicious-domains/main/full-domains-aa.txt"),
            ("ab", "https://raw.githubusercontent.com/romainmarcoux/malicious-domains/main/full-domains-ab.txt"),
            ("ac", "https://raw.githubusercontent.com/romainmarcoux/malicious-domains/main/full-domains-ac.txt"),
            // USOM ulusal siber olaylara müdahale merkezi - URL listesi (tam URL'ler içerir)
            ("usom", "https://www.usom.gov.tr/url-list.txt"),
        };

        // Yerel fallback domain listesi
        private readonly HashSet<string> _fallbackDomains = new(StringComparer.OrdinalIgnoreCase);

        // Önbellek anahtar şablonu.
        private const string CacheKeyPrefix = "RiskDomains:";

        // Önbellek yaşam süresi: listeler çok sık değişmediği için saatlik/yaklaşık 6 saat ideal kabul edilmiştir.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

        public RiskDomainService(IHttpClientFactory httpClientFactory, IMemoryCache cache, 
            CircuitBreakerService circuitBreaker, MemoryProtectionService memoryProtection, 
            ILogger<RiskDomainService> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _circuitBreaker = circuitBreaker;
            _memoryProtection = memoryProtection;
            _logger = logger;
            _configuration = configuration;
            
            // Yerel fallback domain listesini yükle
            LoadFallbackDomains();
        }

        // Yerel fallback domain listesini yükle
        private void LoadFallbackDomains()
        {
            try
            {
                var fallbackDomains = _configuration.GetSection("RiskDomainSettings:FallbackDomains").Get<string[]>();
                if (fallbackDomains != null)
                {
                    foreach (var domain in fallbackDomains)
                    {
                        if (!string.IsNullOrWhiteSpace(domain))
                        {
                            _fallbackDomains.Add(domain.Trim().ToLowerInvariant());
                        }
                    }
                    _logger.LogInformation($"Yerel fallback domain listesine {_fallbackDomains.Count} domain yüklendi");
                }
                
                // Yerel dosyalardan domain listelerini yükle
                LoadLocalDomainLists();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Yerel fallback domain listesi yüklenirken hata oluştu");
            }
        }

        // Yerel dosyalardan domain listelerini yükle
        private void LoadLocalDomainLists()
        {
            try
            {
                var localLists = _configuration.GetSection("RiskDomainSettings:LocalDomainLists").Get<List<LocalDomainListConfig>>();
                if (localLists != null)
                {
                    foreach (var listConfig in localLists)
                    {
                        if (listConfig?.Enabled == true && !string.IsNullOrWhiteSpace(listConfig.FilePath))
                        {
                            LoadLocalDomainFile(listConfig.FilePath, listConfig.Key);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Yerel dosya domain listeleri yüklenirken hata oluştu");
            }
        }

        // Tek bir yerel dosyadan domain yükle
        private void LoadLocalDomainFile(string filePath, string key)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var lines = File.ReadAllLines(filePath);
                    int validDomains = 0;
                    
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        
                        // Yorum satırlarını ve boş satırları atla
                        if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                            continue;
                        
                        // Domain validasyonu
                        if (trimmedLine.Contains('.') && !trimmedLine.Contains(' ') && trimmedLine.Length <= 255)
                        {
                            var domain = trimmedLine.ToLowerInvariant();
                            if (domain.StartsWith("www.")) domain = domain[4..];
                            
                            _fallbackDomains.Add(domain);
                            validDomains++;
                        }
                    }
                    
                    _logger.LogInformation($"Yerel dosyadan {validDomains} domain yüklendi: {filePath} (Key: {key})");
                }
                else
                {
                    _logger.LogWarning($"Yerel domain dosyası bulunamadı: {filePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Yerel domain dosyası yüklenirken hata oluştu: {filePath}");
            }
        }

        // Dışarıya sunulan ana operasyon: Domain kontrolü.
        // Dönen sonuçta, hangi dosyada bulunduğu bilgisi de sağlanır.
        public async Task<(bool IsRisky, string[] FoundInFiles)> CheckDomainAsync(string host)
        {
            // Giriş güvenliği: Boşluk temizleme ve küçük harfe çevirme.
            host = host.Trim().ToLowerInvariant();

            // Önce fallback domain listesini kontrol et
            if (_fallbackDomains.Contains(host))
            {
                return (true, new[] { "local-fallback" });
            }

            // Tüm kaynak setlerini getir (gerekirse indir). Ardından eşleşme ara.
            var foundIn = new List<string>();

            foreach (var (key, url) in SourceFiles)
            {
                var set = await GetOrLoadSetAsync(key, url);
                if (set.Contains(host))
                {
                    foundIn.Add(key);
                }
            }

            return (foundIn.Count > 0, foundIn.ToArray());
        }

        // Bir kaynağa ait HashSet<string> döner; yoksa HTTP ile indirir ve önbelleğe koyar.
        private async Task<HashSet<string>> GetOrLoadSetAsync(string key, string url)
        {
            var cacheKey = CacheKeyPrefix + key;

            // Önbellekte varsa doğrudan dön.
            if (_cache.TryGetValue(cacheKey, out HashSet<string>? existing) && existing is not null)
            {
                return existing;
            }

            // Bellek ve circuit breaker koruması ile indir
            try
            {
                return await _memoryProtection.ExecuteWithMemoryLimit(async () =>
                {
                    return await _circuitBreaker.ExecuteAsync($"GitHub_{key}", async () =>
                    {
                        _logger.LogInformation($"GitHub'dan dosya indiriliyor: {url}");
                        
                        var client = _httpClientFactory.CreateClient("github");
                        using var resp = await client.GetAsync(url);
                        
                        if (!resp.IsSuccessStatusCode)
                        {
                            _logger.LogWarning($"GitHub'dan dosya indirilemedi: {url}, Status: {resp.StatusCode}");
                            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }
                        
                        var content = await resp.Content.ReadAsStringAsync();
                        
                        if (string.IsNullOrWhiteSpace(content))
                        {
                            _logger.LogWarning($"GitHub'dan boş içerik geldi: {url}");
                            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }

                        // Satırları temizleyip bir HashSet'e aktar.
                        // Trim + küçük harf + boş satırları atla.
                        var lines = content.Split('\n');
                        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        
                        // Bellek koruması: Maksimum satır sayısı sınırı
                        const int MAX_LINES = 100000;
                        if (lines.Length > MAX_LINES)
                        {
                            _logger.LogWarning($"Dosya çok büyük, sadece ilk {MAX_LINES} satır işleniyor: {url}");
                            lines = lines.Take(MAX_LINES).ToArray();
                        }
                        
                        int validLines = 0;
                        foreach (var raw in lines)
                        {
                            var line = raw.Trim();
                            if (line.Length == 0) continue;
                            
                            // Satır uzunluğu sınırı
                            if (line.Length > 255) continue;
                            
                            // Hem domain hem tam URL formatını desteklemek için satırları host'a indirgeriz.
                            var lowered = line.ToLowerInvariant();

                            // Eğer tam URL ise host'u çıkar.
                            if (lowered.StartsWith("http://") || lowered.StartsWith("https://"))
                            {
                                if (Uri.TryCreate(lowered, UriKind.Absolute, out var uri))
                                {
                                    var host = uri.Host.ToLowerInvariant();
                                    if (host.StartsWith("www.")) host = host[4..];
                                    if (!string.IsNullOrEmpty(host) && host.Contains('.'))
                                    {
                                        set.Add(host);
                                        validLines++;
                                    }
                                    continue;
                                }
                            }

                            // Aksi halde çıplak domain olarak kabul et ve "www." önekini kaldır.
                            var hostOnly = lowered;
                            if (hostOnly.StartsWith("www.")) hostOnly = hostOnly[4..];
                            
                            // Basit domain validasyonu
                            if (!string.IsNullOrEmpty(hostOnly) && hostOnly.Contains('.') && !hostOnly.Contains(' '))
                            {
                                set.Add(hostOnly);
                                validLines++;
                            }
                        }

                        _logger.LogInformation($"GitHub'dan {validLines} geçerli domain işlendi: {url}");
                        
                        // Önbelleğe koy ve TTL belirle.
                        _cache.Set(cacheKey, set, CacheTtl);
                        return set;
                    });
                }, $"LoadDomains_{key}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Domain listesi yüklenirken hata oluştu: {key} - {url}");
                
                // Hata durumunda boş set dön ama önbelleğe koy
                var emptySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _cache.Set(CacheKeyPrefix + key, emptySet, TimeSpan.FromMinutes(5)); // 5 dakika boyunca yeniden deneme
                return emptySet;
            }
        }

        // Yardımcı: Kullanıcıdan gelen URL veya domaini, kontrol edilecek host'a dönüştür.
        public static string NormalizeToHost(string input)
        {
            // Küçük harfe çevir.
            input = input.Trim();

            // Şema yoksa eklemeyi dene (örn. example.com -> http://example.com).
            // Uri.TryCreate doğru hostu çıkarmayı kolaylaştırır.
            if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                input = "http://" + input;
            }

            if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
            {
                var host = uri.Host.ToLowerInvariant();

                // Yaygın bir normalizasyon: www. öneki kaldır.
                if (host.StartsWith("www."))
                {
                    host = host[4..];
                }

                return host;
            }

            // Uri parse edilemezse, girdiği string'i domain gibi kabul edip basit temizleme uygula.
            var fallback = input.ToLowerInvariant();
            if (fallback.StartsWith("www."))
            {
                fallback = fallback[4..];
            }
            return fallback;
        }
    }
}