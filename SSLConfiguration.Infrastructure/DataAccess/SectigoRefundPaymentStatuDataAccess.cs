using System.Data;
using Microsoft.Data.SqlClient;
using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Infrastructure.DataAccess
{
    /// <summary>
    /// Thin layer for SectigoRefundPaymentStatus — same queries as old UnitOfWork / SqlQuery path.
    /// </summary>
    public static class SectigoRefundPaymentStatuDataAccess
    {
        public static List<SectigoRefundPaymentStatu> GetByStoreOrderIdAndApiOrderNo(int storeOrderId, string apiOrderNo)
        {
            using var db = new SSLConfigurationEntities();
            return db.SectigoRefundPaymentStatus
                .Where(x => x.StoreOrderId == storeOrderId && x.ApiOrderNo == apiOrderNo)
                .ToList();
        }

        public static void Update(SectigoRefundPaymentStatu entity)
        {
            using var db = new SSLConfigurationEntities();
            db.SectigoRefundPaymentStatus.Update(entity);
            db.SaveChanges();
        }

        /// <summary>
        /// Same SP as old: AdminGetSectigoRefundPaymentStatusList_SP.
        /// </summary>
        public static List<AdminGetSectigoRefundPaymentStatusList_SP_Result> AdminGetList(
            int pageNo,
            int pageSize,
            string? apiOrderNo,
            string? refundStatus,
            DateTime startDate,
            DateTime endDate)
        {
            if (string.IsNullOrWhiteSpace(DbConfig.SSLConfigurationEntities))
            {
                throw new InvalidOperationException(
                    "Connection string 'SSLConfigurationEntities' is not configured.");
            }

            var list = new List<AdminGetSectigoRefundPaymentStatusList_SP_Result>();

            using (var connection = new SqlConnection(DbConfig.SSLConfigurationEntities))
            using (var command = new SqlCommand("AdminGetSectigoRefundPaymentStatusList_SP", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@PageNo", SqlDbType.Int) { Value = pageNo });
                command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });
                command.Parameters.Add(new SqlParameter("@ApiOrderNo", SqlDbType.NVarChar)
                {
                    Value = apiOrderNo ?? string.Empty
                });
                command.Parameters.Add(new SqlParameter("@RefundPaymentStatus", SqlDbType.NVarChar)
                {
                    Value = refundStatus ?? string.Empty
                });
                command.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.DateTime2) { Value = startDate });
                command.Parameters.Add(new SqlParameter("@EndDate", SqlDbType.DateTime2) { Value = endDate });

                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new AdminGetSectigoRefundPaymentStatusList_SP_Result
                        {
                            RowNumber = reader["RowNumber"] == DBNull.Value ? null : Convert.ToInt64(reader["RowNumber"]),
                            SectigoRefundPaymentId = Convert.ToInt32(reader["SectigoRefundPaymentId"]),
                            StoreId = reader["StoreId"] == DBNull.Value ? null : Convert.ToInt32(reader["StoreId"]),
                            StoreOrderId = Convert.ToInt32(reader["StoreOrderId"]),
                            ApiOrderNo = reader["ApiOrderNo"] == DBNull.Value ? null : Convert.ToString(reader["ApiOrderNo"]),
                            RefundStatus = reader["RefundStatus"] == DBNull.Value ? null : Convert.ToString(reader["RefundStatus"]),
                            CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                            UpdatedDate = reader["UpdatedDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["UpdatedDate"]),
                            CredentialCode = reader["CredentialCode"] == DBNull.Value ? null : Convert.ToString(reader["CredentialCode"]),
                            TotalRecords = reader["TotalRecords"] == DBNull.Value ? null : Convert.ToInt32(reader["TotalRecords"])
                        });
                    }
                }
            }

            return list;
        }
    }
}
