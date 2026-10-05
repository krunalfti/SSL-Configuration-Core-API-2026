using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using SSLConfiguration.Contracts.SSLConfiguration.GlobalSign;

namespace SSLConfiguration.Contracts.SSLConfiguration.Digicert
{
    /// <summary>
    /// Same JSON shape as old DigicertController Json results (IsSuccess / Msg).
    /// </summary>
    public class DigicertJsonResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? CSRDetailJson { get; set; }
        public string? objCSRResJson { get; set; }
        public string? objEmailListJson { get; set; }
        public string? objccsshippingJson { get; set; }
        public string? countrycode { get; set; }
        public string? ApproverEmail { get; set; }
        public string? IntermediateCAText { get; set; }
        public int usedWildcardSan { get; set; }
        public int allowedMaxWildcardSan { get; set; }
    }

    public class CSRInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? CSR { get; set; }
        public string? domainName { get; set; }
        public bool? isFreeSANInclude { get; set; }
        public bool? isX9 { get; set; }
        public bool? isWildcard { get; set; }
        public string? organisation { get; set; }
        public string? organisationUnit { get; set; }
        public string? locality { get; set; }
        public string? state { get; set; }
        public string? country { get; set; }
        public string? countryName { get; set; }
        public string? email { get; set; }
    }

    /// <summary>
    /// Shared Digicert wizard request: pin starts draft; configurationToken continues it (replaces Session).
    /// </summary>
    public class DigicertSessionRequest
    {
        public string? pin { get; set; }
        public string? configurationToken { get; set; }
    }

    public class SanMutationRequest
    {
        public string? configurationToken { get; set; }
        public string? additionalDomains { get; set; }
        public string? additionalWildcardDomainNames { get; set; }
        public string? sanDomainName { get; set; }
        public string? approvalMethod { get; set; }
        public string? approvalMethodOrEmail { get; set; }
    }

    public class UpdateFreeSANTagRequest
    {
        public string? configurationToken { get; set; }
        public bool IsFreeSANInclude { get; set; }
    }

    public class SummaryPostRequest
    {
        public string? configurationToken { get; set; }
    }

    public class DigicertContactDetailDto
    {
        public string? configurationToken { get; set; }
        public string? WebServerType { get; set; }
        public string? IntermediateCAId { get; set; }
        public string? IntermediateCAText { get; set; }

        public string? TechnicalTitle { get; set; }
        public string? TechnicalFirstName { get; set; }
        public string? TechnicalLastName { get; set; }
        public string? TechnicalEmail { get; set; }
        public string? TechnicalPhoneNo { get; set; }

        public string? AdminTitle { get; set; }
        public string? AdminFirstName { get; set; }
        public string? AdminLastName { get; set; }
        public string? AdminEmail { get; set; }
        public string? ConfirmAdminEmail { get; set; }
        public string? AdminPhoneNo { get; set; }

        public string? ApproverTitle { get; set; }
        public string? ApproverFirstName { get; set; }
        public string? ApproverLastName { get; set; }
        public string? ApproverEmail { get; set; }
        public string? ConfirmApproverEmail { get; set; }
        public string? ApproverPhoneNo { get; set; }
    }

    /// <summary>
    /// GET ContactInfo response.
    /// </summary>
    public class ContactInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? authenticationType { get; set; }
        public DigicertContactDetailDto? contact { get; set; }
    }

    /// <summary>
    /// GET SANInfo / SANInfo_DV response (was DigicertAdditionalDomains + PartialView).
    /// </summary>
    public class SANInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? PrimaryDomainName { get; set; }
        public string? DomainName { get; set; }
        public int NoOfAdditionalDomains { get; set; }
        public int NoOfAdditionalWildCardDomains { get; set; }
        public int TotalNoOfSANAllowed { get; set; }
        public Dictionary<string, string>? AdditionalDomainList { get; set; }
        public Dictionary<string, string>? AdditionalWildCardDomainList { get; set; }
        public bool? isMultiDomain { get; set; }
        public bool? isWildcard { get; set; }
        public bool? isWildcardMultiDomain { get; set; }
        public bool? isFlex { get; set; }
        public int MaxSAN { get; set; }
        public int MaxWildcardSAN { get; set; }
    }

    public class VMCInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? DomainName { get; set; }
        public string? Logo { get; set; }
        public string? FileBase64 { get; set; }
        public string? FileName { get; set; }
        public bool EnableHosting { get; set; }
        public string? MarkType { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? CountryCode { get; set; }
        public bool IsLogoExists { get; set; }
        public DateTime? TrademarkExpiryDate { get; set; }
        public string? TrademarkIdentifier { get; set; }
        public string? CountryName { get; set; }
        public string? Trademarkoffice { get; set; }
        public string? TrademarkURL { get; set; }
        public List<GlobalSign.SelectListItemDto>? TrademarkCountryList { get; set; }
        public string? TrademarkCountryOfficeData { get; set; }
    }

    /// <summary>
    /// GET DCVInfo / DCVInfo_DV response (was PartialView + ViewBag.ApprovalEmailList).
    /// </summary>
    public class DCVInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? domainName { get; set; }
        public string? authenticationType { get; set; }
        public int? approvalMethod { get; set; }
        public string? approverEmail { get; set; }
        public string? dcvScope { get; set; }
        public List<SelectListItemDto>? approvalEmailList { get; set; }
    }

    public class SelectListItemDto
    {
        public string? Value { get; set; }
        public string? Text { get; set; }
        public bool Selected { get; set; }
    }

    public class CodeSignCsrInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? CSR { get; set; }
        public string? CodeSignProvisioningMethod { get; set; }
        public int ServerHardwarePlatformId { get; set; }
        public string? IntermediateCAId { get; set; }
        public DigicertShippingInformationDto? Shipping { get; set; }
        public List<SelectListItemDto>? CodeSignProvisioningMethods { get; set; }
        public List<SelectListItemDto>? CodeSignServerHardwarePlatforms { get; set; }
        public List<SelectListItemDto>? CountryList { get; set; }
    }

    public class ValidateOrganizationRequest
    {
        public string? configurationToken { get; set; }
        public string? orgName { get; set; }
        public string? countryCode { get; set; }
    }

    /// <summary>
    /// Same shape as old ValidateOrganization Json (IsSuccess + objModel).
    /// </summary>
    public class ValidateOrganizationResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public DigicertOrganizationInformationDto? objModel { get; set; }
        public long DigicertOrganizationId { get; set; }
    }

    public class EditOrganizationRequest
    {
        public string? configurationToken { get; set; }
    }

    /// <summary>
    /// POST DCVInfo body (ApprovalMethod, ApprovalEmail).
    /// </summary>
    public class DCVInfoPostRequest
    {
        public string? configurationToken { get; set; }
        public string? ApprovalMethod { get; set; }
        public string? ApprovalEmail { get; set; }
    }

    /// <summary>
    /// GET Summary — returns draft summary fields (was PartialView _DigicertSummary).
    /// </summary>
    public class SummaryGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? domainName { get; set; }
        public string? productName { get; set; }
        public string? webServerType { get; set; }
        public string? approverEmail { get; set; }
        public int? approvalMethod { get; set; }
        public string? authenticationType { get; set; }
        public Dictionary<string, string>? additionalDomainList { get; set; }
        public Dictionary<string, string>? additionalWildCardDomainList { get; set; }
        public DigicertOrganizationInformationDto? organization { get; set; }
        public DigicertContactDetailDto? contact { get; set; }
    }

    public class DigicertOrganizationInformationDto
    {
        public string? configurationToken { get; set; }
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
        public string? Division { get; set; }
        public string? Fax { get; set; }
    }

    public class CodeSignCsrInfoPostRequest
    {
        public string? configurationToken { get; set; }
        public string? CSR { get; set; }
        public string? CodeSignProvisioningMethod { get; set; }
        public int ServerHardwarePlatformId { get; set; }
        public string? IntermediateCAId { get; set; }
        public DigicertShippingInformationDto? Shipping { get; set; }
    }

    /// <summary>
    /// POST DCVInfo_DV body (ApprovalMethod, ApprovalEmail, dcvScope).
    /// </summary>
    public class DCVInfoDvPostRequest
    {
        public string? configurationToken { get; set; }
        public string? ApprovalMethod { get; set; }
        public string? ApprovalEmail { get; set; }
        public string? dcvScope { get; set; }
    }

    public class GetApprovalEmailListRequest
    {
        public string? configurationToken { get; set; }
        public string? sanDomainName { get; set; }
    }

    public class GetApprovalEmailListResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public List<SelectListItemDto>? ApprovalEmailList { get; set; }
    }

    public class DigicertShippingInformationDto
    {
        public string? ShippingName { get; set; }
        public string? ShippingAddress1 { get; set; }
        public string? ShippingAddress2 { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingState { get; set; }
        public string? ShippingCountryCode { get; set; }
        public string? ShippingCountryName { get; set; }
        public string? ShippingPostalCode { get; set; }
    }

    public class ProfileKeyUsageGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? KeyUsages { get; set; }
        public string? ExtendedKeyUsages { get; set; }
        public List<SelectListItemDto>? KeyUsageList { get; set; }
        public List<SelectListItemDto>? ExtendedKeyUsageList { get; set; }
    }

    public class VMCInfoPostRequest
    {
        public string? configurationToken { get; set; }
        public string? DomainName { get; set; }
        public string? Logo { get; set; }
        public string? MarkType { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? CountryCode { get; set; }
        public bool IsLogoExists { get; set; }
        /// <summary>SVG logo content (UTF-8 text) or base64-encoded SVG.</summary>
        public string? strLogo { get; set; }
        // File information sent through JSON
        public string? FileBase64 { get; set; }
        public string? FileName { get; set; }
        public DateTime? TrademarkExpiryDate { get; set; }
        public string? TrademarkIdentifier { get; set; }
        public string? CountryName { get; set; }
        public string? Trademarkoffice { get; set; }
        public string? TrademarkURL { get; set; }
    }

    /// <summary>
    /// Same shape as old DigicertController.Summary POST Json.
    /// </summary>
    public class SummaryPostResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? returnUrl { get; set; }
        public string? DigicertOrderNumber { get; set; }
        public string? DigicertCertificateId { get; set; }
    }

    public class DigicertEntryResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? authenticationType { get; set; }
        public int? storeOrderId { get; set; }
        public int? productId { get; set; }
        public bool? isWildcard { get; set; }
        public bool? isMultiDomain { get; set; }
        public bool? isX9 { get; set; }
        public bool? isFreeSANInclude { get; set; }

        /// <summary>Optional UI redirect (e.g. Comodo ACME products).</summary>
        public string? nextController { get; set; }
        public string? nextAction { get; set; }
        public string? nextArea { get; set; }
    }

    /// <summary>
    /// Same fields as old CSRViewModel + configurationToken.
    /// </summary>
    public class CSRInfoPostRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
        public string? CSR { get; set; }
    }

    /// <summary>
    /// GET OrganizationInfo response.
    /// </summary>
    public class OrganizationInfoGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public DigicertOrganizationInformationDto? organization { get; set; }
        public List<SelectListItemDto>? CountryList { get; set; }
        public long? DigicertOrganizationId { get; set; }
        public bool? IsOrgDetailChange { get; set; }
    }

    public class ProfileKeyUsagePostRequest
    {
        public string? configurationToken { get; set; }
        public string? KeyUsages { get; set; }
        public string? ExtendedKeyUsages { get; set; }
    }

    public class SANInfoPostRequest
    {
        public string? configurationToken { get; set; }
    }

    public class SetCSRRequest
    {
        public string? configurationToken { get; set; }
        public bool isCSRSaved { get; set; }
    }

    /// <summary>
    /// GET ServerTypeAdminEmail response (was PartialView + ViewBag lists).
    /// </summary>
    public class ServerTypeAdminEmailGetResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? WebServerType { get; set; }
        public string? IntermediateCAId { get; set; }
        public string? IntermediateCAText { get; set; }
        public List<SelectListItemDto>? WebServerList { get; set; }
        public List<SelectListItemDto>? AllowedCACerts { get; set; }
    }
}
