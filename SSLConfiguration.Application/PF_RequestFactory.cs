using SSLConfiguration.Infrastructure.DataAccess;
using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Builds PF_Request from PIN/DB the same way Home Index did before putting it in Session.
    /// </summary>
    public static class PF_RequestFactory
    {
        public static PF_Request? CreateFromPin(string pin)
        {
            if (string.IsNullOrWhiteSpace(pin))
            {
                return null;
            }

            StoreOrder? orderDetail = BLStoreOrder.GetStoreOrderDetailByPIN(pin.Trim());
            if (orderDetail == null)
            {
                return null;
            }

            Product? productDetail = BLGeneral.GetProductDetail_StoreOrder(pin.Trim());
            if (productDetail == null)
            {
                return null;
            }

            CACredential? caCredential = BLGeneral.GetCACredentials(orderDetail.CredentialCode ?? string.Empty);
            string controllerName = ResolveControllerName(productDetail.ProductId);

            var request = new PF_Request
            {
                PIN = pin.Trim(),
                StoreOrderDetail = orderDetail,
                ProductDetail = productDetail,
                CACredentialDetails = caCredential,
                ControllerName = controllerName,
                LanguageCode = orderDetail.LanguageCode,
                DigicertOrderRequest = new PF_DigicertOrder
                {
                    AdditionalDomainList = new Dictionary<string, string>(),
                    AdditionalWildCardDomainList = new Dictionary<string, string>(),
                    IsFreeSANInclude = !productDetail.IsX9
                },
                ComodoOrderRequest = new PF_ComodoOrder
                {
                    SAN_ApprovalEmail = new Dictionary<string, string>(),
                    WildcardSAN_ApprovalEmail = new Dictionary<string, string>()
                },
                GlobalSignOrderRequest = new PF_GlobalSignOrder
                {
                    AdditionalDomainsList = new Dictionary<string, string>(),
                    WildcardSANDomainList = new List<string>()
                }
            };

            return request;
        }

        private static string ResolveControllerName(int productId)
        {
            var productType = Common.GetProductType(productId);
            return productType switch
            {
                ProductType.GlobalSign => "GlobalSign",
                ProductType.PrimeSSL or ProductType.PrimeVMCCMC => "PrimeSSL",
                ProductType.Comodo or ProductType.SectigoAcmeDV or ProductType.SectigoAcmeOV => "Comodo",
                ProductType.Digicert => "Digicert",
                ProductType.ClickSSL => "ClickSSL",
                _ => "Digicert"
            };
        }
    }
}
