# RiskyWebsitesAPI Monitoring & Alerting Deployment Guide

## 🎯 Hedef
Bu kılavuz, RiskyWebsitesAPI için kapsamlı monitoring ve alerting sisteminin kurulumunu adım adım açıklar. 200 eşzamanlı kullanıcı senaryosu için optimize edilmiştir.

## 📋 Özellikler

### Monitoring Stack
- **Prometheus**: Metrik toplama ve saklama
- **Grafana**: Görselleştirme ve dashboard
- **AlertManager**: Alert yönetimi ve bildirimler
- **Loki**: Log toplama ve analiz
- **Node Exporter**: Sistem metrikleri
- **cAdvisor**: Container metrikleri

### Dashboard'lar
- HTTP Requests Per Second
- Response Time (95th percentile)
- Error Rate
- Rate Limiting Activity
- CPU Usage
- Memory Usage
- Disk Space Usage
- Network Traffic
- Circuit Breaker Status
- Memory Protection Triggers
- Concurrent Users
- Load Average

### Alert'lar
- **Critical**: Response time > 2s, Error rate > 5%, Circuit breaker open
- **Warning**: CPU > 80%, Memory > 85%, Rate limiting triggered
- **Info**: Scaling önerileri, IP blocking bildirimleri

## 🚀 Kurulum Adımları

### 1. Monitoring Stack'i Başlat

```bash
# Windows PowerShell'de:
docker-compose -f docker-compose.monitoring.yml up -d

# Linux/macOS'te:
./setup-monitoring.sh
```

### 2. Servisleri Kontrol Et

```bash
# Tüm servislerin durumunu kontrol et
docker-compose -f docker-compose.monitoring.yml ps

# Logları kontrol et
docker-compose -f docker-compose.monitoring.yml logs -f
```

### 3. Dashboard'lara Erişim

| Servis | URL | Kullanıcı Adı | Şifre |
|--------|-----|---------------|-------|
| Grafana | http://localhost:3000 | admin | riskywebsites123 |
| Prometheus | http://localhost:9090 | - | - |
| AlertManager | http://localhost:9093 | - | - |

### 4. Grafana Dashboard'u İçe Aktar

1. Grafana'ya giriş yap (admin/riskywebsites123)
2. Sol menüden "Dashboards" → "Import" seç
3. "Upload JSON file" seçeneğini kullan
4. `monitoring/grafana/dashboards/riskywebsitesapi-dashboard.json` dosyasını yükle
5. Prometheus datasource'u seç ve import et

## 📊 Önemli Metrikler (200 Kullanıcı için)

### Performans Hedefleri
- **Response Time**: < 1 saniye (ortalama)
- **95th Percentile**: < 2 saniye
- **Error Rate**: < 1%
- **CPU Usage**: < 70%
- **Memory Usage**: < 80%
- **Concurrent Users**: 200 (maksimum)

### Rate Limiting
- **Limit**: 120 istek/dakika per IP
- **Burst**: 10 istek/saniye
- **Block Süresi**: 5 dakika

### Circuit Breaker
- **Failure Threshold**: 5 başarısızlık
- **Timeout**: 30 saniye
- **Recovery Time**: 60 saniye

## 🚨 Alert Konfigürasyonu

### Email Bildirimleri
AlertManager konfigürasyonunu `monitoring/alertmanager.yml` dosyasında güncelle:

```yaml
global:
  smtp_smarthost: 'your-smtp-server:587'
  smtp_from: 'alerts@yourdomain.com'
  smtp_auth_username: 'your-username'
  smtp_auth_password: 'your-password'

receivers:
- name: 'critical-alerts'
  email_configs:
  - to: 'admin@yourdomain.com'
```

### Slack Bildirimleri (Opsiyonel)
```yaml
- name: 'slack-alerts'
  slack_configs:
  - api_url: 'YOUR_SLACK_WEBHOOK_URL'
    channel: '#alerts'
    title: 'RiskyWebsitesAPI Alert'
```

