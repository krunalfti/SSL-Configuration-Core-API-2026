using SSLConfiguration.Infrastructure.DataAccess;
using SSLConfiguration.Infrastructure.Persistence;
using VerisignGateway;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same refund BL as old BLComodo.GetSectigoPostPaymentStatus
    /// and BLComodo.AdminGetSectigoRefundPaymentStatusList.
    /// </summary>
    public static class BLRefund
    {
        /// <summary>
        /// Same as old BLComodo.GetSectigoPostPaymentStatus.
        /// </summary>
        public static SectigoPostPaymentStatusResponse GetSectigoPostPaymentStatus(int storeOrderId, string apiOrderNo)
        {
            try
            {
                SectigoPostPaymentStatusResponse comodoRefundStatusResponse = new SectigoPostPaymentStatusResponse();

                CACredential? caCredentialDetail = BLGeneral.GetCACredentials(storeOrderId);
                ComodoCACredential objCACredential = new ComodoCACredential();

                objCACredential.UserName = caCredentialDetail!.UserName;
                objCACredential.Password = caCredentialDetail.Password;

                comodoRefundStatusResponse = VerisignAPIHelper.SectigoPostPaymentStatus(objCACredential, apiOrderNo);

                if (string.IsNullOrWhiteSpace(comodoRefundStatusResponse.errorMessage)
                    && comodoRefundStatusResponse.errorCode == 0
                    && comodoRefundStatusResponse.data != null)
                {
                    List<SectigoRefundPaymentStatu> certificateDetail =
                        SectigoRefundPaymentStatuDataAccess.GetByStoreOrderIdAndApiOrderNo(storeOrderId, apiOrderNo);

                    if (certificateDetail != null && certificateDetail.Count > 0)
                    {
                        foreach (var item in certificateDetail)
                        {
                            item.RefundStatus = comodoRefundStatusResponse.data.orderStatus;
                            item.UpdatedDate = DateTime.Now;
                            SectigoRefundPaymentStatuDataAccess.Update(item);
                        }
                    }
                }

                return comodoRefundStatusResponse;
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                throw;
            }
        }

        /// <summary>
        /// Same as old BLComodo.AdminGetSectigoRefundPaymentStatusList.
        /// Date conversion matches old Convert.ToDateTime on search filters
        /// (null/invalid → catch → null list + empty page metadata).
        /// </summary>
        public static List<AdminGetSectigoRefundPaymentStatusList_SP_Result>? AdminGetSectigoRefundPaymentStatusList(
            string? apiOrderNo,
            string? refundStatus,
            DateTime? startDateFilter,
            DateTime? endDateFilter,
            int pageNumber,
            int pageSize,
            out PagedMetadata pageMetadata)
        {
            List<AdminGetSectigoRefundPaymentStatusList_SP_Result>? orderList = null;
            try
            {
                string apiorderno = string.IsNullOrWhiteSpace(apiOrderNo) ? string.Empty : apiOrderNo;
                string refundstatus = string.IsNullOrWhiteSpace(refundStatus) ? string.Empty : refundStatus;

                // Same as old: Convert.ToDateTime(model.OrderSearchFilters.StartDate/EndDate)
                DateTime startDate = Convert.ToDateTime(startDateFilter);
                DateTime endDate = Convert.ToDateTime(endDateFilter);

                orderList = SectigoRefundPaymentStatuDataAccess.AdminGetList(
                    pageNumber, pageSize, apiorderno, refundstatus, startDate, endDate);

                if (orderList != null && orderList.Count > 0)
                    pageMetadata = new PagedMetadata(orderList.FirstOrDefault()!.TotalRecords.Value, pageSize, pageNumber);
                else
                    pageMetadata = new PagedMetadata(0, pageSize, pageNumber);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                orderList = null;
                pageMetadata = new PagedMetadata(0, pageSize, pageNumber);
            }

            return orderList;
        }
    }
}
