using Microsoft.AspNetCore.Http;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same as old DigicertShippingInformation.
    /// </summary>
    public class DigicertShippingInformation
    {
        public string? ShippingName { get; set; }
        public string? ShippingAddress1 { get; set; }
        public string? ShippingAddress2 { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingState { get; set; }
        public string? ShippingCountryName { get; set; }
        public string? ShippingCountryCode { get; set; }
        public string? ShippingPostalCode { get; set; }
    }

    /// <summary>
    /// Same as old VMC_CertificateDetail (API: logo as string/base64 instead of HttpPostedFileBase).
    /// </summary>
    public class VMC_CertificateDetail
    {
        public VMC_CertificateDetail()
        {
            MarkTypeData = new VmcMarkTypeData();
        }

        public string? Logo { get; set; }
        public bool EnableHosting { get; set; }
        public string? MarkType { get; set; }
        public VmcMarkTypeData? MarkTypeData { get; set; }
        public string? DomainName { get; set; }
        public string? FileBase64 { get; set; }
        public string? FileName { get; set; }
        public bool IsLogoExists { get; set; }
        public string? strLogo { get; set; }
    }

    public class VmcMarkTypeData
    {
        public string? RegistrationNumber { get; set; }
        public string? CountryCode { get; set; }
    }
}
