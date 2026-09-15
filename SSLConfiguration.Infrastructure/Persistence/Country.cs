namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.Country.
    /// </summary>
    public partial class Country
    {
        public int countryId { get; set; }
        public string? countryName { get; set; }
        public string? countryCode { get; set; }
    }
}
