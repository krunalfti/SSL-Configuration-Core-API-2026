namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same as old SSLConfiguration_CommonUtility.ConstantUtil ProductType / CAProviderType.
    /// </summary>
    public enum ProductType
    {
        GlobalSign,
        PrimeSSL,
        SectigoAcmeDV,
        SectigoAcmeOV,
        Comodo,
        Digicert,
        ClickSSL,
        PrimeVMCCMC,
        Unknown
    }

    public enum CAProviderType
    {
        Unknown = 0,
        GlobalSign = 1,
        Sectigo = 2,
        Digicert = 3
    }
}
