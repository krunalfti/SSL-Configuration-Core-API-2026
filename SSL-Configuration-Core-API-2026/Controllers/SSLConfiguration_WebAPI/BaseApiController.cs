using Microsoft.AspNetCore.Mvc;
using SSLConfiguration.Application;
using SSLConfiguration.Contracts.SSLConfiguration_WebAPI;

namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration_WebAPI
{
    public class BaseApiController : ControllerBase
    {
        protected OrderErrors BaseError { get; set; }

        protected bool ValidateAPIRequest(OrderBaseRequest baseRequest)
        {
            BaseError = new OrderErrors();

            if (string.IsNullOrEmpty(baseRequest.AuthKey) || Convert.ToInt32(baseRequest.StoreId) == 0)
            {
                BaseError.ErrorField = "UnAuthorizeAccess";
                BaseError.ErrorMessage = "Unauthorize access to api.";
                BaseError.ErrorNumber = -1000;
                return false;
            }
            
            string _authKey = System.Configuration.ConfigurationManager.AppSettings[baseRequest.StoreId.ToString()];

            if (baseRequest.AuthKey.ToLower() == _authKey.ToLower())
            {
                return true;
            }

            BaseError.ErrorField = "UnAuthorizeAccess";
            BaseError.ErrorMessage = "Unauthorize access to api.";
            BaseError.ErrorNumber = -1000;

            return false;
        }
    }
}
