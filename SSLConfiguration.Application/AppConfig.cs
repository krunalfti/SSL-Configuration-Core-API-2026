namespace SSLConfiguration.Application
{
    /// <summary>
    /// App settings used by BL (replaces ConfigurationManager.AppSettings).
    /// </summary>
    public static class AppConfig
    {
        public static string? DefaultComodoCredential { get; set; }
        /// <summary>Same as old Misc.config DigicertWebServerList (id|name,id|name,...).</summary>
        public static string? DigicertWebServerList { get; set; }
        /// <summary>Same as old AppSettings DONOTAuthorizedCodeSign (comma-separated PINs).</summary>
        public static string? DONOTAuthorizedCodeSign { get; set; }

        public static string? AcemeApiUrl { get; set; }
        public static string? SSL2BuyWebApiAuthKey { get; set; }
        public static string? AcmeSSL2BuyApiUrl { get; set; }
        public static string? CheapSSLShopApiUrl { get; set; }

        /// <summary>Same as old AppSettings GoogleTranslateApiUrl.</summary>
        public static string? GoogleTranslateApiUrl { get; set; }
        /// <summary>Same as old AppSettings GoogleTranslateApiKey.</summary>
        public static string? GoogleTranslateApiKey { get; set; }
        public static string? AdminUserName { get; set; }
        public static string? AdminPassword { get; set; }

        /// <summary>Same as old AppSettings smtpserver.</summary>
        public static string? SmtpServer { get; set; }
        /// <summary>Same as old AppSettings smtpusername.</summary>
        public static string? SmtpUsername { get; set; }
        /// <summary>Same as old AppSettings smtppassword.</summary>
        public static string? SmtpPassword { get; set; }
        /// <summary>Same as old AppSettings enableSSL.</summary>
        public static bool EnableSsl { get; set; }
        /// <summary>Same as old AppSettings smptpPort.</summary>
        public static int SmtpPort { get; set; }
    }
}
