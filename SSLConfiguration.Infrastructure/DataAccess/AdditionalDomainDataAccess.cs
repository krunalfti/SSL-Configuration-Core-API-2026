using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Infrastructure.DataAccess
{
    /// <summary>
    /// Same as old AdditionalDomainDataAccess (GetByStoreOrderId).
    /// </summary>
    public static class AdditionalDomainDataAccess
    {
        public static List<AdditionalDomain> GetByStoreOrderId(int storeOrderId)
        {
            using (var dbContext = new SSLConfigurationEntities())
            {
                return dbContext.AdditionalDomains.Where(g => g.StoreOrderId == storeOrderId).ToList();
            }
        }

        /// <summary>Same as old AdditionalDomainDataAccess.GetWildcardSansByStoreOrderId.</summary>
        public static List<AdditionalDomain> GetWildcardSansByStoreOrderId(int storeOrderId)
        {
            using var dbContext = new SSLConfigurationEntities();
            return dbContext.AdditionalDomains
                .Where(g => g.StoreOrderId == storeOrderId
                    && g.IsPrimaryDomain == false
                    && g.IsConsiderAsSAN == true
                    && g.DomainName != null
                    && g.DomainName.StartsWith("*."))
                .ToList();
        }

        /// <summary>Same as old AdditionalDomainDataAccess.GetWildcardSansWithNameByStoreOrderId.</summary>
        public static List<AdditionalDomain> GetWildcardSansWithNameByStoreOrderId(int storeOrderId)
        {
            using var dbContext = new SSLConfigurationEntities();
            return dbContext.AdditionalDomains
                .Where(g => g.StoreOrderId == storeOrderId
                    && g.IsPrimaryDomain == false
                    && g.IsConsiderAsSAN == true
                    && g.DomainName != null
                    && g.DomainName != ""
                    && g.DomainName.StartsWith("*."))
                .ToList();
        }

        /// <summary>Same as old AdditionalDomainDataAccess.GetNonWildcardSansWithNameByStoreOrderId.</summary>
        public static List<AdditionalDomain> GetNonWildcardSansWithNameByStoreOrderId(int storeOrderId)
        {
            using var dbContext = new SSLConfigurationEntities();
            return dbContext.AdditionalDomains
                .Where(g => g.StoreOrderId == storeOrderId
                    && g.IsPrimaryDomain == false
                    && g.IsConsiderAsSAN == true
                    && g.DomainName != null
                    && g.DomainName != ""
                    && !g.DomainName.StartsWith("*."))
                .ToList();
        }

        /// <summary>Withhold check used by AddDomain.</summary>
        public static int GetCountByDomainWithHoldorNot(int storeOrderId, string domainname)
        {
            using var dbContext = new SSLConfigurationEntities();
            return dbContext.AdditionalDomains.Any(s =>
                s.StoreOrderId == storeOrderId
                && s.IsPrimaryDomain == false
                && s.IsRemoveFromApi == true
                && s.DomainName == domainname)
                ? 1
                : 0;
        }
    }
}
