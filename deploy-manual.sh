#!/bin/bash

# Manuel deployment script - Alternatif yöntem
# Bu script daha kontrollü bir deployment sağlar

set -e

echo "🚀 RiskyWebsitesAPI Manuel Deployment Başlatılıyor..."

# Değişkenler
PROJECT_DIR="/home/$USER/risky-websites-api"
API_PORT=5000
PUBLIC_PORT=80

# 1. Gerekli dizinleri oluştur
echo "📁 Proje dizinleri oluşturuluyor..."
mkdir -p $PROJECT_DIR
cd $PROJECT_DIR

# 2. Docker kontrolü
if ! command -v docker &> /dev/null; then
    echo "❌ Docker yüklü değil. Lütfen önce Docker kurun."
    echo "   sudo apt-get update && sudo apt-get install -y docker.io docker-compose"
    exit 1
fi

# 3. Mevcut container'ları durdurma
echo "🛑 Mevcut container'lar durduruluyor..."
docker-compose down || true

# 4. Yeni image build etme
echo "🔨 Yeni Docker image'ı oluşturuluyor..."
docker build -t risky-websites-api:latest .

# 5. Container'ı başlatma
echo "🐳 Yeni container başlatılıyor..."
docker-compose up -d

# 6. Deployment kontrolü
echo "⏳ Container'ın başlaması bekleniyor..."
sleep 15

# 7. Health check
echo "🏥 Health check yapılıyor..."
if curl -f http://localhost:$API_PORT/swagger > /dev/null 2>&1; then
    echo "✅ API başarıyla çalışıyor!"
else
    echo "❌ API health check başarısız. Loglar kontrol ediliyor..."
    docker-compose logs --tail=50
    exit 1
fi

# 8. Sistem durumu
echo "📊 Container durumu:"
docker-compose ps

echo "🎉 Deployment başarıyla tamamlandı!"
echo "🌐 API URL: http://95.217.1.184:$PUBLIC_PORT/swagger"
echo "📊 Container istatistikleri:"
docker stats --no-stream risky-websites-api