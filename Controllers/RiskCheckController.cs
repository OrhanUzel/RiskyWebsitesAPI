using Microsoft.AspNetCore.Mvc;
using RiskyWebsitesAPI.Models;
using RiskyWebsitesAPI.Services;
using System.Text.Json;

namespace RiskyWebsitesAPI.Controllers;

// API kontrolörü: Kullanıcıdan bir site/URL alır ve risk kontrolü yapar.
[ApiController]
[Route("api/[controller]")]
public class RiskCheckController : ControllerBase
{
    private readonly RiskDomainService _riskService;

    // Servis bağımlılığı enjekte edilir.
    public RiskCheckController(RiskDomainService riskService)
    {
        _riskService = riskService;
    }

    // GET /api/riskcheck?url=https://örnek.site
    // Kullanıcıdan "url" soru parametresi bekler ve sonucu JSON olarak döner.
    [HttpGet]
    public async Task<ActionResult<RiskCheckResponse>> Get([FromQuery] string url)
    {
        // Girdi doğrulama: Boş veya sadece boşluklardan oluşuyorsa 400 döner.
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest(new RiskCheckResponse
            {
                IsRisky = false,
                Message = "Lütfen kontrol edilecek site adresini (url) belirtin.",
                CheckedDomain = null,
                FoundInFiles = Array.Empty<string>()
            });
        }

        try
        {
            // URL/Domain normalizasyonu: www. kaldırma, küçük harfe çevirme, şema yoksa ekleme vb.
            var normalizedHost = RiskDomainService.NormalizeToHost(url);

            // Risk kontrolü: GitHub'dan indirilen listelerde var mı?
            var result = await _riskService.CheckDomainAsync(normalizedHost);

            // Detaylı mesaj üretimi
            string message;
            if (result.IsRisky)
            {
                message = $"⚠️ GİRİLEN SİTE RİSKLİ LİSTELERDE BULUNDU! '{normalizedHost}' {string.Join(", ", result.FoundInFiles)} listelerinde yer alıyor. Lütfen dikkatli olun.";
            }
            else if (result.FoundInFiles.Length == 0)
            {
                message = $"✅ '{normalizedHost}' domaini riskli listelerde bulunamadı. Ancak domain listeleri yüklenirken sorun yaşanmış olabilir.";
            }
            else
            {
                message = $"✅ '{normalizedHost}' domaini riskli listelerde bulunamadı.";
            }

            var response = new RiskCheckResponse
            {
                IsRisky = result.IsRisky,
                Message = message,
                CheckedDomain = normalizedHost,
                FoundInFiles = result.FoundInFiles
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            // Genel hata yakalama
            Console.WriteLine($"DEBUG: Exception - {ex.Message}");
            return StatusCode(500, new RiskCheckResponse
            {
                IsRisky = false,
                Message = $"❌ Kontrol sırasında hata oluştu: {ex.Message}",
                CheckedDomain = url,
                FoundInFiles = Array.Empty<string>()
            });
        }
    }
}