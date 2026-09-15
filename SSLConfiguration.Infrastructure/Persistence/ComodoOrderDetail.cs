namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.ComodoOrderDetail.
    /// </summary>
    public partial class ComodoOrderDetail
    {
        public int ComodoOrderDetailId { get; set; }
        public int StoreOrderId { get; set; }
        public int CSRDetailID { get; set; }
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
        public string? ApprovalEmail { get; set; }
        public string? JurictionCity { get; set; }
        public string? JurictionState { get; set; }
        public string? JurictionCountry { get; set; }
        public DateTime? DateOfIncorporation { get; set; }
        public string? DoingBusinessAs { get; set; }
        public string? CSR_MD5 { get; set; }
        public string? CSR_SHA1 { get; set; }
        public int? RequestorId { get; set; }
        public int? ApproverId { get; set; }
        public int? ContractSignerId { get; set; }
        public string? InCorporationAgency { get; set; }
        public string? InCorporationPhoneNo { get; set; }
        public string? DVMethod { get; set; }
        public string? OrgDuns { get; set; }
        public string? OrgCompanyRegNumber { get; set; }
        public string? UniqueValue { get; set; }
        public string? AgreementEmail { get; set; }
        public string? DNSTXTValue { get; set; }
        public string? PACTitle { get; set; }
        public string? PACEmail { get; set; }
        public string? PACUserName { get; set; }
        public string? PACPassword { get; set; }
        public string? CodeSignType { get; set; }
        public string? CodeSignPublisherEmail { get; set; }
        public string? CodeSignProvisioningMethod { get; set; }
        public string? CodeSignHSMType { get; set; }
        public string? CodeSignShippingCode { get; set; }
        public string? ShippingFirstName { get; set; }
        public string? ShippingLastName { get; set; }
        public string? ShippingAddress1 { get; set; }
        public string? ShippingAddress2 { get; set; }
        public string? ShippingAddress3 { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingState { get; set; }
        public string? ShippingCountry { get; set; }
        public string? ShippingPostalCode { get; set; }
        public string? ShippingPhone { get; set; }
        public string? ShippingEmail { get; set; }
    }
}
