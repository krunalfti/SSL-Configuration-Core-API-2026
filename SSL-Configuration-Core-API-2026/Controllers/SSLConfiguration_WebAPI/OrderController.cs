using Microsoft.AspNetCore.Mvc;
using SSLConfiguration.Application;
using SSLConfiguration.Application.Services;
using SSLConfiguration.Contracts.SSLConfiguration_WebAPI;

namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration_WebAPI
{    
    [ApiController]
    [Route("api/SSLConfiguration_WebAPI/[controller]")]
    public class OrderController : BaseApiController
    {
        [HttpPost("SaveStoreOrderDetails")]
        public SaveStoreOrderDetailResponse SaveStoreOrderDetails([FromBody] SaveStoreOrderDetailRequest request)
        {
            var response = new SaveStoreOrderDetailResponse();

            try
            {
                if (!this.ValidateAPIRequest(request))
                {
                    response.ErrorDetail = BaseError;
                    response.StatusCode = -1;
                    return response;
                }


                response = BLStoreOrder.SaveStoreOrderDetails(request);
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
    }
}
