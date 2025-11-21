using RiskyWebsitesAPI.Security;
using System.Text.Json;

namespace RiskyWebsitesAPI.Middleware;

// Request logging middleware - Güvenlik ve monitoring için
public class SecurityLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityLoggingMiddleware> _logger;
    private readonly IMemoryCache _cache;
    
    // Saldırı tespiti için eşik değerleri
    private const int SUSPICIOUS_REQUESTS_PER_MINUTE = 30;
    private const int BLOCKED_REQUESTS_THRESHOLD = 3;

    public SecurityLoggingMiddleware(RequestDelegate next, ILogger<SecurityLoggingMiddleware> logger, IMemoryCache cache)
    {
        _next = next;
        _logger = logger;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        var clientIp = GetClientIp(context);
        var requestPath = context.Request.Path;
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        
        // Request detaylarını logla
        _logger.LogInformation($"Request: {clientIp} {context.Request.Method} {requestPath}");
        
        // Şüpheli aktivite kontrolü
        if (await IsSuspiciousActivity(clientIp, requestPath))
        {
            _logger.LogWarning($"Şüpheli aktivite tespit edildi: {clientIp} - {requestPath}");
        }
        
        // Response'u yakalamak için stream'i değiştir
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        
        try
        {
            await _next(context);
            
            // Response durumunu logla
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation($"Response: {clientIp} {context.Response.StatusCode} {duration.TotalMilliseconds}ms");
            
            // Hatalı response'ları logla
            if (context.Response.StatusCode >= 400)
            {
                _logger.LogWarning($"HTTP Error: {clientIp} {context.Response.StatusCode} {requestPath}");
            }
            
            // Response body'yi kopyala
            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception: {clientIp} {requestPath}");
            throw;
        }
    }

    private async Task<bool> IsSuspiciousActivity(string clientIp, string requestPath)
    {
        var minuteKey = $"SuspiciousActivity:{clientIp}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var blockedKey = $"BlockedIPs:{clientIp}";
        
        // Zaten bloklanmış mı?
        if (_cache.TryGetValue(blockedKey, out _))
        {
            return true;
        }
        
        // Dakikalık istek sayısı
        var requestCount = _cache.GetOrCreate(minuteKey, entry =>
        {
            entry.AbsoluteExpiration = DateTime.UtcNow.AddMinutes(1);
            return 0;
        });
        
        requestCount++;
        _cache.Set(minuteKey, requestCount, TimeSpan.FromMinutes(1));
        
        // Şüpheli aktivite tespiti - 200 kullanıcı için güncellendi
        if (requestCount > SUSPICIOUS_REQUESTS_PER_MINUTE * 2) // 60'tan 120'ye
        {
            // IP'yi geçici olarak blokla
            _cache.Set(blockedKey, true, TimeSpan.FromMinutes(15));
            
            _logger.LogWarning($"IP {clientIp} şüpheli aktivite nedeniyle bloklandı. İstek sayısı: {requestCount}");
            return true;
        }
        
        // Hızlı ardışık istekler (aynı saniye içinde)
        var secondKey = $"RapidRequests:{clientIp}:{DateTime.UtcNow:yyyyMMddHHmmss}";
        var rapidRequests = _cache.GetOrCreate(secondKey, entry =>
        {
            entry.AbsoluteExpiration = DateTime.UtcNow.AddSeconds(1);
            return 0;
        });
        
        rapidRequests++;
        _cache.Set(secondKey, rapidRequests, TimeSpan.FromSeconds(1));
        
        if (rapidRequests > 20) // Saniyede 20+ istek (10'dan 20'ye)
        {
            _logger.LogWarning($"Hızlı istek tespit edildi: {clientIp} - {rapidRequests} istek/saniye");
            return true;
        }
        
        return false;
    }

    private string GetClientIp(HttpContext context)
    {
        // X-Forwarded-For header kontrolü
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
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
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

// Extension method
public static class SecurityLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityLoggingMiddleware>();
    }
}