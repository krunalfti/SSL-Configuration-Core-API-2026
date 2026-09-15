using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Infrastructure.DataAccess
{
    /// <summary>
    /// Same as old GlobalSignContactInfoDataAccess.GetById.
    /// </summary>
    public static class GlobalSignContactInfoDataAccess
    {
        public static GlobalSignContactInfo? GetById(int? contactInfoId)
        {
            if (contactInfoId == null)
            {
                return null;
            }

            using (var dbContext = new SSLConfigurationEntities())
            {
                return dbContext.GlobalSignContactInfos.SingleOrDefault(g => g.GlobalSignContactInfoID == contactInfoId.Value);
            }
        }
    }
}
