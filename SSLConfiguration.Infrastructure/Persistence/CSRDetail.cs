namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.CSRDetail.
    /// </summary>
    public partial class CSRDetail
    {
        public int CSRDetailId { get; set; }
        public string? DNSNames { get; set; }
        public string? DomainName { get; set; }
        public string? PrimaryDomainName { get; set; }
        public string? Email { get; set; }
        public string? Locality { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Organisation { get; set; }
        public string? OrganisationUnit { get; set; }
        public bool? IsCSRSaved { get; set; }
        public string? CSR { get; set; }
    }
}
