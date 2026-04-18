using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NursingCareBackend.Infrastructure;

namespace NursingCareBackend.Api.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly ILogger<HealthController> _logger;

        public HealthController(ResolvedConnectionString connectionString, ILogger<HealthController> logger)
        {
            _connectionString = connectionString.Value;
            _logger = logger;
        }

        /// <summary>
        /// Liveness probe — confirms the process is alive and able to handle requests.
        /// Always returns 200. No external dependency checks.
        /// </summary>
        [HttpGet("live")]
        public IActionResult Live()
        {
            return Ok(new
            {
                status = "Alive",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Readiness probe — confirms the service is ready to serve traffic.
        /// Checks database connectivity. Returns 200 when ready, 503 when not.
        /// </summary>
        [HttpGet("ready")]
        public IActionResult Ready()
        {
            var (dbStatus, dbError) = CheckDatabase();

            var response = new
            {
                status = dbStatus == "Healthy" ? "Ready" : "NotReady",
                timestamp = DateTime.UtcNow,
                checks = new
                {
                    database = dbError is null
                        ? (object)new { status = dbStatus }
                        : new { status = dbStatus, error = dbError }
                }
            };

            return dbStatus == "Healthy" ? Ok(response) : StatusCode(503, response);
        }

        /// <summary>
        /// Aggregate health endpoint — combines liveness and readiness.
        /// Returns 200 when all checks pass, 503 when any check fails.
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            var (dbStatus, dbError) = CheckDatabase();

            var overallHealthy = dbStatus == "Healthy";

            var response = new
            {
                status = overallHealthy ? "Healthy" : "Unhealthy",
                timestamp = DateTime.UtcNow,
                checks = new
                {
                    database = dbError is null
                        ? (object)new { status = dbStatus }
                        : new { status = dbStatus, error = dbError }
                }
            };

            return overallHealthy ? Ok(response) : StatusCode(503, response);
        }

        private (string status, HealthErrorDetail? error) CheckDatabase()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                if (connection.State == System.Data.ConnectionState.Open)
                {
                    return ("Healthy", null);
                }

                _logger.LogWarning("Database connection opened but state is {State}", connection.State);
                return ("Unhealthy", new HealthErrorDetail("DB_UNAVAILABLE", "Database unavailable"));
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Database health check failed with SqlException (Number={Number})", ex.Number);
                return ("Unhealthy", new HealthErrorDetail("DB_UNAVAILABLE", "Database unavailable"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed with unexpected exception");
                return ("Unhealthy", new HealthErrorDetail("DB_UNAVAILABLE", "Database unavailable"));
            }
        }

        private sealed record HealthErrorDetail(string Code, string Message);
    }
}
