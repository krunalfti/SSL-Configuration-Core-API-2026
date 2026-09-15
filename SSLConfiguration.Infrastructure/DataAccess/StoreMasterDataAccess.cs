using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Infrastructure.DataAccess
{
    public static class StoreMasterDataAccess
    {
        public static StoreMaster? GetById(int storeMasterId)
        {
            using (var dbContext = new SSLConfigurationEntities())
            {
                return dbContext.StoreMasters.SingleOrDefault(g => g.StoreMasterId == storeMasterId);
            }
        }
    }
}
