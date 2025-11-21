# RiskyWebsitesAPI - Hetzner Docker Deployment Guide

## 🚀 Hızlı Başlangıç

Bu döküman, RiskyWebsitesAPI'nizi Hetzner sunucunuza Docker ile yüklemeniz için adım adım talimatlar sunar.

## 📋 Gereksinimler

- Hetzner sunucusu (CX23 veya üzeri önerilir)
- Ubuntu 20.04/22.04 veya Debian 11/12
- Root veya sudo erişimi
- 80 ve 443 portlarının açık olması

## 🔧 Kurulum Adımları

### 1. Sunucuya Bağlanma

```bash
ssh kullaniciadi@95.217.1.184
```

### 2. Proje Dosyalarını Sunucuya Aktarma

#### Yöntem A: SCP ile dosya transferi
```bash
# Yerel bilgisayarınızdan sunucuya
scp -r ./* kullaniciadi@95.217.1.184:/home/kullaniciadi/risky-websites-api/
```

#### Yöntem B: Git ile klonlama
```bash
# Sunucuda
mkdir -p /home/kullaniciadi/risky-websites-api
cd /home/kullaniciadi/risky-websites-api
git clone https://github.com/kullaniciadi/risky-websites-api.git .
```

### 3. Kurulum Scriptini Çalıştırma

```bash
cd /home/kullaniciadi/risky-websites-api
chmod +x deploy-hetzner.sh
./deploy-hetzner.sh
```

### 4. Manuel Kurulum (Alternatif)

```bash
# Docker ve Docker Compose kurulumu
sudo apt-get update -y
sudo apt-get install -y docker.io docker-compose

# Proje dizinine git
cd /home/kullaniciadi/risky-websites-api

# Docker image build etme
docker build -t risky-websites-api:latest .

# Container'ı başlatma
docker-compose up -d
```

## 🔍 Kontrol ve Test

### API Sağlık Kontrolü
```bash
curl http://95.217.1.184/swagger
curl http://95.217.1.184/api/RiskCheck/check?domain=example.com
```

### Container Durumu
```bash
docker-compose ps
docker-compose logs -f
```

### Sistem Kaynakları
```bash
docker stats --no-stream
sudo ufw status
```

## 🔒 Güvenlik

### Firewall Ayarları
```bash
# Gerekli portları aç
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP
sudo ufw allow 443/tcp  # HTTPS
sudo ufw enable
```

### SSL Sertifikası (Opsiyonel)
```bash
# Let's Encrypt ile ücretsiz SSL
sudo apt-get install -y certbot
sudo certbot certonly --standalone -d yourdomain.com
```

## 📊 İzleme ve Bakım

### Günlük Kontroller
```bash
# Log kontrolü
./security-check.sh

# Container restart
sudo systemctl restart docker
```

### Backup
```bash
# Container backup
docker commit risky-websites-api risky-websites-api-backup:$(date +%Y%m%d)
```

## 🛠️ Sorun Giderme

### Container Başlamıyor
```bash
# Log kontrolü
docker-compose logs --tail=50

# Container sil ve yeniden oluştur
docker-compose down
docker-compose up -d --build
```

### Port Çakışması
```bash
# 80 portunu kullanan process'leri bul
sudo netstat -tlnp | grep :80

# Container'ı farklı portta başlat
docker-compose -f docker-compose.yml up -d
```

### Bellek Sorunları
```bash
# Docker temizliği
docker system prune -f
docker volume prune -f
```

## 🔄 Güncelleme

### Kod Güncelleme
```bash
# Sunucuda
cd /home/kullaniciadi/risky-websites-api
git pull origin main
./deploy-manual.sh
```

### Docker Image Güncelleme
```bash
docker-compose down
docker pull mcr.microsoft.com/dotnet/aspnet:8.0
docker-compose up -d --build
```

## 📞 Destek

- Container logları: `docker-compose logs -f`
- Sistem logları: `journalctl -u docker.service -f`
- Health check: `curl -f http://localhost:5000/swagger`

## ⚠️ Önemli Notlar

- Güvenlik için non-root user kullanılmıştır
- Health check endpoint'i çalışmaktadır
- Otomatik restart politikası aktiftir
- Log rotation yapılandırılmıştır
- Resource limitleri belirlenmiştir