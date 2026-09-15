namespace SSLConfiguration.Infrastructure.DataAccess
{
    using SSLConfiguration.Infrastructure.Persistence;

    /// <summary>
    /// Thin layer: Country lookup (same role as old Repository&lt;Country&gt;.GetAll).
    /// </summary>
    public static class CountryDataAccess
    {
        public static List<Country> GetAll()
        {
            using (var dbContext = new SSLConfigurationEntities())
            {
                return dbContext.Countries.OrderBy(c => c.countryName).ToList();
            }
        }
    }
}
