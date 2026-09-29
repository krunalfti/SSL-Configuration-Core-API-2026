using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSLConfiguration.Contracts.SSLConfiguration_WebAPI;
using SSLConfiguration.Infrastructure.DataAccess;
using SSLConfiguration.Infrastructure.Persistence;
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
        public static SaveStoreOrderDetailResponse SaveStoreOrderDetails(SaveStoreOrderDetailRequest saveStoreOrderDetailRequest)
        {
            string response = "";
            SaveStoreOrderDetailResponse saveStoreOrderDetailResponse = new SaveStoreOrderDetailResponse();
            var sslConfiguratinLinks = new Dictionary<int, string>();

            try
            {
                using var dbContext = new SSLConfigurationEntities();
                using var transaction = dbContext.Database.BeginTransaction();
                //Call Verisgn API to get reponse of Extended certificate order and save the response in StoreOrder table
                try
                {
                    var caCredentialDetails = BLGeneral.GetCACredentials(saveStoreOrderDetailRequest.StoreOrderDetails[0].CredentialCode);
                    var username = caCredentialDetails?.UserName;
                    var password = caCredentialDetails?.Password;
                    
                    var acmeServerRequest = new VerisignGateway.ACMERenewBaseRequest
                    {
                        loginName = username,
                        loginPassword = password,
                        action = "EXTENDDOMAINS",
                        EABID = saveStoreOrderDetailRequest.ApiOrderNo,
                        QuoteOnly = "Y",
                        years = "1"
                    };
                    var serverResponse = VerisignGateway.AcemeAPIHelper.GetExtendedCertificateReponse(acmeServerRequest);
                    if (serverResponse.success)
                    {
                        response = JsonSerializer.Serialize(serverResponse);                        
                        #region SP CALL - SaveStoreOrderDetailsForRenew
                        var storeOrderDetails = saveStoreOrderDetailRequest.StoreOrderDetails[0];
                        var result = StoreOrderDataAccess.RenewAcmeStoreOrder(dbContext, saveStoreOrderDetailRequest.StoreId, saveStoreOrderDetailRequest.ApiOrderNo, storeOrderDetails);

                        if (result != null && result.Success)
                        {
                            int newStoreOrderId = result.StoreOrderId;
                            string newPin = result.Pin;
                            
                            sslConfiguratinLinks.Add(saveStoreOrderDetailRequest.StoreOrderDetails[0].SSLApiLinkId, newPin);
                            saveStoreOrderDetailResponse.ConfigurationPinDetails = sslConfiguratinLinks;
                            saveStoreOrderDetailResponse.StatusCode = 0;                            
                        }
                        #endregion
                    }
                    else
                    {
                        string message = "Acme Renew Verising Api Failed.";
                        response = JsonSerializer.Serialize(serverResponse);
                        LogWriter.LogAcmeAPIRequest(saveStoreOrderDetailRequest.SSLApiLinkId, "", "", response, message);
                    }
                }
                catch (Exception ex)
                {
                    
                }                               
                transaction.Commit();
                
            }
            catch (Exception ex)
            {
                saveStoreOrderDetailResponse.StatusCode = -1;
                saveStoreOrderDetailResponse.ErrorDetail.ErrorMessage = ex.Message;

                //LogWriter.LogErrorDetails(ex);
            }

            return saveStoreOrderDetailResponse;
        }        
    }
}
