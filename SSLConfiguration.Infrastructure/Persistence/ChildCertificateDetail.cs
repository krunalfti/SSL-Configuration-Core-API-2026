namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.ChildCertificateDetail.
    /// </summary>
    public partial class ChildCertificateDetail
    {
        public int ChildCertificateDetailId { get; set; }
        public int StoreOrderId { get; set; }
        public string? SSLCertificateId { get; set; }
        public DateTime DateAdded { get; set; }
        public bool IsOriginal { get; set; }
        public string? CertAction { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string? DomainName { get; set; }
        public string? ApiOrderNo { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? OrderStatus { get; set; }
        public bool? IsFetchStatus { get; set; }
        public int? ValidityInDays { get; set; }
    }
}
