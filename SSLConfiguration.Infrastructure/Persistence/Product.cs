namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity name/properties as old SSLConfiguration_DatabaseObjects.Product
    /// (used by BLGeneral.GetProductDetail_StoreOrder).
    /// </summary>
    public partial class Product
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? BrandName { get; set; }
        public bool IsWildcard { get; set; }
        public string? AuthenticationType { get; set; }
        public bool IsMultiDomain { get; set; }
        public bool IsWildcardMultiDomain { get; set; }
        public bool IsFlex { get; set; }
        public bool IsCodeSign { get; set; }
        public bool IsPAC { get; set; }
        public bool IsX9 { get; set; }
    }
}
