using RiskyWebsitesAPI.Controllers;

namespace RiskyWebsitesAPI.Middleware;

// Performance monitoring middleware - Request tracking için
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;

    public PerformanceMonitoringMiddleware(RequestDelegate next, ILogger<PerformanceMonitoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var endpoint = context.Request.Path;
        var method = context.Request.Method;
        
        try
        {
            await _next(context);
            stopwatch.Stop();
            
            // Başarılı isteği track et
            PerformanceController.TrackRequest(
                endpoint: endpoint,
                responseTime: stopwatch.ElapsedMilliseconds,
                success: context.Response.StatusCode < 400
            );
            
            // Log performance (yavaş istekler için)
            if (stopwatch.ElapsedMilliseconds > 1000) // 1 saniyeden uzun
            {
                _logger.LogWarning($"Yavaş istek tespit edildi: {method} {endpoint} - {stopwatch.ElapsedMilliseconds}ms");
            }
            
            // Detaylı log (debug için)
            if (context.Response.StatusCode >= 400)
            {
                _logger.LogWarning($"HTTP {context.Response.StatusCode}: {method} {endpoint} - {stopwatch.ElapsedMilliseconds}ms");
            }
            else
            {
                _logger.LogInformation($"{method} {endpoint} - {stopwatch.ElapsedMilliseconds}ms - HTTP {context.Response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Hatalı isteği track et
            PerformanceController.TrackRequest(
                endpoint: endpoint,
                responseTime: stopwatch.ElapsedMilliseconds,
                success: false
            );
            
            _logger.LogError(ex, $"İstek hatası: {method} {endpoint} - {stopwatch.ElapsedMilliseconds}ms");
            throw;
        }
    }
}

// Extension method
public static class PerformanceMonitoringMiddlewareExtensions
{
    public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PerformanceMonitoringMiddleware>();
    }
}