# 200 Eşzamanlı Kullanıcı - Sistem Analizi ve Load Testing Rehberi

## 📈 Mevcut Sistem Kapasitesi

### CX23 Sunucu Özellikleri
- **CPU**: 2 vCPU
- **RAM**: 4 GB
- **Disk**: 40 GB SSD
- **Network**: 20 TB aylık trafik

### Mevcut Limitlerimiz
```yaml
# Rate Limiting (RateLimitingMiddleware.cs)
MAX_REQUESTS_PER_MINUTE: 60    # Kullanıcı başına dakikada 60 istek
MAX_REQUESTS_PER_HOUR: 1000    # Kullanıcı başına saatte 1000 istek

# Memory Protection (MemoryProtectionService.cs)  
MAX_CACHE_SIZE_BYTES: 100MB     # Maksimum cache boyutu
MAX_CONCURRENT_OPERATIONS: 50   # Eşzamanlı işlem sınırı
MAX_CACHE_ENTRIES: 10000        # Maksimum cache entry

# Nginx Rate Limiting (nginx.conf)
api_limit: 10r/s               # Saniyede 10 istek (IP başına)
security_limit: 5r/s            # Security endpoint: 5 istek/s
burst: 20                       # 20 istek burst izni
connection_limit: 10            # 10 eşzamanlı bağlantı/IP
```

## 🚨 200 Kullanıcı Senaryosu - Ne Olur?

### Senaryo 1: Normal Kullanım (Her kullanıcı dakikada 1 istek)
```
200 kullanıcı × 1 istek/dk = 200 istek/dakika
200 ÷ 60 = ~3.3 istek/saniye
```
✅ **SONUÇ**: Sistem RAHATLIKLA karşılar!

### Senaryo 2: Yoğun Kullanım (Her kullanıcı saniyede 1 istek)
```
200 kullanıcı × 1 istek/s = 200 istek/saniye
```
❌ **SONUÇ**: Sistem ÇÖKER! (Nginx limit: 10r/s IP başına)

### Senaryo 3: Saldırı Senaryosu (200 kullanıcı aynı anda)
```
200 kullanıcı × 60 istek/dk = 12,000 istek/dakika
12,000 ÷ 60 = 200 istek/saniye
```
❌ **SONUÇ**: Rate limiting devreye girer, çoğu kullanıcı 429 alır

## 🔧 Yeni Konfigürasyon Önerileri

### 1. Esnek Rate Limiting (200 Kullanıcı için)
```csharp
// RateLimitingMiddleware.cs - GÜNCELLE
private const int MAX_REQUESTS_PER_MINUTE = 120;    // 60'tan 120'ye
private const int MAX_REQUESTS_PER_HOUR = 2000;    // 1000'den 2000'ye
private const int BURST_REQUESTS = 10;            // İlk saniyede 10 istek izni
```

### 2. Connection Pool Ayarları
```csharp
// Program.cs - HTTP Client optimization
builder.Services.AddHttpClient("GitHubClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent", "RiskyWebsitesAPI/1.0");
})
.SetHandlerLifetime(TimeSpan.FromMinutes(5))  // 5 dakika connection pool
.AddPolicyHandler(GetRetryPolicy());
```

### 3. Memory Optimization
```csharp
// MemoryProtectionService.cs - 200 kullanıcı için
private const long MAX_CACHE_SIZE_BYTES = 200 * 1024 * 1024;  // 100MB'tan 200MB'ye
private const int MAX_CONCURRENT_OPERATIONS = 100;          // 50'den 100'e
private const int MAX_CACHE_ENTRIES = 20000;               // 10K'dan 20K'ye
```

## 📋 Load Testing Senaryoları

### Test 1: Basit Load Test (Apache Bench)
```bash
# 200 istek, 10 eşzamanlı
ab -n 200 -c 10 http://95.217.1.184/api/RiskCheck/check?domain=test.com

# 1000 istek, 50 eşzamanlı (daha agresif)
ab -n 1000 -c 50 http://95.217.1.184/api/RiskCheck/check?domain=test.com
```

### Test 2: Artımlı Yük Testi (Hey)
```bash
# GitHub'dan hey tool'u indir
wget https://github.com/rakyll/hey/releases/download/v0.1.4/hey_linux_amd64
chmod +x hey_linux_amd64

# 200 kullanıcı, 30 saniye boyunca
./hey_linux_amd64 -n 2000 -c 200 -t 30 -q 10 \
  "http://95.217.1.184/api/RiskCheck/check?domain=test.com"
```

### Test 3: Gerçekçi Senaryo Testi
```bash
# Farklı domain'lerle test
for domain in {google,facebook,youtube,amazon,twitter}.com; do
  echo "Testing $domain with 200 concurrent users..."
  ./hey_linux_amd64 -n 100 -c 200 \
    "http://95.217.1.184/api/RiskCheck/check?domain=$domain"
done
```

