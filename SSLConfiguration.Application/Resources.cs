using System.Globalization;
using System.Resources;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Localized API messages (same keys as old SSLConfiguration_Resources.Resources).
    /// Reads satellite .resx under Localization/ based on CurrentUICulture (set via CultureHelper.Apply).
    /// </summary>
    public static class Resources
    {
        private static readonly ResourceManager Rm = new(
            "SSLConfiguration.Application.Localization.Resources",
            typeof(Resources).Assembly);

        public static string Val_InvalidPIN => Get("Val_InvalidPIN");
        public static string Val_OrderIsCancelled => Get("Val_OrderIsCancelled");
        public static string Val_EnterCaptcha => Get("Val_EnterCaptcha");
        public static string val_CSRWildcard => Get("val_CSRWildcard");
        public static string InvalidDomainNameInCSR => Get("InvalidDomainNameInCSR");
        public static string Val_CSRRequired => Get("Val_CSRRequired");
        public static string CSR_Note_Wildcard => Get("CSR_Note_Wildcard");
        public static string Val_CSRWithWildcardDomainNameNowAllowed => Get("Val_CSRWithWildcardDomainNameNowAllowed");
        public static string Val_DomainNameRequired => Get("Val_DomainNameRequired");
        public static string DV_SANRequired => Get("DV_SANRequired");
        public static string DV_SelectSubDomainType => Get("DV_SelectSubDomainType");
        public static string DV_MaxSANAllowed => Get("DV_MaxSANAllowed");
        public static string DV_WildCardSAN_MaxLabel => Get("DV_WildCardSAN_MaxLabel");

        public static string Get(string key, string? culture = null)
        {
            CultureInfo ci = string.IsNullOrWhiteSpace(culture)
                ? CultureInfo.CurrentUICulture
                : new CultureInfo(CultureHelper.Normalize(culture));

            return Rm.GetString(key, ci)
                ?? Rm.GetString(key, CultureInfo.GetCultureInfo("en"))
                ?? key;
        }
    }
}
