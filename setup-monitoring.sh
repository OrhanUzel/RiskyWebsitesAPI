#!/bin/bash

# RiskyWebsitesAPI Monitoring Setup Script
# This script sets up Prometheus, Grafana, and alerting for the API

set -e

echo "🚀 Starting RiskyWebsitesAPI Monitoring Setup..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    print_error "Docker is not running. Please start Docker and try again."
    exit 1
fi

# Check if docker-compose is available
if ! command -v docker-compose &> /dev/null; then
    print_warning "docker-compose not found, trying docker compose..."
    DOCKER_COMPOSE="docker compose"
else
    DOCKER_COMPOSE="docker-compose"
fi

print_status "Using Docker Compose command: $DOCKER_COMPOSE"

# Create monitoring directories
print_status "Creating monitoring directories..."
mkdir -p monitoring/grafana/{dashboards,datasources}
mkdir -p monitoring/logs

# Set proper permissions
chmod 755 monitoring
chmod 644 monitoring/*.yml

# Stop existing monitoring stack if running
print_status "Stopping existing monitoring stack..."
$DOCKER_COMPOSE -f docker-compose.monitoring.yml down || true

# Pull latest images
print_status "Pulling latest monitoring images..."
$DOCKER_COMPOSE -f docker-compose.monitoring.yml pull

# Start monitoring stack
print_status "Starting monitoring stack..."
$DOCKER_COMPOSE -f docker-compose.monitoring.yml up -d

# Wait for services to start
print_status "Waiting for services to start..."
sleep 30

# Check if services are running
print_status "Checking service status..."
services=("prometheus" "grafana" "node_exporter" "cadvisor" "alertmanager" "loki" "promtail")

for service in "${services[@]}"; do
    if docker ps | grep -q $service; then
        print_status "✅ $service is running"
    else
        print_error "❌ $service is not running"
    fi
done

# Get Grafana admin password
GRAFANA_ADMIN_PASSWORD="riskywebsites123"

# Display access information
echo ""
echo "🎉 Monitoring setup completed successfully!"
echo ""
echo "📊 Access URLs:"
echo "  • Grafana Dashboard: http://localhost:3000"
echo "  • Prometheus: http://localhost:9090"
echo "  • AlertManager: http://localhost:9093"
echo ""
echo "🔑 Default Credentials:"
echo "  • Grafana: admin / $GRAFANA_ADMIN_PASSWORD"
echo ""
echo "📈 Dashboard Features:"
echo "  • HTTP Requests Per Second"
echo "  • Response Time (95th percentile)"
echo "  • Error Rate"
echo "  • Rate Limiting Activity"
echo "  • CPU Usage"
echo "  • Memory Usage"
echo "  • Disk Space Usage"
echo "  • Network Traffic"
echo "  • Circuit Breaker Status"
echo "  • Memory Protection Triggers"
echo "  • Concurrent Users"
echo "  • Load Average"
echo ""
echo "🚨 Alerting:"
echo "  • CPU Usage > 80%"
echo "  • Memory Usage > 85%"
echo "  • Response Time > 2 seconds"
echo "  • Error Rate > 5%"
echo "  • Rate Limiting Triggered"
echo "  • Circuit Breaker Open"
echo "  • Memory Protection Triggered"
echo "  • Suspicious Requests > 20"
echo "  • IPs Blocked"
echo ""
echo "📁 Configuration Files:"
echo "  • Prometheus: monitoring/prometheus.yml"
echo "  • Alerts: monitoring/alerts.yml"
echo "  • AlertManager: monitoring/alertmanager.yml"
echo "  • Grafana Datasources: monitoring/grafana/datasources/"
echo "  • Grafana Dashboards: monitoring/grafana/dashboards/"
echo ""
echo "🔄 To stop monitoring:"
echo "  $DOCKER_COMPOSE -f docker-compose.monitoring.yml down"
echo ""
echo "🔄 To restart monitoring:"
echo "  $DOCKER_COMPOSE -f docker-compose.monitoring.yml up -d"
echo ""
echo "📊 To view logs:"
echo "  $DOCKER_COMPOSE -f docker-compose.monitoring.yml logs -f [service-name]"
echo ""
echo "🎯 Next Steps:"
echo "  1. Access Grafana dashboard at http://localhost:3000"
echo "  2. Import the RiskyWebsitesAPI dashboard"
echo "  3. Configure email notifications in AlertManager"
echo "  4. Set up custom alerts as needed"
echo "  5. Test the alerting system"
echo ""

# Create a simple test to verify monitoring is working
print_status "Testing monitoring endpoints..."

# Test Prometheus
if curl -s http://localhost:9090/api/v1/label/__name__/values | grep -q "http_requests_total"; then
    print_status "✅ Prometheus is collecting metrics"
else
    print_warning "⚠️  Prometheus metrics not yet available (this is normal on first start)"
fi

# Test Grafana
if curl -s http://localhost:3000/api/health | grep -q "ok"; then
    print_status "✅ Grafana is healthy"
else
    print_warning "⚠️  Grafana health check failed"
fi

print_status "🎊 Monitoring setup script completed!"
print_status "Check the URLs above to access your monitoring dashboard."