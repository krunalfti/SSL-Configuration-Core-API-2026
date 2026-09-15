using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSLConfiguration.Contracts.SSLConfiguration.ACME
{
    public class AccountInfo
    {
        public string? EABID { get; set; }
        public string? AccountStatus { get; set; }
        public string? IpAddress { get; set; }
        public string? EABKey { get; set; }
        public DateTime? LastActivity { get; set; }
        public string? EABIDUrl { get; set; }
        public string? UserAgent { get; set; }
        public List<ContactsInfo>? Contacts { get; set; }
    }
    public class AcemeDomains
    {
        public string? domainName { get; set; }
    }
    public class AcemeOvDomains
    {
        public string? domainName { get; set; }
        public DateTime? expireDate { get; set; }
        public string? organisationNumber { get; set; }
    }
    public class ACMEDetailViewModel
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }

        public string? EabId { get; set; }
        public string? EabKey { get; set; }
        public string? EABAccountStatus { get; set; }
        public string? ServerUrl { get; set; }
        public List<AcemeDomains>? Domains { get; set; }
        public List<AccountInfo>? Accounts { get; set; }
        public string? IpAddress { get; set; }
        public string? LastActivity { get; set; }
        public string? UserAgent { get; set; }
        public string? ApprovalEmail { get; set; }
        public string? domainName { get; set; }
        public int MaxSAN { get; set; }
        public int MaxWildcardSAN { get; set; }
        public List<GetDomains>? domains { get; set; }
        public DateTime? SubscriptionStartDate { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public string? OrderStatus { get; set; }
        public int RemainingMaxSAN { get; set; }
        public int RemainingMaxWildcardSAN { get; set; }
        public int MaxSanUsed { get; set; }
        public int MaxWildUsed { get; set; }
        public long AcmeOrderNo { get; set; }
        public string? SelectedOrgID { get; set; }
        public string? OrgName { get; set; }

        /// <summary>Text/Value list (same role as old SelectListItem OrganisationList).</summary>
        public List<OrganisationListItem>? OrganisationList { get; set; }
        public List<ACMEOrganisation>? FullOrganisationData { get; set; }
        public List<AcemeOvDomains>? AcmeOvDomains { get; set; }

        /// <summary>Was ViewBag.CountryList on ACMEOVInfo.</summary>
        public List<OrganisationListItem>? CountryList { get; set; }
    }

    /// <summary>Same shape as old System.Web.Mvc.SelectListItem for OrganisationList / CountryList.</summary>
    public class OrganisationListItem
    {
        public string? Text { get; set; }
        public string? Value { get; set; }
        public bool Selected { get; set; }
    }
    public class AcmeJsonResponse
    {
        public bool IsSuccess { get; set; }
        public string? Msg { get; set; }
        public string? configurationToken { get; set; }
        public string? domainName { get; set; }
        public List<GetDomains>? domains { get; set; }
        public int? MaxSAN { get; set; }
        public int? MaxWildcardSAN { get; set; }
        public int? SanUsed { get; set; }
        public int? WildUsed { get; set; }
    }
    public class ACMEOrganisation
    {
        public int ACMEOrganisationID { get; set; }
        public string? OrgName { get; set; }
        public string? OrgUnit { get; set; }
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? Address3 { get; set; }
        public string? Locality { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? Title { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNo { get; set; }
        public int UserID { get; set; }
        public string? SectigoOrgID { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
        public string? OrgStatus { get; set; }
        public string? SpecialNote { get; set; }
        public string? CACredentialCode { get; set; }
        public DateTime? OrgStartDate { get; set; }
        public DateTime? OrgEndDate { get; set; }
        public bool IsDeleted { get; set; }
    }
    public class AcmeOVOrganisationRequest
    {
        public string? Pin { get; set; }
        public string? AuthKey { get; set; }
    }
    public class AcmeOVOrganisationResponse
    {
        public int StatusCode { get; set; }
        public List<ACMEOrganisation>? ACMEOrganisationList { get; set; }
    }
    public class AcmeTokenRequest
    {
        public string? configurationToken { get; set; }
        public string? pin { get; set; }
    }
    public class AddDomainRequest : AcmeTokenRequest
    {
        public string? domainname { get; set; }
        public string? status { get; set; }
        public string? acmeOrgId { get; set; }
        public string? acmeOrgName { get; set; }
    }
    public class ContactsInfo
    {
        public string? EmailAddress { get; set; }
    }
    public class GetDomains
    {
        public string? domainName { get; set; }
    }
    public class UpdateAccountStatusRequest : AcmeTokenRequest
    {
        public string? accountId { get; set; }
        public string? actionStatus { get; set; }
    }
}
