using System.Net;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.DataAccess;
using SSLConfiguration.Infrastructure.Persistence;
using VerisignGateway;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Thin layer architecture / Core migration: keep same BLDigicert method names as old repo.
    /// </summary>
    public class BLDigicert
    {
        /// <summary>
        /// Same as old BLDigicert.GetDigicertApprovalEmailList.
        /// Live old code uses Comodo approval email helper (DigicertAPIHelper path is commented out).
        /// </summary>
        public static List<SelectListItem> GetDigicertApprovalEmailList(string domainName, DigicertCACredential? objDigicertCACredential)
        {
            try
            {
                List<SelectListItem> approverEmailList = new List<SelectListItem>();
                approverEmailList.Add(new SelectListItem { Value = "", Text = "--Select--" });

                List<SelectListItem> objResponse = BLGeneral.GetComodoApprovalEmailList(
                    domainName.Replace("*.", string.Empty),
                    BLGeneral.GetDefaultComodoCredential());

                return objResponse;
            }
            catch (Exception)
            {
                return new List<SelectListItem>();
            }
        }

        /// <summary>
        /// Same as old BLDigicert.GetDigicertWebServerList — reads AppSettings DigicertWebServerList (id|name,...).
        /// </summary>
        public static List<SelectListItem> GetDigicertWebServerList()
        {
            List<SelectListItem> serverList = new List<SelectListItem>();
            serverList.Add(new SelectListItem { Value = "", Text = "--Select Option --" });

            string? webserver = AppConfig.DigicertWebServerList;
            if (string.IsNullOrWhiteSpace(webserver))
            {
                return serverList;
            }

            char[] separator = new char[] { ',' };
            string[] strwebserver = webserver.Split(separator);
            foreach (string GetServerList in strwebserver)
            {
                char[] sep = new char[] { '|' };
                string[] strservervalue = GetServerList.Split(sep);
                if (strservervalue.Length >= 2)
                {
                    serverList.Add(new SelectListItem { Value = strservervalue[0].ToString(), Text = strservervalue[1].ToString() });
                }
            }

            return serverList;
        }

        /// <summary>
        /// Same as old BLDigicert.GetOrganization — DigicertAPIHelper.SearchOrganization via VerisignGateway.dll.
        /// </summary>
        public static Organization? GetOrganization(string orgName, string countryName, DigicertCACredential? objDigicertCACredential)
        {
            if (objDigicertCACredential == null)
            {
                return null;
            }

            SearchOrganizationResponse searchOrganizationResponse = DigicertAPIHelper.SearchOrganization(orgName, objDigicertCACredential);

            if (searchOrganizationResponse != null && searchOrganizationResponse.DigicertAPIErrors.StatusCode == 0 && searchOrganizationResponse.Organizations != null)
            {
                if (searchOrganizationResponse.Organizations.Count() > 1)
                {
                    Organization? organization = searchOrganizationResponse.Organizations.FirstOrDefault(d =>
                        d.Name.ToUpper() == orgName.ToUpper() && d.Country.ToUpper() == countryName.ToUpper());

                    return organization;
                }

                return searchOrganizationResponse.Organizations[0];
            }

            return null;
        }

        public static List<SelectListItem> GetDigicertAllowedCACerts(int productId, DigicertCACredential? objDigicertCACredential)
        {
            return GetDigicertAllowedCACerts((VerisignGateway.ProductCode)productId, objDigicertCACredential);
        }

        /// <summary>
        /// Same as old BLDigicert.GetDigicertAllowedCACerts — DigicertAPIHelper.GetProductInfo via VerisignGateway.dll.
        /// </summary>
        public static List<SelectListItem> GetDigicertAllowedCACerts(VerisignGateway.ProductCode enmProductCode, DigicertCACredential? objDigicertCACredential)
        {
            List<SelectListItem> allowedCACertsList = new List<SelectListItem>();

            try
            {
                allowedCACertsList.Add(new SelectListItem { Value = "-1", Text = "--Select--" });

                if (objDigicertCACredential == null)
                {
                    return allowedCACertsList;
                }

                ProductDetails objResponse = DigicertAPIHelper.GetProductInfo(enmProductCode, objDigicertCACredential);

                if (objResponse != null && objResponse.DigicertAPIErrors.StatusCode == 0)
                {
                    AllowedCACerts[]? allowedCACerts = objResponse.AllowedCACerts;

                    if (allowedCACerts != null)
                    {
                        // Note: DigiCert Global Root CA (SHA1) and DigiCert High Assurance EV Root CA (SHA1) Ceritificate disable on 08-May-25
                        string[] excludedCertIds = new[]
                        {
                            "3DA92C032BF164CD", "70F66E5ED541D420",
                            "053E6A5543FD9CA9", "A29E2F41EF3251C1",
                            "E433A8F663DB7CD4", "3DEAA84876BC11AE",
                            "FC829E9A7B7D8F7D", "5A0671AAC0BF4CE6", "AFD3DCAB58801130",
                            "4AE7259E3E5D112A", "ECDE9FF7B344B75D",
                            "33621C1BDD0C9357", "135FCC69649FAE91",
                            "570ADFB8EBCE94B8"
                        };

                        allowedCACerts = allowedCACerts.Where(d => !excludedCertIds.Contains(d.Id)).OrderBy(d => d.Name).ToArray();

                        foreach (var ca in allowedCACerts)
                            allowedCACertsList.Add(new SelectListItem { Value = ca.Id, Text = ca.ChainInfo });
                    }
                }
            }
            catch
            {
            }

            return allowedCACertsList;
        }

        /// <summary>
        /// Same as old BLDigicert.GetWebServerName.
        /// </summary>
        public static string GetWebServerName(string webServerValue)
        {
            string? webserver = AppConfig.DigicertWebServerList;
            if (string.IsNullOrWhiteSpace(webserver) || string.IsNullOrWhiteSpace(webServerValue))
            {
                return string.Empty;
            }

            foreach (string GetServerList in webserver.Split(','))
            {
                string[] strservervalue = GetServerList.Split('|');
                if (strservervalue.Length >= 2 && strservervalue[0].ToUpper() == webServerValue.ToUpper())
                    return strservervalue[1];
            }

            return string.Empty;
        }

        public static string GetCodeSigningWebServerName(string webServerValue)
        {
            // CodeSign list not needed for Phase 5 SSL PlaceOrder; keep stub for SaveDigicertOrderInDB parity.
            return string.Empty;
        }

        /// <summary>
        /// Same as old BLDigicert.SetErrorFromDigicert.
        /// </summary>
        public static void SetErrorFromDigicert(PF_Response objPFResponse, DigicertErrorDetails objDigicertErrorDetails)
        {
            objPFResponse.ErrorCode = -1;
            objPFResponse.ErrorFiled = objDigicertErrorDetails.Errors[0].Code;
            objPFResponse.ErrorMessage = objDigicertErrorDetails.Errors[0].Message;
        }

        /// <summary>
        /// Same as old BLDigicert.GetDigicertOrderRequestObject.
        /// </summary>
        public static DigicertOrderRequest GetDigicertOrderRequestObject(PF_Request objPFRequest)
        {
            DigicertOrderRequest digicertOrderRequest = new DigicertOrderRequest();

            digicertOrderRequest.Certificate = new Certificate();
            digicertOrderRequest.Certificate.CommonName = objPFRequest.CSRDetailRow!.DomainName;
            digicertOrderRequest.Certificate.CSR = objPFRequest.CSR;
            digicertOrderRequest.Certificate.SignatureHash = "sha256";
            digicertOrderRequest.ValidityYears = objPFRequest.StoreOrderDetail!.Year;
            digicertOrderRequest.SkipApproval = true;
            digicertOrderRequest.Certificate.CACertID = objPFRequest.DigicertOrderRequest?.IntermediateCAId;
            digicertOrderRequest.IsFreeSANInclude = objPFRequest.DigicertOrderRequest!.IsFreeSANInclude;

            if (!string.IsNullOrWhiteSpace(objPFRequest.DigicertOrderRequest.KeyUsages))
            {
                digicertOrderRequest.Certificate.KeyUsages = new[] { objPFRequest.DigicertOrderRequest.KeyUsages };
            }
            if (!string.IsNullOrWhiteSpace(objPFRequest.DigicertOrderRequest.ExtendedKeyUsages))
            {
                digicertOrderRequest.Certificate.ExtendedKeyUsages = new[] { objPFRequest.DigicertOrderRequest.ExtendedKeyUsages };
            }

            List<string> sanList = new List<string>();

            if (objPFRequest.DigicertOrderRequest.AdditionalDomainList != null && objPFRequest.DigicertOrderRequest.AdditionalDomainList.Count > 0)
            {
                foreach (var san in objPFRequest.DigicertOrderRequest.AdditionalDomainList)
                {
                    if (!sanList.Contains(san.Key))
                        sanList.Add(san.Key);
                }
            }

            if (objPFRequest.DigicertOrderRequest.AdditionalWildCardDomainList != null && objPFRequest.DigicertOrderRequest.AdditionalWildCardDomainList.Count > 0)
            {
                foreach (var san in objPFRequest.DigicertOrderRequest.AdditionalWildCardDomainList)
                {
                    if (!sanList.Contains(san.Key))
                        sanList.Add(san.Key);
                }
            }

            if (sanList.Count > 0)
            {
                digicertOrderRequest.Certificate.DnsNames = sanList.ToArray();
            }

            if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.Email)
                digicertOrderRequest.DcvMethod = DigicertConstants.DCV_EMAIL;
            else if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.DNS)
                digicertOrderRequest.DcvMethod = DigicertConstants.DCV_DNS_TXT_TOKEN;
            else if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.File)
                digicertOrderRequest.DcvMethod = DigicertConstants.DCV_HTTP_TOKEN;
            else
                digicertOrderRequest.DcvMethod = DigicertConstants.DCV_EMAIL;

            if (!string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.DCVScope))
                digicertOrderRequest.CertificateDCVScope = objPFRequest.DigicertOrderRequest.DCVScope;

            if (objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow != null && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.Email))
            {
                digicertOrderRequest.TechnicalContact = new DigicertContactInfo();
                digicertOrderRequest.TechnicalContact.ContactType = DigicertConstants.CONTACT_TYPE_TECHNICAL;
                digicertOrderRequest.TechnicalContact.Email = objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.Email;
                digicertOrderRequest.TechnicalContact.FirstName = objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.FirstName;
                digicertOrderRequest.TechnicalContact.JobTitle = objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.Title;
                digicertOrderRequest.TechnicalContact.LastName = objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.LastName;
                digicertOrderRequest.TechnicalContact.Telephone = objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.PhoneNo;
            }

            if (objPFRequest.DigicertOrderRequest.OrganizationContactInfo != null && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Email))
            {
                digicertOrderRequest.OrganizationContact = new OrganizationContact();
                digicertOrderRequest.OrganizationContact.ContactType = DigicertConstants.CONTACT_TYPE_ORGANIZATION;
                digicertOrderRequest.OrganizationContact.Email = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Email;
                digicertOrderRequest.OrganizationContact.FirstName = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.FirstName;
                digicertOrderRequest.OrganizationContact.JobTitle = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Title;
                digicertOrderRequest.OrganizationContact.LastName = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.LastName;
                digicertOrderRequest.OrganizationContact.Telephone = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.PhoneNo;
            }

            if (BLGeneral.IsOVEVProduct_StoreOrder(objPFRequest.StoreOrderDetail.StoreOrderId))
            {
                if (objPFRequest.DigicertOrderRequest.OrgranisationInfoRow != null && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.LegalName))
                {
                    long digicertOrganizationID = objPFRequest.DigicertOrderRequest.DigicertOrganizationId;
                    digicertOrderRequest.Organization = new Organization();

                    if (digicertOrganizationID > 0 && !objPFRequest.DigicertOrderRequest.IsOrgDetailChange)
                    {
                        digicertOrderRequest.Organization.Id = digicertOrganizationID;
                        objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.DigicertOrganizationId = digicertOrganizationID;
                        objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.IsModified = false;
                        objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.CreatedDate = DateTime.Now;
                        objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.UpdatedDate = DateTime.Now;
                    }
                    else
                    {
                        if (digicertOrganizationID > 0 && objPFRequest.DigicertOrderRequest.IsOrgDetailChange)
                            objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.IsModified = true;
                        else
                            objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.IsModified = false;

                        long newDigicertOrganizationID = CreateOrganizationAtDigicert(objPFRequest, objPFRequest.CACredentialDetails!.GetDigicertCACredential());
                        digicertOrderRequest.Organization.Id = newDigicertOrganizationID;
                        objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.DigicertOrganizationId = newDigicertOrganizationID;
                        objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.CreatedDate = DateTime.Now;
                        objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.UpdatedDate = DateTime.Now;
                    }

                    var organizationList = new List<OrganizationContact>();
                    if (objPFRequest.DigicertOrderRequest.EVApproverContactInfo != null && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.EVApproverContactInfo.Email))
                    {
                        var evApproverContactInfo = new OrganizationContact();
                        evApproverContactInfo.ContactType = DigicertConstants.CONTACT_TYPE_EV_APPROVER;
                        evApproverContactInfo.Email = objPFRequest.DigicertOrderRequest.EVApproverContactInfo.Email;
                        evApproverContactInfo.FirstName = objPFRequest.DigicertOrderRequest.EVApproverContactInfo.FirstName;
                        evApproverContactInfo.JobTitle = objPFRequest.DigicertOrderRequest.EVApproverContactInfo.Title;
                        evApproverContactInfo.LastName = objPFRequest.DigicertOrderRequest.EVApproverContactInfo.LastName;
                        evApproverContactInfo.Telephone = objPFRequest.DigicertOrderRequest.EVApproverContactInfo.PhoneNo;
                        organizationList.Add(evApproverContactInfo);
                    }

                    if (organizationList.Count > 0)
                    {
                        digicertOrderRequest.Organization.OrganizationContacts = organizationList.ToArray();
                    }
                }
            }

            digicertOrderRequest.DigicertCredential = objPFRequest.CACredentialDetails!.GetDigicertCACredential();
            return digicertOrderRequest;
        }

        /// <summary>
        /// Same as old BLDigicert.CreateOrganizationAtDigicert.
        /// </summary>
        public static long CreateOrganizationAtDigicert(PF_Request objPFRequest, DigicertCACredential? objDigicertCACredential)
        {
            try
            {
                if (objDigicertCACredential == null)
                    return 0;

                if (objPFRequest.DigicertOrderRequest.OrgranisationInfoRow != null && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.LegalName))
                {
                    DigicertCreateOrganizationRequest digicertCreateOrganizationRequest = new DigicertCreateOrganizationRequest();

                    if (objPFRequest.DigicertOrderRequest.DigicertOrganizationId > 0 && objPFRequest.DigicertOrderRequest.IsOrgDetailChange)
                        digicertCreateOrganizationRequest.Name = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.LegalName + " ";
                    else
                        digicertCreateOrganizationRequest.Name = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.LegalName;

                    digicertCreateOrganizationRequest.Address = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.Address1;
                    digicertCreateOrganizationRequest.Address2 = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.Address2;
                    digicertCreateOrganizationRequest.AssumedName = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.AssumedName;
                    digicertCreateOrganizationRequest.City = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.City;
                    digicertCreateOrganizationRequest.Country = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.Country;
                    digicertCreateOrganizationRequest.State = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.State;
                    digicertCreateOrganizationRequest.Telephone = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.PhoneNo;
                    digicertCreateOrganizationRequest.Zip = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.ZipCode;

                    if (objPFRequest.DigicertOrderRequest.OrganizationContactInfo != null && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Email))
                    {
                        digicertCreateOrganizationRequest.OrganizationContact = new OrganizationContact();
                        digicertCreateOrganizationRequest.OrganizationContact.ContactType = DigicertConstants.CONTACT_TYPE_ORGANIZATION;
                        digicertCreateOrganizationRequest.OrganizationContact.Email = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Email;
                        digicertCreateOrganizationRequest.OrganizationContact.FirstName = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.FirstName;
                        digicertCreateOrganizationRequest.OrganizationContact.JobTitle = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Title;
                        digicertCreateOrganizationRequest.OrganizationContact.LastName = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.LastName;
                        digicertCreateOrganizationRequest.OrganizationContact.Telephone = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.PhoneNo;
                    }

                    digicertCreateOrganizationRequest.SkipDuplicationOrgCheck = true;

                    DigicertCreateOrganizationResponse objResponse = DigicertAPIHelper.CreateOrganization(digicertCreateOrganizationRequest, objDigicertCACredential);

                    if (objResponse != null && objResponse.DigicertAPIErrors.StatusCode == 0)
                    {
                        return objResponse.Id;
                    }
                }

                return 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Same as old BLDigicert.SaveDigicertOrderInDB (EF Core transaction).
        /// </summary>
        public static void SaveDigicertOrderInDB(PF_Request objPFRequest, OrderResponse objDigicertOrderResponse)
        {
            BLStoreOrder.UpdateAPIOrderNoInStoreOrder(objPFRequest.StoreOrderDetail!.StoreOrderId, objDigicertOrderResponse.DigicertOrderNumber, objPFRequest.LanguageCode);

            using (var dbContext = new SSLConfiguration.Infrastructure.Persistence.SSLConfigurationEntities())
            using (var transaction = dbContext.Database.BeginTransaction())
            {
                try
                {
                    int CSRDetailsID = 0;
                    int OrganizationinfoID = 0;
                    int OrgContactID = 0;
                    int TechContactID = 0;
                    int ApproverContactId = 0;

                    if (objPFRequest.CSRDetailRow != null)
                    {
                        var objcsrdetail = CloneCsr(objPFRequest.CSRDetailRow);
                        dbContext.CSRDetails.Add(objcsrdetail);
                        dbContext.SaveChanges();
                        CSRDetailsID = objcsrdetail.CSRDetailId;
                    }

                    if (objPFRequest.DigicertOrderRequest.OrgranisationInfoRow != null
                        && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.Address1)
                        && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.LegalName))
                    {
                        var objOrg = CloneOrg(objPFRequest.DigicertOrderRequest.OrgranisationInfoRow);
                        objOrg.Division = string.IsNullOrEmpty(objOrg.Division) ? string.Empty : objOrg.Division;
                        objOrg.DigicertOrganizationId = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.DigicertOrganizationId;
                        dbContext.SymantecOrganizationInfos.Add(objOrg);
                        dbContext.SaveChanges();
                        OrganizationinfoID = objOrg.SymantecOrganizationInfoID;
                    }

                    if (objPFRequest.DigicertOrderRequest.OrganizationContactInfo != null && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Email))
                    {
                        var objOrganizationContactInfo = CloneContact(objPFRequest.DigicertOrderRequest.OrganizationContactInfo);
                        dbContext.SymantecContactInfos.Add(objOrganizationContactInfo);
                        dbContext.SaveChanges();
                        OrgContactID = objOrganizationContactInfo.SymantecContactInfoID;
                    }

                    if (objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow != null && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.Email))
                    {
                        var objTechContactInfo = CloneContact(objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow);
                        dbContext.SymantecContactInfos.Add(objTechContactInfo);
                        dbContext.SaveChanges();
                        TechContactID = objTechContactInfo.SymantecContactInfoID;
                    }

                    if (objPFRequest.DigicertOrderRequest.EVApproverContactInfo != null && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.EVApproverContactInfo.Email))
                    {
                        var objApproverContactInfo = CloneContact(objPFRequest.DigicertOrderRequest.EVApproverContactInfo);
                        dbContext.SymantecContactInfos.Add(objApproverContactInfo);
                        dbContext.SaveChanges();
                        ApproverContactId = objApproverContactInfo.SymantecContactInfoID;
                    }

                    DigicertCertificateDetail objcertificate = new DigicertCertificateDetail();
                    objcertificate.StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId;
                    objcertificate.OrganizationContactId = OrgContactID;
                    objcertificate.TechnicalContactId = TechContactID;
                    objcertificate.CSRDetailId = CSRDetailsID;
                    objcertificate.OrganizationInfoId = OrganizationinfoID;
                    objcertificate.ApproverContactId = ApproverContactId;
                    objcertificate.IssuingCA = !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.IntermediateCAText) ? objPFRequest.DigicertOrderRequest.IntermediateCAText : string.Empty;
                    objcertificate.WebServerType = string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.WebServerType) ? string.Empty : GetWebServerName(objPFRequest.DigicertOrderRequest.WebServerType);
                    objcertificate.CertificateType = string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.CertificateType) ? string.Empty : GetCodeSigningWebServerName(objPFRequest.DigicertOrderRequest.CertificateType);
                    objcertificate.ApprovalEmail = objPFRequest.DigicertOrderRequest.ApproverEmail;

                    if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.Email)
                        objcertificate.DCVMethod = ConstantUtil.Digicert_DCVMethod_EMAIL;
                    else if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.File)
                    {
                        objcertificate.DCVMethod = ConstantUtil.Digicert_DCVMethod_HTTP_TOKEN;
                        objcertificate.DCVRandomValue = objDigicertOrderResponse.DigicertDCVRandomValue;
                    }
                    else if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.DNS)
                    {
                        objcertificate.DCVMethod = ConstantUtil.Digicert_DCVMethod_DNS_TXT_TOKEN;
                        objcertificate.DCVRandomValue = objDigicertOrderResponse.DigicertDCVRandomValue;
                    }
                    else
                        objcertificate.DCVMethod = ConstantUtil.Digicert_DCVMethod_EMAIL;

                    if (!string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.DCVScope))
                        objcertificate.DCVScope = objPFRequest.DigicertOrderRequest.DCVScope;

                    if (BLGeneral.IsCodeSignProduct_StoreOrder(objPFRequest.StoreOrderDetail.StoreOrderId))
                    {
                        objcertificate.CodeSignProvisioningMethod = objPFRequest.DigicertOrderRequest.CodeSignProvisioningMethod;
                    }

                    if (!string.IsNullOrWhiteSpace(objPFRequest.DigicertOrderRequest.KeyUsages))
                        objcertificate.KeyUsages = objPFRequest.DigicertOrderRequest.KeyUsages;

                    if (!string.IsNullOrWhiteSpace(objPFRequest.DigicertOrderRequest.ExtendedKeyUsages))
                        objcertificate.ExtendedKeyUsages = objPFRequest.DigicertOrderRequest.ExtendedKeyUsages;

                    dbContext.DigicertCertificateDetails.Add(objcertificate);
                    dbContext.SaveChanges();

                    if (Convert.ToBoolean(objPFRequest.StoreOrderDetail.IsMultiDomain))
                    {
                        SaveAdditionalDomains(dbContext, objPFRequest, objDigicertOrderResponse);
                    }

                    ChildCertificateDetail objChildCertificate = new ChildCertificateDetail
                    {
                        StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                        SSLCertificateId = objDigicertOrderResponse.DigicertCertificateId,
                        IsOriginal = true,
                        CertAction = "ISSUED",
                        DateAdded = DateTime.Now,
                        IsFetchStatus = true
                    };
                    dbContext.ChildCertificateDetails.Add(objChildCertificate);

                    RenewalOrderDetail renewalOrderDetail = new RenewalOrderDetail
                    {
                        ApiOrderNo = objDigicertOrderResponse.DigicertOrderNumber,
                        OrderStatus = "INPROCESS",
                        CompanyName = objPFRequest.StoreOrderDetail.CompanyName,
                        ProductId = objPFRequest.StoreOrderDetail.ProductId,
                        SSLApiLinkId = objPFRequest.StoreOrderDetail.SSLApiLinkId,
                        DomainName = objPFRequest.CSRDetailRow?.DomainName,
                        NewRenewalOrderID = null
                    };
                    dbContext.RenewalOrderDetails.Add(renewalOrderDetail);
                    dbContext.SaveChanges();

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }

            // Store callback (same as old — outside critical failure path for order itself)
            StoreMaster? storeMasterDetail = StoreMasterDataAccess.GetById(objPFRequest.StoreOrderDetail.StoreId);
            try
            {
                if (storeMasterDetail != null && !string.IsNullOrWhiteSpace(storeMasterDetail.CallBackURL))
                {
                    var handlerURL = storeMasterDetail.CallBackURL;
                    handlerURL += "?sslapiLinkId=" + objPFRequest.StoreOrderDetail.SSLApiLinkId;
                    handlerURL += "&apiOrderNo=" + objDigicertOrderResponse.DigicertOrderNumber;
                    handlerURL += "&OrderStatus=INPROCESS";
                    handlerURL += "&ProductId=" + objPFRequest.StoreOrderDetail.ProductId;
                    handlerURL += "&CompanyName=" + objPFRequest.StoreOrderDetail.CompanyName;
                    handlerURL += "&DomainName=" + objPFRequest.CSRDetailRow?.DomainName;
                    handlerURL += "&IsAutoConfig=" + objPFRequest.CSRDetailRow?.IsCSRSaved;

                    if (objPFRequest.DigicertOrderRequest.OrgranisationInfoRow != null
                        && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.Address1)
                        && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.LegalName))
                    {
                        handlerURL += "&IsOrgInfoChanged=" + (objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.IsModified == null ? false : Convert.ToBoolean(objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.IsModified));
                    }
                    else
                    {
                        handlerURL += "&IsOrgInfoChanged=" + false;
                    }

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(handlerURL);
                    using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogHandlerError(storeMasterDetail?.StoreName, objPFRequest.StoreOrderDetail.SSLApiLinkId, ex.Message);
            }
        }

        private static void SaveAdditionalDomains(SSLConfiguration.Infrastructure.Persistence.SSLConfigurationEntities dbContext, PF_Request objPFRequest, OrderResponse objDigicertOrderResponse)
        {
            if (objPFRequest.DigicertOrderRequest.AdditionalDomainList != null && objPFRequest.DigicertOrderRequest.AdditionalDomainList.Count > 0)
            {
                foreach (var san in objPFRequest.DigicertOrderRequest.AdditionalDomainList)
                {
                    DigicertAdditionalDomain objAdditionalDomain = BuildAdditionalDomain(objPFRequest, objDigicertOrderResponse, san.Key, san.Value,
                        isConsiderAsSan: !san.Key.ToLower().Equals(objPFRequest.CSRDetailRow!.DomainName!.ToLower()),
                        isPrimary: san.Key.ToLower().Equals(objPFRequest.CSRDetailRow.DomainName!.ToLower()));
                    dbContext.DigicertAdditionalDomains.Add(objAdditionalDomain);
                }
            }

            if (objPFRequest.DigicertOrderRequest.AdditionalWildCardDomainList != null && objPFRequest.DigicertOrderRequest.AdditionalWildCardDomainList.Count > 0)
            {
                foreach (var san in objPFRequest.DigicertOrderRequest.AdditionalWildCardDomainList)
                {
                    DigicertAdditionalDomain objAdditionalDomain = BuildAdditionalDomain(objPFRequest, objDigicertOrderResponse, san.Key, san.Value, isConsiderAsSan: true, isPrimary: false);
                    dbContext.DigicertAdditionalDomains.Add(objAdditionalDomain);
                }
            }

            dbContext.SaveChanges();
        }

        private static DigicertAdditionalDomain BuildAdditionalDomain(PF_Request objPFRequest, OrderResponse objDigicertOrderResponse, string domain, string approvalValue, bool isConsiderAsSan, bool isPrimary)
        {
            DigicertAdditionalDomain objAdditionalDomain = new DigicertAdditionalDomain
            {
                DomainName = domain,
                StoreOrderId = objPFRequest.StoreOrderDetail!.StoreOrderId,
                IsConsiderAsSAN = isConsiderAsSan,
                IsPrimaryDomain = isPrimary,
                OrganizationId = objPFRequest.DigicertOrderRequest.OrgranisationInfoRow?.DigicertOrganizationId,
                SpecialNote = string.Empty,
                CreatedDate = DateTime.Now,
                UpdatedDate = DateTime.Now
            };

            if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.Email)
            {
                objAdditionalDomain.DCVMethod = ConstantUtil.Digicert_DCVMethod_EMAIL;
                objAdditionalDomain.ApprovalEmail = approvalValue;
            }
            else if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.File)
            {
                objAdditionalDomain.DCVMethod = ConstantUtil.Digicert_DCVMethod_HTTP_TOKEN;
                objAdditionalDomain.DCVRandomValue = objDigicertOrderResponse.DigicertDCVRandomValue;
            }
            else if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.DNS)
            {
                objAdditionalDomain.DCVMethod = ConstantUtil.Digicert_DCVMethod_DNS_TXT_TOKEN;
                objAdditionalDomain.DCVRandomValue = objDigicertOrderResponse.DigicertDCVRandomValue;
            }

            return objAdditionalDomain;
        }

        private static CSRDetail CloneCsr(CSRDetail src) => new CSRDetail
        {
            DNSNames = src.DNSNames,
            DomainName = src.DomainName,
            Email = src.Email,
            Locality = src.Locality,
            State = src.State,
            Country = src.Country,
            Organisation = src.Organisation,
            OrganisationUnit = src.OrganisationUnit,
            IsCSRSaved = src.IsCSRSaved,
            CSR = src.CSR
        };

        private static SymantecContactInfo CloneContact(SymantecContactInfo src) => new SymantecContactInfo
        {
            Title = src.Title,
            FirstName = src.FirstName,
            LastName = src.LastName,
            Email = src.Email,
            PhoneNo = src.PhoneNo
        };

        private static SymantecOrganizationInfo CloneOrg(SymantecOrganizationInfo src) => new SymantecOrganizationInfo
        {
            LegalName = src.LegalName,
            AssumedName = src.AssumedName,
            Division = src.Division,
            Duns = src.Duns,
            Address1 = src.Address1,
            Address2 = src.Address2,
            City = src.City,
            State = src.State,
            Country = src.Country,
            ZipCode = src.ZipCode,
            PhoneNo = src.PhoneNo,
            Fax = src.Fax,
            DigicertOrganizationId = src.DigicertOrganizationId,
            IsModified = src.IsModified,
            CreatedDate = src.CreatedDate,
            UpdatedDate = src.UpdatedDate
        };

        public static List<SelectListItem> GetDigicertCodeSignProvisingMethods()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "--Select--" },
                new SelectListItem { Value = "email", Text = "Hardware Security Module (HSM)" },
                new SelectListItem { Value = "ship_token", Text = "DigiCert-Provided Hardware Token" },
                new SelectListItem { Value = "client_app", Text = "My Own Qualified Hardware Token" }
            };
        }

        public static List<SelectListItem> GetDigicertCodeSignServerHardwarePlatforms()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "--Select--" },
                new SelectListItem { Value = "20", Text = "SafeNet eToken 5110 FIPS (ECC Only)" },
                new SelectListItem { Value = "24", Text = "SafeNet eToken 5110+ FIPS" },
                new SelectListItem { Value = "23", Text = "SafeNet eToken 5110 CC (RSA 4096 and ECC)" },
                new SelectListItem { Value = "23", Text = "SafeNet eToken 5110+ CC (940B) (ECC P-256 Only)" }
            };
        }

        public static List<SelectListItem> GetKeyUsage()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "--Select Option--" },
                new SelectListItem { Value = "digital_signature", Text = "Digital Signature" },
                new SelectListItem { Value = "key_agreement_encipherment", Text = "Digital Signature and Key Encipherment" }
            };
        }

        public static List<SelectListItem> GetExtendedKeyUsage()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "--Select Option--" },
                new SelectListItem { Value = "client_authentication", Text = "Client Authentication" },
                new SelectListItem { Value = "server_authentication,client_authentication", Text = "Server Authentication and Client Authentication" }
            };
        }

        public static DigicertErrorDetails UploadVMCProductLogo(string CAOrderNo, string strLogo, DigicertCACredential? objDigiCredential)
        {
            return DigicertAPIHelper.UploadVMCProductLogo(CAOrderNo, strLogo, objDigiCredential);
        }

        /// <summary>
        /// Same as old BLDigicert.GetDigicertOrderRequestObject_VMC.
        /// </summary>
        public static DigicertOrderRequest GetDigicertOrderRequestObject_VMC(PF_Request objPFRequest)
        {
            DigicertOrderRequest digicertOrderRequest = new DigicertOrderRequest();
            digicertOrderRequest.Certificate = new Certificate();

            string domainName = objPFRequest.DigicertOrderRequest.VMCCertificateDetail?.DomainName
                ?? objPFRequest.CSRDetailRow?.DomainName
                ?? string.Empty;

            digicertOrderRequest.Certificate.DnsNames = new[] { domainName };
            digicertOrderRequest.Certificate.CommonName = domainName;
            digicertOrderRequest.ValidityYears = objPFRequest.StoreOrderDetail!.Year;
            digicertOrderRequest.OrderValidity = new OrderValidity { Years = objPFRequest.StoreOrderDetail.Year };

            if (objPFRequest.DigicertOrderRequest.VMCCertificateDetail != null
                && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.VMCCertificateDetail.Logo))
            {
                digicertOrderRequest.VMCCertificateDetail = new VMCCertificateDetail
                {
                    Logo = objPFRequest.DigicertOrderRequest.VMCCertificateDetail.Logo,
                    EnableHosting = objPFRequest.DigicertOrderRequest.VMCCertificateDetail.EnableHosting,
                    MarkType = objPFRequest.DigicertOrderRequest.VMCCertificateDetail.MarkType
                };

                if (objPFRequest.DigicertOrderRequest.VMCCertificateDetail.MarkTypeData != null)
                {
                    digicertOrderRequest.VMCCertificateDetail.MarkTypeData = new MarkTypeData
                    {
                        CountryCode = objPFRequest.DigicertOrderRequest.VMCCertificateDetail.MarkTypeData.CountryCode,
                        RegistrationNumber = objPFRequest.DigicertOrderRequest.VMCCertificateDetail.MarkTypeData.RegistrationNumber
                    };
                }
            }

            List<string> sanList = new List<string>();
            if (objPFRequest.DigicertOrderRequest.AdditionalDomainList != null)
            {
                foreach (var san in objPFRequest.DigicertOrderRequest.AdditionalDomainList)
                {
                    if (!sanList.Contains(san.Key))
                        sanList.Add(san.Key);
                }
            }
            if (objPFRequest.DigicertOrderRequest.AdditionalWildCardDomainList != null)
            {
                foreach (var san in objPFRequest.DigicertOrderRequest.AdditionalWildCardDomainList)
                {
                    if (!sanList.Contains(san.Key))
                        sanList.Add(san.Key);
                }
            }
            if (sanList.Count > 0)
                digicertOrderRequest.Certificate.DnsNames = sanList.ToArray();

            if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.Email)
                digicertOrderRequest.DcvMethod = DigicertConstants.DCV_EMAIL;
            else if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.DNS)
                digicertOrderRequest.DcvMethod = DigicertConstants.DCV_DNS_TXT_TOKEN;
            else if (objPFRequest.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.File)
                digicertOrderRequest.DcvMethod = DigicertConstants.DCV_HTTP_TOKEN;
            else
                digicertOrderRequest.DcvMethod = DigicertConstants.DCV_EMAIL;

            if (objPFRequest.DigicertOrderRequest.OrganizationContactInfo != null && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Email))
            {
                digicertOrderRequest.OrganizationContact = new OrganizationContact
                {
                    ContactType = DigicertConstants.CONTACT_TYPE_ORGANIZATION,
                    FirstName = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.FirstName,
                    LastName = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.LastName,
                    Email = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Email,
                    JobTitle = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Title,
                    Telephone = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.PhoneNo
                };
            }

            if (objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow != null && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.Email))
            {
                digicertOrderRequest.TechnicalContact = new DigicertContactInfo
                {
                    ContactType = DigicertConstants.CONTACT_TYPE_TECHNICAL,
                    FirstName = objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.FirstName,
                    LastName = objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.LastName,
                    Email = objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.Email,
                    JobTitle = objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.Title,
                    Telephone = objPFRequest.DigicertOrderRequest.TechnicalContactInfoRow.PhoneNo
                };
            }

            if (BLGeneral.IsOVEVProduct_StoreOrder(objPFRequest.StoreOrderDetail.StoreOrderId)
                && objPFRequest.DigicertOrderRequest.OrgranisationInfoRow != null
                && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.LegalName))
            {
                long digicertOrganizationID = objPFRequest.DigicertOrderRequest.DigicertOrganizationId;
                digicertOrderRequest.Organization = new Organization();

                if (digicertOrganizationID > 0 && !objPFRequest.DigicertOrderRequest.IsOrgDetailChange)
                {
                    digicertOrderRequest.Organization.Id = digicertOrganizationID;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.DigicertOrganizationId = digicertOrganizationID;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.IsModified = false;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.CreatedDate = DateTime.Now;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.UpdatedDate = DateTime.Now;
                }
                else if (digicertOrganizationID > 0 && objPFRequest.DigicertOrderRequest.IsOrgDetailChange)
                {
                    digicertOrderRequest.Organization.Id = digicertOrganizationID;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.DigicertOrganizationId = digicertOrganizationID;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.IsModified = true;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.CreatedDate = DateTime.Now;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.UpdatedDate = DateTime.Now;
                }
                else
                {
                    long newDigicertOrganizationID = CreateOrganizationAtDigicert(objPFRequest, objPFRequest.CACredentialDetails!.GetDigicertCACredential());
                    digicertOrderRequest.Organization.Id = newDigicertOrganizationID;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.DigicertOrganizationId = newDigicertOrganizationID;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.IsModified = false;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.CreatedDate = DateTime.Now;
                    objPFRequest.DigicertOrderRequest.OrgranisationInfoRow.UpdatedDate = DateTime.Now;
                }

                if (objPFRequest.DigicertOrderRequest.OrganizationContactInfo != null
                    && !string.IsNullOrEmpty(objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Email))
                {
                    digicertOrderRequest.Organization.OrganizationContacts = new[]
                    {
                        new OrganizationContact
                        {
                            ContactType = DigicertConstants.CONTACT_TYPE_EV_APPROVER,
                            FirstName = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.FirstName,
                            LastName = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.LastName,
                            Email = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Email,
                            JobTitle = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.Title,
                            Telephone = objPFRequest.DigicertOrderRequest.OrganizationContactInfo.PhoneNo
                        }
                    };
                }
            }

            digicertOrderRequest.DigicertCredential = objPFRequest.CACredentialDetails!.GetDigicertCACredential();
            return digicertOrderRequest;
        }
    }
}
