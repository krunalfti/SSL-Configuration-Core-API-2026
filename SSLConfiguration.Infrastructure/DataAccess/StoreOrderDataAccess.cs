using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSLConfiguration.Contracts.SSLConfiguration_WebAPI;
using SSLConfiguration.Infrastructure.Persistence;
using System.Data;

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
        #region Renew Acme Product
        public static SaveStoreOrderResponse? RenewAcmeStoreOrder(SSLConfigurationEntities dbContext,int StoreId, string ApiOrderNo, StoreOrderDetail storeOrderDetail)
        {
            var parameters = new[]
            {
                new SqlParameter("@StoreId", StoreId),
                new SqlParameter("@ApiOrderNo", ApiOrderNo),

                new SqlParameter("@SSLApiLinkId", storeOrderDetail.SSLApiLinkId),
                new SqlParameter("@ProductId", storeOrderDetail.ProductId),
                new SqlParameter("@Year", storeOrderDetail.Year),
                new SqlParameter("@San", storeOrderDetail.San),
                new SqlParameter("@MinSan", storeOrderDetail.MinSan),
                new SqlParameter("@ProductName",
                    (object?)storeOrderDetail.ProductName ?? DBNull.Value),
                new SqlParameter("@CompanyName",
                    (object?)storeOrderDetail.CompanyName ?? DBNull.Value),
                new SqlParameter("@CredentialCode",
                    (object?)storeOrderDetail.CredentialCode ?? DBNull.Value),
                new SqlParameter("@IsMultiDomain", storeOrderDetail.IsMultiDomain),
                new SqlParameter("@WildcardSAN", storeOrderDetail.WildcardSAN),
                new SqlParameter("@IsSubscription", storeOrderDetail.IsSubscription),

                new SqlParameter("@IsCAMYP",
                    (object?)storeOrderDetail.IsCAMYP ?? DBNull.Value),
                new SqlParameter("@CAOrderValidity",
                    (object?)storeOrderDetail.CAOrderValidity ?? DBNull.Value),
                new SqlParameter("@CAOrderValidFrom",
                    (object?)storeOrderDetail.CAOrderValidFrom ?? DBNull.Value),
                new SqlParameter("@CAOrderValidTo",
                    (object?)storeOrderDetail.CAOrderValidTo ?? DBNull.Value),
                new SqlParameter("@CodeSignProvisioningMethod",
                    (object?)storeOrderDetail.CodeSignProvisioningMethod ?? DBNull.Value),
                new SqlParameter("@CodeSignShippingCode",
                    (object?)storeOrderDetail.CodeSignShippingCode ?? DBNull.Value),
                new SqlParameter("@RemainingValidity",
                    storeOrderDetail.RemainingValidity),
                new SqlParameter("@IsStoreMYP",
                    (object?)storeOrderDetail.IsStoreMYP ?? DBNull.Value),
                new SqlParameter("@SubscriptionYear",
                    (object?)storeOrderDetail.SubscriptionYear ?? DBNull.Value),
                new SqlParameter("@SpecialNote",
                    (object?)storeOrderDetail.SpecialNote ?? DBNull.Value)
            };

            var result = dbContext.Database.SqlQueryRaw<SaveStoreOrderResponse>(@"EXEC dbo.SaveStoreOrderDetailsForRenew
                @StoreId,
                @ApiOrderNo,
                @SSLApiLinkId,
                @ProductId,
                @Year,
                @San,
                @MinSan,
                @ProductName,
                @CompanyName,
                @CredentialCode,
                @IsMultiDomain,
                @WildcardSAN,
                @IsSubscription,
                @IsCAMYP,
                @CAOrderValidity,
                @CAOrderValidFrom,
                @CAOrderValidTo,
                @CodeSignProvisioningMethod,
                @CodeSignShippingCode,
                @RemainingValidity,
                @IsStoreMYP,
                @SubscriptionYear,
                @SpecialNote",
                    parameters).AsEnumerable().FirstOrDefault();

            return result;
        }
        #endregion
    }
}
