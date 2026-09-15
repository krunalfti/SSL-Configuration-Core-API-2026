using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSLConfiguration.Application;
using SSLConfiguration.Application.Services;
using SSLConfiguration.Contracts.SSLConfiguration.GlobalSign;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.Persistence;
using System.Globalization;
using System.Text.Json;
using VerisignGateway;
using DigicertEntryResponse = SSLConfiguration.Contracts.SSLConfiguration.Digicert.DigicertEntryResponse;

namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration
{
    /// <summary>
    /// Ported from SSLConfiguration Controllers/GlobalSignController —
    /// Entry + CSRInfo + DCV/SAN/Contact/Org/Summary + CodeSign + Thanks.
    /// Session PF_Request → configurationToken draft. Business logic unchanged.
    /// </summary>
    [ClientAuthorize]
    [ApiController]
    [Route("api/SSLConfiguration/[controller]")]
    public class GlobalSignController : ControllerBase
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly ConfigurationDraftStore _draftStore;

        public GlobalSignController(IAuthenticationService authenticationService, ConfigurationDraftStore draftStore)
        {
            _authenticationService = authenticationService;
            _draftStore = draftStore;
        }

        #region Entry — DV / OV / EV / CodeSign

        /// <summary>
        /// Same as old GlobalSignController.DV — requires draft + AuthenticationType DV.
        /// Route: GET /api/SSLConfiguration/GlobalSign/DV?pin=&amp;configurationToken=
        /// </summary>
        [HttpGet("DV")]
        public IActionResult DV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            return Entry(ConstantUtil.AuthenticationType_DV, pin, configurationToken, requireAuthTypeMatch: true);
        }

        /// <summary>Same as old GlobalSignController.OV.</summary>
        [HttpGet("OV")]
        public IActionResult OV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            return Entry(ConstantUtil.AuthenticationType_OV, pin, configurationToken, requireAuthTypeMatch: true);
        }

        /// <summary>Same as old GlobalSignController.EV.</summary>
        [HttpGet("EV")]
        public IActionResult EV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            return Entry(ConstantUtil.AuthenticationType_EV, pin, configurationToken, requireAuthTypeMatch: true);
        }

        /// <summary>
        /// Same as old GlobalSignController.CodeSign — requires draft only (no auth-type gate in old).
        /// Route: GET /api/SSLConfiguration/GlobalSign/CodeSign?pin=&amp;configurationToken=
        /// </summary>
        [HttpGet("CodeSign")]
        public IActionResult CodeSign([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            return Entry(ConstantUtil.AuthenticationType_CodeSign, pin, configurationToken, requireAuthTypeMatch: false);
        }

        #endregion

        #region CSR Info

        /// <summary>
        /// Same as old GlobalSignController.GetCSRInfo.
        /// Route: GET /api/SSLConfiguration/GlobalSign/CSRInfo?configurationToken=
        /// </summary>
        [HttpGet("CSRInfo")]
        public IActionResult CSRInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new CsrInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.CSR = PF_RequestObject.CSR;
                response.IsRenew = PF_RequestObject.isRenew;
                response.OldOrderId = PF_RequestObject.GlobalSignOrderRequest?.OldGSOrderID;
                response.isWildcard = PF_RequestObject.ProductDetail?.IsWildcard;
                response.productId = PF_RequestObject.ProductDetail?.ProductId
                    ?? PF_RequestObject.StoreOrderDetail?.ProductId;
                if (PF_RequestObject.CSRDetailRow != null
                    && !string.IsNullOrEmpty(PF_RequestObject.CSRDetailRow.DomainName))
                {
                    response.data = new CsrDetailDto
                    {
                        DomainName = PF_RequestObject.CSRDetailRow.DomainName,
                        Country = PF_RequestObject.CSRDetailRow.Country,
                        Locality = PF_RequestObject.CSRDetailRow.Locality,
                        Organisation = PF_RequestObject.CSRDetailRow.Organisation,
                        OrganisationUnit = PF_RequestObject.CSRDetailRow.OrganisationUnit,
                        State = PF_RequestObject.CSRDetailRow.State,
                        Email = PF_RequestObject.CSRDetailRow.Email,
                        CSR = PF_RequestObject.CSRDetailRow.CSR ?? PF_RequestObject.CSR
                    };
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.UpdateCSRDetail.
        /// Route: POST /api/SSLConfiguration/GlobalSign/CSRInfo
        /// </summary>
        [HttpPost("CSRInfo")]
        public IActionResult CSRInfoPost([FromBody] CsrInfoPostRequest? objModel)
        {
            var response = new CsrInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                CSRDetail objCSRInfo = new CSRDetail();
                if (string.IsNullOrEmpty(objModel?.CSR))
                {
                    PF_RequestObject.CSRDetailRow = null;
                    PF_RequestObject.CSR = null;
                    _draftStore.Update(resolved.token, PF_RequestObject);
                    response.IsSuccess = false;
                    response.Msg = Resources.Val_CSRRequired;
                    return Ok(response);
                }

                VerisignGateway.ProductCode pcode = BLGeneral.GetProductCodeByProductName(
                    PF_RequestObject.StoreOrderDetail?.ProductId
                    ?? PF_RequestObject.ProductDetail?.ProductId
                    ?? 0);

                ValidateAndParseCSRResponse objRes = VerisignAPIHelper.GSValidateAndParseCSR(
                    pcode,
                    objModel!.CSR.Trim(),
                    PF_RequestObject.isRenew,
                    PF_RequestObject.GlobalSignOrderRequest.OldGSOrderID,
                    PF_RequestObject.CACredentialDetails.GetGlobalSignCACredential());

                //int ErrorCode = objRes.error.ErrorCode;
                if (objRes.error != null && objRes.error.ErrorCode == 0)
                {
                    if (PF_RequestObject.ProductDetail!.IsWildcard)
                    {
                        if (!objRes.DomainName.ToLower().StartsWith("*."))
                        {
                            response.IsSuccess = false;
                            response.Msg = Resources.CSR_Note_Wildcard;
                            return Ok(response);
                        }
                    }
                    else
                    {
                        if (objRes.DomainName.ToLower().StartsWith("*."))
                        {
                            response.IsSuccess = false;
                            response.Msg = Resources.Val_CSRWithWildcardDomainNameNowAllowed;
                            return Ok(response);
                        }
                    }

                    objCSRInfo.DomainName = objRes.DomainName;
                    objCSRInfo.Country = objRes.Country;
                    objCSRInfo.Locality = objRes.Locality;
                    objCSRInfo.Organisation = objRes.Organisation;
                    objCSRInfo.OrganisationUnit = objRes.OrganisationUnit;
                    objCSRInfo.State = objRes.State;
                    objCSRInfo.CSR = objModel.CSR;
                    if (objRes.Email.Contains("@"))
                        objCSRInfo.Email = objRes.Email;

                    PF_RequestObject.CSRDetailRow = objCSRInfo;
                    PF_RequestObject.CSR = objModel.CSR;
                    //when CSR changes dropdown of DCV email also changes so done Empty;
                    var ApprovalEmailList = BLGeneral.GetApprovalEmailList(
                        PF_RequestObject.CSRDetailRow.DomainName,
                        PF_RequestObject.StoreOrderDetail!.ProductId,
                        PF_RequestObject.CACredentialDetails.GetGlobalSignCACredential());
                    if (ApprovalEmailList == null || ApprovalEmailList.Count <= 1)
                    {
                        if (!string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.ApprovalEmail))
                            PF_RequestObject.GlobalSignOrderRequest.ApprovalEmail = string.Empty;
                        _draftStore.Update(resolved.token, PF_RequestObject);
                        response.IsSuccess = false;
                        response.Msg = "Domain Name has an invalid value";
                        return Ok(response);
                    }

                    if (PF_RequestObject.StoreOrderDetail.ProductId == (int)VerisignGateway.ProductCode.AlphaSSL
                        || PF_RequestObject.StoreOrderDetail.ProductId == (int)VerisignGateway.ProductCode.AlphaWildCard)
                    {
                        // check for oldGSOrderId / renewal
                        var gsOrderList = VerisignAPIHelper.GSGetOrderStatusForRenewal(
                            DateTime.Now,
                            DateTime.Now.AddDays(90),
                            objRes.DomainName,
                            PF_RequestObject.CACredentialDetails.GetGlobalSignCACredential());

                        if (gsOrderList != null && gsOrderList.Count > 0)
                        {
                            var oldGSOrderDetails = gsOrderList.OrderByDescending(s => s.IssueDate).Take(1).SingleOrDefault();

                            if (oldGSOrderDetails == null)
                            {
                                PF_RequestObject.isRenew = false;
                                PF_RequestObject.GlobalSignOrderRequest.OldGSOrderID = string.Empty;
                                PF_RequestObject.OldGSOrderDetails = null;
                            }
                            else
                            {
                                PF_RequestObject.isRenew = true;
                                PF_RequestObject.GlobalSignOrderRequest.OldGSOrderID = oldGSOrderDetails.GSOrderNumber;
                                PF_RequestObject.OldGSOrderDetails = oldGSOrderDetails;
                            }
                        }
                        else
                        {
                            PF_RequestObject.isRenew = false;
                            PF_RequestObject.GlobalSignOrderRequest.OldGSOrderID = string.Empty;
                            PF_RequestObject.OldGSOrderDetails = null;
                        }
                    }
                    else
                    {
                        PF_RequestObject.isRenew = objModel.IsRenew;
                        PF_RequestObject.GlobalSignOrderRequest.OldGSOrderID = objModel.OldOrderId;
                    }

                    _draftStore.Update(resolved.token, PF_RequestObject);

                    response.IsSuccess = true;
                    response.IsRenew = PF_RequestObject.isRenew;
                    response.OldOrderId = PF_RequestObject.GlobalSignOrderRequest.OldGSOrderID;
                    response.data = new CsrDetailDto
                    {
                        DomainName = objCSRInfo.DomainName,
                        Country = objCSRInfo.Country,
                        Locality = objCSRInfo.Locality,
                        Organisation = objCSRInfo.Organisation,
                        OrganisationUnit = objCSRInfo.OrganisationUnit,
                        State = objCSRInfo.State,
                        Email = objCSRInfo.Email,
                        CSR = objCSRInfo.CSR
                    };
                    return Ok(response);
                }
                else
                {
                    LogWriter.LogCARequestResponseObjectToDB(
                        PF_RequestObject.StoreOrderDetail?.Pin ?? string.Empty,
                        objRes.CARequestObject,
                        JsonSerializer.Serialize(objRes.error),
                        "GSValidateAndParseCSR"); // CA Error log to LogMaster
                    response.IsSuccess = false;
                    response.Msg = TranslateUtility.Translate(
                        "en",
                        CultureHelper.Normalize(CultureInfo.CurrentUICulture.Name),
                        objRes.error?.ErrorMessage ?? string.Empty);
                    return Ok(response);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message.ToString();
                return Ok(response);
            }
        }

        #endregion

        #region DCV Info

        /// <summary>
        /// Same as old GlobalSignController.GetDCVInfo.
        /// Route: GET /api/SSLConfiguration/GlobalSign/DCVInfo
        /// </summary>
        [HttpGet("DCVInfo")]
        public IActionResult DCVInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new DcvInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);

                if (!string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod) && PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod.ToUpper() == ConstantUtil.GlobalSign_DCVMethod_URL)
                {
                    response.ApprovalMethod = 1;
                }
                else if (!string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod) && PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod.ToUpper() == ConstantUtil.GlobalSign_DCVMethod_EMAIL)
                {
                    response.ApprovalMethod = 2;
                }
                else if (!string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod) && PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod.ToUpper() == ConstantUtil.GlobalSign_DCVMethod_DNS)
                {
                    response.ApprovalMethod = 3;
                }
                else
                {
                    response.ApprovalMethod = 0;
                }
                response.ApprovalEmail = string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.ApprovalEmail) ? string.Empty : PF_RequestObject.GlobalSignOrderRequest.ApprovalEmail;
                if (PF_RequestObject.StoreOrderDetail!.ProductId == (int)VerisignGateway.ProductCode.AlphaSSL || PF_RequestObject.StoreOrderDetail.ProductId == (int)VerisignGateway.ProductCode.AlphaWildCard)
                {
                    response.OldOrderId = PF_RequestObject.GlobalSignOrderRequest.OldGSOrderID;
                    response.IsRenew = PF_RequestObject.isRenew;
                }

                var approvalEmailList = BLGeneral.GetApprovalEmailList(
                    PF_RequestObject.CSRDetailRow!.DomainName,
                    PF_RequestObject.StoreOrderDetail.ProductId,
                    PF_RequestObject.CACredentialDetails.GetGlobalSignCACredential());
                response.ApprovalEmailList = approvalEmailList.Select(ToSelectListItemDto).ToList();
                response.productId = PF_RequestObject.StoreOrderDetail.ProductId;
                response.isWildcard = PF_RequestObject.ProductDetail?.IsWildcard;
                response.isMultiDomain = PF_RequestObject.ProductDetail?.IsMultiDomain;
                response.authenticationType = PF_RequestObject.ProductDetail?.AuthenticationType;
                response.hasRenewalInfo = PF_RequestObject.OldGSOrderDetails != null;
                response.renewalDomain = PF_RequestObject.OldGSOrderDetails?.Domain;
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                _draftStore.Update(resolved.token, PF_RequestObject);
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.UpdateDCVInfo.
        /// Route: POST /api/SSLConfiguration/GlobalSign/DCVInfo
        /// </summary>
        [HttpPost("DCVInfo")]
        public IActionResult DCVInfoPost([FromBody] DcvInfoPostRequest? objModel)
        {
            var response = new DcvInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                string ApprovalMethod = objModel?.ApprovalMethod ?? string.Empty;
                string ApprovalEmail = objModel?.ApprovalEmail ?? string.Empty;

                if (string.IsNullOrEmpty(ApprovalMethod))
                {
                    response.IsSuccess = false;
                    response.Message = "Please select approval method.";
                    return Ok(response);
                }
                if (ApprovalMethod == ConstantUtil.DCVMethod_EMAIL)
                {
                    if (string.IsNullOrEmpty(ApprovalEmail))
                    {
                        response.IsSuccess = false;
                        response.Message = "Please select approval email.";
                        return Ok(response);
                    }
                }
                else
                {
                    ApprovalEmail = string.Empty;
                }
                if (ApprovalMethod == ConstantUtil.DCVMethod_FILE)
                    PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod = ConstantUtil.GlobalSign_DCVMethod_URL;
                else if (ApprovalMethod == ConstantUtil.DCVMethod_EMAIL)
                    PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod = ConstantUtil.GlobalSign_DCVMethod_EMAIL;
                else if (ApprovalMethod == ConstantUtil.DCVMethod_DNS)
                    PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod = ConstantUtil.GlobalSign_DCVMethod_DNS;
                else
                    PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod = ConstantUtil.GlobalSign_DCVMethod_EMAIL;

                PF_RequestObject.GlobalSignOrderRequest.ApprovalEmail = ApprovalEmail;

                if (PF_RequestObject.StoreOrderDetail!.ProductId == (int)VerisignGateway.ProductCode.AlphaSSL || PF_RequestObject.StoreOrderDetail.ProductId == (int)VerisignGateway.ProductCode.AlphaWildCard)
                {
                    PF_RequestObject.GlobalSignOrderRequest.OldGSOrderID = objModel!.OldOrderId;
                    PF_RequestObject.isRenew = objModel.IsRenew;
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.ismulti = PF_RequestObject.ProductDetail!.IsMultiDomain;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = ex.Message.ToString();
                return Ok(response);
            }
        }

        #endregion

        #region SAN Info

        /// <summary>
        /// Same as old GlobalSignController.GetSANInfo.
        /// Route: GET /api/SSLConfiguration/GlobalSign/SANInfo
        /// </summary>
        [HttpGet("SANInfo")]
        public IActionResult SANInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new SanInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);

                if (!PF_RequestObject.ProductDetail!.IsWildcardMultiDomain == true)
                {
                    response.IsAutoDiscover = string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.GS_SAN_AutoDiscoverDomain) ? false : true;
                    response.IsMail = string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.GS_SAN_MailDomain) ? false : true;
                    response.IsOWA = string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.GS_SAN_OWADomain) ? false : true;
                    response.DomainName = string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.GS_SAN_DomainName) ? string.Empty : PF_RequestObject.GlobalSignOrderRequest.GS_SAN_DomainName;
                }

                response.NoOfAdditionalDomains = BLGeneral.GetAddDomain(PF_RequestObject.StoreOrderDetail!.StoreOrderId, PF_RequestObject.StoreOrderDetail.ProductId);

                response.AdditionalDomains = string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains) ? string.Empty : PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains;

                response.AdditionalDomainList = new List<string>();

                if (!string.IsNullOrEmpty(response.AdditionalDomains))
                {
                    string[] strSplitArr = response.AdditionalDomains.TrimEnd(',').Split(',');
                    foreach (var str in strSplitArr)
                    {
                        response.AdditionalDomainList.Add(str);
                    }
                }
                // new added by jaynit for Duplication SAN validation Purpose only.
                if (PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList == null)
                    PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList = new Dictionary<string, string>();

                // wilcard san

                response.MaxWildcardSAN = BLGeneral.GetWildcardSANCount(PF_RequestObject.StoreOrderDetail.StoreOrderId, PF_RequestObject.StoreOrderDetail.ProductId);

                if (PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList == null)
                    PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList = new List<string>();

                // bind wildcard domains

                response.WidlcardSANDomainList = new List<string>();
                foreach (var san in PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList)
                {
                    response.WidlcardSANDomainList.AddUniqueKey(san);
                }

                // get domain types
                response.AdditionalDomainType = BLGeneral.GetDomainTypeList(PF_RequestObject.StoreOrderDetail.ProductId)
                    .Select(ToSelectListItemDto).ToList();

                response.isWildcardMultiDomain = PF_RequestObject.ProductDetail.IsWildcardMultiDomain;
                response.isMultiDomain = PF_RequestObject.ProductDetail.IsMultiDomain;
                response.isFlex = PF_RequestObject.ProductDetail.IsFlex;
                response.authenticationType = PF_RequestObject.ProductDetail.AuthenticationType;
                response.csrDomainName = PF_RequestObject.CSRDetailRow?.DomainName;
                response.productId = PF_RequestObject.StoreOrderDetail.ProductId;
                response.WildcardSAN = response.WidlcardSANDomainList != null && response.WidlcardSANDomainList.Count > 0
                    ? string.Join(",", response.WidlcardSANDomainList)
                    : string.Empty;
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                _draftStore.Update(resolved.token, PF_RequestObject);
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.UpdateSANInfo.
        /// Route: POST /api/SSLConfiguration/GlobalSign/SANInfo
        /// </summary>
        [HttpPost("SANInfo")]
        public IActionResult SANInfoPost([FromBody] SanInfoPostRequest? objModel)
        {
            var response = new SanInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                if (!PF_RequestObject.ProductDetail!.IsWildcardMultiDomain == true)
                {
                    if ((objModel!.IsOWA || objModel.IsAutoDiscover || objModel.IsMail) && string.IsNullOrEmpty(objModel.DomainName))
                    {
                        response.IsSuccess = false;
                        response.Message = Resources.Val_DomainNameRequired;
                        return Ok(response);
                    }
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_AutoDiscoverDomain = objModel.IsAutoDiscover ? "autodiscover." + objModel.DomainName!.Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_MailDomain = objModel.IsMail ? "mail." + objModel.DomainName!.Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_OWADomain = objModel.IsOWA ? "owa." + objModel.DomainName!.Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_DomainName = objModel.DomainName;
                }

                if (string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains))
                {
                    PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains = string.Empty;
                }
                else
                {
                    PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains = PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains.TrimEnd(',');
                }

                // bind wildcard domains if null

                if (PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList == null)
                    PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList = new List<string>();

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Message = "";
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = ex.Message.ToString();
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.GetAdditionalDomains.
        /// Route: POST /api/SSLConfiguration/GlobalSign/GetAdditionalDomains
        /// </summary>
        [HttpPost("GetAdditionalDomains")]
        public IActionResult GetAdditionalDomains([FromBody] GetAdditionalDomainsRequest? objModel)
        {
            var response = new GetAdditionalDomainsResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                string? domainName = objModel?.domainName;
                string? additionalDomains = objModel?.additionalDomains;
                string? activeadditionalDomains = objModel?.activeadditionalDomains;
                bool isMail = objModel?.isMail ?? false;
                bool isOWA = objModel?.isOWA ?? false;
                bool isAutoDiscover = objModel?.isAutoDiscover ?? false;
                string? domainType = objModel?.domainType;

                if (!PF_RequestObject.ProductDetail!.IsWildcardMultiDomain == true)
                {
                    if ((isOWA || isAutoDiscover || isMail) && !string.IsNullOrEmpty(domainName))
                        PF_RequestObject.GlobalSignOrderRequest.GS_SAN_AutoDiscoverDomain = isAutoDiscover ? "autodiscover." + domainName.Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_MailDomain = isMail ? "mail." + (domainName ?? string.Empty).Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_OWADomain = isOWA ? "owa." + (domainName ?? string.Empty).Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_DomainName = domainName;
                }

                if (string.IsNullOrEmpty(additionalDomains))
                {
                    response.IsSuccess = false;
                    response.Message = Resources.DV_SANRequired;
                    return Ok(response);
                }

                if (PF_RequestObject.ProductDetail.AuthenticationType != ConstantUtil.AuthenticationType_DV)
                {
                    if (!PF_RequestObject.ProductDetail.IsWildcardMultiDomain == true)
                    {
                        if (string.IsNullOrEmpty(domainType))
                        {
                            response.IsSuccess = false;
                            response.Message = Resources.DV_SelectSubDomainType;
                            return Ok(response);
                        }
                    }
                }
                string[] addDomainList = additionalDomains.TrimEnd(',').TrimEnd('\n').Split('\n');

                int MaxSAN = BLGeneral.GetAddDomain(PF_RequestObject.StoreOrderDetail!.StoreOrderId, PF_RequestObject.StoreOrderDetail.ProductId);
                if (addDomainList.Count() > MaxSAN)
                {
                    if (PF_RequestObject.ProductDetail.IsWildcardMultiDomain)
                    {
                        response.IsSuccess = false;
                        response.Message = Resources.DV_WildCardSAN_MaxLabel + MaxSAN;
                        return Ok(response);
                    }
                    else
                    {
                        response.IsSuccess = false;
                        response.Message = Resources.DV_MaxSANAllowed + MaxSAN;
                        return Ok(response);
                    }
                }

                foreach (var item in addDomainList)
                {
                    if (PF_RequestObject.ProductDetail.IsWildcardMultiDomain) // Only Wildcard SAN Allowed
                    {
                        if (!BLGeneral.isValidWildcardDomain(item.Trim()))
                        {
                            response.IsSuccess = false;
                            response.Message = "Invalid domain name : " + item.Trim().Replace(" ", "[space]");
                            return Ok(response);
                        }
                    }
                    else // Only SAN Allowed
                    {
                        if (!BLGeneral.isValidSingleDomain(item.Trim()))
                        {
                            response.IsSuccess = false;
                            response.Message = "Invalid domain name : " + item.Trim().Replace(" ", "[space]");
                            return Ok(response);
                        }
                    }
                }

                var sanCount = 0;

                if (string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains))
                {
                    sanCount = 0;
                }
                else
                {
                    sanCount = PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains.TrimEnd(',').Split(',').Count();
                }
                if (sanCount + addDomainList.Count() > MaxSAN)
                {
                    if (PF_RequestObject.ProductDetail.IsWildcardMultiDomain)
                    {
                        response.IsSuccess = false;
                        response.Message = Resources.DV_WildCardSAN_MaxLabel + MaxSAN;
                        return Ok(response);
                    }
                    else
                    {
                        //"You can add maximum " + objModel.NoOfAdditionalDomains + " Additional Domains";
                        response.IsSuccess = false;
                        response.Message = Resources.DV_MaxSANAllowed + MaxSAN;
                        return Ok(response);
                    }
                }
                var AddDomainss = string.Empty;
                if (string.IsNullOrEmpty(activeadditionalDomains))
                    activeadditionalDomains = string.Empty;
                if (!string.IsNullOrEmpty(domainType))
                {
                    // Added by Jaynit new Model Property AdditionalDomainsList
                    // NOTE : for concatinating string of  domaintype and SAN
                    if (!string.IsNullOrEmpty(additionalDomains))
                    {
                        foreach (var item in addDomainList)
                        {
                            if (!PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList!.Keys.Contains(item))
                                PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList.Add(item, domainType);
                        }
                    }
                    foreach (var item in PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList!)
                    {
                        AddDomainss += item.Value + "|" + item.Key.Trim() + ",";
                        PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains = AddDomainss.Trim();
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(additionalDomains))
                    {
                        foreach (var item in addDomainList)
                        {
                            if (!PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList!.Keys.Contains(item))
                                PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList.Add(item, "");
                        }
                    }
                    foreach (var item in PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList!)
                    {
                        AddDomainss += item.Key.Trim() + ",";
                        PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains = AddDomainss.Trim();
                    }
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Message = "success";
                response.AdditionalDomains = PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains;
                response.AdditionalDomainsList = PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList;
                response.AdditionalDomainList = string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains)
                    ? new List<string>()
                    : PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains.TrimEnd(',').Split(',').ToList();
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = "error";
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.DeleteAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/GlobalSign/DeleteAdditionalDomain
        /// </summary>
        [HttpPost("DeleteAdditionalDomain")]
        public IActionResult DeleteAdditionalDomain([FromBody] DeleteAdditionalDomainRequest? objModel)
        {
            var response = new DeleteAdditionalDomainResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                string? san = objModel?.san;
                bool isMail = objModel?.isMail ?? false;
                bool isOWA = objModel?.isOWA ?? false;
                bool isAutoDiscover = objModel?.isAutoDiscover ?? false;
                string? domainName = objModel?.domainName;

                if (!PF_RequestObject.ProductDetail!.IsWildcardMultiDomain == true)
                {
                    if ((isOWA || isAutoDiscover || isMail) && !string.IsNullOrEmpty(domainName))
                        PF_RequestObject.GlobalSignOrderRequest.GS_SAN_AutoDiscoverDomain = isAutoDiscover ? "autodiscover." + domainName.Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_MailDomain = isMail ? "mail." + (domainName ?? string.Empty).Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_OWADomain = isOWA ? "owa." + (domainName ?? string.Empty).Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_DomainName = domainName;
                }

                if (!(string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains) && string.IsNullOrEmpty(san)))
                {
                    PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains = (PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains!.EndsWith(",") ?
                        PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains
                        : PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains + ",");
                    int index = PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains.IndexOf((san + ","));
                    string additionalDomains = (index < 0)
                        ? PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains
                        : PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains.Remove(index, (san + ",").Length);

                    PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains = additionalDomains;

                    // Added by Jaynit new Model Property AdditionalDomainsList
                    // NOTE : for Deleting string of  domaintype and SAN
                    string words = san ?? string.Empty;
                    string domain = words.Split('|').Count() > 1 ? words.Split('|')[1] : words;

                    if (PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList!.ContainsKey(domain))
                        PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList.Remove(domain);
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Message = "success";
                response.AdditionalDomains = PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains;
                response.AdditionalDomainList = string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains)
                    ? new List<string>()
                    : PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains.TrimEnd(',').Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList();
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = "error";
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.GetWildCardSans.
        /// Route: POST /api/SSLConfiguration/GlobalSign/GetWildCardSans
        /// </summary>
        [HttpPost("GetWildCardSans")]
        public IActionResult GetWildCardSans([FromBody] GetWildCardSansRequest? objModel)
        {
            var response = new GetWildCardSansResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                string? WildcardSAN = objModel?.WildcardSAN;
                bool isMail = objModel?.isMail ?? false;
                bool isOWA = objModel?.isOWA ?? false;
                bool isAutoDiscover = objModel?.isAutoDiscover ?? false;
                string? domainName = objModel?.domainName;

                if (!PF_RequestObject.ProductDetail!.IsWildcardMultiDomain == true)
                {
                    if ((isOWA || isAutoDiscover || isMail) && !string.IsNullOrEmpty(domainName))
                        PF_RequestObject.GlobalSignOrderRequest.GS_SAN_AutoDiscoverDomain = isAutoDiscover ? "autodiscover." + domainName.Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_MailDomain = isMail ? "mail." + (domainName ?? string.Empty).Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_OWADomain = isOWA ? "owa." + (domainName ?? string.Empty).Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_DomainName = domainName;
                }

                if (string.IsNullOrEmpty(WildcardSAN))
                    WildcardSAN = string.Empty;

                string[] addDomainList = WildcardSAN.Trim().Split('\n');

                int MaxWildcardSAN = BLGeneral.GetWildcardSANCount(PF_RequestObject.StoreOrderDetail!.StoreOrderId, PF_RequestObject.StoreOrderDetail.ProductId);

                if (addDomainList.Count() > MaxWildcardSAN)
                {
                    response.IsSuccess = false;
                    response.Message = Resources.DV_WildCardSAN_MaxLabel + MaxWildcardSAN;
                    return Ok(response);
                }

                foreach (var item in addDomainList)
                {
                    if (!BLGeneral.isValidWildcardDomain(item.Trim()))
                    {
                        response.IsSuccess = false;
                        response.Message = "Invalid domain name : " + item.Trim().Replace(" ", "[space]");
                        return Ok(response);
                    }
                }
                var sanCount = 0;
                if (PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList!.Count() == 0)
                {
                    sanCount = 0;
                }
                else
                {
                    sanCount = PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList.Count();
                }
                if (sanCount + addDomainList.Count() > MaxWildcardSAN)
                {
                    response.IsSuccess = false;
                    response.Message = Resources.DV_WildCardSAN_MaxLabel + MaxWildcardSAN;
                    return Ok(response);
                }

                if (!string.IsNullOrEmpty(WildcardSAN))
                {
                    string[] strSplitArr = WildcardSAN.Trim().Split('\n');
                    foreach (var str in strSplitArr)
                    {
                        PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList.AddUniqueKey(str);
                    }
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Message = "success";
                response.WidlcardSANDomainList = PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList.ToList();
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = "error";
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.DeleteWildCardSAN.
        /// Route: POST /api/SSLConfiguration/GlobalSign/DeleteWildCardSAN
        /// </summary>
        [HttpPost("DeleteWildCardSAN")]
        public IActionResult DeleteWildCardSAN([FromBody] DeleteWildCardSanRequest? objModel)
        {
            var response = new DeleteWildCardSanResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                string? san = objModel?.san;
                bool isMail = objModel?.isMail ?? false;
                bool isOWA = objModel?.isOWA ?? false;
                bool isAutoDiscover = objModel?.isAutoDiscover ?? false;
                string? domainName = objModel?.domainName;

                if (!PF_RequestObject.ProductDetail!.IsWildcardMultiDomain == true)
                {
                    if ((isOWA || isAutoDiscover || isMail) && !string.IsNullOrEmpty(domainName))
                        PF_RequestObject.GlobalSignOrderRequest.GS_SAN_AutoDiscoverDomain = isAutoDiscover ? "autodiscover." + domainName.Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_MailDomain = isMail ? "mail." + (domainName ?? string.Empty).Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_OWADomain = isOWA ? "owa." + (domainName ?? string.Empty).Trim() : string.Empty;
                    PF_RequestObject.GlobalSignOrderRequest.GS_SAN_DomainName = domainName;
                }

                if (!string.IsNullOrEmpty(san))
                {
                    if (PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList!.Contains(san))
                    {
                        PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList.Remove(san);
                    }
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Message = "success";
                response.WidlcardSANDomainList = PF_RequestObject.GlobalSignOrderRequest.WildcardSANDomainList?.ToList();
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = "error";
                return Ok(response);
            }
        }

        #endregion

        #region Contact Info

        /// <summary>
        /// Same as old GlobalSignController.GetContactInfo.
        /// Route: GET /api/SSLConfiguration/GlobalSign/ContactInfo
        /// </summary>
        [HttpGet("ContactInfo")]
        public IActionResult ContactInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new ContactInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                EnsureContactAndOrgRows(PF_RequestObject);

                var contact = new ContactInfoDto();
                // bind ContactInfo from session object
                contact.ContactFirstName = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.FirstName;
                contact.ContactLastName = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.LastName;
                contact.ContactEmail = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.Email;
                contact.ContactPhone = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.PhoneNo;

                // this fields will Require when Product EV is Active
                if (PF_RequestObject.ProductDetail!.AuthenticationType == ConstantUtil.AuthenticationType_EV)
                {
                    // bind requestor info
                    contact.RequestorEmail = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.Email;
                    contact.RequestorFirstName = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.FirstName;
                    contact.RequestorLastName = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.LastName;
                    contact.RequestorOrgName = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.OrganizationName;
                    contact.RequestorOrgRole = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.FunctionInOrg;
                    contact.RequestorOrgUnit = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.OrgUnit;
                    contact.RequestorPhone = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.PhoneNo;
                    // bind Approver info
                    contact.ApprovarEmail = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.Email;
                    contact.ApprovarFirstName = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.FirstName;
                    contact.ApprovarLastName = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.LastName;
                    contact.ApprovarOrgName = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.OrganizationName;
                    contact.ApprovarOrgRole = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.FunctionInOrg;
                    contact.ApprovarOrgUnit = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.OrgUnit;
                    contact.ApprovarPhone = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.PhoneNo;
                    // bind Autherized info
                    contact.AutherizedEmail = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.Email;
                    contact.AutherizedFirstName = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.FirstName;
                    contact.AutherizedLastName = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.LastName;
                    contact.AutherizedOrgName = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.OrganizationName;
                    contact.AutherizedOrgRole = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.FunctionInOrg;
                    contact.AutherizedPhone = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.PhoneNo;
                }

                response.contact = contact;
                response.authenticationType = PF_RequestObject.ProductDetail.AuthenticationType;
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                _draftStore.Update(resolved.token, PF_RequestObject);
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.UpdateContactInfo.
        /// Route: POST /api/SSLConfiguration/GlobalSign/ContactInfo
        /// </summary>
        [HttpPost("ContactInfo")]
        public IActionResult ContactInfoPost([FromBody] ContactInfoPostRequest? objModel)
        {
            var response = new ContactInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                // contact info
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow = new GlobalSignContactInfo();

                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.FirstName = objModel!.ContactFirstName;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.LastName = objModel.ContactLastName;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.Email = objModel.ContactEmail;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.PhoneNo = objModel.ContactPhone;

                // this fields will Require when Product EV is Active
                if (PF_RequestObject.ProductDetail!.AuthenticationType == ConstantUtil.AuthenticationType_EV)
                {
                    EnsureContactAndOrgRows(PF_RequestObject);
                    // requestor info
                    PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.Email = objModel.RequestorEmail;
                    PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.FirstName = objModel.RequestorFirstName;
                    PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.LastName = objModel.RequestorLastName;
                    PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.OrganizationName = objModel.RequestorOrgName;
                    PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.FunctionInOrg = objModel.RequestorOrgRole;
                    PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.OrgUnit = objModel.RequestorOrgUnit;
                    PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.PhoneNo = objModel.RequestorPhone;
                    // Approver info
                    PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.Email = objModel.ApprovarEmail;
                    PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.FirstName = objModel.ApprovarFirstName;
                    PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.LastName = objModel.ApprovarLastName;
                    PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.OrganizationName = objModel.ApprovarOrgName;
                    PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.FunctionInOrg = objModel.ApprovarOrgRole;
                    PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.OrgUnit = objModel.ApprovarOrgUnit;
                    PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.PhoneNo = objModel.ApprovarPhone;
                    // Autherized info
                    PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.Email = objModel.AutherizedEmail;
                    PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.FirstName = objModel.AutherizedFirstName;
                    PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.LastName = objModel.AutherizedLastName;
                    PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.OrganizationName = objModel.AutherizedOrgName;
                    PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.FunctionInOrg = objModel.AutherizedOrgRole;
                    PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.PhoneNo = objModel.AutherizedPhone;
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Message = "";
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = ex.Message.ToString();
                return Ok(response);
            }
        }

        #endregion

        #region Organization Info

        /// <summary>
        /// Same as old GlobalSignController.GetOrganisationInfo.
        /// Route: GET /api/SSLConfiguration/GlobalSign/OrganisationInfo
        /// </summary>
        [HttpGet("OrganisationInfo")]
        public IActionResult OrganisationInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new OrganisationInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                EnsureContactAndOrgRows(PF_RequestObject);

                var organisation = new OrganisationInfoDto();
                // bind GSOrganizationInformation
                organisation.Address1 = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Address1;
                organisation.Address2 = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Address2;
                organisation.City = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.City;
                organisation.CountryName = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Country;
                //organisation.DBAname = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.AssumedName;
                organisation.Division = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Division;
                organisation.Duns = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Duns;
                organisation.Email = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Email;
                organisation.Fax = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Fax;
                organisation.Orgname = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.LegalName;
                //organisation.OrgType = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.LegalName;
                organisation.PhoneNo = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo;
                organisation.PostalCode = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode;
                organisation.State = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.State;
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();

                // this fields will Require when Product EV is Active
                if (PF_RequestObject.ProductDetail!.AuthenticationType == ConstantUtil.AuthenticationType_EV)
                {
                    var s = PF_RequestObject.GlobalSignOrderRequest.enmOrganisaztionType;
                    organisation.OrgType = Convert.ToString(((int)s));
                    // bind juriction info
                    organisation.JurictionCity = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity;
                    organisation.JurictionCountryName = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry;
                    organisation.jurictionState = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState;
                    organisation.AgencyRegNumber = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionRegNo;
                }

                response.organisation = organisation;
                response.authenticationType = PF_RequestObject.ProductDetail.AuthenticationType;
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                _draftStore.Update(resolved.token, PF_RequestObject);
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.UpdateOrganisationInfo.
        /// Route: POST /api/SSLConfiguration/GlobalSign/OrganisationInfo
        /// </summary>
        [HttpPost("OrganisationInfo")]
        public IActionResult OrganisationInfoPost([FromBody] OrganisationInfoPostRequest? objModel)
        {
            var response = new OrganisationInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                // save organizatino info
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow = new GlobalSignOrganizationInfo();
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Address1 = objModel!.Address1;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Address2 = objModel.Address2;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.City = objModel.City;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Country = objModel.CountryName;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Division = objModel.Division;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Duns = objModel.Duns;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Email = objModel.Email;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Fax = objModel.Fax;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.LegalName = objModel.Orgname;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo = objModel.PhoneNo;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode = objModel.PostalCode;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.State = objModel.State;

                // this fields will Require when Product EV is Active
                if (PF_RequestObject.ProductDetail!.AuthenticationType == ConstantUtil.AuthenticationType_EV)
                {
                    // save juriction info
                    PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity = objModel.JurictionCity;
                    PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry = objModel.JurictionCountryName;
                    PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState = objModel.jurictionState;
                    PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionRegNo = objModel.AgencyRegNumber;
                    PF_RequestObject.GlobalSignOrderRequest.enmOrganisaztionType = (GSOrganizationType)Convert.ToInt32(objModel.OrgType);
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Message = "";
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = ex.Message.ToString();
                return Ok(response);
            }
        }

        #endregion

        #region Summary Info

        /// <summary>
        /// Same as old GlobalSignController.GetSummary.
        /// Route: GET /api/SSLConfiguration/GlobalSign/Summary
        /// </summary>
        [HttpGet("Summary")]
        public IActionResult SummaryGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new SummaryGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.CSR = PF_RequestObject.CSR;
                response.domainName = PF_RequestObject.CSRDetailRow?.DomainName;
                response.ApprovalMethod = PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod;
                response.ApprovalEmail = PF_RequestObject.GlobalSignOrderRequest.ApprovalEmail;
                response.AdditionalDomains = PF_RequestObject.GlobalSignOrderRequest.AdditionalDomains;
                response.authenticationType = PF_RequestObject.ProductDetail?.AuthenticationType;
                response.isMultiDomain = PF_RequestObject.ProductDetail?.IsMultiDomain;
                response.isWildcard = PF_RequestObject.ProductDetail?.IsWildcard;
                response.isWildcardMultiDomain = PF_RequestObject.ProductDetail?.IsWildcardMultiDomain;
                response.productId = PF_RequestObject.ProductDetail?.ProductId ?? PF_RequestObject.StoreOrderDetail?.ProductId;
                response.storeOrderId = PF_RequestObject.StoreOrderDetail?.StoreOrderId;
                response.isMultiYearOrder = Convert.ToBoolean(PF_RequestObject.StoreOrderDetail?.IsSubscription)
                    && Convert.ToInt32(PF_RequestObject.StoreOrderDetail?.RemainingValidity ?? 0) > 0;
                response.isCSRSaved = PF_RequestObject.CSRDetailRow?.IsCSRSaved == true;

                if (PF_RequestObject.CSRDetailRow != null)
                {
                    response.csrDetail = new CsrDetailDto
                    {
                        DomainName = PF_RequestObject.CSRDetailRow.DomainName,
                        Country = PF_RequestObject.CSRDetailRow.Country,
                        Locality = PF_RequestObject.CSRDetailRow.Locality,
                        Organisation = PF_RequestObject.CSRDetailRow.Organisation,
                        OrganisationUnit = PF_RequestObject.CSRDetailRow.OrganisationUnit,
                        State = PF_RequestObject.CSRDetailRow.State,
                        Email = PF_RequestObject.CSRDetailRow.Email,
                        CSR = PF_RequestObject.CSRDetailRow.CSR ?? PF_RequestObject.CSR
                    };
                }

                var gs = PF_RequestObject.GlobalSignOrderRequest;
                response.contact = new ContactInfoDto
                {
                    ContactFirstName = gs.ContactInfoRow?.FirstName,
                    ContactLastName = gs.ContactInfoRow?.LastName,
                    ContactEmail = gs.ContactInfoRow?.Email,
                    ContactPhone = gs.ContactInfoRow?.PhoneNo,
                    RequestorFirstName = gs.RequestorInfoRow?.FirstName,
                    RequestorLastName = gs.RequestorInfoRow?.LastName,
                    RequestorOrgName = gs.RequestorInfoRow?.OrganizationName,
                    RequestorOrgUnit = gs.RequestorInfoRow?.OrgUnit,
                    RequestorOrgRole = gs.RequestorInfoRow?.FunctionInOrg,
                    RequestorPhone = gs.RequestorInfoRow?.PhoneNo,
                    RequestorEmail = gs.RequestorInfoRow?.Email,
                    ApprovarFirstName = gs.ApproverInfoRow?.FirstName,
                    ApprovarLastName = gs.ApproverInfoRow?.LastName,
                    ApprovarOrgName = gs.ApproverInfoRow?.OrganizationName,
                    ApprovarOrgUnit = gs.ApproverInfoRow?.OrgUnit,
                    ApprovarOrgRole = gs.ApproverInfoRow?.FunctionInOrg,
                    ApprovarPhone = gs.ApproverInfoRow?.PhoneNo,
                    ApprovarEmail = gs.ApproverInfoRow?.Email,
                    AutherizedFirstName = gs.AuthorisedInfoRow?.FirstName,
                    AutherizedLastName = gs.AuthorisedInfoRow?.LastName,
                    AutherizedPhone = gs.AuthorisedInfoRow?.PhoneNo,
                    AutherizedEmail = gs.AuthorisedInfoRow?.Email,
                    AutherizedOrgName = gs.AuthorisedInfoRow?.OrganizationName,
                    AutherizedOrgRole = gs.AuthorisedInfoRow?.FunctionInOrg
                };

                var org = gs.OrganisationInfoRow;
                if (org != null)
                {
                    var orgType = (int)gs.enmOrganisaztionType;
                    response.organisation = new OrganisationInfoDto
                    {
                        Orgname = org.LegalName,
                        OrgType = orgType == 1 ? "PrivateOrganization" : orgType == 2 ? "GovernmentEntity" : "BusinessEntity",
                        PhoneNo = org.PhoneNo,
                        Email = org.Email,
                        Division = org.Division,
                        Duns = org.Duns,
                        Address1 = org.Address1,
                        Address2 = org.Address2,
                        City = org.City,
                        State = org.State,
                        CountryName = org.Country,
                        PostalCode = org.ZipCode,
                        Fax = org.Fax,
                        AgencyRegNumber = org.JurictionRegNo,
                        JurictionCity = org.JurictionCity,
                        jurictionState = org.JurictionState,
                        JurictionCountryName = org.JurictionCountry
                    };
                }

                response.GS_SAN_MailDomain = gs.GS_SAN_MailDomain;
                response.GS_SAN_OWADomain = gs.GS_SAN_OWADomain;
                response.GS_SAN_AutoDiscoverDomain = gs.GS_SAN_AutoDiscoverDomain;

                response.AdditionalDomainList = string.IsNullOrEmpty(gs.AdditionalDomains)
                    ? new List<string>()
                    : gs.AdditionalDomains.TrimEnd(',').Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

                response.WidlcardSANDomainList = gs.WildcardSANDomainList != null
                    ? gs.WildcardSANDomainList.ToList()
                    : new List<string>();

                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.UpdateSummary — uses GlobalSignPlaceOrder.PlaceOrder.
        /// Route: POST /api/SSLConfiguration/GlobalSign/Summary
        /// </summary>
        [HttpPost("Summary")]
        public IActionResult SummaryPost([FromBody] SummaryPostRequest? objModel)
        {
            var response = new SummaryPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                PF_Response objPF_Response = GlobalSignPlaceOrder.PlaceOrder(PF_RequestObject);

                if (objPF_Response.ErrorCode == 0)
                {
                    _draftStore.SavePlaceOrderResponse(resolved.token, objPF_Response);
                    string returnUrl = string.Empty;
                    if (PF_RequestObject.ProductDetail!.AuthenticationType == ConstantUtil.AuthenticationType_OV || PF_RequestObject.ProductDetail.AuthenticationType == ConstantUtil.AuthenticationType_EV)
                        returnUrl = "/ManageOrder/globalsign/orderhistory"; //returnUrl = Url.Action("changedcvmethod", "globalsign", new { area = "ManageOrder" });
                    else
                    {
                        if (Convert.ToString(PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod).ToUpper() == ConstantUtil.GlobalSign_DCVMethod_URL)
                            returnUrl = "/ManageOrder/globalsign/urlverification";
                        else if (Convert.ToString(PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod).ToUpper() == ConstantUtil.GlobalSign_DCVMethod_DNS)
                            returnUrl = "/ManageOrder/globalsign/dnsverification";
                        else if (Convert.ToString(PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod).ToUpper() == ConstantUtil.GlobalSign_DCVMethod_EMAIL)
                            returnUrl = "/ManageOrder/globalsign/changeapproveremail";
                    }
                    response.IsSuccess = true;
                    response.Msg = "Your order number is : " + objPF_Response.VendorID;
                    response.returnUrl = returnUrl;
                    response.VendorID = objPF_Response.VendorID;
                    response.ErrorCode = objPF_Response.ErrorCode;
                    return Ok(response);


                    //if(PF_RequestObject.ProductDetail.AuthenticationType == ConstantUtil.AuthenticationType_DV)
                    //{
                    //    //redirect to direct manage domain approval page
                    //    //return Json(new { IsSuccess = true, Msg = string.Empty, returnUrl = Url.Action("ChangeDCVMethod", "globalsign", new { area = "ManageOrder" }) }, JsonRequestBehavior.AllowGet);
                    //    //if (Convert.ToString(PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod).ToLower() == ConstantUtil.GlobalSign_DCVMethod_URL.ToLower())
                    //    //{
                    //    //    return Json(new { IsSuccess = true, Msg = string.Empty, returnUrl = Url.Action("UrlVerification", "globalsign", new { area = "ManageOrder" }) }, JsonRequestBehavior.AllowGet);
                    //    //}
                    //    //else if (Convert.ToString(PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod).ToLower() == ConstantUtil.GlobalSign_DCVMethod_DNS.ToLower())
                    //    //{
                    //    //    return Json(new { IsSuccess = true, Msg = string.Empty, returnUrl = Url.Action("DnsVerification", "globalsign", new { area = "ManageOrder" }) }, JsonRequestBehavior.AllowGet);
                    //    //}
                    //}
                    //TempData["Message"] = "Your order number is : " + objPF_Response.VendorID;
                    //Session.Abandon();
                    //Session.RemoveAll();
                    //return Json(new { IsSuccess = true, Msg = TempData["Message"], returnUrl = string.Empty }, JsonRequestBehavior.AllowGet);

                    ////return RedirectToAction("Thanks");
                }
                else
                {
                    response.IsSuccess = false;
                    response.Msg = TranslateUtility.Translate(
                        "en",
                        CultureHelper.Normalize(CultureInfo.CurrentUICulture.Name),
                        objPF_Response.ErrorMessage ?? string.Empty);
                    response.returnUrl = string.Empty;
                    response.ErrorCode = objPF_Response.ErrorCode;
                    return Ok(response);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                response.returnUrl = string.Empty;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.SetCSR.
        /// Route: POST /api/SSLConfiguration/GlobalSign/SetCSR
        /// </summary>
        [HttpPost("SetCSR")]
        public IActionResult SetCSR([FromBody] SetCsrRequest? objModel)
        {
            var response = new SetCsrResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = "error";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                if (PF_RequestObject.CSRDetailRow == null)
                    PF_RequestObject.CSRDetailRow = new CSRDetail();

                if (objModel!.isCSRSaved)
                {
                    PF_RequestObject.CSRDetailRow.IsCSRSaved = true;
                    PF_RequestObject.CSRDetailRow.CSR = PF_RequestObject.CSR;
                }
                else
                {
                    PF_RequestObject.CSRDetailRow.IsCSRSaved = false;
                    PF_RequestObject.CSRDetailRow.CSR = PF_RequestObject.CSR;
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = "success";
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = "error";
                return Ok(response);
            }
        }

        #endregion

        #region Code Sign steps

        /// <summary>
        /// Same as old GlobalSignController.GetCodeSignCSRDetail.
        /// Route: GET /api/SSLConfiguration/GlobalSign/CodeSignCSRInfo
        /// </summary>
        [HttpGet("CodeSignCSRInfo")]
        public IActionResult CodeSignCSRInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new CodeSignCsrInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);

                response.csr = new CodeSignCsrInfoDto
                {
                    DomainName = PF_RequestObject.CSRDetailRow?.DomainName,
                    OrganisationUnit = PF_RequestObject.CSRDetailRow?.OrganisationUnit,
                    Locality = PF_RequestObject.CSRDetailRow?.Locality,
                    State = PF_RequestObject.CSRDetailRow?.State,
                    Country = PF_RequestObject.CSRDetailRow?.Country,
                    Password = string.Empty,
                    ConfirmPassword = string.Empty
                };
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.UpdateCodeSignCSRDetail.
        /// Route: POST /api/SSLConfiguration/GlobalSign/CodeSignCSRInfo
        /// </summary>
        [HttpPost("CodeSignCSRInfo")]
        public IActionResult CodeSignCSRInfoPost([FromBody] CodeSignCsrInfoPostRequest? objModel)
        {
            var response = new CodeSignCsrInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                CSRDetail objCSRInfo = new CSRDetail();

                objCSRInfo.DomainName = objModel!.DomainName;
                objCSRInfo.Country = objModel.Country;
                objCSRInfo.Locality = objModel.Locality;
                objCSRInfo.Organisation = objModel.Organisation;
                objCSRInfo.OrganisationUnit = objModel.OrganisationUnit;
                objCSRInfo.State = objModel.State;
                objCSRInfo.Email = objModel.Email;

                PF_RequestObject.GlobalSignOrderRequest.PickupPassword = objModel.Password;
                PF_RequestObject.CSRDetailRow = objCSRInfo;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Message = "";
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = ex.Message.ToString();
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.GetCodeSignContactInfo.
        /// Route: GET /api/SSLConfiguration/GlobalSign/CodeSignContactInfo
        /// </summary>
        [HttpGet("CodeSignContactInfo")]
        public IActionResult CodeSignContactInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new CodeSignContactInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                EnsureContactAndOrgRows(PF_RequestObject);

                // bind ContactInfo from session object
                response.contact = new CodeSignContactInfoDto
                {
                    ContactFirstName = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.FirstName,
                    ContactLastName = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.LastName,
                    ContactEmail = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.Email,
                    ContactPhoneNo = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.PhoneNo,
                    ContactOrgName = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.OrganizationName,
                    ContactDivision = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.OrgUnit,
                    ContactAddress1 = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.Address1,
                    ContactAddress2 = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.Address2,
                    ContactCity = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.City,
                    ContactState = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.State,
                    ContactCountry = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.Country,
                    ContactZipCode = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.ZipCode,
                    ContactDuns = PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow?.Duns
                };
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.UpdateCodeSignContactInfo.
        /// Route: POST /api/SSLConfiguration/GlobalSign/CodeSignContactInfo
        /// </summary>
        [HttpPost("CodeSignContactInfo")]
        public IActionResult CodeSignContactInfoPost([FromBody] CodeSignContactInfoPostRequest? objModel)
        {
            var response = new CodeSignContactInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;
                EnsureContactAndOrgRows(PF_RequestObject);

                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.FirstName = objModel!.ContactFirstName;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.LastName = objModel.ContactLastName;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.Email = objModel.ContactEmail;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.PhoneNo = objModel.ContactPhoneNo;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.OrganizationName = objModel.ContactOrgName;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.OrgUnit = objModel.ContactDivision;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.Address1 = objModel.ContactAddress1;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.Address2 = objModel.ContactAddress2;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.City = objModel.ContactCity;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.State = objModel.ContactState;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.ZipCode = objModel.ContactZipCode;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.Country = objModel.ContactCountry;
                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow.Duns = objModel.ContactDuns;
                PF_RequestObject.GlobalSignOrderRequest.ApprovalEmail = objModel.ContactEmail;
                PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod = "EMAIL";

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Message = "";
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = ex.Message.ToString();
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.GetCodeSignOrganisationInfo.
        /// Route: GET /api/SSLConfiguration/GlobalSign/CodeSignOrganisationInfo
        /// </summary>
        [HttpGet("CodeSignOrganisationInfo")]
        public IActionResult CodeSignOrganisationInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new CodeSignOrganisationInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                EnsureContactAndOrgRows(PF_RequestObject);

                var organisation = new OrganisationInfoDto();
                // bind organization info
                organisation.Email = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow?.Email;
                organisation.Orgname = PF_RequestObject.CSRDetailRow?.DomainName;
                //organisation.OrgType = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoProp.
                organisation.Address1 = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow?.Address1;
                organisation.Address2 = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow?.Address2;
                organisation.City = PF_RequestObject.CSRDetailRow?.Locality;
                organisation.State = PF_RequestObject.CSRDetailRow?.State;
                organisation.CountryName = PF_RequestObject.CSRDetailRow?.Country;
                organisation.PostalCode = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow?.ZipCode;
                organisation.Fax = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow?.Fax;
                organisation.PhoneNo = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow?.PhoneNo;
                organisation.Division = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow?.Division;
                organisation.Duns = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow?.Duns;
                // bind juriction info
                organisation.JurictionCity = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity;
                organisation.JurictionCountryName = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry;
                organisation.jurictionState = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState;
                organisation.AgencyRegNumber = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionRegNo;
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();

                response.organisation = organisation;
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.UpdateCodeSignOrganisationInfo.
        /// Route: POST /api/SSLConfiguration/GlobalSign/CodeSignOrganisationInfo
        /// </summary>
        [HttpPost("CodeSignOrganisationInfo")]
        public IActionResult CodeSignOrganisationInfoPost([FromBody] CodeSignOrganisationInfoPostRequest? objModel)
        {
            var response = new CodeSignOrganisationInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                // save organizatino info
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow = new GlobalSignOrganizationInfo();
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Address1 = objModel!.Address1;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Address2 = objModel.Address2;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.City = objModel.City;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Country = objModel.CountryName;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Division = objModel.Division;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Duns = objModel.Duns;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Email = objModel.Email;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Fax = objModel.Fax;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.LegalName = objModel.Orgname;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo = objModel.PhoneNo;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode = objModel.PostalCode;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.State = objModel.State;
                // save juriction info
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity = objModel.JurictionCity;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry = objModel.JurictionCountryName;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState = objModel.jurictionState;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionRegNo = objModel.AgencyRegNumber;
                // PF_RequestObject.GlobalSignOrderRequest.OrganizationType = objModel.OrgType;
                //PF_RequestObject.GlobalSignOrderRequest.enmOrganisaztionType = (GSOrganizpationType)Convert.ToInt32(objModel.OrgType);

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Message = "";
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = ex.Message.ToString();
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.GetCodeSignVerificationInfo.
        /// Route: GET /api/SSLConfiguration/GlobalSign/CodeSignVerificationInfo
        /// </summary>
        [HttpGet("CodeSignVerificationInfo")]
        public IActionResult CodeSignVerificationInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new CodeSignVerificationInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                EnsureContactAndOrgRows(PF_RequestObject);

                response.verification = new CodeSignVerificationInfoDto
                {
                    // bind requestor info
                    RequestorEmail = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.Email,
                    RequestorFirstName = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.FirstName,
                    RequestorLastName = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.LastName,
                    RequestorOrgName = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.OrganizationName,
                    RequestorPhone = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.PhoneNo,
                    RequestorJobTitle = PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.Title,
                    // bind Approver info
                    ApproverEmail = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.Email,
                    ApproverFirstName = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.FirstName,
                    ApproverLastName = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.LastName,
                    ApproverOrgName = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.OrganizationName,
                    ApproverPhone = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.PhoneNo,
                    ApproverJobTitle = PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.Title,
                    // bind Authorized Signer info
                    AuthorizedEmail = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.Email,
                    AuthorizedFirstName = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.FirstName,
                    AuthorizedLastName = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.LastName,
                    AuthorizedOrgName = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.OrganizationName,
                    AuthorizedPhone = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.PhoneNo,
                    AuthorizedJobTitle = PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.Title
                };
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.UpdateCodeSignVerificationInfo.
        /// Route: POST /api/SSLConfiguration/GlobalSign/CodeSignVerificationInfo
        /// </summary>
        [HttpPost("CodeSignVerificationInfo")]
        public IActionResult CodeSignVerificationInfoPost([FromBody] CodeSignVerificationInfoPostRequest? objModel)
        {
            var response = new CodeSignVerificationInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;
                EnsureContactAndOrgRows(PF_RequestObject);

                // Requestor info
                PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.Email = objModel!.RequestorEmail;
                PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.FirstName = objModel.RequestorFirstName;
                PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.LastName = objModel.RequestorLastName;
                PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.OrganizationName = objModel.RequestorOrgName;
                PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.PhoneNo = objModel.RequestorPhone;
                PF_RequestObject.GlobalSignOrderRequest.RequestorInfoRow.Title = objModel.RequestorJobTitle;
                // Approver info
                PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.Email = objModel.ApproverEmail;
                PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.FirstName = objModel.ApproverFirstName;
                PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.LastName = objModel.ApproverLastName;
                PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.OrganizationName = objModel.ApproverOrgName;
                PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.PhoneNo = objModel.ApproverPhone;
                PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow.Title = objModel.ApproverJobTitle;
                // Authorized info
                PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.Email = objModel.AuthorizedEmail;
                PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.FirstName = objModel.AuthorizedFirstName;
                PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.LastName = objModel.AuthorizedLastName;
                PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.OrganizationName = objModel.AuthorizedOrgName;
                PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.PhoneNo = objModel.AuthorizedPhone;
                PF_RequestObject.GlobalSignOrderRequest.AuthorisedInfoRow.Title = objModel.AuthorizedJobTitle;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Message = "";
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = ex.Message.ToString();
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.GetCodeSignSummary.
        /// Route: GET /api/SSLConfiguration/GlobalSign/CodeSignSummary
        /// </summary>
        [HttpGet("CodeSignSummary")]
        public IActionResult CodeSignSummaryGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new CodeSignSummaryGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.domainName = PF_RequestObject.CSRDetailRow?.DomainName;
                response.productId = PF_RequestObject.ProductDetail?.ProductId ?? PF_RequestObject.StoreOrderDetail?.ProductId;
                response.storeOrderId = PF_RequestObject.StoreOrderDetail?.StoreOrderId;
                response.authenticationType = PF_RequestObject.ProductDetail?.AuthenticationType;

                if (PF_RequestObject.CSRDetailRow != null)
                {
                    response.csr = new CodeSignCsrInfoDto
                    {
                        DomainName = PF_RequestObject.CSRDetailRow.DomainName,
                        OrganisationUnit = PF_RequestObject.CSRDetailRow.OrganisationUnit,
                        Locality = PF_RequestObject.CSRDetailRow.Locality,
                        State = PF_RequestObject.CSRDetailRow.State,
                        Country = PF_RequestObject.CSRDetailRow.Country,
                        Organisation = PF_RequestObject.CSRDetailRow.Organisation,
                        Email = PF_RequestObject.CSRDetailRow.Email
                    };
                }

                var gs = PF_RequestObject.GlobalSignOrderRequest;
                var c = gs.ContactInfoRow;
                if (c != null)
                {
                    response.contact = new CodeSignContactInfoDto
                    {
                        ContactFirstName = c.FirstName,
                        ContactLastName = c.LastName,
                        ContactEmail = c.Email,
                        ContactPhoneNo = c.PhoneNo,
                        ContactOrgName = c.OrganizationName,
                        ContactDivision = c.OrgUnit,
                        ContactAddress1 = c.Address1,
                        ContactAddress2 = c.Address2,
                        ContactCity = c.City,
                        ContactState = c.State,
                        ContactZipCode = c.ZipCode,
                        ContactCountry = c.Country,
                        ContactDuns = c.Duns
                    };
                }

                var org = gs.OrganisationInfoRow;
                if (org != null)
                {
                    response.organisation = new OrganisationInfoDto
                    {
                        Orgname = org.LegalName,
                        Email = org.Email,
                        Division = org.Division,
                        Duns = org.Duns,
                        Address1 = org.Address1,
                        Address2 = org.Address2,
                        City = org.City,
                        State = org.State,
                        CountryName = org.Country,
                        PostalCode = org.ZipCode,
                        PhoneNo = org.PhoneNo,
                        Fax = org.Fax,
                        AgencyRegNumber = org.JurictionRegNo,
                        JurictionCity = org.JurictionCity,
                        jurictionState = org.JurictionState,
                        JurictionCountryName = org.JurictionCountry,
                        OrgType = Convert.ToString(gs.enmOrganisaztionType)
                    };
                }

                response.verification = new CodeSignVerificationInfoDto
                {
                    RequestorOrgName = gs.RequestorInfoRow?.OrganizationName,
                    RequestorFirstName = gs.RequestorInfoRow?.FirstName,
                    RequestorLastName = gs.RequestorInfoRow?.LastName,
                    RequestorJobTitle = gs.RequestorInfoRow?.Title,
                    RequestorEmail = gs.RequestorInfoRow?.Email,
                    RequestorPhone = gs.RequestorInfoRow?.PhoneNo,
                    ApproverOrgName = gs.ApproverInfoRow?.OrganizationName,
                    ApproverFirstName = gs.ApproverInfoRow?.FirstName,
                    ApproverLastName = gs.ApproverInfoRow?.LastName,
                    ApproverJobTitle = gs.ApproverInfoRow?.Title,
                    ApproverEmail = gs.ApproverInfoRow?.Email,
                    ApproverPhone = gs.ApproverInfoRow?.PhoneNo,
                    AuthorizedOrgName = gs.AuthorisedInfoRow?.OrganizationName,
                    AuthorizedFirstName = gs.AuthorisedInfoRow?.FirstName,
                    AuthorizedLastName = gs.AuthorisedInfoRow?.LastName,
                    AuthorizedJobTitle = gs.AuthorisedInfoRow?.Title,
                    AuthorizedEmail = gs.AuthorisedInfoRow?.Email,
                    AuthorizedPhone = gs.AuthorisedInfoRow?.PhoneNo
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old GlobalSignController.UpdateCodeSignSummary — uses GlobalSignPlaceOrder.PlaceOrder.
        /// Route: POST /api/SSLConfiguration/GlobalSign/CodeSignSummary
        /// </summary>
        [HttpPost("CodeSignSummary")]
        public IActionResult CodeSignSummaryPost([FromBody] CodeSignSummaryPostRequest? objModel)
        {
            var response = new CodeSignSummaryPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                PF_Response objPF_Response = GlobalSignPlaceOrder.PlaceOrder(PF_RequestObject);

                if (objPF_Response.ErrorCode == 0)
                {
                    _draftStore.SavePlaceOrderResponse(resolved.token, objPF_Response);
                    response.IsSuccess = true;
                    response.Msg = "Your order number is : " + objPF_Response.VendorID;
                    response.VendorID = objPF_Response.VendorID;
                    response.ErrorCode = objPF_Response.ErrorCode;
                    return Ok(response);
                    //return RedirectToAction("Thanks");
                }
                else
                {
                    response.IsSuccess = false;
                    response.Msg = TranslateUtility.Translate(
                        "en",
                        CultureHelper.Normalize(CultureInfo.CurrentUICulture.Name),
                        objPF_Response.ErrorMessage ?? string.Empty);
                    response.ErrorCode = objPF_Response.ErrorCode;
                    return Ok(response);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message.ToString();
                return Ok(response);
            }
        }

        #endregion

        #region Thanks

        /// <summary>
        /// Same as old GlobalSignController.Thanks — no Session.Abandon; returns JSON and removes draft.
        /// Route: GET /api/SSLConfiguration/GlobalSign/Thanks
        /// </summary>
        [HttpGet("Thanks")]
        public IActionResult Thanks(
            [FromQuery] string? configurationToken = null,
            [FromQuery] string? pin = null,
            [FromQuery] string? VendorID = null,
            [FromQuery] string? ApprovalEmail = null,
            [FromQuery] string? ApprovalMethod = null,
            [FromQuery] string? GSURLMetaTag = null)
        {
            var response = new ThanksResponse();
            try
            {
                PF_Response? snapshot = _draftStore.GetPlaceOrderResponse(configurationToken);
                string? approvalMethodFromDraft = null;

                if (!string.IsNullOrEmpty(configurationToken) || !string.IsNullOrEmpty(pin))
                {
                    var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                    if (resolved.ok && resolved.request != null)
                    {
                        approvalMethodFromDraft = resolved.request.GlobalSignOrderRequest?.ApprovalMethod;
                        if (snapshot == null && !string.IsNullOrEmpty(resolved.token))
                            snapshot = _draftStore.GetPlaceOrderResponse(resolved.token);
                        response.configurationToken = resolved.token;
                    }
                }

                response.VendorID = VendorID ?? snapshot?.VendorID;
                response.ApprovalEmail = ApprovalEmail ?? snapshot?.ApprovalEmail;
                response.ApprovalMethod = ApprovalMethod ?? approvalMethodFromDraft;
                response.GSURLMetaTag = GSURLMetaTag ?? snapshot?.GSURLMetaTag;
                response.GSURLs = snapshot?.GSURLs;
                response.IsSuccess = !string.IsNullOrEmpty(response.VendorID) || snapshot != null;
                response.Msg = response.IsSuccess
                    ? ("Your order number is : " + response.VendorID)
                    : "Session expired.";

                if (!string.IsNullOrEmpty(response.configurationToken))
                    _draftStore.Remove(response.configurationToken);
                else if (!string.IsNullOrEmpty(configurationToken))
                    _draftStore.Remove(configurationToken);

                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Resolves/creates draft (same as AuthenticationService.Entry) and applies old DV/OV/EV auth-type check.
        /// </summary>
        private IActionResult Entry(string authenticationType, string? pin, string? configurationToken, bool requireAuthTypeMatch)
        {
            var response = new DigicertEntryResponse
            {
                authenticationType = authenticationType
            };

            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    // Same as old RedirectToAction("Error", "Home") when PF_Request missing
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = EnsureGlobalSignDraft(resolved.request, resolved.token);

                if (requireAuthTypeMatch)
                {
                    // Same as old GlobalSignController.DV/OV/EV AuthenticationType gate
                    if (pf.ProductDetail?.AuthenticationType != authenticationType)
                    {
                        response.IsSuccess = false;
                        response.Msg = "Invalid product authentication type.";
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }
                }

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.storeOrderId = pf.StoreOrderDetail?.StoreOrderId;
                response.productId = pf.ProductDetail?.ProductId;
                response.isWildcard = pf.ProductDetail?.IsWildcard;
                response.isMultiDomain = pf.ProductDetail?.IsMultiDomain;
                response.isX9 = pf.ProductDetail?.IsX9;
                response.nextController = "GlobalSign";
                response.nextAction = authenticationType;
                // CodeSign entry is gated by product family, not auth type — expose product EV/OV
                // so the UI can show Organisation/Verification steps (same as old CodeSign.cshtml).
                if (!requireAuthTypeMatch && pf.ProductDetail?.AuthenticationType != null)
                {
                    response.authenticationType = pf.ProductDetail.AuthenticationType;
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        private PF_Request EnsureGlobalSignDraft(PF_Request pf, string token)
        {
            if (pf.GlobalSignOrderRequest == null)
            {
                pf.GlobalSignOrderRequest = new PF_GlobalSignOrder
                {
                    AdditionalDomainsList = new Dictionary<string, string>(),
                    WildcardSANDomainList = new List<string>()
                };
            }
            else
            {
                if (pf.GlobalSignOrderRequest.AdditionalDomainsList == null)
                    pf.GlobalSignOrderRequest.AdditionalDomainsList = new Dictionary<string, string>();
                if (pf.GlobalSignOrderRequest.WildcardSANDomainList == null)
                    pf.GlobalSignOrderRequest.WildcardSANDomainList = new List<string>();
            }

            EnsureContactAndOrgRows(pf);
            _draftStore.Update(token, pf);
            return pf;
        }

        /// <summary>
        /// Minimal Core adaptation: recreate contact/org rows if IssueCertificate (or similar) set them null.
        /// </summary>
        private static void EnsureContactAndOrgRows(PF_Request pf)
        {
            if (pf.GlobalSignOrderRequest == null)
                return;

            if (pf.GlobalSignOrderRequest.ContactInfoRow == null)
                pf.GlobalSignOrderRequest.ContactInfoRow = new GlobalSignContactInfo();
            if (pf.GlobalSignOrderRequest.RequestorInfoRow == null)
                pf.GlobalSignOrderRequest.RequestorInfoRow = new GlobalSignContactInfo();
            if (pf.GlobalSignOrderRequest.ApproverInfoRow == null)
                pf.GlobalSignOrderRequest.ApproverInfoRow = new GlobalSignContactInfo();
            if (pf.GlobalSignOrderRequest.AuthorisedInfoRow == null)
                pf.GlobalSignOrderRequest.AuthorisedInfoRow = new GlobalSignContactInfo();
            if (pf.GlobalSignOrderRequest.OrganisationInfoRow == null)
                pf.GlobalSignOrderRequest.OrganisationInfoRow = new GlobalSignOrganizationInfo();
        }

        private static SelectListItemDto ToSelectListItemDto(SelectListItem i) =>
            new SelectListItemDto { Value = i.Value, Text = i.Text, Selected = i.Selected };

        #endregion
    }
}
