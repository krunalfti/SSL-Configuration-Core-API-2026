using System.Data;
using Microsoft.Data.SqlClient;
using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Infrastructure.DataAccess
{
    /// <summary>
    /// Thin layer architecture / Core migration: CACredential data-access helpers.
    /// Mirrors old BLGeneral.GetCACredentials SP call (GetCACredentialCode).
    /// </summary>
    public static class CACredentialDataAccess
    {
        /// <summary>
        /// Thin layer architecture: fetch CACredential by CredentialCode.
        /// Same as old CACredentialDataAccess.GetByCredentialCode.
        /// </summary>
        public static CACredential? GetByCredentialCode(string credentialCode)
        {
            using (var dbContext = new SSLConfigurationEntities())
            {
                return dbContext.CACredentials.SingleOrDefault(g => g.CredentialCode == credentialCode);
            }
        }

        /// <summary>
        /// Thin layer architecture: fetch CACredential by StoreOrderId via SP GetCACredentialCode.
        /// </summary>
        public static CACredential? GetByStoreOrderId(int storeOrderId)
        {
            if (string.IsNullOrWhiteSpace(DbConfig.SSLConfigurationEntities))
            {
                throw new InvalidOperationException(
                    "Connection string 'SSLConfigurationEntities' is not configured. Set it in appsettings.json and call DbConfig.Initialize in Program.cs.");
            }

            using (SqlConnection connection = new SqlConnection(DbConfig.SSLConfigurationEntities))
            using (SqlCommand command = new SqlCommand("GetCACredentialCode", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@StoreOrderID", SqlDbType.Int) { Value = storeOrderId });

                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new CACredential
                    {
                        CACredentialID = reader["CACredentialID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CACredentialID"]),
                        CredentialCode = reader["CredentialCode"] == DBNull.Value ? null : Convert.ToString(reader["CredentialCode"]),
                        CAName = HasColumn(reader, "CAName") && reader["CAName"] != DBNull.Value ? Convert.ToString(reader["CAName"]) : null,
                        UserName = HasColumn(reader, "UserName") && reader["UserName"] != DBNull.Value ? Convert.ToString(reader["UserName"]) : null,
                        Password = HasColumn(reader, "Password") && reader["Password"] != DBNull.Value ? Convert.ToString(reader["Password"]) : null,
                        ContractId = HasColumn(reader, "ContractId") && reader["ContractId"] != DBNull.Value ? Convert.ToString(reader["ContractId"]) : null,
                        PartnerCode = HasColumn(reader, "PartnerCode") && reader["PartnerCode"] != DBNull.Value ? Convert.ToString(reader["PartnerCode"]) : null,
                        ContactName = HasColumn(reader, "ContactName") && reader["ContactName"] != DBNull.Value ? Convert.ToString(reader["ContactName"]) : null,
                        ContactEmail = HasColumn(reader, "ContactEmail") && reader["ContactEmail"] != DBNull.Value ? Convert.ToString(reader["ContactEmail"]) : null,
                        DigicertAPIKey = HasColumn(reader, "DigicertAPIKey") && reader["DigicertAPIKey"] != DBNull.Value ? Convert.ToString(reader["DigicertAPIKey"]) : null
                    };
                }
            }
        }

        private static bool HasColumn(SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
