using SSLConfiguration.Contracts.SSLConfiguration_WebAPI;
using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Infrastructure.DataAccess
{
    /// <summary>
    /// Thin layer architecture / Core migration: StoreOrder data-access helpers.
    /// Same method names as old SSLConfiguration.DataAccess.StoreOrderDataAccess.
    /// </summary>
    public static class StoreOrderDataAccess
    {
        public static StoreOrder? GetByPin(string pin)
        {
            using (var dbContext = new SSLConfigurationEntities())
            {
                return dbContext.StoreOrders.SingleOrDefault(g => g.Pin == pin);
            }
        }

        public static StoreOrder? GetById(int storeOrderId)
        {
            using (var dbContext = new SSLConfigurationEntities())
            {
                return dbContext.StoreOrders.SingleOrDefault(g => g.StoreOrderId == storeOrderId);
            }
        }

        public static void Update(StoreOrder entity)
        {
            using (var dbContext = new SSLConfigurationEntities())
            {
                dbContext.StoreOrders.Update(entity);
                dbContext.SaveChanges();
            }
        }

        public static void UpdateUsedStatus(int storeOrderId, bool status)
        {
            StoreOrder? storeOrder = GetById(storeOrderId);
            if (storeOrder == null)
            {
                return;
            }

            storeOrder.IsUsed = status;
            storeOrder.UpdatedDate = DateTime.Now;
            Update(storeOrder);
        }
        public static StoreOrder? GetBySSLApiLinkIdAndStoreId(int SSLApiLinkId,int StoreId)
        {
            using (var dbContext = new SSLConfigurationEntities())
            {
                return dbContext.StoreOrders.FirstOrDefault(g => g.SSLApiLinkId == SSLApiLinkId && g.StoreId == StoreId);
            }
        }
        public static StoreOrder? GetBySSLApiLinkIdAndStoreIdLatest(SSLConfigurationEntities dbContext, int SSLApiLinkId, int StoreId)
        {
             return dbContext.StoreOrders.Where(x => x.SSLApiLinkId == SSLApiLinkId && x.StoreId == StoreId).OrderByDescending(x => x.StoreOrderId).FirstOrDefault();            
        }
        public static Product? GetByProductId(SSLConfigurationEntities dbContext, int productId)
        {
            return dbContext.Products.SingleOrDefault(x => x.ProductId == productId);
        }
        public static StoreOrder? GetByStoreId(SSLConfigurationEntities dbContext, int storeOrderId)
        {
            return dbContext.StoreOrders.SingleOrDefault(x => x.StoreOrderId == storeOrderId);
        }
        public static void UpdateStoreOrderRenew(SSLConfigurationEntities dbContext,StoreOrder entity)
        {
            dbContext.StoreOrders.Update(entity);
            dbContext.SaveChanges();            
        }
        public static List<AcemeCertificateDetail>? GetAcemeCertificateDetails(SSLConfigurationEntities dbContext,int storeOrderId)
        {
            return dbContext.AcemeCertificateDetails.Where(x => x.StoreOrderId == storeOrderId).ToList();
        }
        public static List<AdditionalDomain>? GetAdditionalDomains(SSLConfigurationEntities dbContext, int storeOrderId)
        {
            return dbContext.AdditionalDomains.Where(x => x.StoreOrderId == storeOrderId).ToList();
        }        
    }
}
