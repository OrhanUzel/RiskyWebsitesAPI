using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace RiskyWebsitesAPI.Security;

// Memory protection service - Bellek kullanımını sınırlamak için
public class MemoryProtectionService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryProtectionService> _logger;
    
    // Bellek sınırları - 200 kullanıcı için optimize edildi
    private const long MAX_CACHE_SIZE_BYTES = 300 * 1024 * 1024; // 300 MB (100MB'tan)
    private const int MAX_CONCURRENT_OPERATIONS = 150; // 150 eşzamanlı işlem (50'den)
    private const int MAX_CACHE_ENTRIES = 25000; // 25K maksimum cache entry (10K'dan)
    
    private static readonly SemaphoreSlim _semaphore = new(MAX_CONCURRENT_OPERATIONS, MAX_CONCURRENT_OPERATIONS);
    private static int _currentCacheEntries = 0;

    public MemoryProtectionService(IMemoryCache cache, ILogger<MemoryProtectionService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T> ExecuteWithMemoryLimit<T>(Func<Task<T>> operation, string operationName)
    {
        // Bellek kontrolü
        if (IsMemoryLimitExceeded())
        {
            _logger.LogWarning($"Bellek sınırı aşıldı: {operationName}");
            throw new OutOfMemoryException("Bellek sınırı aşıldı. Lütfen daha sonra tekrar deneyin.");
        }

        // Eşzamanlı işlem kontrolü
        if (!_semaphore.Wait(0))
        {
            _logger.LogWarning($"Eşzamanlı işlem sınırı aşıldı: {operationName}");
            throw new InvalidOperationException("Çok fazla eşzamanlı istek. Lütfen daha sonra tekrar deneyin.");
        }

        try
        {
            return await operation();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public bool TryAddCacheEntry(string key, object value, TimeSpan expiration)
    {
        // Cache entry sınırı kontrolü
        if (_currentCacheEntries >= MAX_CACHE_ENTRIES)
        {
            _logger.LogWarning($"Cache entry sınırı aşıldı: {MAX_CACHE_ENTRIES}");
            
            // Eski entry'leri temizlemeye çalış
            CleanupOldCacheEntries();
            
            if (_currentCacheEntries >= MAX_CACHE_ENTRIES)
            {
                return false;
            }
        }

        try
        {
            _cache.Set(key, value, expiration);
            Interlocked.Increment(ref _currentCacheEntries);
            return true;
        }
        catch (OutOfMemoryException)
        {
            _logger.LogError("Bellek yetersiz. Cache entry eklenemedi.");
            return false;
        }
    }

    public void RemoveCacheEntry(string key)
    {
        if (_cache.Remove(key) != null)
        {
            Interlocked.Decrement(ref _currentCacheEntries);
        }
    }

    private bool IsMemoryLimitExceeded()
    {
        // Basit bellek kontrolü - GC kullanımını kontrol et
        var gcMemory = GC.GetTotalMemory(false);
        return gcMemory > MAX_CACHE_SIZE_BYTES;
    }

    private void CleanupOldCacheEntries()
    {
        // Basit cache temizliği - implementasyon gerekebilir
        _logger.LogInformation("Cache temizliği başlatıldı");
        
        // MemoryCache'in kendi temizleme mekanizmasını tetikle
        GC.Collect(2, GCCollectionMode.Optimized);
    }

    public MemoryStats GetMemoryStats()
    {
        return new MemoryStats
        {
            CurrentCacheEntries = _currentCacheEntries,
            MaxCacheEntries = MAX_CACHE_ENTRIES,
            UsedMemoryBytes = GC.GetTotalMemory(false),
            MaxMemoryBytes = MAX_CACHE_SIZE_BYTES,
            AvailableSemaphoreSlots = _semaphore.CurrentCount,
            MaxSemaphoreSlots = MAX_CONCURRENT_OPERATIONS
        };
    }
}

public class MemoryStats
{
    public int CurrentCacheEntries { get; set; }
    public int MaxCacheEntries { get; set; }
    public long UsedMemoryBytes { get; set; }
    public long MaxMemoryBytes { get; set; }
    public int AvailableSemaphoreSlots { get; set; }
    public int MaxSemaphoreSlots { get; set; }
    
    public double CacheUsagePercentage => (double)CurrentCacheEntries / MaxCacheEntries * 100;
    public double MemoryUsagePercentage => (double)UsedMemoryBytes / MaxMemoryBytes * 100;
}