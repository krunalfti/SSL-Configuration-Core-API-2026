namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.SymantecContactInfo.
    /// </summary>
    public partial class SymantecContactInfo
    {
        public int SymantecContactInfoID { get; set; }
        public string? Title { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNo { get; set; }
    }
}
