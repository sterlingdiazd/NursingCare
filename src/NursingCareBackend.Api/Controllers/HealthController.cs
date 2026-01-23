using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace NursingCareBackend.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public HealthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            bool dbHealthy = false;
            string dbMessage = "Unknown";

            try
            {
                using var connection = new SqlConnection(dbConnectionString);
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
