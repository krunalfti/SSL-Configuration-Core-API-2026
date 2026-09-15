namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same constants as old SSLConfiguration_CommonUtility.ConstantUtil (Index PIN flow subset).
    /// </summary>
    public static class ConstantUtil
    {
        public const string Digicert_DCVMethod_HTTP_TOKEN = "FILE";
        public const string Digicert_DCVMethod_DNS_TXT_TOKEN = "DNS";
        public const string Digicert_DCVMethod_EMAIL = "EMAIL";

        public const string DCVMethod_FILE = "FILE";
        public const string DCVMethod_DNS = "DNS";
        public const string DCVMethod_EMAIL = "EMAIL";

        public const string Comodo_DCVMethod_HTTPCsrHash = "HTTPCSRHASH";
        public const string Comodo_DCVMethod_CnameCsrHash = "CNAMECSRHASH";
        public const string Comodo_DCVMethod_Email = "EMAIL";
        public const string Comodo_DCVMethod_DNSTXTRNDVAL = "DNSTXTRNDVAL";
        public const string Comodo_DCVMethod_HTTPSCsrHash = "HTTPSCSRHASH";

        public const string Comodo_CodeSignType_Individual = "Individual";
        public const string Comodo_CodeSignType_Organization = "Organization";
        public const string Comodo_CodeSignType_EV = "EV";

        public const string AuthenticationType_DV = "DV";
        public const string AuthenticationType_OV = "OV";
        public const string AuthenticationType_EV = "EV";
        public const string AuthenticationType_CodeSign = "CodeSign";
        public const string AuthenticationType_PAC = "PAC";
        public const string AuthenticationType_VMC = "VMC";
        public const string AuthenticationType_MarkCert = "MarkCert";
        public const string AuthenticationType_X9 = "X9";

        public const string GlobalSign_DCVMethod_URL = "URL";
        public const string GlobalSign_DCVMethod_DNS = "DNS";
        public const string GlobalSign_DCVMethod_EMAIL = "EMAIL";
    }

    /// <summary>
    /// Same as old SSLConfiguration_CommonUtility.enmDCVMethod.
    /// </summary>
    public enum enmDCVMethod
    {
        EMAIL = 1,
        FILE = 2,
        DNS = 3
    }

    /// <summary>
    /// Same as old DigicertDCVMethod (VerisignGateway / CommonUtility naming used by PlaceOrder).
    /// </summary>
    public enum DigicertDCVMethod
    {
        Email = 1,
        File = 2,
        DNS = 3
    }
}
