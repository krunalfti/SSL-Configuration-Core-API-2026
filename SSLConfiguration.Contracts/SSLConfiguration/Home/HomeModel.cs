using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSLConfiguration.Contracts.SSLConfiguration.Home
{
    /// <summary>
    /// Request for ChangeCulture (old Session CurrentCulture + redirect).
    /// </summary>
    public class ChangeCultureRequest
    {
        public string culture { get; set; } = "en";
        public string? returnUrl { get; set; }
    }
    /// <summary>
    /// Response for ChangeCulture.
    /// </summary>
    public class ChangeCultureResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public string? culture { get; set; }
        public string? returnUrl { get; set; }
    }
    /// <summary>
    /// Response for GET GetCaptcha — UI shows image then posts CaptchaId + CaptchaCode with Index.
    /// </summary>
    public class GetCaptchaResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public string? captchaId { get; set; }
        public string? imageBase64 { get; set; }
        public string? contentType { get; set; }
        public string? imageDataUrl { get; set; }
    }
    /// <summary>
    /// Shared response for Error / SessionExpired (old Session.Abandon + View).
    /// UI should clear local PIN/token/culture and show the matching page.
    /// </summary>
    public class HomeStatusResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public string? action { get; set; }
        public bool clearClientState { get; set; }
    }
    /// <summary>
    /// Request for SSLConfiguration HomeController Index (POST PIN verification).
    /// Same fields as old PinVerficationModel + culture + Core captcha fields.
    /// </summary>
    public class IndexRequest
    {
        public string PIN { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string culture { get; set; } = "en";        
        public string? CaptchaId { get; set; }       
        public string? CaptchaCode { get; set; }
    }
    /// <summary>
    /// Response for SSLConfiguration HomeController Index GET/POST.
    /// Replaces View/Redirect/TempData with JSON (Core API pattern).
    /// </summary>
    public class IndexResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public string? pin { get; set; }
        public int? productId { get; set; }
        public int? storeId { get; set; }
        public int? storeOrderId { get; set; }
        public string? culture { get; set; }       
        public string? nextController { get; set; }        
        public string? nextAction { get; set; }       
        public string? nextArea { get; set; }       
        public bool? isFreeSANInclude { get; set; }
        public string? accessToken { get; set; }
        public string? configurationToken { get; set; }
        public bool? isLinkExpired { get; set; }
        public string? orderDetailJson { get; set; }
        public string? caCredentialDetailsJson { get; set; }
        public string? productDetailJson { get; set; }
    }
    /// <summary>
    /// Request for SSLConfiguration HomeController.IssueCertificate.
    /// </summary>
    public class IssueCertificateRequest
    {
        public string PIN { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string culture { get; set; } = "en";
    }
    /// <summary>
    /// Response for SSLConfiguration HomeController.IssueCertificate.
    /// Replaces View/Redirect/Session PF_Request with JSON.
    /// </summary>
    public class IssueCertificateResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public string? pin { get; set; }
        public int? productId { get; set; }
        public int? storeId { get; set; }
        public int? storeOrderId { get; set; }
        public string? culture { get; set; }
        public bool isNewIssue { get; set; }
        public string? nextController { get; set; }
        public string? nextAction { get; set; }
        public string? nextArea { get; set; }

        /// <summary>JWT for subsequent GlobalSign Entry / wizard calls (same as Index).</summary>
        public string? accessToken { get; set; }

        /// <summary>Replaces Session PF_Request after IssueCertificate prefill.</summary>
        public string? configurationToken { get; set; }

        /// <summary>GlobalSign draft fields loaded for new issue (no Session).</summary>
        public Dictionary<string, string>? additionalDomainsList { get; set; }
        public string? additionalDomains { get; set; }
        public string? approvalMethod { get; set; }
        public string? approvalEmail { get; set; }
    }
    /// <summary>
    /// JSON shape of old PartialView _header (StoreOrder model fields UI needs).
    /// </summary>
    public class LoadHeaderResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public string? pin { get; set; }
        public bool hasOrder { get; set; }
        public string? product { get; set; }

    }
}
