namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.DigicertCertificateDetail.
    /// </summary>
    public partial class DigicertCertificateDetail
    {
        public int DigicertCertificateDetailId { get; set; }
        public int StoreOrderId { get; set; }
        public int OrganizationContactId { get; set; }
        public int? TechnicalContactId { get; set; }
        public int? ApproverContactId { get; set; }
        public int CSRDetailId { get; set; }
        public int? OrganizationInfoId { get; set; }
        public string? ApprovalEmail { get; set; }
        public string? WebServerType { get; set; }
        public string? CertificateType { get; set; }
        public string? DCVMethod { get; set; }
        public string? DCVRandomValue { get; set; }
        public string? CodeSignProvisioningMethod { get; set; }
        public string? ShippingMethod { get; set; }
        public string? ShippingName { get; set; }
        public string? ShippingAddress1 { get; set; }
        public string? ShippingAddress2 { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingState { get; set; }
        public string? ShippingCountry { get; set; }
        public string? ShippingPostalCode { get; set; }
        public string? IssuingCA { get; set; }
        public string? DCVScope { get; set; }
        public string? CodeSignHardwareInitToken { get; set; }
        public string? DNSRecordId { get; set; }
        public string? KeyUsages { get; set; }
        public string? ExtendedKeyUsages { get; set; }
    }
}
