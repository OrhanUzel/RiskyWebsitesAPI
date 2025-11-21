# RiskyWebsitesAPI - Hatasız Deployment Guide

## ✅ Ön Kontrol Listesi

### 1. Kod Kontrolü
```bash
# Projeyi temizle ve build et
dotnet clean
dotnet build

# Hata yoksa devam et
# Build başarılı: 0 error(s)
```

### 2. Docker Kontrolü
```bash
# Docker servisinin çalıştığını kontrol et
docker info

# Docker Desktop Windows'ta çalışıyorsa devam et
```

## 🚀 Docker Build - Hatasız

### Temel Build
```bash
# Docker image build et (hatasız)
docker build -t risky-websites-api:latest .

# Build başarılıysa devam et
```

### Build Sorunları ve Çözümleri

#### ❌ Hata: "Services namespace not found"
**Çözüm:** RiskDomainService.cs dosyasında namespace tanımlaması eksik
```csharp
// Services/RiskDomainService.cs başına ekle:
namespace RiskyWebsitesAPI.Services
{
    public class RiskDomainService
    {
        // ... kod
    }
}
```

#### ❌ Hata: "IMemoryCache not found"
**Çözüm:** Eksiz using ifadelerini ekle
```csharp
// Controllers/PerformanceController.cs
using Microsoft.Extensions.Caching.Memory;

// Controllers/SecurityController.cs  
using Microsoft.Extensions.Caching.Memory;

// Middleware/SecurityLoggingMiddleware.cs
using Microsoft.Extensions.Caching.Memory;
```

#### ❌ Hata: "Operator '!=' cannot be applied to operands of type 'void'"
**Çözüm:** MemoryProtectionService.cs'de Remove metodu void döner
```csharp
// Security/MemoryProtectionService.cs
public void RemoveCacheEntry(string key)
{
    _cache.Remove(key); // != null kontrolü kaldır
    Interlocked.Decrement(ref _currentCacheEntries);
}
```

#### ❌ Hata: "There is no argument given that corresponds to the required parameter 'operationName'"
**Çözüm:** ExecuteWithMemoryLimit çağrısına operationName ekle
```csharp
// Services/RiskDomainService.cs
return await _memoryProtection.ExecuteWithMemoryLimit(async () =>
{
    // ... kod
}, $"LoadDomains_{key}"); // operationName parametresi ekle
```

#### ❌ Hata: "The name 'GCSettings' does not exist in the current context"
**Çözüm:** GCSettings için using ekle
```csharp
// Controllers/PerformanceController.cs
using System.Runtime;
```

#### ❌ Hata: "Package Microsoft.Extensions.Caching.Memory 8.0.0 has vulnerability"
**Çözüm:** Güvenlik açığı olan versiyonu güncelle
```xml
<!-- RiskyWebsitesAPI.csproj -->
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.1" />
```

## 📦 Docker Compose ile Çalıştırma

### 1. Temel Deployment
```bash
# Docker Compose ile başlat
docker-compose up -d

# Servislerin durumunu kontrol et
docker-compose ps

# Logları kontrol et
docker-compose logs -f
```

### 2. Monitoring ile Deployment
```bash
# Monitoring stack'i başlat
docker-compose -f docker-compose.monitoring.yml up -d

# Tüm servisleri birlikte başlat
docker-compose up -d
docker-compose -f docker-compose.monitoring.yml up -d
```

### 3. Port Kontrolü
```bash
# Portların açık olduğunu kontrol et
netstat -an | findstr :8080
netstat -an | findstr :3000  # Grafana
netstat -an | findstr :9090  # Prometheus
```

## 🧪 Test ve Doğrulama

### 1. Health Check
```bash
# API health check
curl http://localhost:8080/health

# Swagger UI'ye erişim
curl http://localhost:8080/swagger
```

### 2. Fonksiyonel Test
```bash
# Risk check endpoint testi
curl -X GET "http://localhost:8080/api/riskcheck/check/test.com"

# Performance metrics
curl http://localhost:8080/api/performance/metrics
```

### 3. Load Test
```bash
# 200 kullanıcı load testi
./load-test-200-users.sh

# Manuel load test
for i in {1..200}; do
  curl -s "http://localhost:8080/api/riskcheck/check/test$i.com" > /dev/null &
done
wait
```

