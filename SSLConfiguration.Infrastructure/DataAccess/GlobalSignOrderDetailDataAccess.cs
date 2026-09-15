using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Infrastructure.DataAccess
{
    /// <summary>
    /// Same as old GlobalSignOrderDetailDataAccess.
    /// </summary>
    public static class GlobalSignOrderDetailDataAccess
    {
        public static GlobalSignOrderDetail? GetByStoreOrderId(int storeOrderId)
        {
            using (var dbContext = new SSLConfigurationEntities())
            {
                return dbContext.GlobalSignOrderDetails.SingleOrDefault(g => g.StoreOrderId == storeOrderId);
            }
        }
    }
}
