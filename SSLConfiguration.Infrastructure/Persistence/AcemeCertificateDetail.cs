namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>Same as old SSLConfiguration_DatabaseObjects.AcemeCertificateDetail.</summary>
    public partial class AcemeCertificateDetail
    {
        public int AcemeDetailsId { get; set; }
        public string? AcmeAccountID { get; set; }
        public int StoreOrderId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string? AuthenticationType { get; set; }
        public string? ServerUrl { get; set; }
    }
}
