using SSLConfiguration.Infrastructure.DataAccess;
using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Thin layer architecture / Core migration: keep same BLStoreOrder method names as old repo.
    /// </summary>
    public class BLStoreOrder
    {
        public static StoreOrder? GetStoreOrderDetailByPIN(string Pin)
        {
            return StoreOrderDataAccess.GetByPin(Pin);
        }

        /// <summary>
        /// Same as old BLStoreOrder.CheckConfigurationLinkUsedStatus (Session/MYP special-case skipped in Core API).
        /// </summary>
        public static bool CheckConfigurationLinkUsedStatus(int storeOrderId)
        {
            StoreOrder? storeOrder = StoreOrderDataAccess.GetById(storeOrderId);

            if (storeOrder == null)
            {
                return false;
            }

            if (Convert.ToBoolean(storeOrder.IsUsed))
            {
                return true;
            }

            if (Convert.ToBoolean(storeOrder.IsActive) == false)
            {
                return true;
            }

            if (Convert.ToBoolean(storeOrder.IsCancel) == true)
            {
                return true;
            }

            return false;
        }

        public static void UpdateConfigurationLinkUsedStatus(int storeOrderId, bool status)
        {
            StoreOrderDataAccess.UpdateUsedStatus(storeOrderId, status);
        }

        /// <summary>
        /// Same as old BLStoreOrder.UpdateAPIOrderNoInStoreOrder (Session culture → LanguageCode param).
        /// </summary>
        public static void UpdateAPIOrderNoInStoreOrder(int storeOrderId, string APIOrderNo, string? languageCode = null)
        {
            StoreOrder? storeOrder = StoreOrderDataAccess.GetById(storeOrderId);

            if (storeOrder != null)
            {
                storeOrder.ApiOrderNo = APIOrderNo;
                storeOrder.IsAllowIssueCertificate = false;
                storeOrder.LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode;
                StoreOrderDataAccess.Update(storeOrder);
            }
        }

        /// <summary>
        /// Persist selected UI culture to StoreOrder.LanguageCode (old Session CurrentCulture).
        /// </summary>
        public static void UpdateLanguageCode(int storeOrderId, string? languageCode)
        {
            StoreOrder? storeOrder = StoreOrderDataAccess.GetById(storeOrderId);
            if (storeOrder == null)
                return;

            storeOrder.LanguageCode = CultureHelper.Normalize(languageCode);
            StoreOrderDataAccess.Update(storeOrder);
        }
    }
}
