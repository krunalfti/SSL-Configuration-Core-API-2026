namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.SymantecOrganizationInfo.
    /// </summary>
    public partial class SymantecOrganizationInfo
    {
        public int SymantecOrganizationInfoID { get; set; }
        public string? LegalName { get; set; }
        public string? AssumedName { get; set; }
        public string? Division { get; set; }
        public string? Duns { get; set; }
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? ZipCode { get; set; }
        public string? PhoneNo { get; set; }
        public string? Fax { get; set; }
        public long? DigicertOrganizationId { get; set; }
        public bool? IsModified { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
