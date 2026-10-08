using Microsoft.AspNetCore.Mvc;
using SSLConfiguration.Application;
using SSLConfiguration.Application.Services;
using SSLConfiguration.Contracts.SSLConfiguration_WebAPI;
using SSLConfiguration.Infrastructure.DataAccess;
using SSLConfiguration.Infrastructure.Persistence;
using System.Text.Json;

namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration_WebAPI
{
    //Summary: Acme Product Renewal API Controller for handling order-related operations.
    // ExtendSectigoACMESubscription
    [ApiController]
    [Route("api/SSLConfiguration_WebAPI/[controller]")]
    public class OrderController : BaseApiController
    {
        [HttpPost("ExtendSectigoACMESubscription")]
        public ExtendSectigoACMESubscriptionResponse ExtendSectigoACMESubscription([FromBody] ExtendSectigoACMESubscriptionRequest request)
        {
            var response = new ExtendSectigoACMESubscriptionResponse();

            try
            {
                if (!this.ValidateAPIRequest(request))
                {
                    response.ErrorDetail = BaseError;
                    response.StatusCode = -1;
                    return response;
                }


                response = BLStoreOrder.ExtendSectigoACMESubscription(request);
                return response;
            }
            catch (Exception ex)
            {
                LogWriter.LogError(ex.Message);
                response.StatusCode = -1;
                response.ErrorDetail.ErrorMessage = "Error while saving order details. please contact support or administrator.";
                return response;
            }
        }
        [HttpPost("RetrySectigoAcmeSubscription")]
        public IActionResult RetrySectigoAcmeSubscription(int errorRecordId)
        {
            try
            {
                using var dbContext = new SSLConfigurationEntities();

                var errorRecord = StoreOrderDataAccess.GetRetrySectigoAcmeSubscriptionErrorData(dbContext,errorRecordId);

                if (errorRecord == null)
                {
                    return NotFound(new { Success = false, Message = "Recovery record not found." });
                }

                if (errorRecord.IsProcessed)
                {
                    return Ok(new { Success = true, Message = "This record has already been processed." });
                }

                var storeOrderDetails = JsonSerializer.Deserialize<StoreOrderDetail>(errorRecord.RequestJson);

                if (storeOrderDetails == null)
                {
                    return BadRequest(new { Success = false, Message = "Invalid RequestJson." });
                }

                var result = StoreOrderDataAccess.RenewAcmeStoreOrder(dbContext, errorRecord.StoreId, errorRecord.OrderNumber, storeOrderDetails);

                if (result != null && result.Success)
                {
                    errorRecord.IsProcessed = true;
                    errorRecord.ErrorMessage = result.Message;
                    errorRecord.RetryCount++;

                    dbContext.SaveChanges();

                    return Ok(new { Success = true, StoreOrderId = result.StoreOrderId, Pin = result.Pin, Message = "ACME database recovery completed successfully." });
                }

                errorRecord.RetryCount++;
                errorRecord.ErrorMessage = result?.Message ?? "Database retry failed.";

                dbContext.SaveChanges();

                return Ok(new { Success = false, Message = result?.Message ?? "Database retry failed." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
    }
}
