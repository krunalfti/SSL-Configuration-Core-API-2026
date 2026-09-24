using System.Text.Json;
using SSLConfiguration.Infrastructure;
using VerisignGateway;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Comodo Summary / PlaceOrder — same gateway path as old ProductFactory Comodo*.PlaceOrder
    /// (VerisignUtil.GetComodoProductObject + QuickComodoOrder).
    /// </summary>
    public static class ComodoPlaceOrder
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

                ProductBase objProd = VerisignUtil.GetComodoProductObject(
                    (VerisignGateway.ProductCode)objPFRequest.StoreOrderDetail.ProductId);

                ComodoOrderRequest objComodoOrderRequest = BLComodo.GetComodoOrderRequestObject(objPFRequest);
                BLComodo.ApplySanList(objComodoOrderRequest, objPFRequest);
                BLComodo.ApplyEvAndJurisdiction(objComodoOrderRequest, objPFRequest);

                OrderResponse? objComodoOrderResponse = null;
                try
                {
                    LogWriter.LogCARequestResponseObjectToDB(
                        objPFRequest.StoreOrderDetail.Pin ?? string.Empty,
                        objComodoOrderRequest,
                        JsonSerializer.Serialize(new { productId = objPFRequest.StoreOrderDetail.ProductId }),
                        "ComodoPlaceOrderRequest");

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

                // Same as old EV factories: ErrorCode 100 = EV contact submit warning (order still succeeds)
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

                ProductBase objProd = VerisignUtil.GetComodoProductObject(
                    (VerisignGateway.ProductCode)objPFRequest.StoreOrderDetail.ProductId);

                ComodoOrderRequest objComodoOrderRequest = BLComodo.GetComodoOrderRequestForCodeSign(objPFRequest);
                objComodoOrderRequest.JurisdictionCity = string.IsNullOrEmpty(objPFRequest.ComodoOrderRequest.ComodoCodeSignOrderInfo.JurictionCity) ? string.Empty : objPFRequest.ComodoOrderRequest.ComodoCodeSignOrderInfo.JurictionCity;
                objComodoOrderRequest.JurisdictionCountry = string.IsNullOrEmpty(objPFRequest.ComodoOrderRequest.ComodoCodeSignOrderInfo.JurictionCountryName) ? string.Empty : objPFRequest.ComodoOrderRequest.ComodoCodeSignOrderInfo.JurictionCountryName;
                objComodoOrderRequest.JurisdictionState = string.IsNullOrEmpty(objPFRequest.ComodoOrderRequest.ComodoCodeSignOrderInfo.jurictionState) ? string.Empty : objPFRequest.ComodoOrderRequest.ComodoCodeSignOrderInfo.jurictionState;
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

        public static PF_Response PlacePACOrder(PF_Request objPFRequest)
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

                ProductBase objProd = VerisignUtil.GetComodoProductObject(
                    (VerisignGateway.ProductCode)objPFRequest.StoreOrderDetail.ProductId);

                ComodoOrderRequest objComodoOrderRequest =
                    BLComodo.GetComodoPersonalAuthenticationCertificateRequestObject(objPFRequest);

                objPFRequest.ComodoOrderRequest.ComodoPACOrderInfo.PACUser = objComodoOrderRequest.PACOrderDetail?.PACUser;
                objPFRequest.ComodoOrderRequest.ComodoPACOrderInfo.PACPassword = objComodoOrderRequest.PACOrderDetail?.PACPassword;

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
                BLComodo.SaveComodoPACOrderInDB(objPFRequest, objComodoOrderResponse);

                try
                {
                    objComodoOrderRequest.PACOrderNumber = objComodoOrderResponse.OrderNumber;
                    OrderResponse objComodoAuthorizeOrderResponse = objProd.AuthorizeComodoPACOrder(objComodoOrderRequest);
                    if (objComodoAuthorizeOrderResponse.error != null
                        && objComodoAuthorizeOrderResponse.error.ErrorCode < 0)
                    {
                        LogWriter.LogError(
                            "ErrorCode: " + objComodoAuthorizeOrderResponse.error.ErrorCode
                            + ", ErrorField: " + objComodoAuthorizeOrderResponse.error.ErrorField
                            + ", ErrorMessage: " + objComodoAuthorizeOrderResponse.error.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogErrorDetails(ex);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex);
                throw;
            }

            return objReturn;
        }
    }
}
