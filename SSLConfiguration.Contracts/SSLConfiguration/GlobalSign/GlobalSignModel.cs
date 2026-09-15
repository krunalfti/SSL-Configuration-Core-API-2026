using System.Collections.Generic;

namespace SSLConfiguration.Contracts.SSLConfiguration.GlobalSign
{
    /// <summary>
    /// Same fields as old GSContactInfoViewModel (CodeSign contact).
    /// </summary>
    public class CodeSignContactInfoDto
    {
        public string? DomainName { get; set; }
        public string? Email { get; set; }
        public string? Locality { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Organisation { get; set; }
        public string? OrganisationUnit { get; set; }
        public string? ContactFirstName { get; set; }
        public string? ContactLastName { get; set; }
        public string? ContactPhoneNo { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactOrgName { get; set; }
        public string? ContactDivision { get; set; }
        public string? ContactAddress1 { get; set; }
        public string? ContactAddress2 { get; set; }
        public string? ContactCity { get; set; }
        public string? ContactState { get; set; }
        public string? ContactZipCode { get; set; }
        public string? ContactCountry { get; set; }
        public string? ContactDuns { get; set; }
    }

    public class CodeSignContactInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public CodeSignContactInfoDto? contact { get; set; }
        public List<SelectListItemDto>? CountryList { get; set; }
    }

    public class CodeSignContactInfoPostRequest : CodeSignContactInfoDto
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
    }

