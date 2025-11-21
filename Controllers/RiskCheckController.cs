using Microsoft.AspNetCore.Mvc;
using RiskyWebsitesAPI.Models;
using RiskyWebsitesAPI.Services;

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

        // URL/Domain normalizasyonu: www. kaldırma, küçük harfe çevirme, şema yoksa ekleme vb.
        var normalizedHost = RiskDomainService.NormalizeToHost(url);

        // Risk kontrolü: GitHub'dan indirilen listelerde var mı?
        var result = await _riskService.CheckDomainAsync(normalizedHost);

        // Kullanıcıya anlayacağı şekilde Türkçe mesaj üretimi.
        string message = result.IsRisky
            ? "Girilen site riskli listelerde bulundu. Lütfen dikkatli olun."
            : "Girilen site riskli listelerde bulunamadı.";

        var response = new RiskCheckResponse
        {
            IsRisky = result.IsRisky,
            Message = message,
            CheckedDomain = normalizedHost,
            FoundInFiles = result.FoundInFiles
        };

        return Ok(response);
    }
}