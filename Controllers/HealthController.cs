using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Health check endpoint to verify that the API and database are operational.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _context;

    public HealthController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/health — Verifies database connectivity.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            // ExecuteScalar throws the actual error instead of the silent bool from CanConnectAsync
            await _context.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return Ok(new
            {
                status = "healthy",
                database = "connected",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            // In development, show the complete error for troubleshooting
            var isDev = HttpContext.RequestServices
                .GetRequiredService<IHostEnvironment>().IsDevelopment();

            return StatusCode(503, new
            {
                status = "unhealthy",
                database = "error",
                message = ex.Message,
                detail = isDev ? ex.ToString() : null,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
