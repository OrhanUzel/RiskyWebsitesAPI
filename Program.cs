using Microsoft.Extensions.Caching.Memory;
using RiskyWebsitesAPI.Services;
using RiskyWebsitesAPI.Security;
using RiskyWebsitesAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Çok yorumlu açıklamalar: Bu bölüm uygulamanın servislerini yapılandırır.
// - MemoryCache: İndirilen riskli domain listelerini bellekte tutmak için kullanılır.
// - HttpClient: GitHub üzerindeki txt dosyalarını indirmek için kullanılır.
// - Controllers: API uç noktalarını (endpoint) barındırır.
builder.Services.AddMemoryCache();
// Configure HttpClient with SSL bypass for GitHub downloads
builder.Services.AddHttpClient("github", client =>
{
    // GitHub raw content download configuration
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Bypass SSL certificate validation for GitHub (temporary fix)
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
    // Use TLS 1.2 for better compatibility
    SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls11 | System.Security.Authentication.SslProtocols.Tls
});
builder.Services.AddControllers();
// Swagger servislerini ekliyoruz: API için otomatik dokümantasyon ve UI.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// RiskDomainService tekil (Singleton) servis olarak eklenir.
// İlk istek geldiğinde listeleri indirip önbelleğe alır ve belli aralıklarla yeniler.
builder.Services.AddSingleton<RiskDomainService>();

// Güvenlik servisleri
builder.Services.AddSingleton<CircuitBreakerService>();
builder.Services.AddSingleton<MemoryProtectionService>();

// Rate limiting middleware
builder.Services.AddSingleton<RateLimitingMiddleware>();

// HTTP Context accessor for IP detection
builder.Services.AddHttpContextAccessor();

// Swagger, geliştirme sırasında API’yi keşfetmeyi kolaylaştırır.
// Swagger bağımlılığı eklemeden devam ediyoruz; temel API kullanımı yeterlidir.

var app = builder.Build();

// Aynı ağdaki cihazlardan erişim için Kestrel'i tüm arayüzlerde (0.0.0.0) dinletecek URL'leri tanımlıyoruz.
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Geliştirme ortamında Swagger UI’yi aktif eder.
// Swagger UI'yi tüm ortamda aktif ediyoruz (lokal doğrulama için kolaylık).
app.UseSwagger();
app.UseSwaggerUI();

// Güvenlik middleware'leri
app.UseSecurityLogging(); // Request/response logging ve saldırı tespiti
app.UseMiddleware<RateLimitingMiddleware>(); // IP tabanlı rate limiting
app.UsePerformanceMonitoring(); // Performans monitoring ve tracking

// Basit yönlendirme ve kontrolör eşlemeleri.
app.UseRouting();
app.MapControllers();

app.Run();