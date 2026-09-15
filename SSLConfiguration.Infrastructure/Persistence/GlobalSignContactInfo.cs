namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.GlobalSignContactInfo.
    /// </summary>
    public partial class GlobalSignContactInfo
    {
        public int GlobalSignContactInfoID { get; set; }
        public string? OrganizationName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        public string? OrgUnit { get; set; }
        public string? FunctionInOrg { get; set; }
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? ZipCode { get; set; }
        public string? Duns { get; set; }
        public string? Title { get; set; }
    }
}
