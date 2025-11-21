#!/bin/bash

# Hetzner sunucusu için güvenlik ve bakım scripti

echo "🔒 Güvenlik ve bakım kontrolleri yapılıyor..."

# 1. Firewall kontrolü
echo "🔥 Firewall durumu kontrol ediliyor..."
sudo ufw status verbose

# 2. Docker güvenlik kontrolü
echo "🐳 Docker güvenlik kontrolü..."
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# 3. Container log kontrolü
echo "📋 Container log kontrolü..."
docker-compose logs --tail=20 | grep -E "(ERROR|WARN|Exception)" || echo "✅ Hata bulunamadı"

# 4. Sistem kaynakları
echo "💻 Sistem kaynakları..."
df -h
free -h

# 5. Container resource kullanımı
echo "📊 Container resource kullanımı..."
docker stats --no-stream --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.NetIO}}"

# 6. Güvenlik güncellemeleri
echo "🔄 Güvenlik güncellemeleri kontrolü..."
sudo apt list --upgradable 2>/dev/null | grep -i security || echo "✅ Güvenlik güncellemesi yok"

echo "✅ Güvenlik kontrolleri tamamlandı!"