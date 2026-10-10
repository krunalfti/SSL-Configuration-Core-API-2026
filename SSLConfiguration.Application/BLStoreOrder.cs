using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSLConfiguration.Contracts.SSLConfiguration_WebAPI;
using SSLConfiguration.Infrastructure.DataAccess;
using SSLConfiguration.Infrastructure.Persistence;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Transactions;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Thin layer architecture / Core migration: keep same BLStoreOrder method names as old repo.
    /// </summary>
    public class BLStoreOrder
    {       
        public static StoreOrder? GetStoreOrderDetailByPIN(string Pin)
        {
            return StoreOrderDataAccess.GetByPin(Pin);
        }

        /// <summary>
        /// Same as old BLStoreOrder.CheckConfigurationLinkUsedStatus (Session/MYP special-case skipped in Core API).
        /// </summary>
        public static bool CheckConfigurationLinkUsedStatus(int storeOrderId)
        {
            StoreOrder? storeOrder = StoreOrderDataAccess.GetById(storeOrderId);

            if (storeOrder == null)
            {
                return false;
            }

            if (Convert.ToBoolean(storeOrder.IsUsed))
            {
                return true;
            }

            if (Convert.ToBoolean(storeOrder.IsActive) == false)
            {
                return true;
            }

            if (Convert.ToBoolean(storeOrder.IsCancel) == true)
            {
                return true;
            }

            return false;
        }

        public static void UpdateConfigurationLinkUsedStatus(int storeOrderId, bool status)
        {
            StoreOrderDataAccess.UpdateUsedStatus(storeOrderId, status);
        }

        /// <summary>
        /// Same as old BLStoreOrder.UpdateAPIOrderNoInStoreOrder (Session culture → LanguageCode param).
        /// </summary>
        public static void UpdateAPIOrderNoInStoreOrder(int storeOrderId, string APIOrderNo, string? languageCode = null)
        {
            StoreOrder? storeOrder = StoreOrderDataAccess.GetById(storeOrderId);

            if (storeOrder != null)
            {
                storeOrder.ApiOrderNo = APIOrderNo;
                storeOrder.IsAllowIssueCertificate = false;
                storeOrder.LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode;
                StoreOrderDataAccess.Update(storeOrder);
            }
        }

        /// <summary>
        /// Persist selected UI culture to StoreOrder.LanguageCode (old Session CurrentCulture).
        /// </summary>
        public static void UpdateLanguageCode(int storeOrderId, string? languageCode)
        {
            StoreOrder? storeOrder = StoreOrderDataAccess.GetById(storeOrderId);
            if (storeOrder == null)
                return;

            storeOrder.LanguageCode = CultureHelper.Normalize(languageCode);
            StoreOrderDataAccess.Update(storeOrder);
        }
        /// <summary>
        /// Renew Old Acme Order 
        /// </summary>
        #region Renew Acme Product
        public static ExtendSectigoACMESubscriptionResponse ExtendSectigoACMESubscription(ExtendSectigoACMESubscriptionRequest extendSectigoACMESubscriptionRequest)
        {
            string request = "";
            string response = "";
            ExtendSectigoACMESubscriptionResponse extendSectigoACMESubscriptionResponseResponse = new ExtendSectigoACMESubscriptionResponse();
            var sslConfiguratinLinks = new Dictionary<int, string>();

            try
            {
                using var dbContext = new SSLConfigurationEntities();
                using var transaction = dbContext.Database.BeginTransaction();
                //Call Verisgn API to get reponse of Extended certificate order and save the response in StoreOrder table

                var caCredentialDetails = BLGeneral.GetCACredentials(extendSectigoACMESubscriptionRequest.StoreOrderDetails[0].CredentialCode);
                var username = caCredentialDetails?.UserName;
                var password = caCredentialDetails?.Password;

                var acmeServerRequest = new VerisignGateway.ACMERenewBaseRequest
                {
                    loginName = username,
                    loginPassword = password,
                    action = "EXTENDDOMAINS",
                    EABID = extendSectigoACMESubscriptionRequest.ApiOrderNo,
                    QuoteOnly = "N",
                    years = "1"
                };
                request = "Request Object: " + JsonSerializer.Serialize(acmeServerRequest);
                LogWriter.LogAcmeAPIRequest(extendSectigoACMESubscriptionRequest.SSLApiLinkId, extendSectigoACMESubscriptionRequest.Pin, "", request, "EXTENDDOMAINS");
                var serverResponse = VerisignGateway.AcemeAPIHelper.GetExtendedCertificateReponse(acmeServerRequest);
                int extensionDurationDays = serverResponse.extensionDurationDays;
                DateTime startDate = DateTime.UtcNow;
                DateTime endDate = startDate.AddDays(extensionDurationDays);
                var storeOrderDetails = extendSectigoACMESubscriptionRequest.StoreOrderDetails[0];
                var storeOrder = StoreOrderDataAccess.GetBySSLApiLinkIdAndStoreId(extendSectigoACMESubscriptionRequest.ApiOrderNo, extendSectigoACMESubscriptionRequest.StoreId);
                var oldStoreOrderId = storeOrder?.StoreOrderId ?? 0;
                if (serverResponse.success)
                {
                    response = "Response Object: " + JsonSerializer.Serialize(serverResponse);
                    LogWriter.LogAcmeAPIRequest(extendSectigoACMESubscriptionRequest.SSLApiLinkId, extendSectigoACMESubscriptionRequest.Pin, "", response, "EXTENDDOMAINS");
                    #region SP CALL - SaveStoreOrderDetailsForRenew
                    var result = StoreOrderDataAccess.RenewAcmeStoreOrder(dbContext, extendSectigoACMESubscriptionRequest.StoreId, extendSectigoACMESubscriptionRequest.ApiOrderNo, storeOrderDetails);

                    if (result != null && result.Success)
                    {
                        int newStoreOrderId = result.StoreOrderId;
                        string newPin = result.Pin;

                        sslConfiguratinLinks.Add(extendSectigoACMESubscriptionRequest.StoreOrderDetails[0].SSLApiLinkId, newPin);
                        extendSectigoACMESubscriptionResponseResponse.ConfigurationPinDetails = sslConfiguratinLinks;
                        extendSectigoACMESubscriptionResponseResponse.StatusCode = 0;
                        // Save Renewal Order Detail at respective Store Database..
                        var storeMasterDetail = StoreOrderDataAccess.GetStoreMasterDetailsForRenewal(extendSectigoACMESubscriptionRequest.StoreId);                     
                        try
                        {
                            if (storeMasterDetail != null)
                            {
                                var handlerURL = storeMasterDetail.CallBackURL;
                                handlerURL += "?sslapiLinkId=" + storeOrderDetails.SSLApiLinkId;
                                handlerURL += "&apiOrderNo=" + extendSectigoACMESubscriptionRequest.ApiOrderNo;
                                handlerURL += "&StartDate=" + startDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
                                handlerURL += "&EndDate=" + endDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
                                handlerURL += "&OrderStatus=InProcess";
                                handlerURL += "&ProductId=" + storeOrderDetails.ProductId;
                                handlerURL += "&IsAcmeConfig=" + true;
                                handlerURL += "&pin=" + newPin;
                                HttpWebRequest callbackRequest = (HttpWebRequest)WebRequest.Create(handlerURL);
                                HttpWebResponse callbackResponse = (HttpWebResponse)callbackRequest .GetResponse();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogHandlerError(storeMasterDetail.StoreName, storeOrderDetails.SSLApiLinkId, ex.Message);
                        }
                        transaction.Commit();
                    }
                    else
                    {
                        transaction.Rollback();
                        extendSectigoACMESubscriptionResponseResponse.StatusCode = -1;
                        extendSectigoACMESubscriptionResponseResponse.ErrorDetail.ErrorMessage = "Your subscription was successfully extended, but the update could not be completed. Please contact Admin or Support for assistance.";
                        #region Enter Response In table 
                        using var errorDbContext = new SSLConfigurationEntities();
                        try
                        {
                            AcmeSubscriptionError objSectigoAcmeSubcription = new AcmeSubscriptionError()
                            {
                                OrderNumber = serverResponse.orderNumber.ToString(),
                                ExtensionDurationDays = serverResponse.extensionDurationDays,
                                StoreId = extendSectigoACMESubscriptionRequest.StoreId,
                                StoreOrderId = oldStoreOrderId,
                                RequestJson = JsonSerializer.Serialize(storeOrderDetails),
                                RetryCount = 0,
                                IsProcessed = false,
                                CreatedDate = DateTime.UtcNow,
                                ErrorMessage = result?.Message ?? "Unknown error"
                            };
                            errorDbContext.AcmeSubscriptionErrors.Add(objSectigoAcmeSubcription);
                            errorDbContext.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            var error = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                            throw new Exception("SectigoAcmeAfterSubcripton insert failed: " + error, ex);
                        }
                        #endregion
                        return extendSectigoACMESubscriptionResponseResponse;
                    }
                    #endregion
                }
                else
                {
                    string message = "Acme Renew Verising Api Failed.";
                    response = JsonSerializer.Serialize(serverResponse);
                    LogWriter.LogAcmeAPIRequest(extendSectigoACMESubscriptionRequest.SSLApiLinkId, "", "", response, message);
                }
            }
            catch (Exception ex)
            {
                extendSectigoACMESubscriptionResponseResponse.StatusCode = -1;
                extendSectigoACMESubscriptionResponseResponse.ErrorDetail.ErrorMessage = ex.Message;
            }
            return extendSectigoACMESubscriptionResponseResponse;
        }
        #endregion
    }
}
