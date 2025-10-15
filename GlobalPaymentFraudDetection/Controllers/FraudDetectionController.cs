using Microsoft.AspNetCore.Mvc;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FraudDetectionController : ControllerBase
{
    private readonly IFraudScoringService _fraudScoringService;
    private readonly ILogger<FraudDetectionController> _logger;

    public FraudDetectionController(IFraudScoringService fraudScoringService, ILogger<FraudDetectionController> logger)
    {
        _fraudScoringService = fraudScoringService;
        _logger = logger;
    }

    [HttpPost("score")]
    public async Task<IActionResult> ScoreTransaction([FromBody] Transaction transaction)
    {
        try
        {
            var result = await _fraudScoringService.ScoreTransactionAsync(transaction);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scoring transaction");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
