using System.Net;
using System.Text.Json;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.DataAccess;
using SSLConfiguration.Infrastructure.Persistence;
using VerisignGateway;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same as old BLComodo PlaceOrder helpers (GetComodoOrderRequestObject / SaveComodoOrderInDB).
    /// </summary>
    public static class BLComodo
    {
        public static ComodoOrderRequest GetComodoOrderRequestObject(PF_Request objPFRequest)
        {
            ComodoOrderRequest objReturn = new ComodoOrderRequest();
            var detail = objPFRequest.ComodoOrderRequest.ComodoOrderDetailRow;

            objReturn.ApproverEmail = detail.ApprovalEmail;
            objReturn.City = detail.OrgCity;
            objReturn.Country = detail.OrgCountry;
            objReturn.CSR = objPFRequest.CSR;
            objReturn.DomainNames = objPFRequest.CSRDetailRow?.DomainName;
            objReturn.NoOfServer = 1;
            objReturn.OrgEmail = detail.OrgEmail;

            if (objPFRequest.ProductDetail?.AuthenticationType == ConstantUtil.AuthenticationType_OV
                || objPFRequest.ProductDetail?.AuthenticationType == ConstantUtil.AuthenticationType_EV)
                objReturn.OrgName = detail.OrgName;
            else
                objReturn.OrgName = objPFRequest.ComodoOrderRequest.FirstName + " " + objPFRequest.ComodoOrderRequest.LastName;

            objReturn.PartnerOrderID = objPFRequest.StoreOrderDetail!.StoreOrderId.ToString();
            objReturn.ProductCode = objPFRequest.StoreOrderDetail.ProductId;
            objReturn.State = detail.OrgState;
            objReturn.StreetAddress1 = detail.OrgAddress1;
            objReturn.StreetAddress2 = detail.OrgAddress2;
            objReturn.StreetAddress3 = detail.OrgAddress3;
            objReturn.PostalCode = detail.OrgPostalCode;
            objReturn.WebServer = objPFRequest.ComodoOrderRequest.WebServerCode.ToString();
            objReturn.Years = objPFRequest.StoreOrderDetail.Year;
            objReturn.DUNS = detail.OrgDuns;
            objReturn.CompanyRegistrationNumber = detail.OrgCompanyRegNumber;

            if (objPFRequest.ComodoOrderRequest.isCSRIncludeSAN)
                objReturn.DomainNames = "CSR_SAN";

            objReturn.ComodoCredential = objPFRequest.CACredentialDetails?.GetComodoCACredential();
            objReturn.UniqueString = objPFRequest.ComodoOrderRequest.UniqueString;

            return objReturn;
        }

        public static void ApplySanList(ComodoOrderRequest request, PF_Request pf)
        {
            if (!Convert.ToBoolean(pf.StoreOrderDetail?.IsMultiDomain) && pf.ProductDetail?.IsMultiDomain != true)
                return;

            if (!string.IsNullOrEmpty(pf.ComodoOrderRequest.ComodoOrderDetailRow.ApprovalEmail)
                && !pf.ComodoOrderRequest.ComodoOrderDetailRow.ApprovalEmail.Contains("@"))
            {
                request.ApproverEmail = string.Empty;
            }

            var sanApprovalList = new Dictionary<string, string>();
            if (pf.ComodoOrderRequest.SAN_ApprovalEmail != null)
            {
                foreach (var dic in pf.ComodoOrderRequest.SAN_ApprovalEmail)
                    sanApprovalList.AddUniqueKey(dic.Key, dic.Value);
            }

            if (pf.ProductDetail?.IsFlex == true && pf.ComodoOrderRequest.WildcardSAN_ApprovalEmail != null)
            {
                foreach (var dic in pf.ComodoOrderRequest.WildcardSAN_ApprovalEmail)
                    sanApprovalList.AddUniqueKey(dic.Key, dic.Value);
            }

            request.SANListAndEmail = sanApprovalList;
            request.PrimayDomainName = pf.ComodoOrderRequest.PrimaryDomain;
        }

        public static void ApplyEvAndJurisdiction(ComodoOrderRequest request, PF_Request pf)
        {
            if (pf.ProductDetail?.AuthenticationType != ConstantUtil.AuthenticationType_EV
                && !BLGeneral.IsEVProduct_StoreOrder(pf.StoreOrderDetail!.StoreOrderId))
                return;

            var detail = pf.ComodoOrderRequest.ComodoOrderDetailRow;
            request.JurisdictionCity = string.IsNullOrEmpty(detail.JurictionCity) ? string.Empty : detail.JurictionCity;
            request.JurisdictionCountry = string.IsNullOrEmpty(detail.JurictionCountry) ? string.Empty : detail.JurictionCountry;
            request.JurisdictionState = string.IsNullOrEmpty(detail.JurictionState) ? string.Empty : detail.JurictionState;

            if (detail.DateOfIncorporation.HasValue)
                request.DateOfIncorporation = detail.DateOfIncorporation.Value;

            request.DoingBusinessAsName = string.IsNullOrEmpty(detail.DoingBusinessAs) ? string.Empty : detail.DoingBusinessAs;
            request.EVIncorporationAgency = detail.InCorporationAgency;
            request.EVIncorporationPhone = detail.InCorporationPhoneNo;

            request.EVCertificateRequestor = ToEvContact(pf.ComodoOrderRequest.CertificateRequestorInfo);
            request.EVCertificateApprover = ToEvContact(pf.ComodoOrderRequest.CertificateApproverInfo);
            request.EVCertificateSigner = ToEvContact(pf.ComodoOrderRequest.ContractSignerInfo);
        }

        public static void SetErrorFromComodo(PF_Response objPFResponse, OrderResponse objComodoOrderResponse)
        {
            if (objComodoOrderResponse.error == null)
                return;

            objPFResponse.ErrorCode = objComodoOrderResponse.error.ErrorCode;
            objPFResponse.ErrorFiled = objComodoOrderResponse.error.ErrorField;
            objPFResponse.ErrorMessage = objComodoOrderResponse.error.ErrorMessage;
        }

        public static void SaveComodoOrderInDB(PF_Request objPFRequest, OrderResponse objComodoOrderResponse)
        {
            BLStoreOrder.UpdateAPIOrderNoInStoreOrder(
                objPFRequest.StoreOrderDetail!.StoreOrderId,
                objComodoOrderResponse.OrderNumber,
                objPFRequest.LanguageCode);

            using var dbContext = new SSLConfigurationEntities();
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                int CSRDetailsID = 0;
                int RequestorID = 0;
                int ApproverID = 0;
                int ContractSingerID = 0;

                if (objPFRequest.CSRDetailRow != null)
                {
                    var objcsrdetail = CloneCsr(objPFRequest.CSRDetailRow);
                    dbContext.CSRDetails.Add(objcsrdetail);
                    dbContext.SaveChanges();
                    CSRDetailsID = objcsrdetail.CSRDetailId;
                }

                if (BLGeneral.IsEVProduct_StoreOrder(objPFRequest.StoreOrderDetail.StoreOrderId))
                {
                    RequestorID = SaveContact(dbContext, objPFRequest.ComodoOrderRequest.CertificateRequestorInfo);
                    ApproverID = SaveContact(dbContext, objPFRequest.ComodoOrderRequest.CertificateApproverInfo);
                    ContractSingerID = SaveContact(dbContext, objPFRequest.ComodoOrderRequest.ContractSignerInfo);
                }

                if (Convert.ToBoolean(objPFRequest.StoreOrderDetail.IsMultiDomain)
                    && objPFRequest.ComodoOrderRequest.SAN_ApprovalEmail != null)
                {
                    string? primary = objPFRequest.ComodoOrderRequest.PrimaryDomain;
                    if (!string.IsNullOrEmpty(primary))
                        objPFRequest.ComodoOrderRequest.SAN_ApprovalEmail.AddUniqueKey(
                            primary, objPFRequest.ComodoOrderRequest.PrimaryDomainEmail ?? string.Empty);

                    foreach (var existing in dbContext.AdditionalDomains
                                 .Where(d => d.StoreOrderId == objPFRequest.StoreOrderDetail.StoreOrderId)
                                 .ToList())
                        dbContext.AdditionalDomains.Remove(existing);
                    dbContext.SaveChanges();

                    foreach (var item in objPFRequest.ComodoOrderRequest.SAN_ApprovalEmail)
                    {
                        dbContext.AdditionalDomains.Add(BuildAdditionalDomain(
                            objPFRequest.StoreOrderDetail.StoreOrderId,
                            item.Key,
                            item.Value,
                            primary));
                    }

                    if (objPFRequest.ProductDetail?.IsFlex == true
                        && objPFRequest.ComodoOrderRequest.WildcardSAN_ApprovalEmail != null)
                    {
                        foreach (var item in objPFRequest.ComodoOrderRequest.WildcardSAN_ApprovalEmail)
                        {
                            dbContext.AdditionalDomains.Add(BuildAdditionalDomain(
                                objPFRequest.StoreOrderDetail.StoreOrderId,
                                item.Key,
                                item.Value,
                                primary));
                        }
                    }

                    dbContext.SaveChanges();
                }

                var detail = objPFRequest.ComodoOrderRequest.ComodoOrderDetailRow;
                var objComodoOrderDetail = new ComodoOrderDetail
                {
                    StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                    CSRDetailID = CSRDetailsID,
                    WebServer = detail.WebServer,
                    FirstName = objPFRequest.ComodoOrderRequest.FirstName,
                    LastName = objPFRequest.ComodoOrderRequest.LastName,
                    OrgName = objPFRequest.ProductDetail?.AuthenticationType == ConstantUtil.AuthenticationType_OV
                        || objPFRequest.ProductDetail?.AuthenticationType == ConstantUtil.AuthenticationType_EV
                            ? detail.OrgName
                            : objPFRequest.ComodoOrderRequest.FirstName + " " + objPFRequest.ComodoOrderRequest.LastName,
                    OrgAddress1 = detail.OrgAddress1,
                    OrgAddress2 = detail.OrgAddress2,
                    OrgAddress3 = detail.OrgAddress3,
                    OrgCity = detail.OrgCity,
                    OrgState = detail.OrgState,
                    OrgCountry = detail.OrgCountry,
                    OrgEmail = detail.OrgEmail,
                    OrgPhone = detail.OrgPhone,
                    OrgPostalCode = string.IsNullOrEmpty(detail.OrgPostalCode) ? "" : detail.OrgPostalCode,
                    OrgFax = detail.OrgFax,
                    ApprovalEmail = detail.ApprovalEmail,
                    DVMethod = string.IsNullOrEmpty(objPFRequest.ComodoOrderRequest.ApprovalMethod)
                        ? (string.IsNullOrEmpty(objPFRequest.ComodoOrderRequest.PrimaryDomainEmail)
                            || objPFRequest.ComodoOrderRequest.PrimaryDomainEmail.Contains("@")
                                ? "EMAIL"
                                : objPFRequest.ComodoOrderRequest.PrimaryDomainEmail)
                        : objPFRequest.ComodoOrderRequest.ApprovalMethod,
                    JurictionCity = string.IsNullOrEmpty(detail.JurictionCity) ? "" : detail.JurictionCity,
                    JurictionCountry = string.IsNullOrEmpty(detail.JurictionCountry) ? "" : detail.JurictionCountry,
                    JurictionState = string.IsNullOrEmpty(detail.JurictionState) ? "" : detail.JurictionState,
                    DateOfIncorporation = detail.DateOfIncorporation,
                    DoingBusinessAs = detail.DoingBusinessAs,
                    CSR_MD5 = detail.CSR_MD5,
                    CSR_SHA1 = detail.CSR_SHA1,
                    InCorporationAgency = detail.InCorporationAgency,
                    InCorporationPhoneNo = detail.InCorporationPhoneNo,
                    RequestorId = RequestorID,
                    ApproverId = ApproverID,
                    ContractSignerId = ContractSingerID,
                    OrgDuns = string.IsNullOrEmpty(detail.OrgDuns) ? string.Empty : detail.OrgDuns,
                    OrgCompanyRegNumber = string.IsNullOrEmpty(detail.OrgCompanyRegNumber) ? string.Empty : detail.OrgCompanyRegNumber,
                    UniqueValue = objComodoOrderResponse.ComodoUniqueValue,
                    AgreementEmail = string.IsNullOrEmpty(detail.OrgEmail) ? string.Empty : detail.OrgEmail,
                    DNSTXTValue = objComodoOrderResponse.DNSTXTValue
                };

                dbContext.ComodoOrderDetails.Add(objComodoOrderDetail);

                dbContext.ChildCertificateDetails.Add(new ChildCertificateDetail
                {
                    StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                    SSLCertificateId = objComodoOrderResponse.ComodoCertficateID,
                    IsOriginal = true,
                    CertAction = "ISSUED",
                    DateAdded = DateTime.Now,
                    IsFetchStatus = true
                });

                dbContext.RenewalOrderDetails.Add(new RenewalOrderDetail
                {
                    ApiOrderNo = objComodoOrderResponse.OrderNumber,
                    OrderStatus = "INPROCESS",
                    CompanyName = objPFRequest.StoreOrderDetail.CompanyName,
                    ProductId = objPFRequest.StoreOrderDetail.ProductId,
                    SSLApiLinkId = objPFRequest.StoreOrderDetail.SSLApiLinkId,
                    DomainName = objPFRequest.CSRDetailRow?.DomainName,
                    NewRenewalOrderID = null
                });

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
                if (storeMasterDetail != null && !string.IsNullOrWhiteSpace(storeMasterDetail.CallBackURL))
                {
                    var handlerURL = storeMasterDetail.CallBackURL;
                    handlerURL += "?sslapiLinkId=" + objPFRequest.StoreOrderDetail.SSLApiLinkId;
                    handlerURL += "&apiOrderNo=" + objComodoOrderResponse.OrderNumber;
                    handlerURL += "&OrderStatus=INPROCESS";
                    handlerURL += "&ProductId=" + objPFRequest.StoreOrderDetail.ProductId;
                    handlerURL += "&CompanyName=" + objPFRequest.StoreOrderDetail.CompanyName;
                    handlerURL += "&DomainName=" + objPFRequest.CSRDetailRow?.DomainName;
                    handlerURL += "&IsAutoConfig=" + objPFRequest.CSRDetailRow?.IsCSRSaved;

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(handlerURL);
                    using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogHandlerError(storeMasterDetail?.StoreName, objPFRequest.StoreOrderDetail.SSLApiLinkId, ex.Message);
            }
        }

        private static ComodoEVContactDetail ToEvContact(ComodoContactInfoDraft src) =>
            new ComodoEVContactDetail
            {
                Address1 = src.Address1,
                Address2 = src.Address2,
                City = src.City,
                Country = src.Country,
                Email = src.Email,
                FirstName = src.FirstName,
                LastName = src.LastName,
                Phone = src.Phone,
                PostalCode = src.PostalCode,
                RelationWithIncorporation = src.Relationship,
                State = src.State,
                Title = src.Title
            };

        private static int SaveContact(SSLConfigurationEntities dbContext, ComodoContactInfoDraft src)
        {
            var entity = new ComodoContactInfo
            {
                Address1 = string.IsNullOrEmpty(src.Address1) ? string.Empty : src.Address1,
                Address2 = string.IsNullOrEmpty(src.Address2) ? string.Empty : src.Address2,
                City = src.City,
                Country = src.Country,
                Email = src.Email,
                FirstName = src.FirstName,
                LastName = src.LastName,
                Phone = src.Phone,
                PostalCode = src.PostalCode,
                Relationship = src.Relationship,
                State = src.State,
                Title = src.Title
            };
            dbContext.ComodoContactInfos.Add(entity);
            dbContext.SaveChanges();
            return entity.ComodoContactInfoId;
        }

        private static AdditionalDomain BuildAdditionalDomain(int storeOrderId, string domain, string approvalEmail, string? primaryDomain) =>
            new AdditionalDomain
            {
                ApprovalEmail = Convert.ToString(approvalEmail),
                DomainName = Convert.ToString(domain),
                StoreOrderId = storeOrderId,
                IsConsiderAsSAN = !string.Equals(primaryDomain, domain, StringComparison.OrdinalIgnoreCase),
                IsPrimaryDomain = string.Equals(primaryDomain, domain, StringComparison.OrdinalIgnoreCase),
                SpecialNote = string.Empty,
                CreatedDate = DateTime.Now,
                UpdatedDate = DateTime.Now
            };

        private static CSRDetail CloneCsr(CSRDetail src) => new CSRDetail
        {
            DomainName = src.DomainName,
            Country = src.Country,
            Locality = src.Locality,
            Organisation = src.Organisation,
            OrganisationUnit = src.OrganisationUnit,
            State = src.State,
            Email = src.Email,
            CSR = src.CSR,
            IsCSRSaved = src.IsCSRSaved,
            DNSNames = src.DNSNames
        };

        public static ComodoOrderRequest GetComodoOrderRequestForCodeSign(PF_Request objPFRequest)
        {
            ComodoOrderRequest objReturn = new ComodoOrderRequest();
            var cs = objPFRequest.ComodoOrderRequest.ComodoCodeSignOrderInfo;

            objReturn.CSR = Convert.ToString(objPFRequest.CSR);
            objReturn.ProductCode = objPFRequest.StoreOrderDetail!.ProductId;
            objReturn.Years = objPFRequest.StoreOrderDetail.Year;
            objReturn.PartnerOrderID = objPFRequest.StoreOrderDetail.StoreOrderId.ToString();

            objReturn.OrgName = cs.OrganizationName;
            objReturn.OrgEmail = cs.AdminEmail;
            objReturn.City = cs.City;
            objReturn.Country = cs.CountryName;
            objReturn.State = cs.State;
            objReturn.StreetAddress1 = cs.OrganizationAddress1;
            objReturn.StreetAddress2 = cs.OrganizationAddress2;
            objReturn.StreetAddress3 = cs.OrganizationAddress3;
            objReturn.PostalCode = cs.PostalCode;
            objReturn.PhoneNumber = cs.PhoneNo;

            objReturn.PACOrderDetail = new ComdooPACOrderDetail
            {
                Title = cs.AdminTitle,
                Email = cs.AdminEmail,
                FirstName = cs.AdminFirstName,
                LastName = cs.AdminLastName,
                PACUser = cs.AdminUserName,
                PACPassword = cs.AdminPassword
            };

            objReturn.CodeSignPublisherEmail = cs.CodeSignPublisherEmail;
            objReturn.CodeSignHSMType = cs.CodeSignHSMType;
            if (cs.CodeSignHSMType == "MARVELL_GOOGLE")
                objReturn.CodeSignKeyAttestation = Uri.EscapeDataString(cs.CodeSignKeyAttestation ?? string.Empty);
            else
                objReturn.CodeSignKeyAttestation = cs.CodeSignKeyAttestation;

            objReturn.IsSmartCardToken = (objPFRequest.StoreOrderDetail.CodeSignProvisioningMethod ?? string.Empty).ToUpper() != "HSM";
            objReturn.CodeSignShippingMethod = objPFRequest.StoreOrderDetail.CodeSignShippingCode;

            if (objReturn.IsSmartCardToken)
            {
                objReturn.ShippingForename = cs.ShippingForename;
                objReturn.ShippingSurname = cs.ShippingSurname;
                objReturn.ShippingStreetAddress1 = cs.ShippingStreetAddress1;
                objReturn.ShippingStreetAddress2 = cs.ShippingStreetAddress2;
                objReturn.ShippingStreetAddress3 = cs.ShippingStreetAddress3;
                objReturn.ShippingLocalityName = cs.ShippingCity;
                objReturn.ShippingStateOrProvinceName = cs.ShippingState;
                objReturn.ShippingCountryName = cs.ShippingCountryCode;
                objReturn.ShippingPostalCode = cs.ShippingPostalCode;
                objReturn.ShippingEmailAddress = cs.ShippingEmailAddress;
                objReturn.ShippingTelephone = cs.ShippingPhoneNo;
            }

            objReturn.ComodoCredential = objPFRequest.CACredentialDetails?.GetComodoCACredential();
            return objReturn;
        }

        public static void SaveComodoCodeSignOrderInDB(PF_Request objPFRequest, OrderResponse objComodoOrderResponse)
        {
            BLStoreOrder.UpdateAPIOrderNoInStoreOrder(
                objPFRequest.StoreOrderDetail!.StoreOrderId,
                objComodoOrderResponse.OrderNumber,
                objPFRequest.LanguageCode);

            using var dbContext = new SSLConfigurationEntities();
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                string domainName = string.Empty;
                CSRDetail objcsrdetail;
                var cs = objPFRequest.ComodoOrderRequest.ComodoCodeSignOrderInfo;
                int productId = objPFRequest.StoreOrderDetail.ProductId;

                if (productId == (int)VerisignGateway.ProductCode.ComodoEVCodeSignCertificate
                    || productId == (int)VerisignGateway.ProductCode.SectigoEVCodeSignCertificate)
                {
                    objcsrdetail = new CSRDetail
                    {
                        Country = cs.CountryName,
                        DomainName = cs.OrganizationName,
                        Email = cs.AdminEmail,
                        Locality = cs.City,
                        Organisation = cs.OrganizationName,
                        State = cs.State
                    };
                    domainName = cs.OrganizationName ?? string.Empty;
                }
                else
                {
                    objcsrdetail = objPFRequest.CSRDetailRow != null
                        ? CloneCsr(objPFRequest.CSRDetailRow)
                        : new CSRDetail();
                    domainName = objPFRequest.CSRDetailRow?.DomainName ?? string.Empty;
                }

                dbContext.CSRDetails.Add(objcsrdetail);
                dbContext.SaveChanges();
                int CSRDetailsID = objcsrdetail.CSRDetailId;

                var objComodoOrderDetail = new ComodoOrderDetail
                {
                    StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                    CSRDetailID = CSRDetailsID,
                    WebServer = "OTHER",
                    FirstName = cs.AdminFirstName,
                    LastName = cs.AdminLastName,
                    PACTitle = cs.AdminTitle ?? string.Empty,
                    PACEmail = cs.AdminEmail,
                    PACUserName = cs.AdminUserName,
                    PACPassword = cs.AdminPassword,
                    OrgName = cs.OrganizationName,
                    OrgAddress1 = cs.OrganizationAddress1,
                    OrgAddress2 = cs.OrganizationAddress2,
                    OrgAddress3 = cs.OrganizationAddress3,
                    OrgCity = cs.City,
                    OrgState = cs.State,
                    OrgCountry = cs.CountryName,
                    OrgEmail = cs.AdminEmail,
                    OrgPhone = cs.PhoneNo,
                    OrgPostalCode = cs.PostalCode,
                    OrgFax = string.Empty,
                    ApprovalEmail = cs.AdminEmail,
                    DVMethod = "EMAIL",
                    CodeSignType = objPFRequest.ComodoOrderRequest.ValidationTypeId,
                    CodeSignPublisherEmail = cs.CodeSignPublisherEmail,
                    CodeSignProvisioningMethod = objPFRequest.StoreOrderDetail.CodeSignProvisioningMethod,
                    CodeSignHSMType = cs.CodeSignHSMType,
                    CodeSignShippingCode = objPFRequest.StoreOrderDetail.CodeSignShippingCode,
                    AgreementEmail = cs.AdminEmail,
                    ShippingFirstName = cs.ShippingForename,
                    ShippingLastName = cs.ShippingSurname,
                    ShippingAddress1 = cs.ShippingStreetAddress1,
                    ShippingAddress2 = cs.ShippingStreetAddress2,
                    ShippingAddress3 = cs.ShippingStreetAddress3,
                    ShippingCity = cs.ShippingCity,
                    ShippingState = cs.ShippingState,
                    ShippingCountry = cs.ShippingCountryCode,
                    ShippingPostalCode = cs.ShippingPostalCode,
                    ShippingPhone = cs.ShippingPhoneNo,
                    ShippingEmail = cs.ShippingEmailAddress
                };

                if (productId == (int)VerisignGateway.ProductCode.ComodoEVCodeSignCertificate
                    || productId == (int)VerisignGateway.ProductCode.SectigoEVCodeSignCertificate)
                {
                    objComodoOrderDetail.JurictionCity = cs.JurictionCity;
                    objComodoOrderDetail.JurictionState = cs.jurictionState;
                    objComodoOrderDetail.JurictionCountry = cs.JurictionCountryName;
                }

                dbContext.ComodoOrderDetails.Add(objComodoOrderDetail);

                dbContext.ChildCertificateDetails.Add(new ChildCertificateDetail
                {
                    StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                    SSLCertificateId = objComodoOrderResponse.OrderNumber,
                    IsOriginal = true,
                    CertAction = "ISSUED",
                    DateAdded = DateTime.Now,
                    IsFetchStatus = true
                });

                dbContext.RenewalOrderDetails.Add(new RenewalOrderDetail
                {
                    ApiOrderNo = objComodoOrderResponse.OrderNumber,
                    OrderStatus = "INPROCESS",
                    CompanyName = objPFRequest.StoreOrderDetail.CompanyName,
                    ProductId = objPFRequest.StoreOrderDetail.ProductId,
                    SSLApiLinkId = objPFRequest.StoreOrderDetail.SSLApiLinkId,
                    DomainName = domainName,
                    NewRenewalOrderID = null
                });

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
                if (storeMasterDetail != null && !string.IsNullOrWhiteSpace(storeMasterDetail.CallBackURL))
                {
                    string domainName = objPFRequest.CSRDetailRow?.DomainName
                        ?? objPFRequest.ComodoOrderRequest.ComodoCodeSignOrderInfo.OrganizationName
                        ?? string.Empty;

                    var handlerURL = storeMasterDetail.CallBackURL;
                    handlerURL += "?sslapiLinkId=" + objPFRequest.StoreOrderDetail.SSLApiLinkId;
                    handlerURL += "&apiOrderNo=" + objComodoOrderResponse.OrderNumber;
                    handlerURL += "&OrderStatus=INPROCESS";
                    handlerURL += "&ProductId=" + objPFRequest.StoreOrderDetail.ProductId;
                    handlerURL += "&CompanyName=" + objPFRequest.StoreOrderDetail.CompanyName;
                    handlerURL += "&DomainName=" + domainName;
                    handlerURL += "&IsAutoConfig=" + objPFRequest.CSRDetailRow?.IsCSRSaved;

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(handlerURL);
                    using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogHandlerError(storeMasterDetail?.StoreName, objPFRequest.StoreOrderDetail.SSLApiLinkId, ex.Message);
            }
        }

        public static ComodoOrderRequest GetComodoPersonalAuthenticationCertificateRequestObject(PF_Request objPFRequest)
        {
            ComodoOrderRequest objReturn = new ComodoOrderRequest();
            var pac = objPFRequest.ComodoOrderRequest.ComodoPACOrderInfo;
            var detail = objPFRequest.ComodoOrderRequest.ComodoOrderDetailRow;

            objReturn.CSR = Convert.ToString(objPFRequest.CSR);
            objReturn.ProductCode = objPFRequest.StoreOrderDetail!.ProductId;
            objReturn.Years = objPFRequest.StoreOrderDetail.Year;
            objReturn.PartnerOrderID = objPFRequest.StoreOrderDetail.StoreOrderId.ToString();

            // Same as old commented UniqueKey path — generate when draft has no credentials yet
            if (string.IsNullOrWhiteSpace(pac.PACUser))
                pac.PACUser = (pac.FirstName ?? "user") + "_" + UniqueKey.RandomString(6, true);
            if (string.IsNullOrWhiteSpace(pac.PACPassword))
                pac.PACPassword = UniqueKey.RandomPassword();

            objReturn.PACOrderDetail = new ComdooPACOrderDetail
            {
                Title = pac.Title,
                FirstName = pac.FirstName,
                LastName = pac.LastName,
                Email = pac.Email,
                PACUser = pac.PACUser,
                PACPassword = pac.PACPassword
            };

            objReturn.OrgName = detail.OrgName;
            objReturn.OrgEmail = detail.OrgEmail;
            objReturn.City = detail.OrgCity;
            objReturn.Country = detail.OrgCountry;
            objReturn.State = detail.OrgState;
            objReturn.StreetAddress1 = detail.OrgAddress1;
            objReturn.StreetAddress2 = detail.OrgAddress2;
            objReturn.StreetAddress3 = detail.OrgAddress3;
            objReturn.PostalCode = detail.OrgPostalCode;
            objReturn.PhoneNumber = detail.OrgPhone;
            objReturn.ComodoCredential = objPFRequest.CACredentialDetails?.GetComodoCACredential();
            return objReturn;
        }

        public static void SaveComodoPACOrderInDB(PF_Request objPFRequest, OrderResponse objComodoOrderResponse)
        {
            BLStoreOrder.UpdateAPIOrderNoInStoreOrder(
                objPFRequest.StoreOrderDetail!.StoreOrderId,
                objComodoOrderResponse.OrderNumber,
                objPFRequest.LanguageCode);

            using var dbContext = new SSLConfigurationEntities();
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                string pacDomainName = "PAC Certificate for " + objPFRequest.ComodoOrderRequest.ComodoPACOrderInfo.Email;
                var pac = objPFRequest.ComodoOrderRequest.ComodoPACOrderInfo;
                var detail = objPFRequest.ComodoOrderRequest.ComodoOrderDetailRow;

                var objcsrdetail = new CSRDetail
                {
                    Country = string.Empty,
                    DNSNames = string.Empty,
                    DomainName = pacDomainName,
                    Email = string.Empty,
                    Locality = string.Empty,
                    Organisation = string.Empty,
                    OrganisationUnit = string.Empty,
                    State = string.Empty
                };
                dbContext.CSRDetails.Add(objcsrdetail);
                dbContext.SaveChanges();

                dbContext.ComodoOrderDetails.Add(new ComodoOrderDetail
                {
                    FirstName = pac.FirstName,
                    LastName = pac.LastName,
                    PACTitle = pac.Title,
                    PACEmail = pac.Email,
                    PACUserName = pac.PACUser,
                    PACPassword = pac.PACPassword,
                    StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                    CSRDetailID = objcsrdetail.CSRDetailId,
                    WebServer = string.Empty,
                    OrgName = detail.OrgName ?? string.Empty,
                    OrgAddress1 = detail.OrgAddress1 ?? string.Empty,
                    OrgAddress2 = detail.OrgAddress2 ?? string.Empty,
                    OrgAddress3 = detail.OrgAddress3 ?? string.Empty,
                    OrgCity = detail.OrgCity ?? string.Empty,
                    OrgState = detail.OrgState ?? string.Empty,
                    OrgCountry = detail.OrgCountry ?? string.Empty,
                    OrgEmail = detail.OrgEmail ?? string.Empty,
                    OrgPhone = detail.OrgPhone ?? string.Empty,
                    OrgPostalCode = detail.OrgPostalCode ?? string.Empty,
                    OrgFax = detail.OrgFax ?? string.Empty,
                    DVMethod = "EMAIL",
                    AgreementEmail = detail.OrgEmail ?? string.Empty
                });

                dbContext.ChildCertificateDetails.Add(new ChildCertificateDetail
                {
                    StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                    SSLCertificateId = objComodoOrderResponse.OrderNumber,
                    IsOriginal = true,
                    CertAction = "ISSUED",
                    DateAdded = DateTime.Now,
                    IsFetchStatus = true
                });

                dbContext.RenewalOrderDetails.Add(new RenewalOrderDetail
                {
                    ApiOrderNo = objComodoOrderResponse.OrderNumber,
                    OrderStatus = "INPROCESS",
                    CompanyName = objPFRequest.StoreOrderDetail.CompanyName,
                    ProductId = objPFRequest.StoreOrderDetail.ProductId,
                    SSLApiLinkId = objPFRequest.StoreOrderDetail.SSLApiLinkId,
                    DomainName = pacDomainName,
                    NewRenewalOrderID = null
                });

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
                if (storeMasterDetail != null && !string.IsNullOrWhiteSpace(storeMasterDetail.CallBackURL))
                {
                    string pacDomainName = "PAC Certificate for " + objPFRequest.ComodoOrderRequest.ComodoPACOrderInfo.Email;
                    var handlerURL = storeMasterDetail.CallBackURL;
                    handlerURL += "?sslapiLinkId=" + objPFRequest.StoreOrderDetail.SSLApiLinkId;
                    handlerURL += "&apiOrderNo=" + objComodoOrderResponse.OrderNumber;
                    handlerURL += "&OrderStatus=INPROCESS";
                    handlerURL += "&ProductId=" + objPFRequest.StoreOrderDetail.ProductId;
                    handlerURL += "&CompanyName=" + objPFRequest.StoreOrderDetail.CompanyName;
                    handlerURL += "&DomainName=" + pacDomainName;
                    handlerURL += "&IsAutoConfig=" + objPFRequest.CSRDetailRow?.IsCSRSaved;

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(handlerURL);
                    using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogHandlerError(storeMasterDetail?.StoreName, objPFRequest.StoreOrderDetail.SSLApiLinkId, ex.Message);
            }
        }
    }
}
