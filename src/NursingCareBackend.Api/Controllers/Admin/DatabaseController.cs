using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/[controller]")]
public class DatabaseController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseController> _logger;

    public DatabaseController(IServiceProvider serviceProvider, ILogger<DatabaseController> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Applies pending database migrations. For admin use only.
    /// </summary>
    [HttpPost("migrate")]
    [Authorize]
    public async Task<IActionResult> ApplyMigrations()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

            _logger.LogInformation("Manually applying database migrations...");
            
            await db.Database.MigrateAsync();
            
            _logger.LogInformation("Database migrations applied successfully.");
            
            return Ok(new { message = "Migrations applied successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply migrations");
            return StatusCode(500, new { message = "Migration failed", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets the current database status. For admin use only.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetDatabaseStatus()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();

            var canConnect = await db.Database.CanConnectAsync();
            var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
            var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();

            return Ok(new
            {
                connected = canConnect,
                pendingMigrations = pendingMigrations.ToList(),
                appliedMigrations = appliedMigrations.ToList(),
                migrationCount = appliedMigrations.Count()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get database status");
            return StatusCode(500, new { message = "Failed to get database status", error = ex.Message });
        }
    }
}
