using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Infrastructure.DataAccess
{
    /// <summary>Same as old SSLConfiguration.DataAccess.AcemeCertificateDetailDataAccess.</summary>
    public static class AcemeCertificateDetailDataAccess
    {
        public static AcemeCertificateDetail? GetByStoreOrderId(int storeOrderId)
        {
            using var dbContext = new SSLConfigurationEntities();
            return dbContext.AcemeCertificateDetails.FirstOrDefault(g => g.StoreOrderId == storeOrderId);
        }

        public static AcemeCertificateDetail? GetFirstByStoreOrderId(int storeOrderId)
        {
            using var dbContext = new SSLConfigurationEntities();
            return dbContext.AcemeCertificateDetails.FirstOrDefault(g => g.StoreOrderId == storeOrderId);
        }

        public static void Add(AcemeCertificateDetail entity)
        {
            using var dbContext = new SSLConfigurationEntities();
            dbContext.AcemeCertificateDetails.Add(entity);
            dbContext.SaveChanges();
        }
    }
}
