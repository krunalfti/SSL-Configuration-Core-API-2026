namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.GlobalSignOrganizationInfo (IssueCertificate subset).
    /// </summary>
    public partial class GlobalSignOrganizationInfo
    {
        public int GlobalSignOrganizationInfoID { get; set; }
        public int StoreOrderId { get; set; }
        public string? LegalName { get; set; }
        public string? AssumedName { get; set; }
        public string? Email { get; set; }
        public string? Division { get; set; }
        public string? Duns { get; set; }
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? ZipCode { get; set; }
        public string? PhoneNo { get; set; }
        public string? Fax { get; set; }
        public string? JurictionRegNo { get; set; }
        public string? JurictionCity { get; set; }
        public string? JurictionState { get; set; }
        public string? JurictionCountry { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? RegisteredMarkLicenseExpiryDate { get; set; }
        public string? TrademarkIdentifier { get; set; }
        public string? TrademarkCountryOrRegionName { get; set; }
        public string? TrademarkOfficeName { get; set; }
        public string? BusinessCategory { get; set; }
        public string? CorporateRegistrationNumber { get; set; }
        public string? TrademarkURL { get; set; }
    }
}
