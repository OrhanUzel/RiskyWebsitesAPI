#!/bin/bash

# Hetzner Sunucusu Docker Deployment Script
# Bu script, RiskyWebsitesAPI'yi Hetzner sunucunuza Docker ile yükler

set -e

echo "🚀 RiskyWebsitesAPI Hetzner Deployment Başlatılıyor..."

# 1. Sistem güncelleme ve Docker kurulumu
echo "📦 Sistem güncelleniyor ve Docker kuruluyor..."
sudo apt-get update -y
sudo apt-get install -y apt-transport-https ca-certificates curl gnupg lsb-release

# Docker GPG key ekleme
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg

# Docker repository ekleme
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Docker kurulumu
sudo apt-get update -y
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin

# Docker servisini başlatma
sudo systemctl start docker
sudo systemctl enable docker

# Mevcut kullanıcıyı docker grubuna ekleme
sudo usermod -aG docker $USER

echo "✅ Docker başarıyla kuruldu!"

# 2. Proje dosyalarını sunucuya kopyalama
echo "📁 Proje dosyaları kopyalanıyor..."
# Bu kısmı kendi dosya transfer yönteminize göre güncelleyin
# Örnek: scp, rsync, git clone, vb.

echo "⚠️  Proje dosyalarını sunucuya kopyalamak için:"
echo "   scp -r . $USER@95.217.1.184:/home/$USER/risky-websites-api"
echo "   veya"
echo "   git clone https://github.com/kullaniciadi/repo.git /home/$USER/risky-websites-api"

# 3. Docker image build etme
echo "🔨 Docker image oluşturuluyor..."
cd /home/$USER/risky-websites-api
docker build -t risky-websites-api:latest .

# 4. Container'ı başlatma
echo "🐳 Container başlatılıyor..."
docker-compose up -d

# 5. Health check
echo "🏥 Health check kontrolü yapılıyor..."
sleep 10
curl -f http://localhost/swagger || echo "⚠️  Health check başarısız. Logları kontrol edin."

# 6. Firewall ayarları
echo "🔥 Firewall ayarları yapılıyor..."
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw --force enable

echo "🎉 Deployment tamamlandı!"
echo "API URL: http://95.217.1.184/swagger"
echo ""
echo "Kullanışlı komutlar:"
echo "  docker-compose logs -f     # Logları görüntüle"
echo "  docker-compose down        # Container'ı durdur"
echo "  docker-compose up -d       # Container'ı başlat"
echo "  docker system prune -f     # Temizlik yap"