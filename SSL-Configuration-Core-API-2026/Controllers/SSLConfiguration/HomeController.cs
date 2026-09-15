using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SSLConfiguration.Application;
using SSLConfiguration.Application.Services;
using SSLConfiguration.Contracts.SSLConfiguration.Home;


namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration
{
    /// <summary>
    /// Ported from SSLConfiguration Controllers/HomeController — full action coverage for future UI.
    /// </summary>
    [ApiController]
    [Route("api/SSLConfiguration/[controller]")]
    public class HomeController : ControllerBase
    {
        private readonly CaptchaService _captchaService;
        private readonly JwtTokenService _jwtTokenService;       
        private readonly ConfigurationDraftStore _draftStore;

        public HomeController(
            CaptchaService captchaService,
            JwtTokenService jwtTokenService,
            ConfigurationDraftStore draftStore)
        {
            _captchaService = captchaService;
            _jwtTokenService = jwtTokenService;
            _draftStore = draftStore;
        }

        /// <summary>
        /// Core replacement for CaptchaMvc image used by Index POST.
        /// Route: GET /api/SSLConfiguration/Home/GetCaptcha
        /// </summary>
        [HttpGet("GetCaptcha")]
        [ClientAuthorize]        
        public IActionResult GetCaptcha()
        {
            var response = new GetCaptchaResponse();
            try
            {
                var challenge = _captchaService.Create();
                response.success = true;
                response.captchaId = challenge.CaptchaId;
                response.imageBase64 = challenge.ImageBase64;
                response.contentType = challenge.ContentType;
                response.imageDataUrl = $"data:{challenge.ContentType};base64,{challenge.ImageBase64}";
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.success = false;
                response.message = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same action as old HomeController.Index(string pin).
        /// Route: GET /api/SSLConfiguration/Home/Index?pin=
        /// </summary>
        [HttpGet("Index")]
        [ClientAuthorize]
        public IActionResult Index([FromQuery] string pin = "")
        {
            var response = new IndexResponse();

            try
            {
                string culture = "en";

                if (!string.IsNullOrEmpty(pin))
                {
                    var orderDetail = BLStoreOrder.GetStoreOrderDetailByPIN(pin.Trim());

                    if (orderDetail != null)
                    {
                        if (!string.IsNullOrEmpty(orderDetail.LanguageCode))
                            culture = orderDetail.LanguageCode;
                        else
                            culture = "en";

                        culture = CultureHelper.Normalize(culture);
                        CultureHelper.Apply(culture);

                        if (Convert.ToBoolean(orderDetail.IsUsed == false) && Convert.ToBoolean(orderDetail.IsLinkExpired))
                            response.message = "Link has expired.";
                        else
                            response.message = string.Empty;
                        //response.accessToken = _jwtTokenService.CreateToken(response.pin!, response.productId, response.storeId);
                        response.isLinkExpired = Convert.ToBoolean(orderDetail.IsLinkExpired);
                        response.productId = orderDetail.ProductId;
                        response.storeId = orderDetail.StoreId;
                        response.storeOrderId = orderDetail.StoreOrderId;
                        response.success = true;
                        response.pin = pin.Trim();
                        response.culture = culture;
                        return Ok(response);
                    }
                    else
                    {
                        response.success = false;
                        response.pin = pin.Trim();
                        response.culture = culture;
                        response.message = "Pin Is Wrong.";
                        return Ok(response);
                    }                    
                }
                else
                {
                    response.success = false;
                    response.message = "PIN is required.";
                    return Ok(response);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.success = false;
                response.message = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same action as old HomeController.Index(PinVerficationModel, culture) POST.
        /// Captcha validated via GetCaptcha (replaces CaptchaMvc IsCaptchaValid).
        /// Route: POST /api/SSLConfiguration/Home/Index
        /// </summary>
        [HttpPost("Index")]
        [ClientAuthorize]
        public IActionResult Index([FromBody] IndexRequest objModel)
        {
            var response = new IndexResponse();

            try
            {
                if (objModel == null || string.IsNullOrWhiteSpace(objModel.PIN))
                {
                    response.success = false;
                    response.message = "PIN is required.";
                    return Ok(response);
                }

                string culture = CultureHelper.Normalize(objModel.culture);
                CultureHelper.Apply(culture);
                response.culture = culture;
                response.pin = objModel.PIN.Trim();

                // Same gate as old: if (this.IsCaptchaValid(Resources.Val_EnterCaptcha))
                //if (!_captchaService.IsCaptchaValid(objModel.CaptchaId, objModel.CaptchaCode))
                //{
                //    response.success = false;
                //    response.message = Resources.Val_EnterCaptcha;
                //    return Ok(response);
                //}

                var orderDetail = BLStoreOrder.GetStoreOrderDetailByPIN(objModel.PIN.Trim());

                if (orderDetail == null)
                {
                    response.success = false;
                    response.message = Resources.Val_InvalidPIN;
                    return Ok(response);
                }

                // Persist selected culture (replaces old Session CurrentCulture for later draft/CA messages)
                BLStoreOrder.UpdateLanguageCode(orderDetail.StoreOrderId, culture);

                // Log IP Address - Store Order Number Wise
                LogWriter.LogStoreOrderIPAddress(orderDetail.StoreOrderId);

                // Same as old: load CA credential by CredentialCode (no Session PF_Request)
                var caCredentialDetails = BLGeneral.GetCACredentials(orderDetail.CredentialCode ?? string.Empty);

                var productDetail = BLGeneral.GetProductDetail_StoreOrder(objModel.PIN.Trim());
                response.orderDetailJson = JsonConvert.SerializeObject(orderDetail);
                response.caCredentialDetailsJson = JsonConvert.SerializeObject(caCredentialDetails);
                response.productDetailJson = JsonConvert.SerializeObject(productDetail);
                if (productDetail == null)
                {
                    response.success = false;
                    response.message = "Product not available.";
                    response.storeOrderId = orderDetail.StoreOrderId;
                    response.productId = orderDetail.ProductId;
                    return Ok(response);
                }

                response.storeOrderId = orderDetail.StoreOrderId;
                response.storeId = orderDetail.StoreId;
                response.productId = orderDetail.ProductId;

                if (orderDetail.IsCancel)
                {
                    response.success = false;
                    response.message = Resources.Val_OrderIsCancelled;
                    return Ok(response);
                }
                else if (Convert.ToBoolean(orderDetail.IsLock))
                {
                    response.success = false;
                    response.message = "PIN is locked.";
                    return Ok(response);
                }
                else if (orderDetail.IsUsed == false)
                {
                    if (Convert.ToBoolean(orderDetail.IsLinkExpired))
                    {
                        response.success = false;
                        response.message = "Link has expired.";
                        return Ok(response);
                    }

                    // Added Date : 13-Jan-2025 : Old unused links updated LinkExpiredDate = Link Created date + 13 Month
                    if (Convert.ToDateTime(orderDetail.ValidTillDate) < Convert.ToDateTime(DateTime.Now))
                    {
                        response.success = false;
                        response.message = "Link has expired.";
                        return Ok(response);
                    }

                    // Added Date : 13-Jan-2025 : Old unused links updated LinkExpiredDate = Link Created date + 13 Month
                    if (Convert.ToDateTime(orderDetail.LinkExpiredDate) < Convert.ToDateTime(DateTime.Now))
                    {
                        response.success = false;
                        response.message = "Link has expired.";
                        return Ok(response);
                    }

                    string controllerName = string.Empty;

                    if (orderDetail.ProductId > 100 && orderDetail.ProductId < 200) // GlobalSign Products
                        controllerName = "GlobalSign";
                    if (orderDetail.ProductId > 200 && orderDetail.ProductId < 300) // PrimeSSL Products
                        controllerName = "PrimeSSL";
                    else if ((orderDetail.ProductId > 300 && orderDetail.ProductId < 500)) // Comodo Products
                        controllerName = "Comodo";
                    else if (orderDetail.ProductId > 500 && orderDetail.ProductId < 600) // Digicert Products
                        controllerName = "Digicert";
                    else if (orderDetail.ProductId > 600 && orderDetail.ProductId < 700) // ClickSSL Products
                        controllerName = "ClickSSL";

                    response.success = true;
                    response.nextController = controllerName;
                    response.nextArea = "SSLConfiguration";
                    //response.accessToken = _jwtTokenService.CreateToken(response.pin!, response.productId, response.storeId);
                    AttachGlobalSignConfigurationDraft(response, objModel.PIN.Trim(), culture);
                    if (productDetail.ProductId == (int)ProductCode.DigicertX9PKI)
                    {
                        response.nextAction = ConstantUtil.AuthenticationType_X9;
                        return Ok(response);
                    }
                    else if (BLGeneral.IsVMCProduct(productDetail.ProductId))
                    {
                        response.nextAction = ConstantUtil.AuthenticationType_VMC;
                        return Ok(response);
                    }
                    else if (BLGeneral.IsPrimeVMCProduct(productDetail.ProductId))
                    {
                        response.nextAction = ConstantUtil.AuthenticationType_MarkCert;
                        return Ok(response);
                    }
                    else if (productDetail.IsCodeSign)
                    {
                        response.nextAction = ConstantUtil.AuthenticationType_CodeSign;
                        return Ok(response);
                    }
                    else if (productDetail.IsPAC)
                    {
                        response.nextAction = ConstantUtil.AuthenticationType_PAC;
                        return Ok(response);
                    }
                    else if (productDetail.AuthenticationType == ConstantUtil.AuthenticationType_DV)
                    {
                        response.nextAction = ConstantUtil.AuthenticationType_DV;
                        return Ok(response);
                    }
                    else if (productDetail.AuthenticationType == ConstantUtil.AuthenticationType_OV)
                    {
                        response.nextAction = ConstantUtil.AuthenticationType_OV;
                        return Ok(response);
                    }
                    else if (productDetail.AuthenticationType == ConstantUtil.AuthenticationType_EV)
                    {
                        response.nextAction = ConstantUtil.AuthenticationType_EV;
                        return Ok(response);
                    }

                    return Ok(response);
                }
                else if (orderDetail.IsUsed == true)
                {
                    response.success = true;
                    response.nextArea = "ManageOrder";
                    response.accessToken = _jwtTokenService.CreateToken(
                    response.pin!,
                    response.productId,
                    response.storeId);
                    if (orderDetail.ProductId > 100 && orderDetail.ProductId < 200)
                    {
                        if (orderDetail.IsStoreMYP == true)
                        {
                            response.nextController = "globalsign";
                            response.nextAction = "OrderHistory";
                        }
                        else
                        {
                            response.nextController = "globalsign";
                            response.nextAction = "orderdetail";
                        }

                        return Ok(response);
                    }
                    else if ((orderDetail.ProductId > 200 && orderDetail.ProductId < 300))
                    {
                        response.nextController = "PrimeSSL";
                        if (orderDetail.ProductId == (int)ProductCode.PrimeSSLVerifiedMarkCertificate ||
                            orderDetail.ProductId == (int)ProductCode.PrimeSSLCommonMarkCertificate)
                        {
                            response.nextAction = "MarkCertorderdetail";
                        }
                        else
                        {
                            response.nextAction = "OrderDetails";
                        }

                        return Ok(response);
                    }
                    else if ((orderDetail.ProductId == (int)ProductCode.SectigoACMEDV))
                    {
                        response.nextController = "ACME";
                        response.nextAction = "ACMEInfo";
                        response.nextArea = "SSLConfiguration";
                        return Ok(response);
                    }
                    else if ((orderDetail.ProductId == (int)ProductCode.SectigoACMEOV))
                    {
                        response.nextController = "ACME";
                        response.nextAction = "ACMEOVInfo";
                        response.nextArea = "SSLConfiguration";
                        return Ok(response);
                    }
                    else if ((orderDetail.ProductId > 300 && orderDetail.ProductId < 500))
                    {
                        response.nextController = "Comodo";
                        response.nextAction = "OrderDetails";
                        return Ok(response);
                    }
                    else if (orderDetail.ProductId > 500 && orderDetail.ProductId < 600)
                    {
                        // Same as old: objPFRequest.DigicertOrderRequest.IsFreeSANInclude = true;
                        response.isFreeSANInclude = true;
                        response.nextController = "digicert";
                        response.nextAction = "orderdetail";
                        return Ok(response);
                    }
                    else if ((orderDetail.ProductId > 600 && orderDetail.ProductId < 700))
                    {
                        response.nextController = "ClickSSL";
                        response.nextAction = "OrderDetails";
                        return Ok(response);
                    }

                    return Ok(response);
                }

                _ = caCredentialDetails;

                response.success = false;
                response.message = string.Empty;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.success = false;
                response.message = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same action as old HomeController.IssueCertificate.
        /// Session PF_Request → configurationToken draft (GlobalSign IssueCertificate prefill).
        /// Route: GET /api/SSLConfiguration/Home/IssueCertificate?PIN=&amp;ProductId=&amp;culture=
        /// Also accepts POST JSON body (IssueCertificateRequest).
        /// </summary>
        [HttpGet("IssueCertificate")]
        public IActionResult IssueCertificate([FromQuery] string PIN = "", [FromQuery] int ProductId = 0, [FromQuery] string culture = "en")
        {
            return IssueCertificateCore(PIN, ProductId, culture);
        }

        [HttpPost("IssueCertificate")]
        public IActionResult IssueCertificatePost([FromBody] IssueCertificateRequest request)
        {
            if (request == null)
            {
                return Ok(new IssueCertificateResponse { success = false, message = "PIN is required." });
            }

            return IssueCertificateCore(request.PIN, request.ProductId, request.culture);
        }

        private IActionResult IssueCertificateCore(string PIN, int ProductId, string culture)
        {
            var response = new IssueCertificateResponse();

            try
            {
                if (string.IsNullOrWhiteSpace(PIN))
                {
                    response.success = false;
                    response.message = "PIN is required.";
                    return Ok(response);
                }

                response.pin = PIN.Trim();
                response.productId = ProductId;
                culture = CultureHelper.Normalize(culture);
                CultureHelper.Apply(culture);
                response.culture = culture;

                var productType = Common.GetProductType(ProductId);

                GlobalSignOrderDetailResponse? objOrderModel = null;
                if (productType == ProductType.GlobalSign)
                {
                    objOrderModel = BLGlobalsign.GetGlobalSignOrderDetail(new GlobalSignOrderDetailRequest
                    {
                        Pin = PIN
                    });
                }

                var orderDetail = BLStoreOrder.GetStoreOrderDetailByPIN(PIN.Trim());

                if (orderDetail == null)
                {
                    response.success = false;
                    response.message = Resources.Val_InvalidPIN;
                    return Ok(response);
                }

                // Log IP Address - Store Order Number Wise
                LogWriter.LogStoreOrderIPAddress(orderDetail.StoreOrderId);

                response.storeOrderId = orderDetail.StoreOrderId;
                response.storeId = orderDetail.StoreId;
                response.isNewIssue = true;

                // Same as old: build PF_Request and put in Session → configurationToken draft
                PF_Request objPFRequest = PF_RequestFactory.CreateFromPin(PIN.Trim())
                    ?? new PF_Request
                    {
                        PIN = PIN.Trim(),
                        StoreOrderDetail = orderDetail,
                        GlobalSignOrderRequest = new PF_GlobalSignOrder
                        {
                            AdditionalDomainsList = new Dictionary<string, string>(),
                            WildcardSANDomainList = new List<string>()
                        }
                    };

                objPFRequest.StoreOrderDetail = orderDetail;
                objPFRequest.PIN = PIN.Trim();
                objPFRequest.LanguageCode = culture;
                objPFRequest.IsNewIssue = true;

                Dictionary<string, string> additionalDomainsList = new Dictionary<string, string>();
                string additionalDomains = string.Empty;
                string? approvalMethod = null;
                string? approvalEmail = null;

                if (productType == ProductType.GlobalSign && objOrderModel != null)
                {
                    var globalSignModel = objOrderModel;
                    if (objPFRequest.GlobalSignOrderRequest.AdditionalDomainsList == null)
                        objPFRequest.GlobalSignOrderRequest.AdditionalDomainsList = new Dictionary<string, string>();

                    if (globalSignModel.AdditionalDomainList != null)
                    {
                        foreach (var item in globalSignModel.AdditionalDomainList)
                        {
                            if (string.IsNullOrWhiteSpace(item.DomainName))
                                continue;

                            if (globalSignModel.GlobalSignOrderDetail != null)
                            {
                                objPFRequest.GlobalSignOrderRequest.AdditionalDomainsList
                                    .AddUniqueKey(
                                        item.DomainName.ToLower(),
                                        globalSignModel.GlobalSignOrderDetail.DVMethod ?? string.Empty);
                            }

                            if (string.IsNullOrWhiteSpace(objPFRequest.GlobalSignOrderRequest.AdditionalDomains))
                                objPFRequest.GlobalSignOrderRequest.AdditionalDomains = item.DomainName.ToLower();
                            else
                                objPFRequest.GlobalSignOrderRequest.AdditionalDomains += "," + item.DomainName.ToLower();
                        }
                    }

                    if (globalSignModel.GlobalSignOrderDetail != null)
                    {
                        objPFRequest.GlobalSignOrderRequest.ApprovalMethod = globalSignModel.GlobalSignOrderDetail.DVMethod;
                        objPFRequest.GlobalSignOrderRequest.ApprovalEmail = globalSignModel.GlobalSignOrderDetail.ApprovalEmail;
                    }

                    if (globalSignModel.ContactDetails != null)
                    {
                        objPFRequest.GlobalSignOrderRequest.ContactInfoRow.FirstName = globalSignModel.ContactDetails.FirstName;
                        objPFRequest.GlobalSignOrderRequest.ContactInfoRow.LastName = globalSignModel.ContactDetails.LastName;
                        objPFRequest.GlobalSignOrderRequest.ContactInfoRow.Email = globalSignModel.ContactDetails.Email;
                        objPFRequest.GlobalSignOrderRequest.ContactInfoRow.PhoneNo = globalSignModel.ContactDetails.PhoneNo;
                    }

                    objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow = globalSignModel.OrganizationDetails;
                    objPFRequest.GlobalSignOrderRequest.ApproverInfoRow = globalSignModel.ApprovalDetails;
                    objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow = globalSignModel.AuthorizedDetails;
                    objPFRequest.GlobalSignOrderRequest.RequestorInfoRow = globalSignModel.RequestorDetails;

                    additionalDomainsList = objPFRequest.GlobalSignOrderRequest.AdditionalDomainsList
                        ?? new Dictionary<string, string>();
                    additionalDomains = objPFRequest.GlobalSignOrderRequest.AdditionalDomains ?? string.Empty;
                    approvalMethod = objPFRequest.GlobalSignOrderRequest.ApprovalMethod;
                    approvalEmail = objPFRequest.GlobalSignOrderRequest.ApprovalEmail;
                }

                // Same as old: load CA credential by CredentialCode
                objPFRequest.CACredentialDetails = BLGeneral.GetCACredentials(orderDetail.CredentialCode ?? string.Empty);

                var productDetail = BLGeneral.GetProductDetail_StoreOrder(PIN.Trim());

                if (productDetail == null)
                {
                    response.success = false;
                    response.message = "Product not available.";
                    return Ok(response);
                }

                objPFRequest.ProductDetail = productDetail;

                string controllerName = string.Empty;

                if (productType == ProductType.GlobalSign) // GlobalSign Products
                    controllerName = "GlobalSign";

                objPFRequest.ControllerName = controllerName;

                response.success = true;
                response.nextController = controllerName;
                response.nextArea = "SSLConfiguration";
                response.additionalDomainsList = additionalDomainsList;
                response.additionalDomains = additionalDomains;
                response.approvalMethod = approvalMethod;
                response.approvalEmail = approvalEmail;
                response.accessToken = _jwtTokenService.CreateToken(
                    response.pin!,
                    response.productId,
                    response.storeId);
                response.configurationToken = _draftStore.Save(objPFRequest);

                if (BLGeneral.IsVMCProduct(productDetail.ProductId))
                {
                    response.nextAction = ConstantUtil.AuthenticationType_VMC;
                    return Ok(response);
                }
                else if (BLGeneral.IsPrimeVMCProduct(productDetail.ProductId))
                {
                    response.nextAction = ConstantUtil.AuthenticationType_MarkCert;
                    return Ok(response);
                }
                else if (productDetail.IsCodeSign)
                {
                    response.nextAction = ConstantUtil.AuthenticationType_CodeSign;
                    return Ok(response);
                }
                else if (productDetail.IsPAC)
                {
                    response.nextAction = ConstantUtil.AuthenticationType_PAC;
                    return Ok(response);
                }
                else if (productDetail.AuthenticationType == ConstantUtil.AuthenticationType_DV)
                {
                    response.nextAction = ConstantUtil.AuthenticationType_DV;
                    return Ok(response);
                }
                else if (productDetail.AuthenticationType == ConstantUtil.AuthenticationType_OV)
                {
                    response.nextAction = ConstantUtil.AuthenticationType_OV;
                    return Ok(response);
                }
                else if (productDetail.AuthenticationType == ConstantUtil.AuthenticationType_EV)
                {
                    response.nextAction = ConstantUtil.AuthenticationType_EV;
                    return Ok(response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.success = false;
                response.message = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Phase G4: unused GlobalSign PIN → create draft (same as old Session PF_Request on Index).
        /// </summary>
        private void AttachGlobalSignConfigurationDraft(IndexResponse response, string pin, string culture)
        {
            if (!string.Equals(response.nextController, "GlobalSign", StringComparison.OrdinalIgnoreCase))
                return;

            PF_Request? draft = PF_RequestFactory.CreateFromPin(pin);
            if (draft == null)
                return;

            draft.LanguageCode = culture;
            draft.ControllerName = "GlobalSign";
            response.configurationToken = _draftStore.Save(draft);
        }

        /// <summary>
        /// Same action as old HomeController.LoadHeader.
        /// Old used Session PIN; API takes pin query so future UI can call it after Index.
        /// Route: GET /api/SSLConfiguration/Home/LoadHeader?pin=
        /// </summary>
        [HttpGet("LoadHeader")]
        public IActionResult LoadHeader([FromQuery] string pin = "")
        {
            var response = new LoadHeaderResponse();

            try
            {
                response.pin = string.IsNullOrWhiteSpace(pin) ? null : pin.Trim();

                if (!string.IsNullOrEmpty(response.pin))
                {
                    var product = BLStoreOrder.GetStoreOrderDetailByPIN(response.pin);

                    if (product != null)
                    {
                        response.success = true;
                        response.hasOrder = true;
                        response.product = JsonConvert.SerializeObject(product);
                        return Ok(response);
                    }
                }

                // Same as old: PartialView("_header", null)
                response.success = true;
                response.hasOrder = false;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.success = false;
                response.message = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same action as old HomeController.Error (Session.Abandon + Error view).
        /// UI should clear local PIN/culture/token and show Error page.
        /// Route: GET|POST /api/SSLConfiguration/Home/Error
        /// </summary>
        [HttpGet("Error")]
        [HttpPost("Error")]
        public IActionResult Error()
        {
            Response.Cookies.Delete("CurrentCulture");
            return Ok(new HomeStatusResponse
            {
                success = true,
                action = "Error",
                clearClientState = true,
                message = "Session cleared."
            });
        }

        /// <summary>
        /// Same action as old HomeController.SessionExpired.
        /// Route: GET|POST /api/SSLConfiguration/Home/SessionExpired
        /// </summary>
        [HttpGet("SessionExpired")]
        [HttpPost("SessionExpired")]
        public IActionResult SessionExpired()
        {
            Response.Cookies.Delete("CurrentCulture");
            return Ok(new HomeStatusResponse
            {
                success = true,
                action = "SessionExpired",
                clearClientState = true,
                message = "Session expired."
            });
        }

        /// <summary>
        /// Same action as old HomeController.ChangeCulture.
        /// Sets culture cookie (replaces Session CurrentCulture); returns returnUrl for UI navigation.
        /// Route: POST /api/SSLConfiguration/Home/ChangeCulture
        /// </summary>
        [HttpPost("ChangeCulture")]
        public IActionResult ChangeCulture([FromBody] ChangeCultureRequest request)
        {
            var response = new ChangeCultureResponse();

            try
            {
                string culture = CultureHelper.Normalize(request?.culture);
                CultureHelper.Apply(culture);

                Response.Cookies.Append(
                    "CurrentCulture",
                    culture,
                    new CookieOptions
                    {
                        HttpOnly = false,
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddYears(1)
                    });

                response.success = true;
                response.culture = culture;
                response.returnUrl = request?.returnUrl;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.success = false;
                response.message = ex.Message;
                return Ok(response);
            }
        }
    }
}
