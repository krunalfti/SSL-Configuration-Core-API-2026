namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity name/properties as old SSLConfiguration_DatabaseObjects.CACredential.
    /// </summary>
    public partial class CACredential
    {
        public int CACredentialID { get; set; }
        public string? CredentialCode { get; set; }
        public string? CAName { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? ContractId { get; set; }
        public string? PartnerCode { get; set; }
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public string? DigicertAPIKey { get; set; }
    }
}
