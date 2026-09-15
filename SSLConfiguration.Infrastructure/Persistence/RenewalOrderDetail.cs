namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.RenewalOrderDetail.
    /// </summary>
    public partial class RenewalOrderDetail
    {
        public int RenewalOrderDetailID { get; set; }
        public int SSLApiLinkId { get; set; }
        public string? ApiOrderNo { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? OrderStatus { get; set; }
        public int? ProductId { get; set; }
        public string? CompanyName { get; set; }
        public string? DomainName { get; set; }
        public int? NewRenewalOrderID { get; set; }
    }
}