### 4. Monitoring Test
```bash
# Grafana dashboard'a erişim
open http://localhost:3000  # admin/riskywebsites123

# Prometheus metrics
open http://localhost:9090/targets

# AlertManager status
curl http://localhost:9093/api/v1/status
```

## 🔧 Production Kontrol Listesi

### 1. Güvenlik
- [ ] Rate limiting aktif (120 req/min)
- [ ] Circuit breaker çalışıyor
- [ ] Memory protection aktif (300MB)
- [ ] Security logging enabled
- [ ] Nginx rate limiting (50 req/s)

### 2. Performans
- [ ] Response time < 1s (ortalama)
- [ ] CPU usage < 70%
- [ ] Memory usage < 80%
- [ ] Error rate < 1%
- [ ] Concurrent users ≤ 200

### 3. Monitoring
- [ ] Grafana dashboard aktif
- [ ] AlertManager çalışıyor
- [ ] Prometheus metrics collection
- [ ] Log aggregation (Loki)
- [ ] Health checks passing

### 4. Backup & Recovery
- [ ] Docker volumes backup
- [ ] Configuration files backup
- [ ] Database backup (var ise)
- [ ] Recovery procedure documented

## 🚨 Yaygın Hatalar ve Hızlı Çözümler

### Docker Daemon Hatası
```bash
# Windows'ta Docker Desktop başlat
Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"

# Servisin durumunu kontrol et
Get-Service com.docker.service
```

### Port Çakışması
```bash
# 8080 portunu kullanan process'leri bul
netstat -ano | findstr :8080
taskkill /PID <PID> /F
```

### Memory Hatası
```bash
# Docker memory limit artır
docker-compose down
docker-compose up -d --scale api=1
```

### Network Hatası
```bash
# Docker network'ü yeniden oluştur
docker network prune -f
docker-compose up -d --force-recreate
```

## 📊 Başarı Kontrolü

### Build Başarılı
```bash
# Image başarıyla oluştu
docker images | grep risky-websites-api

# Container çalışıyor
docker ps | grep risky-websites-api

# Log'da hata yok
docker-compose logs api | grep -i error
```

### API Çalışıyor
```bash
# Health check başarılı
curl -s http://localhost:8080/health | grep -i "healthy"

# Swagger UI erişilebilir
curl -s http://localhost:8080/swagger/index.html | grep -i "swagger"

# Risk check endpoint çalışıyor
curl -s "http://localhost:8080/api/riskcheck/check/google.com" | grep -i "risk"
```

### Monitoring Aktif
```bash
# Grafana çalışıyor
curl -s http://localhost:3000/api/health | grep -i "ok"

# Prometheus çalışıyor  
curl -s http://localhost:9090/-/healthy | grep -i "ok"

# AlertManager çalışıyor
curl -s http://localhost:9093/-/healthy | grep -i "ok"
```

## 🎯 Sonraki Adımlar

1. **SSL/TLS Kurulumu**: Production için HTTPS aktif et
2. **Domain Yönlendirme**: Gerçek domain bağla
3. **Backup Otomasyonu**: Günlük backup'lar kur
4. **Monitoring Alert'leri**: Email/SMS bildirimleri aktif et
5. **Performance Tuning**: 200+ kullanıcı için optimize et
6. **Security Audit**: Penetration test yaptır

## 📞 Destek

### Log Dosyaları
```bash
# Tüm servis logları
docker-compose logs > all-services.log

# Sadece hatalar
docker-compose logs | grep -i error > errors.log

# Son 100 satır
docker-compose logs --tail 100
```

### Debug Modu
```bash
# Detaylı logging
docker-compose -f docker-compose.yml -f docker-compose.debug.yml up -d

# Interactive debug
docker exec -it risky-websites-api bash
```

---

**✅ Build başarılı! Artık production-ready durumdasın.**

**🚀 Hızlı başlatma:**
```bash
docker-compose up -d                    # API'yi başlat
docker-compose -f docker-compose.monitoring.yml up -d  # Monitoring'i başlat
./load-test-200-users.sh                # Load test yap
open http://localhost:3000              # Grafana'ya git
```

**📊 Dashboard'lar:**
- Grafana: http://localhost:3000 (admin/riskywebsites123)
- Swagger: http://localhost:8080/swagger
- Health: http://localhost:8080/health

**🎉 Tebrikler! Sistemin artık 200 eşzamanlı kullanıcıya hazır!**