using Microsoft.AspNetCore.Http.HttpResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SSLConfiguration.Contracts.SSLConfiguration_WebAPI
{
    public class ExtendSectigoACMESubscriptionRequest : OrderBaseRequest
    {
        public List<StoreOrderDetail> StoreOrderDetails { get; set; }
    }

    public class StoreOrderDetail
    {
        public int SSLApiLinkId { get; set; }
        public int ProductId { get; set; }
        public int Year { get; set; }
        public int San { get; set; }
        public int MinSan { get; set; }
        public string ProductName { get; set; }
        public string CompanyName { get; set; }
        public string CredentialCode { get; set; }
        public bool IsMultiDomain { get; set; }
        public int WildcardSAN { get; set; }
        public bool IsSubscription { get; set; }

        public bool? IsCAMYP { get; set; }
        public int? CAOrderValidity { get; set; }
        public DateTime? CAOrderValidFrom { get; set; }
        public DateTime? CAOrderValidTo { get; set; }
        public string? CodeSignProvisioningMethod { get; set; }
        public string? CodeSignShippingCode { get; set; }
        public int RemainingValidity { get; set; }

        public bool? IsAutoConfig { get; set; }
        public bool? IsStoreMYP { get; set; }
        public int? SubscriptionYear { get; set; }
        public string? SpecialNote { get; set; }        
    }
    public class ExtendSectigoACMESubscriptionResponse : OrderBaseResponse
    {
        public ExtendSectigoACMESubscriptionResponse()
        {
            ConfigurationPinDetails = new Dictionary<int, string>();
        }
        public Dictionary<int, string> ConfigurationPinDetails { get; set; }
    }
    public class OrderBaseRequest
    {
        public int StoreId { get; set; }
        public string Pin { get; set; }
        public int SSLApiLinkId { get; set; }
        public string AuthKey { get; set; }
        public string ApiOrderNo { get; set; }

    }
    public class OrderBaseResponse
    {
        public OrderErrors ErrorDetail;
        public int StatusCode { get; set; }
        public string CredentialCode { get; set; }

        public OrderBaseResponse()
        {
            ErrorDetail = new OrderErrors();
            ErrorDetail.ErrorField = string.Empty;
            ErrorDetail.ErrorMessage = string.Empty;
        }
    }
    public class OrderErrors
    {
        public int ErrorNumber;
        public string ErrorField;
        public string ErrorMessage;
    }
    public class SaveStoreOrderResponse
    {
        public bool Success { get; set; }
        public int StoreOrderId { get; set; }
        public int SSLApiLinkId { get; set; }
        public string Pin { get; set; }
        public string Message { get; set; }
    }
}