## 🔍 Troubleshooting

### Prometheus Metrik Yok
```bash
# Prometheus hedeflerini kontrol et
curl http://localhost:9090/api/v1/targets

# API metrik endpoint'ini test et
curl http://localhost:8080/metrics
```

### Grafana Dashboard Boş
```bash
# Prometheus datasource'unun çalıştığını kontrol et
curl http://localhost:9090/api/v1/label/__name__/values

# Grafana loglarını kontrol et
docker-compose -f docker-compose.monitoring.yml logs grafana
```

### Alert'lar Çalışmıyor
```bash
# AlertManager durumunu kontrol et
curl http://localhost:9093/api/v1/status

# Prometheus alert'larını kontrol et
curl http://localhost:9090/api/v1/alerts
```

## 📈 Ölçeklendirme (Scaling)

### Monitoring Stack Ölçeklendirme
```yaml
# docker-compose.monitoring.yml
deploy:
  resources:
    limits:
      cpus: '0.5'
      memory: 512M
    reservations:
      cpus: '0.2'
      memory: 256M
```

### Retention Ayarları
```yaml
# Prometheus retention
command:
  - '--storage.tsdb.retention.time=30d'  # 30 günlük veri saklama
  - '--storage.tsdb.retention.size=10GB'  # 10GB'a kadar veri
```

## 🔒 Güvenlik

### Grafana Güvenliği
- Varsayılan şifreyi değiştir
- SSL/TLS aktif et
- Güçlü parolalar kullan
- İki faktörlü kimlik doğrulama aktif et

### Prometheus Güvenliği
- Basic authentication ekle
- Network policies kullan
- Firewall kuralları yapılandır

## 🔄 Bakım

### Günlük Bakım
```bash
# Servis durumlarını kontrol et
docker-compose -f docker-compose.monitoring.yml ps

# Disk kullanımını kontrol et
df -h

# Memory kullanımını kontrol et
free -h
```

### Haftalık Bakım
```bash
# Log rotasyonu
docker-compose -f docker-compose.monitoring.yml logs --tail 100 > monitoring-weekly.log

# Prometheus veri boyutunu kontrol et
du -sh prometheus_data/

# Eski alert'ları temizle
curl -X DELETE http://localhost:9093/api/v1/alerts
```

### Aylık Bakım
```bash
# Tüm monitoring stack'i yeniden başlat
docker-compose -f docker-compose.monitoring.yml restart

# Dashboard'ları güncelle
# Grafana'dan yeni versiyonları kontrol et

# Alert kurallarını gözden geçir
# monitoring/alerts.yml dosyasını güncelle
```

## 📞 Destek

### Log Dosyaları
```bash
# Tüm servis loglarını görüntüle
docker-compose -f docker-compose.monitoring.yml logs

# Belirli servis logu
docker-compose -f docker-compose.monitoring.yml logs prometheus

# Real-time log takibi
docker-compose -f docker-compose.monitoring.yml logs -f
```

### Metrik Sorguları
```promql
# CPU kullanımı
100 - (avg by(instance) (rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)

# Memory kullanımı
(1 - (node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes)) * 100

# HTTP istekleri
rate(http_requests_total[5m])

# Hata oranı
rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m])
```

## 🎯 Sonraki Adımlar

1. **Load Testing**: `./load-test-200-users.sh` scriptini çalıştır
2. **Alert Test**: Sistemde kasıtlı hata oluştur ve alert'ları test et
3. **Dashboard Özelleştirme**: İhtiyaçlarına göre dashboard'u özelleştir
4. **Monitoring Dokümantasyonu**: Takımına monitoring prosedürlerini anlat
5. **Otomasyon**: Monitoring kurulumunu CI/CD pipeline'ına ekle

Bu monitoring sistemi sayesinde 200 eşzamanlı kullanıcı senaryosunda sistemin sağlıklı çalıştığını gerçek-zamanlı olarak izleyebilir, problemleri önceden tespit edebilir ve hızlı müdahale edebilirsin. 🚀