## 🚀 Optimizasyon Stratejileri

### 1. Response Caching (Redis Integration)
```csharp
// Redis caching for 200 concurrent users
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "redis:6379";
    options.InstanceName = "RiskyWebsitesAPI";
});

// Cache responses for 5 minutes
[ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
public async Task<ActionResult<RiskCheckResponse>> Get([FromQuery] string url)
```

### 2. Connection Pool Optimization
```csharp
// Program.cs
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxConcurrentConnections = 200;        // 200 eşzamanlı bağlantı
    options.Limits.MaxRequestBodySize = 1024;            // 1KB max request size
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});
```

### 3. Async Processing
```csharp
// RiskDomainService.cs - Async enumerable for large datasets
public async IAsyncEnumerable<string> ProcessDomainsAsync(IEnumerable<string> domains)
{
    foreach (var domain in domains)
    {
        var result = await CheckDomainAsync(domain);
        yield return result;
    }
}
```

## 📊 Monitoring Dashboard

### Real-time Metrics Endpoint
```bash
# Sistem metrikleri (her 5 saniyede bir güncellenir)
curl http://95.217.1.184/api/security/stats

# Detaylı performans metrikleri
curl http://95.217.1.184/api/security/performance
```

### Key Performance Indicators (KPIs)
```json
{
  "concurrentUsers": 200,
  "requestsPerSecond": 45.2,
  "averageResponseTime": "85ms",
  "errorRate": 0.1,
  "cpuUsage": 65,
  "memoryUsage": 78,
  "cacheHitRate": 92.5
}
```

## 🎯 Scaling Stratejileri

### 1. Vertical Scaling (CX23 Üzerinde)
```bash
# Docker container limits güncelle
# docker-compose.yml
deploy:
  resources:
    limits:
      cpus: '1.5'      # 0.5'tan 1.5'e
      memory: 1G       # 512MB'tan 1GB'ye
    reservations:
      cpus: '1.0'
      memory: 512M
```

### 2. Horizontal Scaling (Multiple Instances)
```yaml
# docker-compose.scale.yml
services:
  risky-websites-api:
    deploy:
      replicas: 2
      update_config:
        parallelism: 1
        delay: 10s
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
```

### 3. Load Balancing (Nginx)
```nginx
# nginx-scale.conf
upstream api_cluster {
    least_conn;
    server risky-websites-api-1:5000 weight=2 max_fails=3 fail_timeout=30s;
    server risky-websites-api-2:5000 weight=1 max_fails=3 fail_timeout=30s;
    keepalive 64;
}
```

## ⚠️ Sınırlar ve Kapasite

### Mevcut CX23 Sunucu İçin
| Metrik | Mevcut | Önerilen Maksimum |
|--------|--------|-------------------|
| Eşzamanlı Kullanıcı | 50 | **150** |
| İstek/Saniye | 10 | **50** |
| Bellek Kullanımı | 512MB | **2GB** |
| CPU Kullanımı | 50% | **80%** |
| Response Time | <100ms | **<500ms** |

### Sonuç: 200 Kullanıcı İçin
- ✅ **Başarılı**: 150 kullanıcıya kadar (optimizasyon ile)
- ⚠️ **Sınıra yakın**: 150-180 kullanıcı (dikkatli monitoring)
- ❌ **Başarısız**: 200+ kullanıcı (daha güçlü sunucu gerekli)

## 🚀 Önerilen Eylem Planı

### 1. Hemen Uygula (5 dk)
```bash
# Rate limiting limitlerini güncelle
sed -i 's/MAX_REQUESTS_PER_MINUTE = 60/MAX_REQUESTS_PER_MINUTE = 120/g' Security/RateLimitingMiddleware.cs
sed -i 's/MAX_REQUESTS_PER_HOUR = 1000/MAX_REQUESTS_PER_HOUR = 2000/g' Security/RateLimitingMiddleware.cs
```

### 2. Load Test Et (15 dk)
```bash
# Gerçekçi load test
./hey_linux_amd64 -n 1000 -c 200 -t 60 \
  "http://95.217.1.184/api/RiskCheck/check?domain=test.com"
```

### 3. Monitoring Kur (10 dk)
```bash
# Real-time monitoring
watch -n 5 'curl -s http://95.217.1.184/api/security/stats | jq'
```

### 4. Gerekiyorsa Scale Et (30 dk)
```bash
# Daha güçlü sunucuya geçiş planı
# Veya load balancer + multiple instances
```

**SONUÇ**: 200 kullanıcı için **optimize edilmiş konfigürasyon** ile **150 kullanıcıya kadar** güvenli çalışma sağlanabilir. Daha fazlası için **daha güçlü sunucu** veya **horizontal scaling** gerekir! 🚀