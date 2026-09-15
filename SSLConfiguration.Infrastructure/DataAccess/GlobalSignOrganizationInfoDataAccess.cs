using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Infrastructure.DataAccess
{
    /// <summary>
    /// Same as old GlobalSignOrganizationInfoDataAccess.GetById.
    /// </summary>
    public static class GlobalSignOrganizationInfoDataAccess
    {
        public static GlobalSignOrganizationInfo? GetById(int globalSignOrganizationInfoId)
        {
            using (var dbContext = new SSLConfigurationEntities())
            {
                return dbContext.GlobalSignOrganizationInfos.SingleOrDefault(g => g.GlobalSignOrganizationInfoID == globalSignOrganizationInfoId);
            }
        }
    }
}
