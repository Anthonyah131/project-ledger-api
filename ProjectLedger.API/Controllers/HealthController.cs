using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Endpoint de health check para verificar que la API y la base de datos están operativas.
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
    /// GET /api/health — Verifica conectividad con la base de datos.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            // ExecuteScalar da el error real en lugar del bool silencioso de CanConnectAsync
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
            // En desarrollo mostramos el error completo para diagnóstico
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
