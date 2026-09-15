using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SSLConfiguration.Application;
using SSLConfiguration.Infrastructure.Persistence;

namespace SSL_Configuration_Core_API_2026.Controllers
{
    /// <summary>
    /// Read-only checks so we can validate Core DB wiring against real data
    /// before calling Digicert mutation APIs.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        /// <summary>
        /// Verifies SQL connection string and basic connectivity.
        /// GET /api/Diagnostics/VerifyDatabase
        /// </summary>
        [HttpGet("VerifyDatabase")]
        public IActionResult VerifyDatabase()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(DbConfig.SSLConfigurationEntities))
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Connection string SSLConfigurationEntities is not loaded."
                    });
                }

                using (var connection = new SqlConnection(DbConfig.SSLConfigurationEntities))
                {
                    connection.Open();

                    using (var command = new SqlCommand("SELECT DB_NAME() AS DatabaseName, @@SERVERNAME AS ServerName, SYSTEM_USER AS DbUser", connection))
                    using (var reader = command.ExecuteReader())
                    {
                        reader.Read();

                        return Ok(new
                        {
                            success = true,
                            message = "Database connection successful.",
                            serverName = Convert.ToString(reader["ServerName"]),
                            databaseName = Convert.ToString(reader["DatabaseName"]),
                            dbUser = Convert.ToString(reader["DbUser"])
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Verifies old SP path: BLGeneral.GetCACredentials -> GetCACredentialCode.
        /// GET /api/Diagnostics/VerifyCACredential?storeOrderId=123
        /// </summary>
        [HttpGet("VerifyCACredential")]
        public IActionResult VerifyCACredential([FromQuery] int storeOrderId)
        {
            try
            {
                if (storeOrderId <= 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "storeOrderId must be greater than 0."
                    });
                }

                var credential = BLGeneral.GetCACredentials(storeOrderId);

                if (credential == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "No CACredential returned by SP GetCACredentialCode for this StoreOrderId.",
                        storeOrderId
                    });
                }

                string? apiKey = credential.DigicertAPIKey;
                bool hasApiKey = !string.IsNullOrWhiteSpace(apiKey);
                string? maskedApiKey = null;

                if (hasApiKey && apiKey!.Length > 8)
                {
                    maskedApiKey = apiKey.Substring(0, 4) + "****" + apiKey.Substring(apiKey.Length - 4);
                }
                else if (hasApiKey)
                {
                    maskedApiKey = "****";
                }

                return Ok(new
                {
                    success = true,
                    message = "CACredential loaded successfully via GetCACredentialCode.",
                    storeOrderId,
                    caCredentialId = credential.CACredentialID,
                    credentialCode = credential.CredentialCode,
                    caName = credential.CAName,
                    hasDigicertApiKey = hasApiKey,
                    digicertApiKeyMasked = maskedApiKey
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = ex.Message,
                    storeOrderId
                });
            }
        }
    }
}
