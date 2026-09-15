namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>Same as old SSLConfiguration_DatabaseObjects.AcmeDomainHistory.</summary>
    public partial class AcmeDomainHistory
    {
        public int AcmeDomainHistoryId { get; set; }
        public int StoreOrderId { get; set; }
        public string? DomainName { get; set; }
        public string? Action { get; set; }
        public string? Remark { get; set; }
        public DateTime DateAdded { get; set; }
        public string? AcmeOrderNo { get; set; }
        public string? AcmeOrgId { get; set; }
        public string? AcmeOrgName { get; set; }
    }
}
