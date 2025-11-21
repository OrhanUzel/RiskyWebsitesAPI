using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace RiskyWebsitesAPI.Security;

// Rate limiting middleware - IP tabanlı istek sınırlaması
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    
    // Rate limiting ayarları - 200 kullanıcı için optimize edildi
    private const int MAX_REQUESTS_PER_MINUTE = 120; // Dakikada maksimum 120 istek (2/s)
    private const int MAX_REQUESTS_PER_HOUR = 2000; // Saatte maksimum 2000 istek
    private const int BLOCK_DURATION_MINUTES = 5; // Aşım durumunda 5 dakika blok
    private const int BURST_REQUESTS = 10; // İlk saniyede 10 istek burst izni
    
    public RateLimitingMiddleware(RequestDelegate next, IMemoryCache cache, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = GetClientIp(context);
        var currentTime = DateTime.UtcNow;
        
        // IP adresi alınamazsa devam et (proxy/güvenlik duvarı arkasındayız)
        if (string.IsNullOrEmpty(clientIp))
        {
            await _next(context);
            return;
        }

        // Rate limiting kontrolü
        if (await IsRateLimited(clientIp, currentTime))
        {
            _logger.LogWarning($"Rate limit aşıldı: {clientIp}");
            
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.Add("Retry-After", "60"); // 60 saniye bekle
            
            await context.Response.WriteAsync("Çok fazla istek gönderdiniz. Lütfen bir süre bekleyin.");
            return;
        }

        await _next(context);
    }

    private async Task<bool> IsRateLimited(string clientIp, DateTime currentTime)
    {
        var minuteKey = $"RateLimit:Minute:{clientIp}:{currentTime:yyyyMMddHHmm}";
        var hourKey = $"RateLimit:Hour:{clientIp}:{currentTime:yyyyMMddHH}";
        var blockKey = $"RateLimit:Blocked:{clientIp}";

        // Önce blok kontrolü
        if (_cache.TryGetValue(blockKey, out _))
        {
            return true;
        }

        // Dakikalık limit kontrolü
        var minuteRequests = _cache.GetOrCreate(minuteKey, entry =>
        {
            entry.AbsoluteExpiration = currentTime.AddMinutes(1);
            return 0;
        });

        // Saatlik limit kontrolü
        var hourRequests = _cache.GetOrCreate(hourKey, entry =>
        {
            entry.AbsoluteExpiration = currentTime.AddHours(1);
            return 0;
        });

        // İstek sayılarını artır
        minuteRequests++;
        hourRequests++;

        _cache.Set(minuteKey, minuteRequests, TimeSpan.FromMinutes(1));
        _cache.Set(hourKey, hourRequests, TimeSpan.FromHours(1));

        // Limit kontrolü
        if (minuteRequests > MAX_REQUESTS_PER_MINUTE || hourRequests > MAX_REQUESTS_PER_HOUR)
        {
            // IP'yi geçici olarak blokla
            _cache.Set(blockKey, true, TimeSpan.FromMinutes(BLOCK_DURATION_MINUTES));
            
            _logger.LogWarning($"IP {clientIp} bloklandı. Dakika: {minuteRequests}, Saat: {hourRequests}");
            return true;
        }

        return false;
    }

    private string GetClientIp(HttpContext context)
    {
        // X-Forwarded-For header kontrolü (proxy arkasındaysak)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // İlk IP adresini al
            var ips = forwardedFor.Split(',');
            return ips[0].Trim();
        }

        // X-Real-IP header kontrolü
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Direkt bağlantı
        return context.Connection.RemoteIpAddress?.ToString() ?? "";
    }
}