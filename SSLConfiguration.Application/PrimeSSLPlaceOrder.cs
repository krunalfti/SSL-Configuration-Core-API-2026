using System.Text.Json;
using SSLConfiguration.Infrastructure;
using VerisignGateway;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// PrimeSSL Summary / PlaceOrder — same gateway path as old ProductFactory PrimeSSL/*.PlaceOrder
    /// (VerisignUtil.GetPrimeSSLProductObject + QuickComodoOrder).
    /// </summary>
    public static class PrimeSSLPlaceOrder
    {
        public static PF_Response PlaceOrder(PF_Request objPFRequest)
        {
            PF_Response objReturn = new PF_Response();

            try
            {
                bool isLinkUsed = BLStoreOrder.CheckConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail!.StoreOrderId);
                if (isLinkUsed)
                {
                    objReturn.ErrorCode = -1000;
                    objReturn.ErrorFiled = "PIN";
                    objReturn.ErrorMessage = "Link is already used.";
                    return objReturn;
                }

                BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, true);

                int productId = objPFRequest.StoreOrderDetail.ProductId;
                var productCode = (VerisignGateway.ProductCode)productId;

                if (productId == (int)VerisignGateway.ProductCode.PrimeSSLVerifiedMarkCertificate
                    || productId == (int)VerisignGateway.ProductCode.PrimeSSLCommonMarkCertificate)
                {
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    objReturn.ErrorCode = -9999;
                    objReturn.ErrorMessage = "Use PrimeSSL VMC Summary endpoint for this product.";
                    return objReturn;
                }

                ProductBase objProd = ResolveSslProductObject(productId, productCode);

                ComodoOrderRequest objComodoOrderRequest = BLComodo.GetComodoOrderRequestObject(objPFRequest);
                BLComodo.ApplySanList(objComodoOrderRequest, objPFRequest);
                BLComodo.ApplyEvAndJurisdiction(objComodoOrderRequest, objPFRequest);

                OrderResponse? objComodoOrderResponse = null;
                try
                {
                    LogWriter.LogCARequestResponseObjectToDB(
                        objPFRequest.StoreOrderDetail.Pin ?? string.Empty,
                        objComodoOrderRequest,
                        JsonSerializer.Serialize(new { productId, factory = "PrimeSSLPlaceOrder" }),
                        "PrimeSSLPlaceOrderRequest");

                    objComodoOrderResponse = objProd.QuickComodoOrder(objComodoOrderRequest);
                }
                catch (Exception ex)
                {
                    LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    throw;
                }

                if (objComodoOrderResponse == null)
                {
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    objReturn.ErrorCode = -9999;
                    objReturn.ErrorMessage = "No response received from Comodo.";
                    return objReturn;
                }

                if (objComodoOrderResponse.error != null && objComodoOrderResponse.error.ErrorCode < 0)
                {
                    LogWriter.LogCARequestResponseObjectToDB(objPFRequest.StoreOrderDetail.Pin, objComodoOrderResponse);
                    LogWriter.LogComodoOrderRequestObjectsToDB(objPFRequest);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    BLComodo.SetErrorFromComodo(objReturn, objComodoOrderResponse);
                    return objReturn;
                }

                if (objComodoOrderResponse.error != null && objComodoOrderResponse.error.ErrorCode == 100)
                    BLComodo.SetErrorFromComodo(objReturn, objComodoOrderResponse);

                objReturn.VendorID = objComodoOrderResponse.OrderNumber;
                objReturn.ApprovalEmail = objPFRequest.ComodoOrderRequest.ComodoOrderDetailRow.ApprovalEmail;
                objReturn.DomainName = objPFRequest.ComodoOrderRequest.PrimaryDomain ?? objPFRequest.CSRDetailRow?.DomainName;
                objReturn.Comodo_DV_MD5String = objPFRequest.ComodoOrderRequest.ComodoOrderDetailRow.CSR_MD5;
                objReturn.Comodo_DV_SHA1string = objPFRequest.ComodoOrderRequest.ComodoOrderDetailRow.CSR_SHA1;
                objReturn.ComodoDVMethod = objPFRequest.ComodoOrderRequest.ApprovalMethod;
                objReturn.ComodoUniqueValue = objComodoOrderResponse.ComodoUniqueValue;
                objReturn.DNSTXTValue = objComodoOrderResponse.DNSTXTValue;
                objReturn.ComodoCertficateId = objComodoOrderResponse.ComodoCertficateID;

                BLComodo.SaveComodoOrderInDB(objPFRequest, objComodoOrderResponse);
            }
            catch (Exception ex)
            {
                LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex);
                throw;
            }

            return objReturn;
        }

        /// <summary>
        /// Same as old PrimeCodeSign / PrimeEVCodeSign ProductFactory PlaceOrder.
        /// </summary>
        public static PF_Response PlaceCodeSignOrder(PF_Request objPFRequest)
        {
            PF_Response objReturn = new PF_Response();

            try
            {
                bool isLinkUsed = BLStoreOrder.CheckConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail!.StoreOrderId);
                if (isLinkUsed)
                {
                    objReturn.ErrorCode = -1000;
                    objReturn.ErrorFiled = "PIN";
                    objReturn.ErrorMessage = "Link is already used.";
                    return objReturn;
                }

                BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, true);

                int productId = objPFRequest.StoreOrderDetail.ProductId;
                var productCode = (VerisignGateway.ProductCode)productId;
                ProductBase objProd = ResolveCodeSignProductObject(productId, productCode);

                ComodoOrderRequest objComodoOrderRequest = BLComodo.GetComodoOrderRequestForCodeSign(objPFRequest);
                ApplyPrimeEvCodeSignJurisdiction(objComodoOrderRequest, objPFRequest, productId);

                OrderResponse? objComodoOrderResponse;
                try
                {
                    objComodoOrderResponse = objProd.QuickComodoOrder(objComodoOrderRequest);
                }
                catch (Exception ex)
                {
                    LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    throw;
                }

                if (objComodoOrderResponse == null)
                {
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    objReturn.ErrorCode = -9999;
                    objReturn.ErrorMessage = "No response received from Comodo.";
                    return objReturn;
                }

                if (objComodoOrderResponse.error != null && objComodoOrderResponse.error.ErrorCode < 0)
                {
                    LogWriter.LogCARequestResponseObjectToDB(objPFRequest.StoreOrderDetail.Pin, objComodoOrderResponse);
                    LogWriter.LogComodoOrderRequestObjectsToDB(objPFRequest);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    BLComodo.SetErrorFromComodo(objReturn, objComodoOrderResponse);
                    return objReturn;
                }

                objReturn.VendorID = objComodoOrderResponse.OrderNumber;
                objReturn.DomainName = objPFRequest.ComodoOrderRequest.PrimaryDomain
                    ?? objPFRequest.CSRDetailRow?.DomainName
                    ?? objPFRequest.ComodoOrderRequest.ComodoCodeSignOrderInfo.OrganizationName;

                BLComodo.SaveComodoCodeSignOrderInDB(objPFRequest, objComodoOrderResponse);

                string skipPins = AppConfig.DONOTAuthorizedCodeSign ?? string.Empty;
                string pin = objPFRequest.StoreOrderDetail.Pin ?? string.Empty;
                if (!skipPins.Contains(pin))
                {
                    try
                    {
                        var comodoCredential = objPFRequest.CACredentialDetails?.GetComodoCACredential();
                        if (comodoCredential != null)
                        {
                            OrderResponse objComodoAuthorizeOrderResponse = VerisignUtil.AuthorizeComodoCodeSign(
                                objComodoOrderResponse.OrderNumber,
                                comodoCredential.UserName,
                                comodoCredential.Password);

                            if (objComodoAuthorizeOrderResponse.error != null
                                && objComodoAuthorizeOrderResponse.error.ErrorCode < 0)
                            {
                                LogWriter.LogComodoCodeSignAuthorizationError(
                                    objComodoAuthorizeOrderResponse.error.ErrorMessage,
                                    objPFRequest.StoreOrderDetail.StoreOrderId);
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex);
                throw;
            }

            return objReturn;
        }

        private static ProductBase ResolveSslProductObject(int productId, VerisignGateway.ProductCode productCode)
        {
            if (productId == (int)VerisignGateway.ProductCode.PrimeCodeSignCertificate
                || productId == (int)VerisignGateway.ProductCode.PrimeEVCodeSignCertificate)
            {
                throw new InvalidOperationException(
                    $"ProductId {productId} is a CodeSign product. Use PlaceCodeSignOrder.");
            }

            if (Common.GetProductType(productId) == ProductType.PrimeSSL)
                return VerisignUtil.GetPrimeSSLProductObject(productCode);

            throw new InvalidOperationException(
                $"ProductId {productId} is not a PrimeSSL SSL place-order product.");
        }

        private static ProductBase ResolveCodeSignProductObject(int productId, VerisignGateway.ProductCode productCode)
        {
            if (productId == (int)VerisignGateway.ProductCode.PrimeCodeSignCertificate)
                return VerisignUtil.GetComodoProductObject(VerisignGateway.ProductCode.PrimeCodeSignCertificate);

            if (productId == (int)VerisignGateway.ProductCode.PrimeEVCodeSignCertificate)
                return VerisignUtil.GetComodoProductObject(VerisignGateway.ProductCode.PrimeEVCodeSignCertificate);

            return VerisignUtil.GetComodoProductObject(productCode);
        }

        private static void ApplyPrimeEvCodeSignJurisdiction(
            ComodoOrderRequest request,
            PF_Request pf,
            int productId)
        {
            if (productId != (int)VerisignGateway.ProductCode.PrimeEVCodeSignCertificate)
                return;

            var cs = pf.ComodoOrderRequest.ComodoCodeSignOrderInfo;
            request.JurisdictionCity = string.IsNullOrEmpty(cs.JurictionCity) ? string.Empty : cs.JurictionCity;
            request.JurisdictionCountry = string.IsNullOrEmpty(cs.JurictionCountryName) ? string.Empty : cs.JurictionCountryName;
            request.JurisdictionState = string.IsNullOrEmpty(cs.jurictionState) ? string.Empty : cs.jurictionState;
        }
    }
}
