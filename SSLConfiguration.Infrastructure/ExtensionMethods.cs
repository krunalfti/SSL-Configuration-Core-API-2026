using SSLConfiguration.Infrastructure.Persistence;
using VerisignGateway;

namespace SSLConfiguration.Infrastructure
{
    /// <summary>
    /// Same extension method names as old SSLConfiguration_CommonUtility.ExtensionMethods.
    /// </summary>
    public static class ExtensionMethods
    {
        public static DigicertCACredential GetDigicertCACredential(this CACredential objCACredential)
        {
            DigicertCACredential objReturn = new DigicertCACredential();
            objReturn.ApiKey = objCACredential?.DigicertAPIKey;
            return objReturn;
        }

        public static ComodoCACredential GetComodoCACredential(this CACredential objCACredential)
        {
            ComodoCACredential objReturn = new ComodoCACredential();
            objReturn.UserName = objCACredential?.UserName;
            objReturn.Password = objCACredential?.Password;
            return objReturn;
        }

        public static GlobalsignCACredential GetGlobalSignCACredential(this CACredential objCACredential)
        {
            GlobalsignCACredential objReturn = new GlobalsignCACredential();
            objReturn.UserName = objCACredential?.UserName;
            objReturn.Password = objCACredential?.Password;
            return objReturn;
        }

        /// <summary>
        /// Same as old ExtensionMethods.GetDomain (email → domain part).
        /// </summary>
        public static string GetDomain(this string email)
        {
            if (string.IsNullOrEmpty(email))
                return string.Empty;

            int at = email.IndexOf('@');
            if (at < 0 || at >= email.Length - 1)
                return string.Empty;

            return email.Substring(at + 1).Trim();
        }
    }
}
