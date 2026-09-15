using Microsoft.AspNetCore.Mvc;
using SSLConfiguration.Application;
using SSLConfiguration.Contracts.ManageOrder.Digicert;
using SSLConfiguration.Infrastructure;
using VerisignGateway;

namespace SSL_Configuration_Core_API_2026.Controllers.ManageOrder
{
    /// <summary>
    /// Ported from SSLConfiguration Areas/ManageOrder DigicertController.
    /// </summary>
    [ApiController]
    [Route("api/ManageOrder/[controller]")]
    public class DigicertController : ControllerBase
    {
        /// <summary>
        /// Same action as ManageOrder DigicertController.UpdateDigicertVMCHostingFileSetting.
        /// Route: POST /api/ManageOrder/Digicert/UpdateDigicertVMCHostingFileSetting
        /// </summary>
        [HttpPost("UpdateDigicertVMCHostingFileSetting")]
        public IActionResult UpdateDigicertVMCHostingFileSetting(
            [FromBody] UpdateDigicertVMCHostingFileSettingRequest request)
        {
            var response = new UpdateDigicertVMCHostingFileSettingResponse();

            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.CAOrderNo) ||
                    request.StoreOrderId <= 0)
                {
                    response.success = false;
                    response.message = "CAOrderNo and StoreOrderId are required.";
                    return Ok(response);
                }

                var objCACredential = BLGeneral.GetCACredentials(request.StoreOrderId);

                if (objCACredential == null)
                {
                    response.success = false;
                    response.message = "CA credential not found for StoreOrderId.";
                    return Ok(response);
                }

                if (string.IsNullOrWhiteSpace(objCACredential.DigicertAPIKey))
                {
                    response.success = false;
                    response.message = "DigicertAPIKey is missing on CACredential for this StoreOrderId.";
                    return Ok(response);
                }

                DigicertAPIHelper.UpdateDigicertVMCHostingFileSetting(
                    request.CAOrderNo,
                    request.EnableDigicertHost,
                    objCACredential.GetDigicertCACredential());

                response.success = true;
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.success = false;
                response.message = ex.Message;
                return Ok(response);
            }
        }
    }
}
