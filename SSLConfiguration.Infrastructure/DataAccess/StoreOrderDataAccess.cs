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
    }
}
