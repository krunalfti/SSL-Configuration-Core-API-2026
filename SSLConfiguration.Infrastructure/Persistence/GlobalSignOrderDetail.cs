namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.GlobalSignOrderDetail (IssueCertificate subset).
    /// </summary>
    public partial class GlobalSignOrderDetail
    {
        public int GlobalSignOrderDetailID { get; set; }
        public int StoreOrderId { get; set; }
        public int CSRDetailId { get; set; }
        public int GlobalSignOrganizationInfoID { get; set; }
        public string? ApprovalEmail { get; set; }
        public int? RequestorInfoId { get; set; }
        public int? ApprovalInfoId { get; set; }
        public int? AuthorizedInfoId { get; set; }
        public int? ContactInfoId { get; set; }
        public string? URLMetaTag { get; set; }
        public string? URLVerificationDomains { get; set; }
        public string? DVMethod { get; set; }
        public string? PickupPassword { get; set; }
        public string? DNSRecordId { get; set; }
    }
}
