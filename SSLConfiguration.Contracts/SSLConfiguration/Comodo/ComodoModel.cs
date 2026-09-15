using System;
using System.Collections.Generic;

namespace SSLConfiguration.Contracts.SSLConfiguration.Comodo
{
    /// <summary>
    /// Same fields as old ComodoCodeSigningModel + token.
    /// </summary>
    public class CodeSignContactInfoDto
    {
        public string? configurationToken { get; set; }
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
    /// GET ContactInfo_CodeSign — was PartialView _ComodoContactInfo_CodeSign.
    /// </summary>
    public class CodeSignContactInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? CodeSignProvisioningMethod { get; set; }
        public string? ValidationTypeId { get; set; }
        public string? authenticationType { get; set; }
        public CodeSignContactInfoDto? contact { get; set; }
        public List<Digicert.SelectListItemDto>? CountryList { get; set; }
    }

    /// <summary>
    /// Same fields as old ComodoCodeSignGenerateCSRModel + token.
    /// </summary>
    public class CodeSignCsrInfoDto
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
        public bool IsNewCSR { get; set; }
        public string? DomainName { get; set; }
        public string? Locality { get; set; }
        public string? Organisation { get; set; }
        public string? OrganisationUnit { get; set; }
        public string? Country { get; set; }
        public string? State { get; set; }
        public string? CSR { get; set; }
        public string? PrivateKey { get; set; }
        public string? ValidationTypeId { get; set; }
        public string? KeyAttestation { get; set; }
        public string? HSMType { get; set; }
    }

    /// <summary>
    /// GET CSRInfo_CodeSign — was PartialView _ComodoCSRInfo_CodeSign.
    /// EV non-HSM sets nextAction = ContactInfo_CodeSign (old RedirectToAction).
    /// </summary>
    public class CodeSignCsrInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? nextAction { get; set; }
        public string? CodeSignProvisioningMethod { get; set; }
        public string? authenticationType { get; set; }
        public CodeSignCsrInfoDto? csr { get; set; }
        public List<Digicert.SelectListItemDto>? ValidationTypeList { get; set; }
        public List<Digicert.SelectListItemDto>? HSMTypeList { get; set; }
        public List<Digicert.SelectListItemDto>? CountryList { get; set; }
    }

    /// <summary>
    /// GET Summary_CodeSign — draft snapshot for UI (was PartialView _ComodoSummary_CodeSign).
    /// </summary>
    public class CodeSignSummaryGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? productName { get; set; }
        public string? authenticationType { get; set; }
        public string? CodeSignProvisioningMethod { get; set; }
        public string? ValidationTypeId { get; set; }
        public string? CSR { get; set; }
        public string? DomainName { get; set; }
        public CodeSignContactInfoDto? contact { get; set; }
    }

    /// <summary>
    /// Same as old ComodoCommonContactInfo.
    /// </summary>
    public class ComodoCommonContactInfoDto
    {
        public string? Title { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? RelationShip { get; set; }
    }

    /// <summary>
    /// Same as old ComodoEVContactInfo + token.
    /// </summary>
    public class ComodoEVContactInfoDto
    {
        public string? configurationToken { get; set; }
        public string? InCorporationAgency { get; set; }
        public string? InCorporationPhoneNumber { get; set; }
        public ComodoCommonContactInfoDto? CertificateRequestorInfo { get; set; }
        public ComodoCommonContactInfoDto? CertificateApproverInfo { get; set; }
        public ComodoCommonContactInfoDto? ContractSignerInfo { get; set; }
    }

    /// <summary>
    /// Same fields as old ComodoOrganizationInformation (Contact + Org steps) + token.
    /// </summary>
    public class ComodoOrganizationInformationDto
    {
        public string? configurationToken { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? ConfirmEmail { get; set; }
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? CountryName { get; set; }
        public string? CountryCode { get; set; }
        public string? PhoneNo { get; set; }
        public string? Fax { get; set; }
        public string? PostalCode { get; set; }
        public string? OrganizationName { get; set; }
        public string? Duns { get; set; }
        public string? CompanyRegisterNumber { get; set; }
        public string? Title { get; set; }
    }

    /// <summary>
    /// GET ContactInfo response.
    /// </summary>
    public class ContactInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public ComodoOrganizationInformationDto? contact { get; set; }
        public List<Digicert.SelectListItemDto>? CountryList { get; set; }
    }

    public class ComodoCsrDetailDto
    {
        public string? domainName { get; set; }
        public string? organisation { get; set; }
        public string? organisationUnit { get; set; }
        public string? locality { get; set; }
        public string? state { get; set; }
        public string? country { get; set; }
        public string? countryName { get; set; }
        public string? email { get; set; }
    }

    public class GenerateKeyAttestationResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? keyAttestation { get; set; }
    }

    /// <summary>
    /// Same as old JurictionInfo + token.
    /// </summary>
    public class JurisdictionInfoDto
    {
        public string? configurationToken { get; set; }
        public string? JurictionCity { get; set; }
        public string? jurictionState { get; set; }
        public string? JurictionCountryName { get; set; }
        public string? JurictionCountryCode { get; set; }
        public DateTime? DateOfIncorporation { get; set; }
        public string? DoingBusinessAs { get; set; }
        public string? AgencyRegNumber { get; set; }
    }

    /// <summary>
    /// GET JurisdictionInfo response.
    /// </summary>
    public class JurisdictionInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public JurisdictionInfoDto? jurisdiction { get; set; }
        public List<Digicert.SelectListItemDto>? CountryList { get; set; }
    }

    /// <summary>
    /// GET OrganizationInfo response.
    /// </summary>
    public class OrganizationInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public ComodoOrganizationInformationDto? organization { get; set; }
        public List<Digicert.SelectListItemDto>? CountryList { get; set; }
    }

    public class PacContactInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public PacOrderInfoDto? contact { get; set; }
    }

    /// <summary>Parsed CSR fields for PAC summary (same shape as old CSRDetailRow).</summary>
    public class PacCsrDetailDto
    {
        public string? DomainName { get; set; }
        public string? Country { get; set; }
        public string? Locality { get; set; }
        public string? Organisation { get; set; }
        public string? OrganisationUnit { get; set; }
        public string? State { get; set; }
        public string? Email { get; set; }
        public string? CSR { get; set; }
    }

    public class PacCsrInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? domainName { get; set; }
        public PacOrderInfoDto? pac { get; set; }
    }

    /// <summary>
    /// Same shape as old CSRInfo_PAC POST Json (IsSuccess/Msg/FileName).
    /// </summary>
    public class PacCsrInfoPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? FileName { get; set; }
        public string? CSR { get; set; }
        public string? objCSRResJson { get; set; }
        public string? CSRDetailJson { get; set; }
    }

    /// <summary>
    /// Same fields as old ComodoPACOrderInfo + token.
    /// </summary>
    public class PacOrderInfoDto
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
        public string? Title { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? CSR { get; set; }
        public string? PrivateKey { get; set; }
        public bool IsNewCSR { get; set; }
    }

    public class PacOrganizationInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public ComodoOrganizationInformationDto? organization { get; set; }
        public List<Digicert.SelectListItemDto>? CountryList { get; set; }
    }

    public class PacSummaryGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? productName { get; set; }
        public string? CSR { get; set; }
        public PacOrderInfoDto? contact { get; set; }
        public ComodoOrganizationInformationDto? organization { get; set; }
        public PacCsrDetailDto? csrDetail { get; set; }
    }

    /// <summary>
    /// GET Summary — returns draft summary fields (was PartialView _ComodoSummary).
    /// </summary>
    public class SummaryGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? domainName { get; set; }
        public string? primaryDomain { get; set; }
        public string? productName { get; set; }
        public string? approvalMethod { get; set; }
        public string? approverEmail { get; set; }
        public string? authenticationType { get; set; }
        public string? firstName { get; set; }
        public string? lastName { get; set; }
        public Dictionary<string, string>? sanApprovalEmailList { get; set; }
        public Dictionary<string, string>? wildcardSanApprovalEmailList { get; set; }
        public ComodoOrganizationInformationDto? organization { get; set; }
        public JurisdictionInfoDto? jurisdiction { get; set; }
        public ComodoEVContactInfoDto? verification { get; set; }
        public ComodoCsrDetailDto? csrDetail { get; set; }
        public bool? isMultiYearOrder { get; set; }
    }

    public class SummaryPostRequest
    {
        public string? configurationToken { get; set; }
    }

    /// <summary>
    /// Same shape as old ComodoController.Summary POST Json.
    /// </summary>
    public class SummaryPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? returnUrl { get; set; }
        public string? VendorID { get; set; }
        public int ErrorCode { get; set; }
    }

    /// <summary>
    /// GET VerificationInfo response (EV).
    /// </summary>
    public class VerificationInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public ComodoEVContactInfoDto? verification { get; set; }
        public List<Digicert.SelectListItemDto>? CountryList { get; set; }
    }
}
