using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Infrastructure.DataAccess
{
    /// <summary>
    /// Same as old CSRDetailDataAccess.GetById.
    /// </summary>
    public static class CSRDetailDataAccess
    {
        public static CSRDetail? GetById(int csrDetailId)
        {
            using (var dbContext = new SSLConfigurationEntities())
            {
                return dbContext.CSRDetails.SingleOrDefault(g => g.CSRDetailId == csrDetailId);
            }
        }
    }
}
