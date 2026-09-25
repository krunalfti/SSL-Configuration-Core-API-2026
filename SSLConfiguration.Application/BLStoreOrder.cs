using Microsoft.Data.SqlClient;
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
                        acmeAccountID = saveStoreOrderDetailRequest.ApiOrderNo,
                        years = "1"
                    };
                    var serverResponse = VerisignGateway.AcemeAPIHelper.GetExtendedCertificateReponse(acmeServerRequest);
                    if (serverResponse.success)
                    {
                        response = JsonSerializer.Serialize(serverResponse);
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
                foreach (var storeOrderDetail in saveStoreOrderDetailRequest.StoreOrderDetails)
                {
                
                    var storeOrder = StoreOrderDataAccess.GetBySSLApiLinkIdAndStoreId(storeOrderDetail.SSLApiLinkId, saveStoreOrderDetailRequest.StoreId);

                    StoreOrder objStoreOrder = new StoreOrder()
                    {
                        StoreId = saveStoreOrderDetailRequest.StoreId,
                        SSLApiLinkId = storeOrderDetail.SSLApiLinkId,
                        ApiOrderNo = "AbcdNew001",
                        CompanyName = storeOrderDetail.CompanyName,
                        CreatedDate = DateTime.Now,
                        CredentialCode = storeOrderDetail.CredentialCode,
                        IsActive = true,
                        IsCancel = false,
                        IsUsed = false,
                        MinSAN = storeOrderDetail.MinSan,
                        Pin = Guid.NewGuid().ToString(),
                        ProductId = storeOrderDetail.ProductId,
                        ProductName = storeOrderDetail.ProductName,
                        San = storeOrderDetail.San,
                        SpecialNote = string.Empty,
                        UpdatedDate = DateTime.Now,
                        ValidTillDate = DateTime.Now.AddMonths(13),
                        LinkExpiredDate = DateTime.Now.AddMonths(13),
                        Year = storeOrderDetail.Year,
                        IsMultiDomain = storeOrderDetail.IsMultiDomain,
                        IsLock = false,
                        WildcardSAN = storeOrderDetail.WildcardSAN,
                        IsSubscription = storeOrderDetail.IsSubscription,
                        IsCAMYP = storeOrderDetail.IsCAMYP,
                        IsStoreMYP = storeOrderDetail.IsStoreMYP,
                        SubscriptionYear = storeOrderDetail.SubscriptionYear,
                        CAOrderValidFrom = storeOrderDetail.CAOrderValidFrom,
                        CAOrderValidTo = storeOrderDetail.CAOrderValidTo,
                        CAOrderValidity = storeOrderDetail.CAOrderValidity,
                        CodeSignProvisioningMethod = storeOrderDetail.CodeSignProvisioningMethod,
                        CodeSignShippingCode = storeOrderDetail.CodeSignShippingCode,
                        //StoreOrderValidFrom = DateTime.Now,
                        //StoreOrderValidTo = DateTime.Now.AddYears(storeOrderDetail.SubscriptionYear ?? 1),
                        RemainingValidity = storeOrderDetail.RemainingValidity
                    };

                    dbContext.StoreOrders.Add(objStoreOrder);
                    dbContext.SaveChanges();
                    // Get latest StoreOrder and update StoreOrderId
                    // in AcemeCertificateDetails and AdditionalDomains
                    int storeOrderId = storeOrder.StoreOrderId;
                    UpdateStoreOrderIdInRelatedTables(dbContext, storeOrderDetail.SSLApiLinkId, saveStoreOrderDetailRequest.StoreId , storeOrderId);
                    // add sslapiLinkId & Pin for response
                    sslConfiguratinLinks.Add(objStoreOrder.SSLApiLinkId, objStoreOrder.Pin);

                    UpdateStoreOrderProductDetails(dbContext, objStoreOrder.StoreOrderId); // Update StoreOrder - Cofiguration Settings
                }               
                transaction.Commit();
                saveStoreOrderDetailResponse.ConfigurationPinDetails = sslConfiguratinLinks;
                

                saveStoreOrderDetailResponse.StatusCode = 0;
            }
            catch (Exception ex)
            {
                saveStoreOrderDetailResponse.StatusCode = -1;
                saveStoreOrderDetailResponse.ErrorDetail.ErrorMessage = ex.Message;

                //LogWriter.LogErrorDetails(ex);
            }

            return saveStoreOrderDetailResponse;
        }
        public static void UpdateStoreOrderProductDetails(SSLConfigurationEntities dbContext,int storeOrderId)
        {
            if (storeOrderId != 0)
            {
                var storeOrder = StoreOrderDataAccess.GetByStoreId(dbContext, storeOrderId);
                if (storeOrder != null)
                {
                    var productDetail = BLGeneral.GetProductDetail(dbContext,storeOrder.ProductId);

                    if (productDetail != null)
                    {
                        storeOrder.IsWildcard = productDetail.IsWildcard;
                        storeOrder.AuthenticationType = productDetail.AuthenticationType;
                        storeOrder.IsMultiDomain = productDetail.IsMultiDomain;
                        storeOrder.IsWildcardMultiDomain = productDetail.IsWildcardMultiDomain;
                        storeOrder.IsFlex = productDetail.IsFlex;
                        storeOrder.IsCodeSign = productDetail.IsCodeSign;
                        storeOrder.IsPAC = productDetail.IsPAC;
                        storeOrder.UpdatedDate = DateTime.Now;
                        storeOrder.IsX9 = productDetail.IsX9;

                        StoreOrderDataAccess.UpdateStoreOrderRenew(dbContext,storeOrder);
                    }
                }
            }
        }
        public static void UpdateStoreOrderIdInRelatedTables(SSLConfigurationEntities dbContext, int sslApiLinkId, int storeId , int storeOrderId)
        {
            if (sslApiLinkId == 0 || storeId == 0)
                return;

            // Get the latest StoreOrder for this SSLApiLinkId + StoreId
            var latestStoreOrder = StoreOrderDataAccess.GetBySSLApiLinkIdAndStoreIdLatest(dbContext ,sslApiLinkId, storeId);

            if (latestStoreOrder == null)
                return;
            int latestStoreOrderId = latestStoreOrder.StoreOrderId;
            // Update ALL AcemeCertificateDetails records
            var acemeCertificateDetails = StoreOrderDataAccess.GetAcemeCertificateDetails(dbContext, storeOrderId);

            foreach (var acmeDetail in acemeCertificateDetails)
            {
                acmeDetail.StoreOrderId = latestStoreOrderId;
            }
            // Update ALL AdditionalDomains records            
            var additionalDomains = StoreOrderDataAccess.GetAdditionalDomains(dbContext, storeOrderId);
            foreach (var additionalDomain in additionalDomains)
            {
                additionalDomain.StoreOrderId = latestStoreOrderId;
            }
            dbContext.SaveChanges();
        }
    }
}
