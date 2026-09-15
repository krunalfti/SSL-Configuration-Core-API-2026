namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.DigicertAdditionalDomain
    /// (table DigicertAdditionalDomains).
    /// </summary>
    public partial class DigicertAdditionalDomain
    {
        public int AdditionalDomainId { get; set; }
        public int StoreOrderId { get; set; }
        public string? DomainName { get; set; }
        public bool IsPrimaryDomain { get; set; }
        public bool IsConsiderAsSAN { get; set; }
        public string? ApprovalEmail { get; set; }
        public string? DCVMethod { get; set; }
        public string? DCVRandomValue { get; set; }
        public long? DomainId { get; set; }
        public long? OrganizationId { get; set; }
        public string? SpecialNote { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