    public class CodeSignContactInfoPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
    }

    /// <summary>
    /// Same fields as old CSRDetailViewModel (CodeSign CSR step).
    /// </summary>
    public class CodeSignCsrInfoDto
    {
        public string? DomainName { get; set; }
        public string? Email { get; set; }
        public string? Locality { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Organisation { get; set; }
        public string? OrganisationUnit { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
    }

    public class CodeSignCsrInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public CodeSignCsrInfoDto? csr { get; set; }
        public List<SelectListItemDto>? CountryList { get; set; }
    }

    public class CodeSignCsrInfoPostRequest : CodeSignCsrInfoDto
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
    }

    public class CodeSignCsrInfoPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
    }

    public class CodeSignOrganisationInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public OrganisationInfoDto? organisation { get; set; }
        public List<SelectListItemDto>? CountryList { get; set; }
    }

    public class CodeSignOrganisationInfoPostRequest : OrganisationInfoDto
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
    }

    public class CodeSignOrganisationInfoPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
    }

    public class CodeSignSummaryGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? domainName { get; set; }
        public int? productId { get; set; }
        public int? storeOrderId { get; set; }
        public string? authenticationType { get; set; }
        public CodeSignCsrInfoDto? csr { get; set; }
        public CodeSignContactInfoDto? contact { get; set; }
        public OrganisationInfoDto? organisation { get; set; }
        public CodeSignVerificationInfoDto? verification { get; set; }
    }

    public class CodeSignSummaryPostRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
    }

    /// <summary>
    /// Same shape as old UpdateCodeSignSummary Json.
    /// </summary>
    public class CodeSignSummaryPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? VendorID { get; set; }
        public int ErrorCode { get; set; }
    }

    /// <summary>
    /// Same fields as old GSCSEVContactInfo (Get/UpdateCodeSignVerificationInfo).
    /// Commented EmailAddress validation on AuthorizedEmail left out of contracts (no logic).
    /// </summary>
    public class CodeSignVerificationInfoDto
    {
        public string? RequestorOrgName { get; set; }
        public string? RequestorFirstName { get; set; }
        public string? RequestorLastName { get; set; }
        public string? RequestorPhone { get; set; }
        public string? RequestorEmail { get; set; }
        public string? RequestorJobTitle { get; set; }

        public string? ApproverOrgName { get; set; }
        public string? ApproverFirstName { get; set; }
        public string? ApproverLastName { get; set; }
        public string? ApproverPhone { get; set; }
        public string? ApproverEmail { get; set; }
        public string? ApproverJobTitle { get; set; }

        public string? AuthorizedOrgName { get; set; }
        public string? AuthorizedFirstName { get; set; }
        public string? AuthorizedLastName { get; set; }
        public string? AuthorizedPhone { get; set; }
        public string? AuthorizedEmail { get; set; }
        public string? AuthorizedJobTitle { get; set; }
    }

    public class CodeSignVerificationInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public CodeSignVerificationInfoDto? verification { get; set; }
    }

    public class CodeSignVerificationInfoPostRequest : CodeSignVerificationInfoDto
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
    }

    public class CodeSignVerificationInfoPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
    }

    /// <summary>
    /// Same fields as old GetContactInfo / UpdateContactInfo (GSEVContactInfo).
    /// </summary>
    public class ContactInfoDto
    {
        public string? ContactFirstName { get; set; }
        public string? ContactLastName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }

        public string? RequestorEmail { get; set; }
        public string? RequestorFirstName { get; set; }
        public string? RequestorLastName { get; set; }
        public string? RequestorOrgName { get; set; }
        public string? RequestorOrgRole { get; set; }
        public string? RequestorOrgUnit { get; set; }
        public string? RequestorPhone { get; set; }

        public string? ApprovarEmail { get; set; }
        public string? ApprovarFirstName { get; set; }
        public string? ApprovarLastName { get; set; }
        public string? ApprovarOrgName { get; set; }
        public string? ApprovarOrgRole { get; set; }
        public string? ApprovarOrgUnit { get; set; }
        public string? ApprovarPhone { get; set; }

        public string? AutherizedEmail { get; set; }
        public string? AutherizedFirstName { get; set; }
        public string? AutherizedLastName { get; set; }
        public string? AutherizedOrgName { get; set; }
        public string? AutherizedOrgRole { get; set; }
        public string? AutherizedPhone { get; set; }
    }

    public class ContactInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public ContactInfoDto? contact { get; set; }
        public string? authenticationType { get; set; }
    }

    public class ContactInfoPostRequest : ContactInfoDto
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
    }

    /// <summary>
    /// Same shape as old UpdateContactInfo Json.
    /// </summary>
    public class ContactInfoPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
    }

    /// <summary>
    /// Parsed CSR fields returned by old UpdateCSRDetail Json data (CSRDetail).
    /// </summary>
    public class CsrDetailDto
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

    /// <summary>
    /// Same fields as old GlobalSign GetCSRInfo (CSRViewModel) + configurationToken.
    /// </summary>
    public class CsrInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? CSR { get; set; }
        public bool IsRenew { get; set; }
        public string? OldOrderId { get; set; }
        public bool? isWildcard { get; set; }
        public int? productId { get; set; }
        /// <summary>Parsed CSR from draft when already validated (same as old Session CSRDetailRow).</summary>
        public CsrDetailDto? data { get; set; }
    }

    /// <summary>
    /// Same fields as old CSRViewModel + configurationToken for UpdateCSRDetail.
    /// </summary>
    public class CsrInfoPostRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
        public string? CSR { get; set; }
        public bool IsRenew { get; set; }
        public string? OldOrderId { get; set; }
    }

    /// <summary>
    /// Same shape as old UpdateCSRDetail Json (IsSuccess, data message or CSRDetail).
    /// </summary>
    public class CsrInfoPostResponse
    {
        public bool IsSuccess { get; set; }
        /// <summary>Error message when IsSuccess is false (old Json "data" string).</summary>
        public string? Msg { get; set; }
        /// <summary>Same as old Json "data" when success (parsed CSR).</summary>
        public CsrDetailDto? data { get; set; }
        public string? configurationToken { get; set; }
        public bool IsRenew { get; set; }
        public string? OldOrderId { get; set; }
    }

    /// <summary>
    /// Same fields as old GetDCVInfo (GSContactInfo DCV subset).
    /// </summary>
    public class DcvInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        /// <summary>1=URL, 2=EMAIL, 3=DNS, 0=unset (same as old GSContactInfo.ApprovalMethod).</summary>
        public int ApprovalMethod { get; set; }
        public string? ApprovalEmail { get; set; }
        public string? OldOrderId { get; set; }
        public bool IsRenew { get; set; }
        public List<SelectListItemDto>? ApprovalEmailList { get; set; }
        public int? productId { get; set; }
        public bool? isWildcard { get; set; }
        public bool? isMultiDomain { get; set; }
        public string? authenticationType { get; set; }
        /// <summary>True when OldGSOrderDetails exists (Alpha renewal UI, same as old).</summary>
        public bool hasRenewalInfo { get; set; }
        public string? renewalDomain { get; set; }
    }

    /// <summary>
    /// Same as old UpdateDCVInfo parameters (ApprovalMethod string FILE/EMAIL/DNS + ApprovalEmail).
    /// </summary>
    public class DcvInfoPostRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
        public string? ApprovalMethod { get; set; }
        public string? ApprovalEmail { get; set; }
        public string? OldOrderId { get; set; }
        public bool IsRenew { get; set; }
    }

    /// <summary>
    /// Same shape as old UpdateDCVInfo Json.
    /// </summary>
    public class DcvInfoPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
        public bool ismulti { get; set; }
    }

    /// <summary>
    /// Same parameters as old DeleteAdditionalDomain.
    /// </summary>
    public class DeleteAdditionalDomainRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
        public string? san { get; set; }
        public bool isMail { get; set; }
        public bool isOWA { get; set; }
        public bool isAutoDiscover { get; set; }
        public string? domainName { get; set; }
    }

    public class DeleteAdditionalDomainResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
        public string? AdditionalDomains { get; set; }
        public List<string>? AdditionalDomainList { get; set; }
    }

    /// <summary>
    /// Same parameters as old DeleteWildCardSAN.
    /// </summary>
    public class DeleteWildCardSanRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
        public string? san { get; set; }
        public bool isMail { get; set; }
        public bool isOWA { get; set; }
        public bool isAutoDiscover { get; set; }
        public string? domainName { get; set; }
    }

    public class DeleteWildCardSanResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
        public List<string>? WidlcardSANDomainList { get; set; }
    }

    /// <summary>
    /// Same parameters as old GetAdditionalDomains.
    /// </summary>
    public class GetAdditionalDomainsRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
        public string? domainName { get; set; }
        public string? additionalDomains { get; set; }
        public string? activeadditionalDomains { get; set; }
        public bool isMail { get; set; }
        public bool isOWA { get; set; }
        public bool isAutoDiscover { get; set; }
        public string? domainType { get; set; }
    }

    /// <summary>
    /// Old GetAdditionalDomains returned a string (HTML fragment or error). Wrap for API.
    /// </summary>
    public class GetAdditionalDomainsResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
        public string? AdditionalDomains { get; set; }
        public List<string>? AdditionalDomainList { get; set; }
        public Dictionary<string, string>? AdditionalDomainsList { get; set; }
    }

    /// <summary>
    /// Same parameters as old GetWildCardSans.
    /// </summary>
    public class GetWildCardSansRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
        public string? WildcardSAN { get; set; }
        public bool isMail { get; set; }
        public bool isOWA { get; set; }
        public bool isAutoDiscover { get; set; }
        public string? domainName { get; set; }
    }

    public class GetWildCardSansResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
        public List<string>? WidlcardSANDomainList { get; set; }
    }

    /// <summary>
    /// Same fields as old GSEVOrganizationInformation used by Get/UpdateOrganisationInfo.
    /// </summary>
    public class OrganisationInfoDto
    {
        public string? Orgname { get; set; }
        public string? DBAname { get; set; }
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? PhoneNo { get; set; }
        public string? Duns { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? CountryName { get; set; }
        public string? CountryCode { get; set; }
        public string? PostalCode { get; set; }
        public string? Email { get; set; }
        public string? Division { get; set; }
        public string? Fax { get; set; }
        public string? OrgType { get; set; }
        public string? JurictionCity { get; set; }
        public string? jurictionState { get; set; }
        public string? JurictionCountryName { get; set; }
        public string? JurictionCountryCode { get; set; }
        public string? AgencyRegNumber { get; set; }
        public string? BusinessCategory { get; set; }
        public string? CoRegistrationNumber { get; set; }
        public string? OrganizationCountryName { get; set; }
    }

    public class OrganisationInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public OrganisationInfoDto? organisation { get; set; }
        public List<SelectListItemDto>? CountryList { get; set; }
        public string? authenticationType { get; set; }
    }

    public class OrganisationInfoPostRequest : OrganisationInfoDto
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
    }

    /// <summary>
    /// Same shape as old UpdateOrganisationInfo Json.
    /// </summary>
    public class OrganisationInfoPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
        public string? countryValue { get; set; }
    }

    /// <summary>
    /// Same fields as old GetSANInfo (GSUccSanDomainInfo).
    /// </summary>
    public class SanInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public bool IsMail { get; set; }
        public bool IsOWA { get; set; }
        public bool IsAutoDiscover { get; set; }
        public string? DomainName { get; set; }
        public string? AdditionalDomains { get; set; }
        public List<string>? AdditionalDomainList { get; set; }
        public int NoOfAdditionalDomains { get; set; }
        public int MaxWildcardSAN { get; set; }
        public List<string>? WidlcardSANDomainList { get; set; }
        public string? WildcardSAN { get; set; }
        public List<SelectListItemDto>? AdditionalDomainType { get; set; }
        public bool? isWildcardMultiDomain { get; set; }
        public bool? isMultiDomain { get; set; }
        public bool? isFlex { get; set; }
        public string? authenticationType { get; set; }
        public string? csrDomainName { get; set; }
        public int? productId { get; set; }
        public string? primarydomainName { get; set; }
        public int? TotalNoOfSANAllowed { get; set; }
    }

    /// <summary>
    /// Same fields as old UpdateSANInfo (GSUccSanDomainInfo) + configurationToken.
    /// </summary>
    public class SanInfoPostRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
        public bool IsMail { get; set; }
        public bool IsOWA { get; set; }
        public bool IsAutoDiscover { get; set; }
        public string? DomainName { get; set; }
        public string? AdditionalDomains { get; set; }
    }

    /// <summary>
    /// Same shape as old UpdateSANInfo Json.
    /// </summary>
    public class SanInfoPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? configurationToken { get; set; }
    }

    /// <summary>
    /// Simple dropdown option (replaces MVC SelectListItem in JSON).
    /// </summary>
    public class SelectListItemDto
    {
        public string? Text { get; set; }
        public string? Value { get; set; }
        public bool Selected { get; set; }
    }

    /// <summary>
    /// Same as old SetCSR(bool isCSRSaved).
    /// </summary>
    public class SetCsrRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
        public bool isCSRSaved { get; set; }
    }

    /// <summary>
    /// Old SetCSR returned "success" / "error" string — wrapped for API.
    /// </summary>
    public class SetCsrResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
    }

    /// <summary>
    /// Summary GET — draft data for UI (old returned PF_Request partial).
    /// </summary>
    public class SummaryGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? CSR { get; set; }
        public string? domainName { get; set; }
        public string? ApprovalMethod { get; set; }
        public string? ApprovalEmail { get; set; }
        public string? AdditionalDomains { get; set; }
        public List<string>? AdditionalDomainList { get; set; }
        public List<string>? WidlcardSANDomainList { get; set; }
        public string? authenticationType { get; set; }
        public bool? isMultiDomain { get; set; }
        public bool? isWildcard { get; set; }
        public bool? isWildcardMultiDomain { get; set; }
        public int? productId { get; set; }
        public int? storeOrderId { get; set; }
        public bool isMultiYearOrder { get; set; }
        public bool isCSRSaved { get; set; }
        public CsrDetailDto? csrDetail { get; set; }
        public ContactInfoDto? contact { get; set; }
        public OrganisationInfoDto? organisation { get; set; }
        public string? GS_SAN_MailDomain { get; set; }
        public string? GS_SAN_OWADomain { get; set; }
        public string? GS_SAN_AutoDiscoverDomain { get; set; }
    }

    public class SummaryPostRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
    }

    /// <summary>
    /// Same shape as old UpdateSummary Json (IsSuccess, Msg, returnUrl).
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
    /// Same fields as old Thanks view (PF_Response + ApprovalMethod from draft).
    /// </summary>
    public class ThanksResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? VendorID { get; set; }
        public string? ApprovalEmail { get; set; }
        public string? ApprovalMethod { get; set; }
        public string? GSURLMetaTag { get; set; }
        public string[]? GSURLs { get; set; }
    }

    /// <summary>Same fields as old GlobalSignVMCContactDetail.</summary>
    public class VmcContactInfoDto
    {
        public string? RequestorTitle { get; set; }
        public string? RequestorFirstName { get; set; }
        public string? RequestorLastName { get; set; }
        public string? RequestorPhoneNo { get; set; }
        public string? RequestorEmail { get; set; }
        public string? RequestorCountryName { get; set; }
        public string? RequestorOrgName { get; set; }
        public string? ApproverTitle { get; set; }
        public string? ApproverFirstName { get; set; }
        public string? ApproverLastName { get; set; }
        public string? ApproverEmail { get; set; }
        public string? ApproverPhoneNo { get; set; }
        public string? ApproverOrgName { get; set; }
        public string? ContactTitle { get; set; }
        public string? ContactFirstName { get; set; }
        public string? ContactLastName { get; set; }
        public string? ContactPhoneNo { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactOrgName { get; set; }
    }

    public class VmcContactInfoPostRequest : VmcContactInfoDto
    {
        public string? configurationToken { get; set; }
    }

    /// <summary>Same fields as old GlobalSignVMCCSRModel GET.</summary>
    public class VmcCsrInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? DomainName { get; set; }
        public string? Organisation { get; set; }
        public string? Locality { get; set; }
        public string? State { get; set; }
        public string? DomainCountryName { get; set; }
        public List<SelectListItemDto>? CountryList { get; set; }
    }

    /// <summary>Same fields as old UpdateVMC_CSRInfo POST.</summary>
    public class VmcCsrInfoPostRequest
    {
        public string? configurationToken { get; set; }
        public string? DomainName { get; set; }
        public string? Organisation { get; set; }
        public string? Locality { get; set; }
        public string? State { get; set; }
        public string? DomainCountryName { get; set; }
    }
}
