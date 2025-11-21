# RiskyWebsitesAPI Scaling Stratejileri

## Mevcut Kapasite (Hetzner CX23 - 2 vCPU, 4GB RAM)

### 200 Kullanıcı İçin Optimize Edilmiş Limitler:
- **Rate Limiting**: 120 istek/dakika (2 istek/saniye) per IP
- **Memory Limit**: 300MB per container
- **Concurrent Operations**: 150
- **Nginx Rate Limit**: 50 istek/saniye
- **Circuit Breaker**: 5 başarısızlık sonrası 30 saniye kapalı

## Scaling Stratejileri

### 1. Vertical Scaling (Dikey Ölçeklendirme)

#### Mevcut Sunucu için Maksimum Kapasite:
```
CX23 -> CX31 -> CX41 -> CX51
2vCPU/4GB -> 4vCPU/8GB -> 8vCPU/16GB -> 16vCPU/32GB
```

**CX31 (4vCPU/8GB) ile:**
- 400-500 eşzamanlı kullanıcı
- 240 istek/dakika rate limit
- 600MB memory limit
- 300 concurrent operations

**CX41 (8vCPU/16GB) ile:**
- 800-1000 eşzamanlı kullanıcı
- 480 istek/dakika rate limit
- 1.2GB memory limit
- 600 concurrent operations

### 2. Horizontal Scaling (Yatay Ölçeklendirme)

#### Docker Swarm ile Load Balancing:
```yaml
# docker-compose.swarm.yml
version: '3.8'
services:
  api:
    image: riskywebsitesapi:latest
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '0.8'
          memory: 1G
        reservations:
          cpus: '0.4'
          memory: 512M
    ports:
      - "8080-8082:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - RateLimit__RequestsPerMinute=120
      - Memory__MaxMemoryMB=300
      - CircuitBreaker__FailureThreshold=5
```

**3 Replika ile:**
- 600-750 eşzamanlı kullanıcı (200 x 3.75)
- Otomatik yük dağılımı
- High availability
- Rolling updates

### 3. Database Scaling (Veritabanı Ölçeklendirme)

#### Redis Cache Layer:
```csharp
// Redis cache implementation
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "RiskyWebsitesAPI";
});

// Cache domains for 1 hour
[HttpGet("check/{domain}")]
[ResponseCache(Duration = 3600, VaryByQueryKeys = new[] { "domain" })]
public async Task<IActionResult> CheckDomain(string domain)
{
    var cacheKey = $"domain_check:{domain}";
    var cached = await _cache.GetStringAsync(cacheKey);
    if (cached != null)
        return Ok(JsonSerializer.Deserialize<RiskCheckResponse>(cached));
    
    // Process and cache result
    var result = await ProcessDomainCheck(domain);
    await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), TimeSpan.FromHours(1));
    return Ok(result);
}
```

**Cache Hit Rate Optimizasyonu:**
- 80% cache hit rate hedefle
- 1 saat TTL (Time To Live)
- LRU (Least Recently Used) eviction policy

### 4. CDN ve Edge Caching

#### Cloudflare Integration:
```nginx
# nginx.conf with Cloudflare headers
location /api/ {
    proxy_pass http://api;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header CF-Connecting-IP $http_cf_connecting_ip;
    
    # Cache static responses
    proxy_cache_valid 200 302 10m;
    proxy_cache_valid 404 1m;
}
```

### 5. Auto-Scaling Policies

#### CPU-Based Scaling:
```yaml
# Kubernetes HPA example
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: riskywebsitesapi-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: riskywebsitesapi
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

#### Custom Metrics Scaling:
```csharp
// Custom metrics endpoint
[HttpGet("metrics/load")]
public IActionResult GetLoadMetrics()
{
    var metrics = new
    {
        activeConnections = _connectionManager.ActiveCount,
        queueLength = _requestQueue.Count,
        responseTime = _metrics.AverageResponseTime,
        errorRate = _metrics.ErrorRate,
        timestamp = DateTime.UtcNow
    };
    
    return Ok(metrics);
}
```

## Scaling Karar Matrisi

### Kullanıcı Sayısına Göre Öneriler:

| Kullanıcı Sayısı | Önerilen Strateji | Sunucu Konfigürasyonu | Replika Sayısı |
|------------------|-------------------|----------------------|----------------|
| 1-200           | Mevcut CX23       | Single container     | 1              |
| 201-500         | Vertical Scaling  | CX31 (4vCPU/8GB)     | 1              |
| 501-1000        | Horizontal + Cache| CX31 (4vCPU/8GB)     | 2-3            |
| 1001-2000       | Full Cluster      | CX41 (8vCPU/16GB)    | 3-5            |
| 2000+           | Auto-scaling      | CX51 (16vCPU/32GB)   | 5-10           |

## Monitoring ve Alerting

### Scaling Trigger'ları:
```yaml
# Prometheus alerting rules
groups:
- name: scaling-alerts
  rules:
  - alert: HighCPUUsage
    expr: cpu_usage_percent > 80
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "High CPU usage detected"
      
  - alert: HighMemoryUsage
    expr: memory_usage_percent > 85
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "High memory usage detected"
      
  - alert: HighResponseTime
    expr: avg_response_time > 2000
    for: 2m
    labels:
      severity: critical
    annotations:
      summary: "High response time detected"
      
  - alert: ScaleUpNeeded
    expr: concurrent_users > 180
    for: 1m
    labels:
      severity: info
    annotations:
      summary: "Consider scaling up for {{ $value }} users"
```

## Uygulama Adımları

### 1. Mevcut Durum Analizi:
```bash
# Mevcut kapasiteyi test et
./load-test-200-users.sh

# Resource kullanımını kontrol et
docker stats riskywebsitesapi
```

### 2. Vertical Scaling Uygulaması:
```bash
# Hetzner sunucu yükseltme
# 1. Sunucuyu durdur
# 2. CX23 -> CX31 yükselt
# 3. Docker limitlerini güncelle
```

### 3. Horizontal Scaling Uygulaması:
```bash
# Docker Swarm başlat
docker swarm init

# Stack deploy et
docker stack deploy -c docker-compose.swarm.yml riskywebsitesapi

# Replika durumunu kontrol et
docker service ls
```

### 4. Monitoring Kurulumu:
```bash
# Prometheus + Grafana kur
./setup-monitoring.sh

# Dashboard'u import et
./import-dashboards.sh
```

## Maliyet Analizi

### Hetzner Maliyetleri (aylık):
- CX23: €4.15
- CX31: €8.30
- CX41: €16.60
- CX51: €33.20

### Önerilen Yol Haritası:
1. **Başlangıç**: CX23 (mevcut) - 200 kullanıcı
2. **Büyüme**: CX31 + 2 replika - 1000 kullanıcı
3. **Olgunluk**: CX41 + 5 replika + Redis - 5000 kullanıcı

Bu stratejiler ile sistemini 200'den 5000+ kullanıcıya kadar ölçeklendirebilirsin.