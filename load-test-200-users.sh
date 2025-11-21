#!/bin/bash

# 200 Kullanıcı Load Testing Script
# Bu script 200 eşzamanlı kullanıcı senaryosunu test eder

set -e

# Renkli çıktı
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test konfigürasyonu
API_URL="http://95.217.1.184/api/RiskCheck/check?domain="
TEST_DOMAINS=("google.com" "facebook.com" "youtube.com" "amazon.com" "twitter.com" "linkedin.com" "github.com" "stackoverflow.com" "reddit.com" "netflix.com")
CONCURRENT_USERS=200
TOTAL_REQUESTS=2000
TEST_DURATION=60

# Tool kontrolü
check_tools() {
    echo -e "${YELLOW}🔧 Gerekli tool'lar kontrol ediliyor...${NC}"
    
    if ! command -v curl &> /dev/null; then
        echo -e "${RED}❌ curl yüklü değil${NC}"
        exit 1
    fi
    
    if ! command -v jq &> /dev/null; then
        echo -e "${YELLOW}⚠️  jq yüklü değil, JSON parsing sınırlı olacak${NC}"
    fi
    
    echo -e "${GREEN}✅ Tool'lar kontrol edildi${NC}"
}

# Basit load test (curl ile)
basic_load_test() {
    echo -e "${YELLOW}🚀 Basit load test başlatılıyor...${NC}"
    echo -e "${YELLOW}   Kullanıcılar: $CONCURRENT_USERS, İstekler: $TOTAL_REQUESTS${NC}"
    
    local start_time=$(date +%s)
    local success_count=0
    local error_count=0
    local rate_limited=0
    
    # Eşzamanlı istekler gönder
    for ((i=1; i<=TOTAL_REQUESTS; i++)); do
        (
            local domain=${TEST_DOMAINS[$((i % ${#TEST_DOMAINS[@]}))]}
            local response_code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "${API_URL}${domain}")
            
            if [[ $response_code -eq 200 ]]; then
                ((success_count++))
            elif [[ $response_code -eq 429 ]]; then
                ((rate_limited++))
            else
                ((error_count++))
            fi
            
            # Progress göster
            if (( i % 100 == 0 )); then
                echo -e "${YELLOW}   İlerleme: $i/$TOTAL_REQUESTS${NC}"
            fi
        ) &
        
        # Eşzamanlılık limiti
        if (( i % CONCURRENT_USERS == 0 )); then
            wait
        fi
    done
    
    # Tüm işlemlerin bitmesini bekle
    wait
    
    local end_time=$(date +%s)
    local duration=$((end_time - start_time))
    local rps=$(echo "scale=2; $TOTAL_REQUESTS / $duration" | bc -l)
    
    echo -e "${GREEN}📊 Basit Load Test Sonuçları:${NC}"
    echo -e "${GREEN}   Süre: ${duration}s${NC}"
    echo -e "${GREEN}   Başarılı: $success_count${NC}"
    echo -e "${GREEN}   Rate Limited: $rate_limited${NC}"
    echo -e "${GREEN}   Hatalı: $error_count${NC}"
    echo -e "${GREEN}   İstek/Saniye: $rps${NC}"
}

# Hey tool ile gelişmiş test (varsa)
advanced_load_test() {
    if command -v hey &> /dev/null; then
        echo -e "${YELLOW}🎯 Gelişmiş load test (hey) başlatılıyor...${NC}"
        
        local test_domain=${TEST_DOMAINS[0]}
        hey -n $TOTAL_REQUESTS -c $CONCURRENT_USERS -t ${TEST_DURATION}s \
            -H "User-Agent: LoadTestBot/1.0" \
            "${API_URL}${test_domain}" | tee hey_results.txt
            
        echo -e "${GREEN}✅ Gelişmiş test tamamlandı${NC}"
    else
        echo -e "${YELLOW}⚠️  hey tool'u bulunamadı, gelişmiş test atlanıyor${NC}"
        echo -e "${YELLOW}   Kurmak için: go install github.com/rakyll/hey@latest${NC}"
    fi
}

# Sistem metriklerini kontrol et
check_system_metrics() {
    echo -e "${YELLOW}📈 Sistem metrikleri kontrol ediliyor...${NC}"
    
    # API health check
    local health_status=$(curl -s -o /dev/null -w "%{http_code}" http://95.217.1.184/api/security/health)
    
    if [[ $health_status -eq 200 ]]; then
        echo -e "${GREEN}✅ API sağlıklı (HTTP $health_status)${NC}"
    else
        echo -e "${RED}❌ API sağlıksız (HTTP $health_status)${NC}"
    fi
    
    # Security stats
    local security_stats=$(curl -s http://95.217.1.184/api/security/stats 2>/dev/null || echo "{}" )
    
    if command -v jq &> /dev/null; then
        local memory_usage=$(echo $security_stats | jq -r '.memory.memoryUsagePercentage // "N/A"')
        local cache_usage=$(echo $security_stats | jq -r '.cache.cacheUsagePercentage // "N/A"')
        
        echo -e "${GREEN}   Bellek Kullanımı: %$memory_usage${NC}"
        echo -e "${GREEN}   Cache Kullanımı: %$cache_usage${NC}"
    fi
}

# Rate limiting testi
rate_limiting_test() {
    echo -e "${YELLOW}🔒 Rate limiting testi yapılıyor...${NC}"
    
    # 130 istek gönder (limit: 120/dk)
    echo -e "${YELLOW}   130 istek gönderiliyor (limit: 120/dk)...${NC}"
    
    local success_count=0
    local rate_limited_count=0
    
    for ((i=1; i<=130; i++)); do
        local response_code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "${API_URL}test.com")
        
        if [[ $response_code -eq 200 ]]; then
            ((success_count++))
        elif [[ $response_code -eq 429 ]]; then
            ((rate_limited_count++))
        fi
    done
    
    echo -e "${GREEN}   Başarılı: $success_count${NC}"
    echo -e "${GREEN}   Rate Limited (429): $rate_limited_count${NC}"
    
    if [[ $rate_limited_count -gt 0 ]]; then
        echo -e "${GREEN}✅ Rate limiting çalışıyor!${NC}"
    else
        echo -e "${RED}⚠️  Rate limiting devreye girmedi${NC}"
    fi
}

# Memory protection testi
memory_test() {
    echo -e "${YELLOW}💾 Memory protection testi yapılıyor...${NC}"
    
    # Büyük domain listesi ile test
    for ((i=1; i<=1000; i++)); do
        (
            local long_domain="very-long-domain-name-$i-that-might-cause-memory-issues.com"
            curl -s -o /dev/null --max-time 5 "${API_URL}${long_domain}" 2>/dev/null || true
        ) &
        
        if (( i % 100 == 0 )); then
            wait
            echo -e "${YELLOW}   Memory test: $i/1000${NC}"
        fi
    done
    
    wait
    echo -e "${GREEN}✅ Memory test tamamlandı${NC}"
}

# Sonuç raporu
generate_report() {
    echo -e "${GREEN}📋 LOAD TEST RAPORU${NC}"
    echo -e "${GREEN}==================${NC}"
    echo -e "${GREEN}Tarih: $(date)${NC}"
    echo -e "${GREEN}API URL: $API_URL${NC}"
    echo -e "${GREEN}Kullanıcılar: $CONCURRENT_USERS${NC}"
    echo -e "${GREEN}Toplam İstek: $TOTAL_REQUESTS${NC}"
    echo -e "${GREEN}Test Süresi: ${TEST_DURATION}s${NC}"
    echo -e "${GREEN}Test Domain'leri: ${TEST_DOMAINS[*]}${NC}"
    echo -e ""
    
    if [[ -f hey_results.txt ]]; then
        echo -e "${GREEN}Detaylı hey sonuçları: hey_results.txt${NC}"
    fi
    
    echo -e "${YELLOW}Not: Daha detaylı test için 'hey' tool'u kurun:${NC}"
    echo -e "${YELLOW}go install github.com/rakyll/hey@latest${NC}"
}

# Ana menü
main() {
    echo -e "${GREEN}🚀 RiskyWebsitesAPI - 200 Kullanıcı Load Test${NC}"
    echo -e "${GREEN}==============================================${NC}"
    
    check_tools
    check_system_metrics
    echo -e ""
    
    rate_limiting_test
    echo -e ""
    
    memory_test
    echo -e ""
    
    basic_load_test
    echo -e ""
    
    advanced_load_test
    echo -e ""
    
    generate_report
    
    echo -e "${GREEN}✅ Tüm testler tamamlandı!${NC}"
}

# Script'i çalıştır
main "$@"