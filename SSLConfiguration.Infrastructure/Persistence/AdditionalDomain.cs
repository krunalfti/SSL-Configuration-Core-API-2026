namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.AdditionalDomain.
    /// </summary>
    public partial class AdditionalDomain
    {
        public int AdditionalDomainId { get; set; }
        public int StoreOrderId { get; set; }
        public string? DomainName { get; set; }
        public string? ApprovalEmail { get; set; }
        public bool IsPrimaryDomain { get; set; }
        public bool IsConsiderAsSAN { get; set; }
        public string? SpecialNote { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public bool? IsRemoveFromApi { get; set; }
        public string? AcmeOrderNo { get; set; }
        public string? AcmeOrgId { get; set; }
        public string? AcmeOrgName { get; set; }
    }
}
