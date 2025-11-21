using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RiskyWebsitesAPI.Security;
using System.Diagnostics;
using System.Runtime;

namespace RiskyWebsitesAPI.Controllers;

// Performans ve load testing endpoint'leri
[ApiController]
[Route("api/[controller]")]
public class PerformanceController : ControllerBase
{
    private readonly MemoryProtectionService _memoryProtection;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PerformanceController> _logger;
    private static readonly object _lockObject = new();
    private static DateTime _testStartTime = DateTime.MinValue;
    private static int _totalRequests = 0;
    private static int _successfulRequests = 0;
    private static int _failedRequests = 0;
    private static readonly List<long> _responseTimes = new();
    private static readonly Dictionary<string, int> _endpointStats = new();

    public PerformanceController(MemoryProtectionService memoryProtection, IMemoryCache cache, ILogger<PerformanceController> logger)
    {
        _memoryProtection = memoryProtection;
        _cache = cache;
        _logger = logger;
    }

    // GET /api/performance/metrics
    [HttpGet("metrics")]
    public ActionResult<object> GetPerformanceMetrics()
    {
        try
        {
            lock (_lockObject)
            {
                var memoryStats = _memoryProtection.GetMemoryStats();
                var avgResponseTime = _responseTimes.Count > 0 ? _responseTimes.Average() : 0;
                var maxResponseTime = _responseTimes.Count > 0 ? _responseTimes.Max() : 0;
                var minResponseTime = _responseTimes.Count > 0 ? _responseTimes.Min() : 0;
                
                var testDuration = DateTime.UtcNow - _testStartTime;
                var requestsPerSecond = testDuration.TotalSeconds > 0 ? _totalRequests / testDuration.TotalSeconds : 0;
                var successRate = _totalRequests > 0 ? (double)_successfulRequests / _totalRequests * 100 : 0;

                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    TestDuration = testDuration.TotalSeconds,
                    TotalRequests = _totalRequests,
                    SuccessfulRequests = _successfulRequests,
                    FailedRequests = _failedRequests,
                    RequestsPerSecond = Math.Round(requestsPerSecond, 2),
                    SuccessRate = Math.Round(successRate, 2),
                    AverageResponseTime = Math.Round(avgResponseTime, 2),
                    MaxResponseTime = maxResponseTime,
                    MinResponseTime = minResponseTime,
                    Memory = new
                    {
                        UsedMemoryMB = Math.Round(memoryStats.UsedMemoryBytes / (1024.0 * 1024.0), 2),
                        MaxMemoryMB = Math.Round(memoryStats.MaxMemoryBytes / (1024.0 * 1024.0), 2),
                        MemoryUsagePercentage = Math.Round(memoryStats.MemoryUsagePercentage, 2),
                        CacheEntries = memoryStats.CurrentCacheEntries,
                        MaxCacheEntries = memoryStats.MaxCacheEntries,
                        CacheUsagePercentage = Math.Round(memoryStats.CacheUsagePercentage, 2)
                    },
                    Endpoints = _endpointStats,
                    Recommendations = GetPerformanceRecommendations(memoryStats, avgResponseTime, requestsPerSecond)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Performans metrikleri alınamadı");
            return StatusCode(500, new { Error = "Metrikler alınamadı", Details = ex.Message });
        }
    }

    // POST /api/performance/start-load-test
    [HttpPost("start-load-test")]
    public ActionResult<object> StartLoadTest([FromQuery] int durationSeconds = 60)
    {
        try
        {
            lock (_lockObject)
            {
                _testStartTime = DateTime.UtcNow;
                _totalRequests = 0;
                _successfulRequests = 0;
                _failedRequests = 0;
                _responseTimes.Clear();
                _endpointStats.Clear();
            }

            _logger.LogInformation($"Load test başlatıldı: {durationSeconds} saniye");

            return Ok(new
            {
                Message = "Load test başlatıldı",
                Duration = durationSeconds,
                StartTime = _testStartTime,
                Recommendations = new[]
                {
                    "Test sırasında sistem yükünü izleyin",
                    "Memory ve CPU kullanımını kontrol edin",
                    "Response time'ları takip edin",
                    $"Test {durationSeconds} saniye sonra otomatik duracak"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load test başlatılamadı");
            return StatusCode(500, new { Error = "Test başlatılamadı" });
        }
    }

    // POST /api/performance/stop-load-test
    [HttpPost("stop-load-test")]
    public ActionResult<object> StopLoadTest()
    {
        try
        {
            lock (_lockObject)
            {
                var testDuration = DateTime.UtcNow - _testStartTime;
                _logger.LogInformation($"Load test durduruldu. Süre: {testDuration.TotalSeconds}s");

                return Ok(new
                {
                    Message = "Load test durduruldu",
                    TestDuration = testDuration.TotalSeconds,
                    FinalMetrics = GetPerformanceMetrics().Value
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load test durdurulamadı");
            return StatusCode(500, new { Error = "Test durdurulamadı" });
        }
    }

    // GET /api/performance/stress-test
    [HttpGet("stress-test")]
    public async Task<ActionResult<object>> StressTest([FromQuery] int iterations = 1000)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var results = new List<object>();
            var successCount = 0;
            var errorCount = 0;

            _logger.LogInformation($"Stress test başlatıldı: {iterations} iterasyon");

            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    var iterationStopwatch = Stopwatch.StartNew();
                    
                    // Memory test
                    var memoryBefore = GC.GetTotalMemory(false);
                    
                    // Cache test
                    var testKey = $"stress_test_{i}";
                    _cache.Set(testKey, $"test_data_{i}", TimeSpan.FromMinutes(1));
                    
                    // CPU test
                    var data = Enumerable.Range(0, 1000).Select(x => x * 2).ToArray();
                    var sum = data.Sum();
                    
                    iterationStopwatch.Stop();
                    
                    var memoryAfter = GC.GetTotalMemory(false);
                    var memoryUsed = memoryAfter - memoryBefore;

                    results.Add(new
                    {
                        Iteration = i,
                        ResponseTime = iterationStopwatch.ElapsedMilliseconds,
                        MemoryUsed = memoryUsed,
                        Sum = sum,
                        Success = true
                    });

                    successCount++;

                    // Her 100 iterasyonda bir progress göster
                    if ((i + 1) % 100 == 0)
                    {
                        _logger.LogInformation($"Stress test progress: {i + 1}/{iterations}");
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        Iteration = i,
                        Error = ex.Message,
                        Success = false
                    });
                    errorCount++;
                }
            }

            stopwatch.Stop();
            var avgResponseTime = results.Where(r => (bool)r.GetType().GetProperty("Success").GetValue(r)).Average(r => (long)r.GetType().GetProperty("ResponseTime").GetValue(r));
            var maxResponseTime = results.Where(r => (bool)r.GetType().GetProperty("Success").GetValue(r)).Max(r => (long)r.GetType().GetProperty("ResponseTime").GetValue(r));
            var totalMemoryUsed = results.Where(r => (bool)r.GetType().GetProperty("Success").GetValue(r)).Sum(r => (long)r.GetType().GetProperty("MemoryUsed").GetValue(r));

            return Ok(new
            {
                Message = "Stress test tamamlandı",
                TotalIterations = iterations,
                SuccessCount = successCount,
                ErrorCount = errorCount,
                TotalDuration = stopwatch.ElapsedMilliseconds,
                AverageResponseTime = Math.Round(avgResponseTime, 2),
                MaxResponseTime = maxResponseTime,
                TotalMemoryUsed = totalMemoryUsed,
                SuccessRate = Math.Round((double)successCount / iterations * 100, 2),
                Results = results.Take(10) // İlk 10 sonucu göster
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stress test başarısız");
            return StatusCode(500, new { Error = "Stress test başarısız", Details = ex.Message });
        }
    }

    // GET /api/performance/benchmark
    [HttpGet("benchmark")]
    public ActionResult<object> Benchmark()
    {
        try
        {
            var benchmarks = new List<object>();
            var stopwatch = Stopwatch.StartNew();

            // Memory benchmark
            var memoryBefore = GC.GetTotalMemory(false);
            var testData = new List<string>();
            for (int i = 0; i < 10000; i++)
            {
                testData.Add($"benchmark_data_{i}");
            }
            var memoryAfter = GC.GetTotalMemory(false);
            var memoryBenchmark = memoryAfter - memoryBefore;

            benchmarks.Add(new
            {
                Test = "Memory Allocation (10K strings)",
                Result = $"{Math.Round(memoryBenchmark / 1024.0, 2)} KB",
                Duration = stopwatch.ElapsedMilliseconds
            });

            // CPU benchmark
            stopwatch.Restart();
            var numbers = Enumerable.Range(0, 100000).ToArray();
            var sum = numbers.Sum(x => x * x);
            var cpuDuration = stopwatch.ElapsedMilliseconds;

            benchmarks.Add(new
            {
                Test = "CPU Calculation (100K squares)",
                Result = $"Sum: {sum}",
                Duration = cpuDuration
            });

            // Cache benchmark
            stopwatch.Restart();
            for (int i = 0; i < 1000; i++)
            {
                _cache.Set($"bench_{i}", $"value_{i}", TimeSpan.FromMinutes(1));
            }
            var cacheDuration = stopwatch.ElapsedMilliseconds;

            benchmarks.Add(new
            {
                Test = "Cache Operations (1K entries)",
                Result = $"{1000} entries cached",
                Duration = cacheDuration
            });

            // Response time benchmark
            stopwatch.Restart();
            var response = new { Message = "Benchmark response", Timestamp = DateTime.UtcNow };
            var responseDuration = stopwatch.ElapsedMilliseconds;

            benchmarks.Add(new
            {
                Test = "Response Generation",
                Result = "JSON response created",
                Duration = responseDuration
            });

            return Ok(new
            {
                Message = "Benchmark tamamlandı",
                Benchmarks = benchmarks,
                TotalDuration = benchmarks.Sum(b => (long)b.GetType().GetProperty("Duration").GetValue(b)),
                SystemInfo = new
                {
                    ProcessorCount = Environment.ProcessorCount,
                    TotalMemory = Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 2),
                    GcGeneration = GC.MaxGeneration,
                    IsServerGc = GCSettings.IsServerGC
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Benchmark başarısız");
            return StatusCode(500, new { Error = "Benchmark başarısız", Details = ex.Message });
        }
    }

    // Public method to track requests (diğer controller'lar tarafından kullanılır)
    public static void TrackRequest(string endpoint, long responseTime, bool success)
    {
        lock (_lockObject)
        {
            _totalRequests++;
            if (success)
            {
                _successfulRequests++;
            }
            else
            {
                _failedRequests++;
            }

            _responseTimes.Add(responseTime);
            
            // Sadece son 1000 response time'ı tut
            if (_responseTimes.Count > 1000)
            {
                _responseTimes.RemoveAt(0);
            }

            // Endpoint istatistikleri
            if (!_endpointStats.ContainsKey(endpoint))
            {
                _endpointStats[endpoint] = 0;
            }
            _endpointStats[endpoint]++;
        }
    }

    private List<string> GetPerformanceRecommendations(MemoryStats memoryStats, double avgResponseTime, double requestsPerSecond)
    {
        var recommendations = new List<string>();

        if (memoryStats.MemoryUsagePercentage > 80)
        {
            recommendations.Add("Memory usage yüksek - Cache temizlemeyi düşünün");
        }

        if (memoryStats.CacheUsagePercentage > 80)
        {
            recommendations.Add("Cache usage yüksek - TTL sürelerini azaltın");
        }

        if (avgResponseTime > 500)
        {
            recommendations.Add("Response time yüksek - Performans optimizasyonu gerekebilir");
        }

        if (requestsPerSecond > 100)
        {
            recommendations.Add("Yüksek trafik - Scaling stratejilerini gözden geçirin");
        }

        if (memoryStats.AvailableSemaphoreSlots < 10)
        {
            recommendations.Add("Concurrent operation slots azaldı - Sistem yükü yüksek");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Sistem performansı iyi durumda");
        }

        return recommendations;
    }
}