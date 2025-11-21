using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace RiskyWebsitesAPI.Security;

// Circuit breaker pattern - Dış servis çağrıları için
public class CircuitBreakerService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CircuitBreakerService> _logger;
    
    // Circuit breaker ayarları
    private const int FAILURE_THRESHOLD = 5; // 5 başarısız istek
    private const int TIME_WINDOW_SECONDS = 60; // 60 saniye içinde
    private const int OPEN_DURATION_SECONDS = 300; // 5 dakika açık kalır
    private const int HALF_OPEN_MAX_REQUESTS = 3; // Half-open durumunda 3 istek test et

    public CircuitBreakerService(IMemoryCache cache, ILogger<CircuitBreakerService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(string serviceName, Func<Task<T>> operation)
    {
        var circuitState = GetCircuitState(serviceName);
        
        switch (circuitState.State)
        {
            case CircuitState.Open:
                _logger.LogWarning($"Circuit breaker AÇIK for {serviceName}");
                throw new CircuitBreakerOpenException($"Circuit breaker is open for {serviceName}");
                
            case CircuitState.HalfOpen:
                if (circuitState.HalfOpenRequests >= HALF_OPEN_MAX_REQUESTS)
                {
                    _logger.LogWarning($"Circuit breaker HALF-OPEN limit reached for {serviceName}");
                    throw new CircuitBreakerOpenException($"Half-open limit reached for {serviceName}");
                }
                break;
        }

        try
        {
            var result = await operation();
            OnSuccess(serviceName);
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(serviceName);
            _logger.LogError(ex, $"Circuit breaker hata for {serviceName}");
            throw;
        }
    }

    private CircuitStateInfo GetCircuitState(string serviceName)
    {
        var key = $"CircuitBreaker:{serviceName}";
        return _cache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);
            return new CircuitStateInfo
            {
                ServiceName = serviceName,
                State = CircuitState.Closed,
                FailureCount = 0,
                LastFailureTime = null,
                HalfOpenRequests = 0
            };
        }) ?? new CircuitStateInfo { ServiceName = serviceName };
    }

    private void OnSuccess(string serviceName)
    {
        var state = GetCircuitState(serviceName);
        
        if (state.State == CircuitState.HalfOpen)
        {
            state.HalfOpenRequests++;
            if (state.HalfOpenRequests >= HALF_OPEN_MAX_REQUESTS)
            {
                // Circuit breaker'ı kapat
                state.State = CircuitState.Closed;
                state.FailureCount = 0;
                state.HalfOpenRequests = 0;
                _logger.LogInformation($"Circuit breaker KAPANDI for {serviceName}");
            }
        }
        else if (state.State == CircuitState.Closed)
        {
            // Başarılı istek sayacı sıfırla
            state.FailureCount = 0;
        }

        UpdateCircuitState(serviceName, state);
    }

    private void OnFailure(string serviceName)
    {
        var state = GetCircuitState(serviceName);
        var now = DateTime.UtcNow;
        
        if (state.State == CircuitState.Closed)
        {
            // Eski failure'lar zaman penceresi dışındaysa sıfırla
            if (state.LastFailureTime.HasValue && 
                (now - state.LastFailureTime.Value).TotalSeconds > TIME_WINDOW_SECONDS)
            {
                state.FailureCount = 0;
            }
            
            state.FailureCount++;
            state.LastFailureTime = now;
            
            if (state.FailureCount >= FAILURE_THRESHOLD)
            {
                // Circuit breaker'ı aç
                state.State = CircuitState.Open;
                state.OpenUntil = now.AddSeconds(OPEN_DURATION_SECONDS);
                _logger.LogError($"Circuit breaker AÇILDI for {serviceName}");
            }
        }
        else if (state.State == CircuitState.HalfOpen)
        {
            // Half-open durumunda failure olursa tekrar aç
            state.State = CircuitState.Open;
            state.OpenUntil = now.AddSeconds(OPEN_DURATION_SECONDS);
            state.HalfOpenRequests = 0;
            _logger.LogError($"Circuit breaker tekrar AÇILDI for {serviceName}");
        }

        UpdateCircuitState(serviceName, state);
    }

    private void UpdateCircuitState(string serviceName, CircuitStateInfo state)
    {
        var key = $"CircuitBreaker:{serviceName}";
        _cache.Set(key, state, TimeSpan.FromMinutes(10));
    }
}

public class CircuitStateInfo
{
    public string ServiceName { get; set; } = "";
    public CircuitState State { get; set; } = CircuitState.Closed;
    public int FailureCount { get; set; } = 0;
    public DateTime? LastFailureTime { get; set; }
    public DateTime? OpenUntil { get; set; }
    public int HalfOpenRequests { get; set; } = 0;
}

public enum CircuitState
{
    Closed,     // Normal çalışma
    Open,       // Hizmet kapalı
    HalfOpen    // Test modu
}

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}