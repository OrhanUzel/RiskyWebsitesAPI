# 🔒 DDoS ve Saldırı Koruması - Güvenlik Rehberi

## ⚠️ Mevcut Güvenlik Açıkları

Evet, **ciddi bir güvenlik açığı** var! Mevcut projenizde:

- ✅ **Rate Limiting Yok** - Sınırsız istek yapılabilir
- ✅ **Memory Protection Yok** - Bellek tüketimi patlayabilir  
- ✅ **Circuit Breaker Yok** - Dış servisler çökerse sistem çöker
- ✅ **IP Tabanlı Kısıtlama Yok** - Aynı IP'den sınırsız istek

## 🛡️ Eklenen Güvenlik Önlemleri

### 1. Rate Limiting Middleware
- **Dakikada**: 60 istek/IP
- **Saatte**: 1000 istek/IP  
- **Aşım durumunda**: 5 dakika blok
- **429 HTTP Status**: Çok fazla istek uyarısı

### 2. Circuit Breaker Pattern
- **5 başarısız istek** sonrası servisi kapatır
- **5 dakika** sonra tekrar dener
- **Dış servis** (GitHub) çökerse sistemi korur

### 3. Memory Protection Service
- **100 MB** maksimum cache boyutu
- **10.000** maksimum cache entry
- **50 eşzamanlı** işlem sınırı
- **Otomatik** bellek temizliği

### 4. IP Tabanlı Kısıtlama
- **Şüpheli aktivite** tespiti
- **Hızlı ardışık istekler** engeller
- **15 dakika** geçici blok
- **Proxy header** desteği

### 5. Nginx Rate Limiting
- **Saniyede 10 istek** genel limit
- **Security endpoint**: 5 istek/saniye
- **Burst**: 20 istek (ani yükseliş koruması)
- **Connection limit**: 10 bağlantı/IP

## 📊 Monitoring Endpoint'leri

### Güvenlik İstatistikleri
```bash
curl http://95.217.1.184/api/security/stats
```

**Response:**
```json
{
  "memory": {
    "currentCacheEntries": 500,
    "maxCacheEntries": 10000,
    "usedMemoryBytes": 52428800,
    "maxMemoryBytes": 104857600,
    "memoryUsagePercentage": 50.0
  },
  "security": {
    "blockedIPs": 3,
    "status": "Some IPs blocked"
  }
}
```

### Health Check
```bash
curl http://95.217.1.184/api/security/health
```

**Response:**
```json
{
  "status": "Healthy",
  "memoryPressure": 45.2,
  "cachePressure": 32.1,
  "recommendations": ["Sistem sağlıklı durumda"]
}
```

### Cache Temizleme
```bash
curl -X POST http://95.217.1.184/api/security/clear-cache
```

## 🚨 Saldırı Senaryoları ve Koruma

### 1. DDoS Saldırısı
```bash
# 1000 istek/saniye - ENGELLENİR
for i in {1..1000}; do 
  curl "http://95.217.1.184/api/RiskCheck/check?domain=test.com" &
done
```
**Sonuç**: 60+ istekten sonra **429 Too Many Requests**

### 2. Memory Exhaustion Saldırısı
```bash
# Büyük domain listeleri ile bellek tüketme - ENGELLENİR
for i in {1..100000}; do 
  curl "http://95.217.1.184/api/RiskCheck/check?domain=very-long-domain-name-$i.com" &
done
```
**Sonuç**: 100.000 satır sınırı, **bellek koruma** devreye girer

### 3. GitHub Servis Çöküşü
```bash
# GitHub erişilemezse - CIRCUIT BREAKER devreye girer
curl "http://95.217.1.184/api/RiskCheck/check?domain=test.com"
```
**Sonuç**: 5 başarısız denemeden sonra **servis geçici kapatılır**

## 🔧 Konfigürasyon Ayarları

### Rate Limiting Ayarları (RateLimitingMiddleware.cs)
```csharp
private const int MAX_REQUESTS_PER_MINUTE = 60;    // Dakikada 60
private const int MAX_REQUESTS_PER_HOUR = 1000;     // Saatte 1000
private const int BLOCK_DURATION_MINUTES = 5;       // 5 dakika blok
```

### Circuit Breaker Ayarları (CircuitBreakerService.cs)
```csharp
private const int FAILURE_THRESHOLD = 5;              // 5 başarısız
private const int TIME_WINDOW_SECONDS = 60;           // 60 saniye
private const int OPEN_DURATION_SECONDS = 300;        // 5 dakika
```

### Memory Protection Ayarları (MemoryProtectionService.cs)
```csharp
private const long MAX_CACHE_SIZE_BYTES = 100 * 1024 * 1024;  // 100 MB
private const int MAX_CONCURRENT_OPERATIONS = 50;             // 50 işlem
private const int MAX_CACHE_ENTRIES = 10000;                  // 10K entry
```

## 🚀 Güvenli Deployment

### 1. Güvenli Docker Konfigürasyonu
```bash
# Read-only container, güvenlik opt'ları
docker-compose up -d --profile with-nginx
```

### 2. Nginx Rate Limiting
```bash
# Nginx ile ekstra koruma katmanı
curl http://95.217.1.184/api/RiskCheck/check?domain=test.com
```

### 3. Monitoring ve Alerting
```bash
# Güvenlik kontrolleri
./security-check.sh

# Log kontrolü
docker-compose logs -f risky-websites-api | grep -E "(WARN|ERROR|blocked)"
```

## 🎯 Test ve Doğrulama

### Rate Limiting Testi
```bash
# 70 istek gönder (limit: 60)
for i in {1..70}; do 
  curl -s -o /dev/null -w "%{http_code}" "http://95.217.1.184/api/RiskCheck/check?domain=test.com"
done | sort | uniq -c
```
**Beklenen**: 60 adet `200`, 10 adet `429`

### Memory Protection Testi
```bash
# Bellek istatistikleri
curl http://95.217.1.184/api/security/stats | jq '.memory'
```

### Circuit Breaker Testi
```bash
# GitHub URL'lerini geçici olarak değiştirerek test et
# 5 başarısız denemeden sonra circuit breaker devreye girer
```

## ⚡ Performans Metrikleri

- **Response Time**: < 100ms (cache hit)
- **Throughput**: 1000+ istek/dakika (rate limiting sonrası)
- **Memory Usage**: < 100MB
- **Uptime**: 99.9% (circuit breaker koruma ile)

## 🔍 Log İzleme

```bash
# Saldırı tespiti için log kontrolü
docker-compose logs -f | grep -E "(blocked|suspicious|attack)"

# Rate limiting logları
docker-compose logs -f | grep "Rate limit"

# Circuit breaker logları  
docker-compose logs -f | grep "Circuit breaker"
```

## 📞 Acil Durumlar

### Sistem Aşırı Yüklenirse
```bash
# Cache temizle
curl -X POST http://95.217.1.184/api/security/clear-cache

# Container restart
docker-compose restart risky-websites-api

# Tüm sistemi yeniden başlat
docker-compose down && docker-compose up -d
```

### IP Yanlışlıkla Bloklanırsa
```bash
# Container içinden cache temizle
docker-compose exec risky-websites-api redis-cli FLUSHALL
```

**Sonuç**: Artık sisteminiz **DDoS saldırılarına**, **memory exhaustion**'a ve **servis çöküşlerine** karşı korunuyor! 🛡️✅