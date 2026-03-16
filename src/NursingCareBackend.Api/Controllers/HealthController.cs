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

        public HealthController(ResolvedConnectionString connectionString)
        {
            _connectionString = connectionString.Value;
        }


        [HttpGet]
        public IActionResult Get()
        {
            bool dbHealthy = false;
            string dbMessage = "Unknown";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();
                dbHealthy = connection.State == System.Data.ConnectionState.Open;
                dbMessage = "Connected";
            }
            catch (Exception ex)
            {
                dbHealthy = false;
                dbMessage = ex.Message;
            }

            var overallStatus = dbHealthy ? "Healthy" : "Unhealthy";

            var response = new
            {
                status = overallStatus,
                timestamp = DateTime.UtcNow,
                database = dbMessage
            };

            return dbHealthy ? Ok(response) : StatusCode(503, response);
        }
    }
}
