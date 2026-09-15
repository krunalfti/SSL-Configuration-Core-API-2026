namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Thin layer architecture / Core migration: holds connection string for static data-access callers (same pattern as old ConfigurationManager usage).
    /// </summary>
    public static class DbConfig
    {
        public static string? SSLConfigurationEntities { get; set; }

        public static void Initialize(string? connectionString)
        {
            SSLConfigurationEntities = connectionString;
        }
    }
}
