namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same enum values as old SSLConfiguration_CommonUtility.ProductCode
    /// (values used by Home Index / IssueCertificate / GetProductType).
    /// </summary>
    public enum ProductCode
    {
        AlphaSSL = 101,
        PrimeDVSSL = 201,
        PrimeSSLVerifiedMarkCertificate = 212,
        PrimeSSLCommonMarkCertificate = 213,
        ComodoPositiveSSL = 301,
        SectigoACMEDV = 401,
        SectigoACMEOV = 402,
        DigicertRapidSSLCertificate = 501,
        DigicertX9PKI = 534,
        ClickSSLDV = 601
    }
}
