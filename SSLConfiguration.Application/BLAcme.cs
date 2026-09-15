using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SSLConfiguration.Contracts.SSLConfiguration.ACME;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.DataAccess;
using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Ported from old SSLConfiguration_BusinessObjects.BLAcme (ACME account/domain helpers).
    /// UnitOfWork/TransactionScope → EF Core SSLConfigurationEntities.
    /// </summary>
    public static class BLAcme
    {
        /// <summary>Same as old enmStoreName.SSL2BUY.</summary>
        private const int Ssl2BuyStoreId = 2;

        public static AcemeCertificateDetail GetAcemeAccountDetails(int StoreOrderId)
        {
            AcemeCertificateDetail objresponse = new AcemeCertificateDetail();
            try
            {
                AcemeCertificateDetail? storeMasterDetail = AcemeCertificateDetailDataAccess.GetFirstByStoreOrderId(StoreOrderId);

                if (storeMasterDetail != null)
                {
                    objresponse.AcmeAccountID = storeMasterDetail.AcmeAccountID;
                }

                return objresponse;
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                throw;
            }
        }

        public static AcemeCertificateDetail? GetAcemeDetail(int StoreOrderId)
        {
            return AcemeCertificateDetailDataAccess.GetByStoreOrderId(StoreOrderId);
        }

        public static void SaveAcemeAccountDetailsInDB(PF_Request objPFRequest, ACMEDetailViewModel objACMEDetailResponse)
        {
            using var dbContext = new SSLConfigurationEntities();
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                var repoAcemeDetails = new AcemeCertificateDetail
                {
                    AcmeAccountID = objACMEDetailResponse.EabId,
                    StoreOrderId = objPFRequest.StoreOrderDetail!.StoreOrderId,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now,
                    AuthenticationType = objPFRequest.StoreOrderDetail.AuthenticationType,
                    ServerUrl = objACMEDetailResponse.ServerUrl
                };

                dbContext.AcemeCertificateDetails.Add(repoAcemeDetails);
                dbContext.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        #region SAN / WildCard Max Count Used and Remaining

        public static int GetWildcardSANbyStore(int storeOrderId, int productId)
        {
            try
            {
                StoreOrder? sslApiLink = StoreOrderDataAccess.GetById(storeOrderId);

                if (sslApiLink != null)
                {
                    return Convert.ToInt32(sslApiLink.WildcardSAN);
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        public static int GetSANbyStore(int storeOrderId, int productId)
        {
            try
            {
                int san = 0;
                int addDomain = 0;

                StoreOrder? sslApiLink = StoreOrderDataAccess.GetById(storeOrderId);

                if (sslApiLink != null)
                {
                    san = Convert.ToInt32(sslApiLink.San);
                    addDomain = san;
                }

                return addDomain;
            }
            catch
            {
                return 0;
            }
        }

        public static int GetMaxWildcardAllowedSAN(int storeOrderId)
        {
            int maxDomain = 0;
            StoreOrder? objSSLApiLink = StoreOrderDataAccess.GetById(storeOrderId);
            if (objSSLApiLink == null)
                return 0;

            maxDomain = Convert.ToInt32(objSSLApiLink.WildcardSAN);

            List<AdditionalDomain> additionalDomains = AdditionalDomainDataAccess.GetWildcardSansWithNameByStoreOrderId(storeOrderId);

            if (additionalDomains.Count > 0)
                maxDomain = (maxDomain - additionalDomains.Count);

            return maxDomain;
        }

        public static int GetMaxAllowedSAN(int storeOrderId, bool IsWildcardMultiDomain)
        {
            int maxDomain = 0;
            StoreOrder? objSSLApiLink = StoreOrderDataAccess.GetById(storeOrderId);
            if (objSSLApiLink == null)
                return 0;

            maxDomain = Convert.ToInt32(objSSLApiLink.San);

            List<AdditionalDomain> additionalDomains = IsWildcardMultiDomain
                ? AdditionalDomainDataAccess.GetWildcardSansWithNameByStoreOrderId(storeOrderId)
                : AdditionalDomainDataAccess.GetNonWildcardSansWithNameByStoreOrderId(storeOrderId);

            if (additionalDomains.Count > 0)
                maxDomain = (maxDomain - additionalDomains.Count);

            return maxDomain;
        }

        public static int GetWildcardAllowedSANCount(int storeOrderId)
        {
            int maxDomain = 0;
            StoreOrder? objSSLApiLink = StoreOrderDataAccess.GetById(storeOrderId);
            if (objSSLApiLink == null)
                return 0;

            maxDomain = Convert.ToInt32(objSSLApiLink.WildcardSAN);

            List<AdditionalDomain> additionalDomains = AdditionalDomainDataAccess.GetWildcardSansWithNameByStoreOrderId(storeOrderId);

            maxDomain = additionalDomains.Count;

            return maxDomain;
        }

        public static int GetSANAllowedCount(int storeOrderId)
        {
            int maxDomain = 0;
            StoreOrder? objSSLApiLink = StoreOrderDataAccess.GetById(storeOrderId);
            if (objSSLApiLink == null)
                return 0;

            maxDomain = Convert.ToInt32(objSSLApiLink.San);

            List<AdditionalDomain> additionalDomains = AdditionalDomainDataAccess.GetNonWildcardSansWithNameByStoreOrderId(storeOrderId);

            maxDomain = additionalDomains.Count;

            return maxDomain;
        }

        #endregion

        public static void SaveAcemeDomainDetailsInDB(PF_Request objPFRequest, ACMEDetailViewModel objACMEDetailResponse)
        {
            var domains = objACMEDetailResponse.Domains;
            if (domains == null)
                return;

            List<string> domainNames = domains.Select(d => d.domainName ?? string.Empty).ToList();

            using var dbContext = new SSLConfigurationEntities();
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                foreach (var item in domains)
                {
                    DateTime createdDate = objACMEDetailResponse.SubscriptionStartDate ?? DateTime.Now;
                    DateTime updatedDate = objACMEDetailResponse.SubscriptionEndDate ?? DateTime.Now;

                    var objAdditionalDomain = new AdditionalDomain
                    {
                        ApprovalEmail = string.Empty,
                        DomainName = item.domainName,
                        StoreOrderId = objPFRequest.StoreOrderDetail!.StoreOrderId,
                        IsPrimaryDomain = false,
                        SpecialNote = string.Empty,
                        CreatedDate = createdDate,
                        UpdatedDate = updatedDate,
                        IsRemoveFromApi = false,
                        AcmeOrderNo = Convert.ToString(objACMEDetailResponse.AcmeOrderNo),
                        AcmeOrgId = objACMEDetailResponse.SelectedOrgID,
                        AcmeOrgName = objACMEDetailResponse.OrgName,
                        IsConsiderAsSAN = DetermineIsConsiderAsSAN(item.domainName ?? string.Empty, domainNames)
                    };

                    var objacmeDomainHistory = new AcmeDomainHistory
                    {
                        StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                        DomainName = item.domainName,
                        Action = "Added",
                        Remark = "Added by Client",
                        DateAdded = DateTime.Now,
                        AcmeOrderNo = Convert.ToString(objACMEDetailResponse.AcmeOrderNo)
                    };

                    dbContext.AdditionalDomains.Add(objAdditionalDomain);
                    dbContext.AcmeDomainHistories.Add(objacmeDomainHistory);
                }

                dbContext.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static bool DetermineIsConsiderAsSAN(string domainName, List<string> allDomains)
        {
            if (domainName.StartsWith("*."))
                return true;

            if (domainName.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                string baseDomain = domainName.Substring(4);
                if (allDomains.Any(d => d.Equals(baseDomain, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            string wildcardVersion = "*." + domainName;
            if (allDomains.Contains(wildcardVersion, StringComparer.OrdinalIgnoreCase))
                return false;

            string wwwVersion = "www." + domainName;
            if (allDomains.Contains(wwwVersion, StringComparer.OrdinalIgnoreCase))
                return true;

            var parts = domainName.Split('.');
            if (parts.Length == 2)
                return true;

            return true;
        }

        /// <summary>
        /// Same as old UpdateAPIOrderNoInStoreOrder — no HttpContext Session; LanguageCode defaults to "en".
        /// </summary>
        public static void UpdateAPIOrderNoInStoreOrder(int storeOrderId, string AcmeAccountID, string? languageCode = null)
        {
            try
            {
                StoreOrder? storeOrder = StoreOrderDataAccess.GetById(storeOrderId);

                if (storeOrder != null)
                {
                    storeOrder.ApiOrderNo = AcmeAccountID;
                    storeOrder.LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode;
                    StoreOrderDataAccess.Update(storeOrder);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>ACME version of SaveComodoOrderInDB (RenewalOrderDetail + callback).</summary>
        public static void SaveComodoOrderInDB(PF_Request objPFRequest, ACMEDetailViewModel viewModel)
        {
            UpdateAPIOrderNoInStoreOrder(
                objPFRequest.StoreOrderDetail!.StoreOrderId,
                viewModel.EabId ?? string.Empty,
                objPFRequest.LanguageCode);

            using var dbContext = new SSLConfigurationEntities();
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                RenewalOrderDetail? existingDetail = dbContext.RenewalOrderDetails
                    .FirstOrDefault(r =>
                        r.ProductId == objPFRequest.StoreOrderDetail.ProductId
                        && r.SSLApiLinkId == objPFRequest.StoreOrderDetail.SSLApiLinkId);

                if (existingDetail != null)
                {
                    existingDetail.OrderStatus = viewModel.OrderStatus;
                    dbContext.RenewalOrderDetails.Update(existingDetail);
                }
                else
                {
                    dbContext.RenewalOrderDetails.Add(new RenewalOrderDetail
                    {
                        ApiOrderNo = viewModel.EabId,
                        OrderStatus = viewModel.OrderStatus,
                        CompanyName = objPFRequest.StoreOrderDetail.CompanyName,
                        ProductId = objPFRequest.StoreOrderDetail.ProductId,
                        SSLApiLinkId = objPFRequest.StoreOrderDetail.SSLApiLinkId,
                        DomainName = null
                    });
                }

                dbContext.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            StoreMaster? storeMasterDetail = StoreMasterDataAccess.GetById(objPFRequest.StoreOrderDetail.StoreId);
            try
            {
                if (storeMasterDetail != null)
                {
                    var handlerURL = storeMasterDetail.CallBackURL;
                    handlerURL += "?sslapiLinkId=" + objPFRequest.StoreOrderDetail.SSLApiLinkId;
                    handlerURL += "&apiOrderNo=" + viewModel.EabId;
                    handlerURL += "&StartDate=" + (viewModel.SubscriptionStartDate.HasValue ? viewModel.SubscriptionStartDate.Value.ToString("yyyy-MM-ddTHH:mm:ssZ") : "");
                    handlerURL += "&EndDate=" + (viewModel.SubscriptionEndDate.HasValue ? viewModel.SubscriptionEndDate.Value.ToString("yyyy-MM-ddTHH:mm:ssZ") : "");
                    handlerURL += "&OrderStatus=" + viewModel.OrderStatus;
                    handlerURL += "&ProductId=" + objPFRequest.StoreOrderDetail.ProductId;
                    handlerURL += "&IsAcmeConfig=" + true;

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(handlerURL!);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    response.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogHandlerError(storeMasterDetail?.StoreName, objPFRequest.StoreOrderDetail.SSLApiLinkId, ex.Message);
            }
        }

        public static int GetAdditionalDomainCount(int StoreOrderId)
        {
            int Domaincount = 0;

            List<AdditionalDomain> additionalDomains = AdditionalDomainDataAccess.GetWildcardSansByStoreOrderId(StoreOrderId);

            if (additionalDomains.Count > 0)
            {
                Domaincount = 1;
            }
            else
            {
                Domaincount = 0;
            }
            return Domaincount;
        }

        public static int GetCountByDomainWithHoldorNot(int StoreOrderId, string domainname)
        {
            return AdditionalDomainDataAccess.GetCountByDomainWithHoldorNot(StoreOrderId, domainname);
        }

        public static void SaveAcemeOVDomainDetailsInDB(PF_Request objPFRequest, ACMEDetailViewModel objACMEDetailResponse)
        {
            var domains = objACMEDetailResponse.AcmeOvDomains;
            if (domains == null)
                return;

            List<string> domainNames = domains.Select(d => d.domainName ?? string.Empty).ToList();

            using var dbContext = new SSLConfigurationEntities();
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                foreach (var item in domains)
                {
                    DateTime createdDate = objACMEDetailResponse.SubscriptionStartDate ?? DateTime.Now;
                    DateTime updatedDate = objACMEDetailResponse.SubscriptionEndDate ?? DateTime.Now;

                    var objAdditionalDomain = new AdditionalDomain
                    {
                        ApprovalEmail = string.Empty,
                        DomainName = item.domainName,
                        StoreOrderId = objPFRequest.StoreOrderDetail!.StoreOrderId,
                        IsPrimaryDomain = false,
                        SpecialNote = string.Empty,
                        CreatedDate = createdDate,
                        UpdatedDate = updatedDate,
                        IsRemoveFromApi = false,
                        AcmeOrderNo = Convert.ToString(objACMEDetailResponse.AcmeOrderNo),
                        AcmeOrgId = objACMEDetailResponse.SelectedOrgID,
                        AcmeOrgName = objACMEDetailResponse.OrgName,
                        IsConsiderAsSAN = DetermineIsConsiderAsSAN(item.domainName ?? string.Empty, domainNames)
                    };

                    var objacmeDomainHistory = new AcmeDomainHistory
                    {
                        StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                        DomainName = item.domainName,
                        Action = "Added",
                        Remark = "Added by Client",
                        DateAdded = DateTime.Now,
                        AcmeOrderNo = Convert.ToString(objACMEDetailResponse.AcmeOrderNo),
                        AcmeOrgId = objACMEDetailResponse.SelectedOrgID,
                        AcmeOrgName = objACMEDetailResponse.OrgName
                    };

                    dbContext.AdditionalDomains.Add(objAdditionalDomain);
                    dbContext.AcmeDomainHistories.Add(objacmeDomainHistory);
                }

                dbContext.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static AcmeOVOrganisationResponse GetOrganisationDetails(string pin, int storeId)
        {
            AcmeOVOrganisationResponse getAcmeOVOrganisationResponse = new AcmeOVOrganisationResponse();
            string? authKey = AppConfig.SSL2BuyWebApiAuthKey;
            string? apiUrl = storeId == Ssl2BuyStoreId ? AppConfig.AcmeSSL2BuyApiUrl : AppConfig.CheapSSLShopApiUrl;

            AcmeOVOrganisationRequest request = new AcmeOVOrganisationRequest
            {
                Pin = pin,
                AuthKey = authKey
            };

            try
            {
                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    getAcmeOVOrganisationResponse.StatusCode = -1;
                    getAcmeOVOrganisationResponse.ACMEOrganisationList = null;
                    return getAcmeOVOrganisationResponse;
                }

                using var client = new HttpClient();
                client.BaseAddress = new Uri(apiUrl);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                string endpoint = storeId == Ssl2BuyStoreId ? "user/GetACMEOrgUserDetails" : "acme/GetACMEOrganisationByUser";

                using var response = client.PostAsJsonAsync(endpoint, request).Result;
                if (response.IsSuccessStatusCode)
                {
                    var result = response.Content.ReadFromJsonAsync<AcmeOVOrganisationResponse>().Result;
                    return result ?? getAcmeOVOrganisationResponse;
                }

                getAcmeOVOrganisationResponse.StatusCode = -1;
                getAcmeOVOrganisationResponse.ACMEOrganisationList = null;
                return getAcmeOVOrganisationResponse;
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                throw;
            }
        }
    }
}
