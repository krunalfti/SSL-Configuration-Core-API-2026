using SSLConfiguration.Infrastructure.Persistence;
using VerisignGateway;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same idea as old SSLConfiguration_Models.PF_BaseRequest / PF_Request (Digicert Phase 1–5 subset + Comodo Phase 1 + GlobalSign G0).
    /// </summary>
    public class PF_BaseRequest
    {
        public string? PIN { get; set; }
        public string? CSR { get; set; }
        public bool isRenew { get; set; }
        public string? LanguageCode { get; set; }
        public CSRDetail? CSRDetailRow { get; set; }
        public CACredential? CACredentialDetails { get; set; }
        /// <summary>Same as old PF_BaseRequest.OldGSOrderDetails (AlphaSSL renew lookup).</summary>
        public GSOrderDetailsList? OldGSOrderDetails { get; set; }
        public StoreOrder? StoreOrderDetail { get; set; }
        public Product? ProductDetail { get; set; }
        public string? ControllerName { get; set; }
    }

    public class PF_Request : PF_BaseRequest
    {
        public PF_Request()
        {
            DigicertOrderRequest = new PF_DigicertOrder();
            ComodoOrderRequest = new PF_ComodoOrder();
            GlobalSignOrderRequest = new PF_GlobalSignOrder();
        }

        public PF_DigicertOrder DigicertOrderRequest { get; set; }
        public PF_ComodoOrder ComodoOrderRequest { get; set; }
        public PF_GlobalSignOrder GlobalSignOrderRequest { get; set; }
        public bool IsNewIssue { get; set; }
    }

    /// <summary>
    /// Same as old PF_GlobalSignOrder (wizard draft / configurationToken store).
    /// </summary>
    public class PF_GlobalSignOrder
    {
        public PF_GlobalSignOrder()
        {
            RequestorInfoRow = new GlobalSignContactInfo();
            ApproverInfoRow = new GlobalSignContactInfo();
            AuthorisedInfoRow = new GlobalSignContactInfo();
            ContactInfoRow = new GlobalSignContactInfo();
            OrganisationInfoRow = new GlobalSignOrganizationInfo();
        }

        public GlobalSignContactInfo RequestorInfoRow { get; set; }
        public GlobalSignContactInfo ApproverInfoRow { get; set; }
        public GlobalSignContactInfo AuthorisedInfoRow { get; set; }
        public GlobalSignContactInfo ContactInfoRow { get; set; }
        public string? ApprovalEmail { get; set; }
        public string? AdditionalDomains { get; set; }
        public GlobalSignOrganizationInfo OrganisationInfoRow { get; set; }
        public string? OldGSOrderID { get; set; }

        public GSOrganizationType enmOrganisaztionType { get; set; }
        public string? ApprovalMethod { get; set; }

        public string? GS_SAN_MailDomain { get; set; }
        public string? GS_SAN_OWADomain { get; set; }
        public string? GS_SAN_AutoDiscoverDomain { get; set; }
        public string? GS_SAN_DomainName { get; set; }

        public string? PickupPassword { get; set; }

        public List<string>? WildcardSANDomainList { get; set; }

        // new added by jaynit for Extra SAN
        //public List<string> AdditionalDomainsList { get; set; }
        public Dictionary<string, string>? AdditionalDomainsList { get; set; }
        public VMC_CertificateDetail? VMCCertificateDetail { get; set; }
        public int ApprovalMethods;
        public Dictionary<string, string>? AdditionalWildCardDomainList;
        public string? ApproverEmail;
        public int NoOfAdditionalDomains;
        public int NoOfAdditionalWildCardDomains;
        public CSRDetail? CSRDetailInfoRow { get; set; }
    }

    /// <summary>
    /// Same as old PF_ComodoOrder (fields needed for Comodo Phase 1 CSR + later steps).
    /// </summary>
    public class PF_ComodoOrder
    {
        public PF_ComodoOrder()
        {
            ComodoOrderDetailRow = new ComodoOrderDetailDraft();
            SAN_ApprovalEmail = new Dictionary<string, string>();
            WildcardSAN_ApprovalEmail = new Dictionary<string, string>();
            CertificateRequestorInfo = new ComodoContactInfoDraft();
            CertificateApproverInfo = new ComodoContactInfoDraft();
            ContractSignerInfo = new ComodoContactInfoDraft();
            ComodoCodeSignOrderInfo = new ComodoCodeSignOrderInfoDraft();
            ComodoPACOrderInfo = new ComodoPACOrderInfoDraft();
        }

        public ComodoOrderDetailDraft ComodoOrderDetailRow { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PrimaryDomain { get; set; }
        public string? PrimaryDomainEmail { get; set; }
        public Dictionary<string, string> SAN_ApprovalEmail { get; set; }
        public Dictionary<string, string> WildcardSAN_ApprovalEmail { get; set; }
        public bool isCSRIncludeSAN { get; set; }
        public string? ApprovalMethod { get; set; }
        public string? AdminEmail { get; set; }
        public int WebServerCode { get; set; }
        public string? ValidationTypeId { get; set; }
        public string? UniqueString;
        public ComodoContactInfoDraft CertificateRequestorInfo { get; set; }
        public ComodoContactInfoDraft CertificateApproverInfo { get; set; }
        public ComodoContactInfoDraft ContractSignerInfo { get; set; }
        public ComodoCodeSignOrderInfoDraft ComodoCodeSignOrderInfo { get; set; }
        public ComodoPACOrderInfoDraft ComodoPACOrderInfo { get; set; }
        /// <summary>Same as old Session["IsNewCSRPAC"].</summary>
        public bool IsNewCSRPAC { get; set; }
    }

    /// <summary>
    /// Same as old ComodoPACOrderInfo (draft / token store).
    /// </summary>
    public class ComodoPACOrderInfoDraft
    {
        public string? Title { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PACUser { get; set; }
        public string? PACPassword { get; set; }
        public string? CSR { get; set; }
        public string? PrivateKey { get; set; }
        public bool IsNewCSR { get; set; }
    }

    /// <summary>
    /// Same as old ComodoCodeSigningModel (draft / token store).
    /// </summary>
    public class ComodoCodeSignOrderInfoDraft
    {
        public string? AdminTitle { get; set; }
        public string? AdminFirstName { get; set; }
        public string? AdminLastName { get; set; }
        public string? AdminEmail { get; set; }
        public string? AdminUserName { get; set; }
        public string? AdminPassword { get; set; }
        public string? AdminContactEmail { get; set; }
        public string? OrganizationName { get; set; }
        public string? OrganizationUnit { get; set; }
        public string? OrganizationAddress1 { get; set; }
        public string? OrganizationAddress2 { get; set; }
        public string? OrganizationAddress3 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? PhoneNo { get; set; }
        public string? CountryName { get; set; }
        public string? CountryCode { get; set; }
        public string? JurictionCity { get; set; }
        public string? jurictionState { get; set; }
        public string? JurictionCountryName { get; set; }
        public string? JurictionCountryCode { get; set; }
        public string? CodeSignPublisherEmail { get; set; }
        public string? CodeSignHSMType { get; set; }
        public string? CodeSignKeyAttestation { get; set; }
        public string? ShippingForename { get; set; }
        public string? ShippingSurname { get; set; }
        public string? ShippingStreetAddress1 { get; set; }
        public string? ShippingStreetAddress2 { get; set; }
        public string? ShippingStreetAddress3 { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingState { get; set; }
        public string? ShippingCountryCode { get; set; }
        public string? ShippingCountryName { get; set; }
        public string? ShippingPostalCode { get; set; }
        public string? ShippingEmailAddress { get; set; }
        public string? ShippingPhoneNo { get; set; }
    }

    /// <summary>
    /// Draft subset of old ComodoContactInfo (EV verification contacts).
    /// </summary>
    public class ComodoContactInfoDraft
    {
        public string? Title { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? Phone { get; set; }
        public string? Relationship { get; set; }
    }

    /// <summary>
    /// Draft subset of old ComodoOrderDetail (not EF entity — session/token store).
    /// </summary>
    public class ComodoOrderDetailDraft
    {
        public string? ApprovalEmail { get; set; }
        public string? CSR_MD5 { get; set; }
        public string? CSR_SHA1 { get; set; }
        public string? WebServer { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? OrgName { get; set; }
        public string? OrgAddress1 { get; set; }
        public string? OrgAddress2 { get; set; }
        public string? OrgAddress3 { get; set; }
        public string? OrgCity { get; set; }
        public string? OrgState { get; set; }
        public string? OrgCountry { get; set; }
        public string? OrgEmail { get; set; }
        public string? OrgPhone { get; set; }
        public string? OrgFax { get; set; }
        public string? OrgPostalCode { get; set; }
        public string? OrgDuns { get; set; }
        public string? OrgCompanyRegNumber { get; set; }
        public string? JurictionCity { get; set; }
        public string? JurictionState { get; set; }
        public string? JurictionCountry { get; set; }
        public DateTime? DateOfIncorporation { get; set; }
        public string? DoingBusinessAs { get; set; }
        public string? InCorporationAgency { get; set; }
        public string? InCorporationPhoneNo { get; set; }
        public string? DVMethod { get; set; }
    }

    /// <summary>
    /// Same as old PF_DigicertOrder (fields needed for Phase 1–5 Digicert steps).
    /// </summary>
    public class PF_DigicertOrder
    {
        public PF_DigicertOrder()
        {
            TechnicalContactInfoRow = new SymantecContactInfo();
            OrganizationContactInfo = new SymantecContactInfo();
            EVApproverContactInfo = new SymantecContactInfo();
            OrgranisationInfoRow = new SymantecOrganizationInfo();
            ShippingInformation = new DigicertShippingInformation();
            VMCCertificateDetail = new VMC_CertificateDetail();
        }

        public string? WebServerType;
        public int ApprovalMethod;
        public string? ApproverEmail;
        public string? CertificateType;
        public string? CodeSignProvisioningMethod;
        public int NoOfAdditionalDomains;
        public int NoOfAdditionalWildCardDomains;
        public bool IsOrgDetailChange { get; set; }
        public long DigicertOrganizationId { get; set; }
        public Dictionary<string, string>? AdditionalDomainList;
        public Dictionary<string, string>? AdditionalWildCardDomainList;
        public SymantecContactInfo? TechnicalContactInfoRow;
        public SymantecContactInfo? OrganizationContactInfo;
        public SymantecContactInfo? EVApproverContactInfo;
        public SymantecOrganizationInfo? OrgranisationInfoRow;
        public DigicertShippingInformation? ShippingInformation;
        public VMC_CertificateDetail? VMCCertificateDetail;
        public int ServerHardwarePlatformId;
        public string? IntermediateCAId { get; set; }
        public string? IntermediateCAText { get; set; }
        public bool IsFreeSANInclude { get; set; }
        public string? DCVScope { get; set; }
        public string? KeyUsages { get; set; }
        public string? ExtendedKeyUsages { get; set; }
    }

    /// <summary>
    /// Same as old SSLConfiguration_Models.PF_Response (Digicert + Comodo subset).
    /// </summary>
    public class PF_Response
    {
        public int ErrorCode;
        public string? ErrorFiled;
        public string? ErrorMessage;
        public string? ApprovalEmail;
        public string? DomainName;
        public string? VendorID;
        public string? DigicertOrderNumber { get; set; }
        public string? DigicertCertificateId { get; set; }
        public string? DigicertDCVRandomValue { get; set; }
        public long DigicertOrganizationId { get; set; }
        public string? Comodo_DV_MD5String { get; set; }
        public string? Comodo_DV_SHA1string { get; set; }
        public string? ComodoDVMethod { get; set; }
        public string? ComodoUniqueValue { get; set; }
        public string? DNSTXTValue { get; set; }
        public string? ComodoCertficateId { get; set; }

        public string? GSURLMetaTag;
        public string[]? GSURLs;
        public string? DVC;
        public string? DomainVerificationPage;
    }
}
