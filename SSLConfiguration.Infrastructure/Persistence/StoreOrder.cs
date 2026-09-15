namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity name/properties as old SSLConfiguration_DatabaseObjects.StoreOrder
    /// (fields needed for Home Index PIN flow and related BL).
    /// </summary>
    public partial class StoreOrder
    {
        public int StoreOrderId { get; set; }
        public int StoreId { get; set; }
        public int SSLApiLinkId { get; set; }
        public string? Pin { get; set; }
        public string? ApiOrderNo { get; set; }
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? CompanyName { get; set; }
        public int Year { get; set; }
        public int? San { get; set; }
        public int? MinSAN { get; set; }
        public string? CredentialCode { get; set; }
        public bool IsUsed { get; set; }
        public bool IsActive { get; set; }
        public bool IsCancel { get; set; }
        public DateTime? CancelledDate { get; set; }
        public DateTime ValidTillDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public string? SpecialNote { get; set; }
        public bool? IsMultiDomain { get; set; }
        public string? LanguageCode { get; set; }
        public bool? IsLock { get; set; }
        public int? WildcardSAN { get; set; }
        public bool? IsSubscription { get; set; }
        public string? OldApiOrderNo { get; set; }
        public bool? IsMovedToDigicert { get; set; }
        public string? DigiCertOrderNo { get; set; }
        public bool? IsLinkExpired { get; set; }
        public DateTime? LinkExpiredDate { get; set; }
        public string? UserAgent { get; set; }
        public bool? IsCAMYP { get; set; }
        public int? CAOrderValidity { get; set; }
        public DateTime? CAOrderValidFrom { get; set; }
        public DateTime? CAOrderValidTo { get; set; }
        public string? CodeSignProvisioningMethod { get; set; }
        public string? CodeSignShippingCode { get; set; }
        public bool? IsWildcard { get; set; }
        public string? AuthenticationType { get; set; }
        public bool? IsWildcardMultiDomain { get; set; }
        public bool? IsFlex { get; set; }
        public bool? IsCodeSign { get; set; }
        public bool? IsPAC { get; set; }
        public int? RemainingValidity { get; set; }
        public bool? IsStoreMYP { get; set; }
        public DateTime? StoreOrderValidFrom { get; set; }
        public DateTime? StoreOrderValidTo { get; set; }
        public int? SubscriptionYear { get; set; }
        public bool? IsAllowIssueCertificate { get; set; }
        public string? ApiToken { get; set; }
        public bool IsX9 { get; set; }
    }
}
