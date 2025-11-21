using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RiskyWebsitesAPI.Security;
using System.Collections.Concurrent;

namespace RiskyWebsitesAPI.Controllers;

// Güvenlik ve monitoring endpoint'leri
[ApiController]
[Route("api/[controller]")]
public class SecurityController : ControllerBase
{
    private readonly MemoryProtectionService _memoryProtection;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SecurityController> _logger;

    public SecurityController(MemoryProtectionService memoryProtection, IMemoryCache cache, ILogger<SecurityController> logger)
    {
        _memoryProtection = memoryProtection;
        _cache = cache;
        _logger = logger;
    }

    // GET /api/security/stats
    [HttpGet("stats")]
    public ActionResult<object> GetSecurityStats()
    {
        try
        {
            var memoryStats = _memoryProtection.GetMemoryStats();
            var cacheEntries = GetCacheEntryCount();
            var blockedIps = GetBlockedIpCount();
            
            return Ok(new
            {
                Memory = memoryStats,
                Cache = new
                {
                    Entries = cacheEntries,
                    MemoryUsagePercentage = memoryStats.MemoryUsagePercentage
                },
                Security = new
                {
                    BlockedIPs = blockedIps,
                    Status = blockedIps > 0 ? "Some IPs blocked" : "All clear"
                },
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Güvenlik istatistikleri alınamadı");
            return StatusCode(500, new { Error = "İstatistikler alınamadı" });
        }
    }

    // GET /api/security/health
    [HttpGet("health")]
    public ActionResult<object> GetHealthStatus()
    {
        try
        {
            var memoryStats = _memoryProtection.GetMemoryStats();
            var isHealthy = memoryStats.MemoryUsagePercentage < 90 && memoryStats.CacheUsagePercentage < 90;
            
            return Ok(new
            {
                Status = isHealthy ? "Healthy" : "Under pressure",
                MemoryPressure = memoryStats.MemoryUsagePercentage,
                CachePressure = memoryStats.CacheUsagePercentage,
                Recommendations = GetHealthRecommendations(memoryStats),
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check başarısız");
            return StatusCode(500, new { Status = "Unhealthy", Error = ex.Message });
        }
    }

    // POST /api/security/clear-cache
    [HttpPost("clear-cache")]
    public ActionResult<object> ClearCache()
    {
        try
        {
            // Memory cache'i temizle
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0); // Tüm cache'i temizle
            }
            
            _logger.LogInformation("Cache manuel olarak temizlendi");
            
            return Ok(new
            {
                Message = "Cache başarıyla temizlendi",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache temizleme başarısız");
            return StatusCode(500, new { Error = "Cache temizlenemedi" });
        }
    }

    // GET /api/security/blocked-ips
    [HttpGet("blocked-ips")]
    public ActionResult<object> GetBlockedIPs()
    {
        try
        {
            var blockedIps = GetBlockedIPsList();
            
            return Ok(new
            {
                BlockedIPs = blockedIps,
                Count = blockedIps.Count,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bloklanan IP listesi alınamadı");
            return StatusCode(500, new { Error = "IP listesi alınamadı" });
        }
    }

    private int GetCacheEntryCount()
    {
        // Basit cache entry sayısı - gerçek implementasyonda daha detaylı olabilir
        return 0; // Placeholder
    }

    private int GetBlockedIpCount()
    {
        // Cache'de RateLimiting pattern'lerini say
        var blockedCount = 0;
        // Implementasyon gerekebilir
        return blockedCount;
    }

    private List<string> GetBlockedIPsList()
    {
        // Cache'de bloklanan IP'leri listele
        var blockedIPs = new List<string>();
        // Implementasyon gerekebilir
        return blockedIPs;
    }

    private List<string> GetHealthRecommendations(MemoryStats stats)
    {
        var recommendations = new List<string>();
        
        if (stats.MemoryUsagePercentage > 80)
        {
            recommendations.Add("Memory usage yüksek - Cache temizlemeyi düşünün");
        }
        
        if (stats.CacheUsagePercentage > 80)
        {
            recommendations.Add("Cache usage yüksek - TTL sürelerini azaltın");
        }
        
        if (stats.AvailableSemaphoreSlots < 5)
        {
            recommendations.Add("Concurrent operation slots azaldı - Sistem yükü yüksek");
        }
        
        if (recommendations.Count == 0)
        {
            recommendations.Add("Sistem sağlıklı durumda");
        }
        
        return recommendations;
    }
}