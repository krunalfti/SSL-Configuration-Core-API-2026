namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same as old SSLConfiguration_CommonUtility.Common (GetProductType used by IssueCertificate).
    /// </summary>
    public class Common
    {
        public static ProductType GetProductType(int productId)
        {
            if (productId >= (int)ProductCode.AlphaSSL && productId <= 200) //GlobalSign Products 101-200
                return ProductType.GlobalSign;
            else if (productId == (int)ProductCode.PrimeSSLVerifiedMarkCertificate || productId == (int)ProductCode.PrimeSSLCommonMarkCertificate) //Prime VMC/CMC Products 212,213
                return ProductType.PrimeVMCCMC;
            else if (productId >= (int)ProductCode.PrimeDVSSL && productId <= 300) //PrimeSSL Products 201-300
                return ProductType.PrimeSSL;
            else if (productId == (int)ProductCode.SectigoACMEDV) //SectigoACMEDV 401
                return ProductType.SectigoAcmeDV;
            else if (productId == (int)ProductCode.SectigoACMEOV) //SectigoAcmeOV 402
                return ProductType.SectigoAcmeOV;
            else if (productId >= (int)ProductCode.ComodoPositiveSSL && productId <= 500) //Comodo/Sectigo Products 301-500
                return ProductType.Comodo;
            else if (productId >= (int)ProductCode.DigicertRapidSSLCertificate && productId <= 600) //Digicert Products 501-600
                return ProductType.Digicert;
            else if (productId >= (int)ProductCode.ClickSSLDV && productId <= 700) // ClickSSL Products 601-700
                return ProductType.ClickSSL;
            else
                return ProductType.Unknown;
        }
    }
}
