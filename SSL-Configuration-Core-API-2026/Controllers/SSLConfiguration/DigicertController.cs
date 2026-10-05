using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SSLConfiguration.Application;
using SSLConfiguration.Application.Services;
using SSLConfiguration.Contracts.SSLConfiguration.Digicert;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.Persistence;
using System.Text.Json;
using VerisignGateway;

namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration
{
    /// <summary>
    /// Ported from SSLConfiguration Controllers/DigicertController — Phase 1–8:
    /// SSL wizard + Summary PlaceOrder + CodeSign + VMC + X9.
    /// </summary>
    [ClientAuthorize]
    [ApiController]
    [Route("api/SSLConfiguration/[controller]")]
    public class DigicertController : ControllerBase
    {
        private readonly ConfigurationDraftStore _draftStore;
        private readonly IAuthenticationService _authenticationService;

        public DigicertController(IAuthenticationService authenticationService, ConfigurationDraftStore draftStore)
        {
            _authenticationService = authenticationService;
            _draftStore = draftStore;
        }

        /// <summary>
        /// Same as old DigicertController.DV — validates PF_Request (draft) exists.
        /// Route: GET /api/SSLConfiguration/Digicert/DV?pin=&amp;configurationToken=
        /// </summary>
        [HttpGet("DV")]
        public IActionResult DV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry("DV", pin, configurationToken);
            return Ok(response);
        }

        /// <summary>Same as old DigicertController.OV.</summary>
        [HttpGet("OV")]
        public IActionResult OV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry("OV", pin, configurationToken);
            return Ok(response);
        }

        /// <summary>Same as old DigicertController.EV.</summary>
        [HttpGet("EV")]
        public IActionResult EV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry("EV", pin, configurationToken);
            return Ok(response);
        }

        /// <summary>
        /// Same as old DigicertController.CSRInfo GET — returns CSR draft fields (was PartialView).
        /// Route: GET /api/SSLConfiguration/Digicert/CSRInfo?configurationToken=
        /// </summary>
        [HttpGet("CSRInfo")]
        public IActionResult CSRInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new CSRInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = resolved.request;
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.CSR = pf.CSR;
                response.domainName = pf.CSRDetailRow?.DomainName;
                response.isFreeSANInclude = pf.DigicertOrderRequest?.IsFreeSANInclude;
                response.isX9 = pf.ProductDetail?.IsX9;
                response.isWildcard = pf.ProductDetail?.IsWildcard;
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
        /// Same as old DigicertController.CSRInfo POST — BLGeneral.PasreCSR + update draft.
        /// Route: POST /api/SSLConfiguration/Digicert/CSRInfo
        /// </summary>
        [HttpPost("CSRInfo")]
        public IActionResult CSRInfoPost([FromBody] CSRInfoPostRequest objModel)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                response.configurationToken = resolved.token;

                if (string.IsNullOrEmpty(objModel?.CSR))
                {
                    PF_RequestObject.CSR = string.Empty;
                    PF_RequestObject.CSRDetailRow = new CSRDetail();
                    PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();
                    _draftStore.Update(resolved.token, PF_RequestObject);
                    response.IsSuccess = false;
                    response.Msg = "Please enter CSR Details.";
                    return Ok(response);
                }

                var objRes = BLGeneral.PasreCSR(objModel.CSR.Trim());
                response.objCSRResJson = JsonConvert.SerializeObject(objRes);
                if (objRes.error == null || objRes.error.ErrorCode == 0)
                {
                    if (PF_RequestObject.ProductDetail != null && PF_RequestObject.ProductDetail.IsWildcard)
                    {
                        if (string.IsNullOrEmpty(objRes.DomainName) || !objRes.DomainName.ToLower().StartsWith("*."))
                        {
                            response.IsSuccess = false;
                            response.Msg = Resources.val_CSRWildcard;
                            return Ok(response);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(objRes.DomainName) && objRes.DomainName.ToLower().StartsWith("*."))
                        {
                            response.IsSuccess = false;
                            response.Msg = "THE COMMON NAME (DOMAIN NAME) MAY NOT CONTAIN A * IN CSR";
                            return Ok(response);
                        }
                    }

                    if (PF_RequestObject.DigicertOrderRequest == null)
                        PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();

                    if (PF_RequestObject.DigicertOrderRequest.AdditionalDomainList == null)
                        PF_RequestObject.DigicertOrderRequest.AdditionalDomainList = new Dictionary<string, string>();
                    else if (!string.IsNullOrEmpty(PF_RequestObject.CSRDetailRow?.DomainName))
                        PF_RequestObject.DigicertOrderRequest.AdditionalDomainList.Remove(PF_RequestObject.CSRDetailRow.DomainName);

                    if (PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList == null)
                        PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList = new Dictionary<string, string>();
                    else if (!string.IsNullOrEmpty(PF_RequestObject.CSRDetailRow?.DomainName))
                        PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList.Remove(PF_RequestObject.CSRDetailRow.DomainName);

                    CSRDetail CSRDetail = new CSRDetail();
                    CSRDetail.DomainName = (objRes.DomainName ?? string.Empty).Trim().ToLower();
                    CSRDetail.Country = objRes.Country;
                    CSRDetail.Locality = objRes.Locality;
                    CSRDetail.Organisation = objRes.Organisation;
                    CSRDetail.OrganisationUnit = objRes.OrganisationUnit;
                    CSRDetail.State = objRes.State;
                    CSRDetail.Email = objRes.Email;
                    CSRDetail.CSR = objModel.CSR;
                    response.CSRDetailJson = JsonConvert.SerializeObject(CSRDetail);
                    PF_RequestObject.CSRDetailRow = CSRDetail;
                    PF_RequestObject.CSR = objModel.CSR;

                    if (PF_RequestObject.ProductDetail != null && !PF_RequestObject.ProductDetail.IsX9)
                        PF_RequestObject.DigicertOrderRequest.IsFreeSANInclude = true;

                    _draftStore.Update(resolved.token, PF_RequestObject);

                    response.IsSuccess = true;
                    response.Msg = string.Empty;
                    return Ok(response);
                }
                else
                {
                    LogWriter.LogCARequestResponseObjectToDB(PF_RequestObject.StoreOrderDetail?.Pin ?? string.Empty, objRes.CARequestObject, System.Text.Json.JsonSerializer.Serialize(objRes.error), "DigicertCSR");

                    response.IsSuccess = false;
                    response.Msg = TranslateUtility.Translate("en", PF_RequestObject.LanguageCode ?? "en", objRes.error?.ErrorMessage ?? string.Empty);
                    return Ok(response);
                }
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
        /// Same as old DigicertController.UpdateFreeSANTag.
        /// Route: POST /api/SSLConfiguration/Digicert/UpdateFreeSANTag
        /// </summary>
        [HttpPost("UpdateFreeSANTag")]
        public IActionResult UpdateFreeSANTag([FromBody] UpdateFreeSANTagRequest request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                if (PF_RequestObject.DigicertOrderRequest == null)
                    PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();

                PF_RequestObject.DigicertOrderRequest.IsFreeSANInclude = request!.IsFreeSANInclude;
                _draftStore.Update(resolved.token, PF_RequestObject);

                response.IsSuccess = true;
                response.Msg = string.Empty;
                response.configurationToken = resolved.token;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                // Old catch returned IsSuccess = true with message
                response.IsSuccess = true;
                response.Msg = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old DigicertController.SetCSR — returns "success" / "error" string like old Json.
        /// Route: POST /api/SSLConfiguration/Digicert/SetCSR
        /// </summary>
        [HttpPost("SetCSR")]
        public IActionResult SetCSR([FromBody] SetCSRRequest request)
        {
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                    return Ok("error");

                PF_Request PF_RequestObject = resolved.request;
                if (PF_RequestObject.CSRDetailRow == null)
                    PF_RequestObject.CSRDetailRow = new CSRDetail();

                if (request!.isCSRSaved)
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
                return Ok("success");
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                return Ok("error");
            }
        }

        /// <summary>
        /// Same as old DigicertController.DCVInfo_DV GET — ApprovalEmailList for UI.
        /// Route: GET /api/SSLConfiguration/Digicert/DCVInfo_DV?configurationToken=
        /// </summary>
        [HttpGet("DCVInfo_DV")]
        public IActionResult DCVInfo_DV_Get([FromQuery] string? configurationToken = null)
        {
            return DcvInfoGet(configurationToken, isDv: true);
        }

        /// <summary>
        /// Same as old DigicertController.DCVInfo_DV POST.
        /// Route: POST /api/SSLConfiguration/Digicert/DCVInfo_DV
        /// </summary>
        [HttpPost("DCVInfo_DV")]
        public IActionResult DCVInfo_DV_Post([FromBody] DCVInfoDvPostRequest request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                if (PF_RequestObject.DigicertOrderRequest == null)
                    PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();

                string? ApprovalMethod = request?.ApprovalMethod;
                string? ApprovalEmail = request?.ApprovalEmail;
                string? dcvScope = request?.dcvScope;

                if (ConstantUtil.DCVMethod_EMAIL == ApprovalMethod)
                    PF_RequestObject.DigicertOrderRequest.ApprovalMethod = (int)enmDCVMethod.EMAIL;
                else if (ConstantUtil.DCVMethod_FILE == ApprovalMethod)
                    PF_RequestObject.DigicertOrderRequest.ApprovalMethod = (int)enmDCVMethod.FILE;
                else if (ConstantUtil.DCVMethod_DNS == ApprovalMethod)
                    PF_RequestObject.DigicertOrderRequest.ApprovalMethod = (int)enmDCVMethod.DNS;

                PF_RequestObject.DigicertOrderRequest.ApproverEmail = ApprovalEmail;

                if (PF_RequestObject.ProductDetail?.AuthenticationType == ConstantUtil.AuthenticationType_DV)
                {
                    if (!string.IsNullOrEmpty(dcvScope))
                        PF_RequestObject.DigicertOrderRequest.DCVScope = dcvScope;

                    if (PF_RequestObject.DigicertOrderRequest.AdditionalDomainList != null && PF_RequestObject.DigicertOrderRequest.AdditionalDomainList.Count > 0)
                    {
                        foreach (var item in PF_RequestObject.DigicertOrderRequest.AdditionalDomainList.Keys.ToList())
                            PF_RequestObject.DigicertOrderRequest.AdditionalDomainList[item] = ApprovalMethod == ConstantUtil.DCVMethod_EMAIL ? (ApprovalEmail ?? string.Empty) : (ApprovalMethod ?? string.Empty);
                    }

                    if (PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList != null && PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList.Count > 0)
                    {
                        foreach (var item in PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList.Keys.ToList())
                            PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList[item] = ApprovalMethod == ConstantUtil.DCVMethod_EMAIL ? (ApprovalEmail ?? string.Empty) : (ApprovalMethod ?? string.Empty);
                    }
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old DigicertController.DCVInfo GET.
        /// Route: GET /api/SSLConfiguration/Digicert/DCVInfo?configurationToken=
        /// </summary>
        [HttpGet("DCVInfo")]
        public IActionResult DCVInfo_Get([FromQuery] string? configurationToken = null)
        {
            return DcvInfoGet(configurationToken, isDv: false);
        }

        /// <summary>
        /// Same as old DigicertController.DCVInfo POST.
        /// Route: POST /api/SSLConfiguration/Digicert/DCVInfo
        /// </summary>
        [HttpPost("DCVInfo")]
        public IActionResult DCVInfo_Post([FromBody] DCVInfoPostRequest request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                if (PF_RequestObject.DigicertOrderRequest == null)
                    PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();

                string? ApprovalMethod = request?.ApprovalMethod;
                string? ApprovalEmail = request?.ApprovalEmail;

                if (ConstantUtil.DCVMethod_EMAIL == ApprovalMethod)
                    PF_RequestObject.DigicertOrderRequest.ApprovalMethod = (int)enmDCVMethod.EMAIL;
                else if (ConstantUtil.DCVMethod_FILE == ApprovalMethod)
                    PF_RequestObject.DigicertOrderRequest.ApprovalMethod = (int)enmDCVMethod.FILE;
                else if (ConstantUtil.DCVMethod_DNS == ApprovalMethod)
                    PF_RequestObject.DigicertOrderRequest.ApprovalMethod = (int)enmDCVMethod.DNS;

                PF_RequestObject.DigicertOrderRequest.ApproverEmail = ApprovalEmail;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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

        #region Phase 3 — SAN Info / Wildcard / GetApprovalEmailList

        /// <summary>
        /// Same as old DigicertController.SANInfo_DV GET.
        /// Route: GET /api/SSLConfiguration/Digicert/SANInfo_DV?configurationToken=
        /// </summary>
        [HttpGet("SANInfo_DV")]
        public IActionResult SANInfo_DV_Get([FromQuery] string? configurationToken = null)
        {
            return SanInfoGet(configurationToken);
        }

        /// <summary>
        /// Same as old DigicertController.SANInfo_DV POST.
        /// Route: POST /api/SSLConfiguration/Digicert/SANInfo_DV
        /// </summary>
        [HttpPost("SANInfo_DV")]
        public IActionResult SANInfo_DV_Post([FromBody] SANInfoPostRequest? request)
        {
            return SanInfoPost(request?.configurationToken);
        }

        /// <summary>
        /// Same as old DigicertController.SANInfo GET.
        /// Route: GET /api/SSLConfiguration/Digicert/SANInfo?configurationToken=
        /// </summary>
        [HttpGet("SANInfo")]
        public IActionResult SANInfo_Get([FromQuery] string? configurationToken = null)
        {
            return SanInfoGet(configurationToken);
        }

        /// <summary>
        /// Same as old DigicertController.SANInfo POST.
        /// Route: POST /api/SSLConfiguration/Digicert/SANInfo
        /// </summary>
        [HttpPost("SANInfo")]
        public IActionResult SANInfo_Post([FromBody] SANInfoPostRequest? request)
        {
            return SanInfoPost(request?.configurationToken);
        }

        /// <summary>
        /// Same as old DigicertController.GetApprovalEmailList.
        /// Route: POST /api/SSLConfiguration/Digicert/GetApprovalEmailList
        /// </summary>
        [HttpPost("GetApprovalEmailList")]
        public IActionResult GetApprovalEmailList([FromBody] GetApprovalEmailListRequest? request)
        {
            var response = new GetApprovalEmailListResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                var approvalEmailList = BLDigicert.GetDigicertApprovalEmailList(
                    request?.sanDomainName ?? string.Empty,
                    PF_RequestObject.CACredentialDetails?.GetDigicertCACredential());

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.ApprovalEmailList = approvalEmailList.Select(i => new SelectListItemDto
                {
                    Value = i.Value,
                    Text = i.Text,
                    Selected = i.Selected
                }).ToList();
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
        /// Same as old DigicertController.AddAdditionalDomains.
        /// Route: POST /api/SSLConfiguration/Digicert/AddAdditionalDomains
        /// </summary>
        [HttpPost("AddAdditionalDomains")]
        public IActionResult AddAdditionalDomains([FromBody] SanMutationRequest? request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureSanLists(PF_RequestObject);

                if (PF_RequestObject.ProductDetail?.IsMultiDomain == true)
                {
                    string primaryDomain = PF_RequestObject.CSRDetailRow?.DomainName ?? string.Empty;
                    var usedSan = BLGeneral.GetTotalAdditionalSANForSymantec(primaryDomain, PF_RequestObject.DigicertOrderRequest.AdditionalDomainList);
                    int allowedMaxSan = BLGeneral.GetAddDomain(
                        PF_RequestObject.StoreOrderDetail!.StoreOrderId,
                        PF_RequestObject.StoreOrderDetail.ProductId);

                    if (usedSan >= allowedMaxSan)
                    {
                        response.IsSuccess = false;
                        response.Msg = "Maximum allowed SAN : " + allowedMaxSan;
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }

                    if (string.IsNullOrEmpty(request?.additionalDomains))
                    {
                        response.IsSuccess = false;
                        response.Msg = "Enter san.";
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }

                    string[] addDomainList = request.additionalDomains.Trim().Split('\n').Distinct().ToArray();
                    int newSan = 0;
                    bool isWildcardMultiDomain = PF_RequestObject.ProductDetail.IsWildcardMultiDomain == true;

                    foreach (string san in addDomainList)
                    {
                        if (isWildcardMultiDomain)
                        {
                            if (!BLGeneral.isValidWildcardDomain(san.Trim()))
                            {
                                response.IsSuccess = false;
                                response.Msg = "Invalid domain name : " + san.Trim().Replace(" ", "[space]");
                                response.configurationToken = resolved.token;
                                return Ok(response);
                            }
                        }
                        else
                        {
                            if (!BLGeneral.isValidSingleDomain(san.Trim()))
                            {
                                response.IsSuccess = false;
                                response.Msg = "Invalid domain name : " + san.Trim().Replace(" ", "[space]");
                                response.configurationToken = resolved.token;
                                return Ok(response);
                            }
                        }

                        if (usedSan + newSan >= allowedMaxSan)
                        {
                            response.IsSuccess = false;
                            response.Msg = "Maximum SAN allowed : " + allowedMaxSan;
                            response.configurationToken = resolved.token;
                            return Ok(response);
                        }

                        newSan++;
                    }

                    foreach (string san in addDomainList)
                    {
                        PF_RequestObject.DigicertOrderRequest.AdditionalDomainList!.AddUniqueKey(
                            san.Trim().ToLower(),
                            isWildcardMultiDomain
                                ? ConstantUtil.Digicert_DCVMethod_DNS_TXT_TOKEN
                                : ConstantUtil.Digicert_DCVMethod_HTTP_TOKEN);
                    }

                    _draftStore.Update(resolved.token, PF_RequestObject);
                    response.IsSuccess = true;
                    response.Msg = string.Empty;
                    response.configurationToken = resolved.token;
                    return Ok(response);
                }

                response.IsSuccess = false;
                response.Msg = "Additional SAN not allowed.";
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
        /// Same as old DigicertController.DeleteAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/Digicert/DeleteAdditionalDomain
        /// </summary>
        [HttpPost("DeleteAdditionalDomain")]
        public IActionResult DeleteAdditionalDomain([FromBody] SanMutationRequest? request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureSanLists(PF_RequestObject);

                if (string.IsNullOrEmpty(request?.sanDomainName))
                {
                    response.IsSuccess = false;
                    response.Msg = "Domain Name not available.";
                    response.configurationToken = resolved.token;
                    return Ok(response);
                }

                string sanDomainName = request.sanDomainName;
                if (PF_RequestObject.DigicertOrderRequest.AdditionalDomainList!.ContainsKey(sanDomainName.ToLower()))
                    PF_RequestObject.DigicertOrderRequest.AdditionalDomainList.Remove(sanDomainName.ToLower());

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old DigicertController.UpdateMethodForAllAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/Digicert/UpdateMethodForAllAdditionalDomain
        /// </summary>
        [HttpPost("UpdateMethodForAllAdditionalDomain")]
        public IActionResult UpdateMethodForAllAdditionalDomain([FromBody] SanMutationRequest? request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureSanLists(PF_RequestObject);

                if (string.IsNullOrEmpty(request?.approvalMethod))
                {
                    response.IsSuccess = false;
                    response.Msg = "Approval method not selected.";
                    response.configurationToken = resolved.token;
                    return Ok(response);
                }

                List<string> sanList = PF_RequestObject.DigicertOrderRequest.AdditionalDomainList!.Keys.ToList();
                foreach (string san in sanList)
                    PF_RequestObject.DigicertOrderRequest.AdditionalDomainList[san] = request.approvalMethod;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old DigicertController.UpdateMethodForAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/Digicert/UpdateMethodForAdditionalDomain
        /// </summary>
        [HttpPost("UpdateMethodForAdditionalDomain")]
        public IActionResult UpdateMethodForAdditionalDomain([FromBody] SanMutationRequest? request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureSanLists(PF_RequestObject);

                if (string.IsNullOrEmpty(request?.approvalMethodOrEmail))
                {
                    response.IsSuccess = false;
                    response.Msg = "Approval method not selected.";
                    response.configurationToken = resolved.token;
                    return Ok(response);
                }

                string key = (request.sanDomainName ?? string.Empty).Trim();
                if (PF_RequestObject.DigicertOrderRequest.AdditionalDomainList!.ContainsKey(key.ToLower()))
                    key = key.ToLower();

                PF_RequestObject.DigicertOrderRequest.AdditionalDomainList[key] = request.approvalMethodOrEmail;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old DigicertController.AddWildcardSAN.
        /// Route: POST /api/SSLConfiguration/Digicert/AddWildcardSAN
        /// </summary>
        [HttpPost("AddWildcardSAN")]
        public IActionResult AddWildcardSAN([FromBody] SanMutationRequest? request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureSanLists(PF_RequestObject);

                if (PF_RequestObject.ProductDetail?.IsFlex == true)
                {
                    string primaryDomain = PF_RequestObject.CSRDetailRow?.DomainName ?? string.Empty;
                    var usedWildcardSan = BLGeneral.GetTotalAdditionalSANForSymantec(
                        primaryDomain,
                        PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList);
                    int allowedMaxWildcardSan = BLGeneral.GetWildcardSANCount(
                        PF_RequestObject.StoreOrderDetail!.StoreOrderId,
                        PF_RequestObject.StoreOrderDetail.ProductId);
                    response.usedWildcardSan = usedWildcardSan;
                    response.allowedMaxWildcardSan = allowedMaxWildcardSan;
                    if (usedWildcardSan >= allowedMaxWildcardSan)
                    {
                        response.IsSuccess = false;
                        response.Msg = "Maximum Wildcard SAN allowed : " + allowedMaxWildcardSan;
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }

                    if (string.IsNullOrEmpty(request?.additionalWildcardDomainNames))
                    {
                        response.IsSuccess = false;
                        response.Msg = "Enter san.";
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }

                    string[] addDomainList = request.additionalWildcardDomainNames.Trim().Split('\n').Distinct().ToArray();
                    int newWildcardSan = 0;

                    foreach (string san in addDomainList)
                    {
                        if (!BLGeneral.isValidWildcardDomain(san.Trim()))
                        {
                            response.IsSuccess = false;
                            response.Msg = "Invalid domain name : " + san.Trim().Replace(" ", "[space]");
                            response.configurationToken = resolved.token;
                            return Ok(response);
                        }

                        if (usedWildcardSan + newWildcardSan >= allowedMaxWildcardSan)
                        {
                            response.IsSuccess = false;
                            response.Msg = "Maximum Wildcard SAN allowed : " + allowedMaxWildcardSan;
                            response.configurationToken = resolved.token;
                            return Ok(response);
                        }

                        newWildcardSan++;
                    }

                    foreach (string san in addDomainList)
                    {
                        if (san.StartsWith("*."))
                        {
                            if (PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList!.Count >= allowedMaxWildcardSan)
                            {
                                response.IsSuccess = false;
                                response.Msg = "Maximum Wildcard SAN allowed : " + allowedMaxWildcardSan;
                                response.configurationToken = resolved.token;
                                return Ok(response);
                            }

                            PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList.AddUniqueKey(
                                san.Trim().ToLower(),
                                ConstantUtil.Digicert_DCVMethod_DNS_TXT_TOKEN);
                        }
                    }

                    _draftStore.Update(resolved.token, PF_RequestObject);
                    response.IsSuccess = true;
                    response.Msg = string.Empty;
                    response.configurationToken = resolved.token;
                    return Ok(response);
                }

                response.IsSuccess = false;
                response.Msg = "Product is not flex.";
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
        /// Same as old DigicertController.DeleteWildcardSAN.
        /// Route: POST /api/SSLConfiguration/Digicert/DeleteWildcardSAN
        /// </summary>
        [HttpPost("DeleteWildcardSAN")]
        public IActionResult DeleteWildcardSAN([FromBody] SanMutationRequest? request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureSanLists(PF_RequestObject);

                if (string.IsNullOrEmpty(request?.sanDomainName))
                {
                    response.IsSuccess = false;
                    response.Msg = "Domain Name not available.";
                    response.configurationToken = resolved.token;
                    return Ok(response);
                }

                string sanDomainName = request.sanDomainName;
                if (PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList!.ContainsKey(sanDomainName.ToLower()))
                    PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList.Remove(sanDomainName.ToLower());

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old DigicertController.UpdateMethodForAllAdditionalDomain_WildcardSAN.
        /// Route: POST /api/SSLConfiguration/Digicert/UpdateMethodForAllAdditionalDomain_WildcardSAN
        /// </summary>
        [HttpPost("UpdateMethodForAllAdditionalDomain_WildcardSAN")]
        public IActionResult UpdateMethodForAllAdditionalDomain_WildcardSAN([FromBody] SanMutationRequest? request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureSanLists(PF_RequestObject);

                if (string.IsNullOrEmpty(request?.approvalMethod))
                {
                    response.IsSuccess = false;
                    response.Msg = "Approval method not selected.";
                    response.configurationToken = resolved.token;
                    return Ok(response);
                }

                List<string> sanList = PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList!.Keys.ToList();
                foreach (string san in sanList)
                    PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList[san] = request.approvalMethod;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old DigicertController.UpdateMethodForAdditionalDomain_WildcardSAN.
        /// Route: POST /api/SSLConfiguration/Digicert/UpdateMethodForAdditionalDomain_WildcardSAN
        /// </summary>
        [HttpPost("UpdateMethodForAdditionalDomain_WildcardSAN")]
        public IActionResult UpdateMethodForAdditionalDomain_WildcardSAN([FromBody] SanMutationRequest? request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureSanLists(PF_RequestObject);

                if (string.IsNullOrEmpty(request?.approvalMethodOrEmail))
                {
                    response.IsSuccess = false;
                    response.Msg = "Approval method not selected.";
                    response.configurationToken = resolved.token;
                    return Ok(response);
                }

                string key = (request.sanDomainName ?? string.Empty).Trim();
                if (PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList!.ContainsKey(key.ToLower()))
                    key = key.ToLower();

                PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList[key] = request.approvalMethodOrEmail;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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

        #endregion

        #region Phase 4 — ServerType / Contact / Organization / Validate / EditOrganization

        /// <summary>
        /// Same as old DigicertController.ServerTypeAdminEmail GET.
        /// Route: GET /api/SSLConfiguration/Digicert/ServerTypeAdminEmail?configurationToken=
        /// </summary>
        [HttpGet("ServerTypeAdminEmail")]
        public IActionResult ServerTypeAdminEmail_Get([FromQuery] string? configurationToken = null)
        {
            var response = new ServerTypeAdminEmailGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                if (PF_RequestObject.DigicertOrderRequest == null)
                    PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();

                var webServerList = BLDigicert.GetDigicertWebServerList();
                var allowedCACerts = BLDigicert.GetDigicertAllowedCACerts(
                    PF_RequestObject.ProductDetail!.ProductId,
                    PF_RequestObject.CACredentialDetails?.GetDigicertCACredential());

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.WebServerType = PF_RequestObject.DigicertOrderRequest.WebServerType;
                response.IntermediateCAId = PF_RequestObject.DigicertOrderRequest.IntermediateCAId;
                response.IntermediateCAText = PF_RequestObject.DigicertOrderRequest.IntermediateCAText;
                response.WebServerList = webServerList.Select(ToSelectListItemDto).ToList();
                response.AllowedCACerts = allowedCACerts.Select(ToSelectListItemDto).ToList();
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
        /// Same as old DigicertController.ServerTypeAdminEmail POST.
        /// Route: POST /api/SSLConfiguration/Digicert/ServerTypeAdminEmail
        /// </summary>
        [HttpPost("ServerTypeAdminEmail")]
        public IActionResult ServerTypeAdminEmail_Post([FromBody] DigicertContactDetailDto? objModel)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                if (PF_RequestObject.DigicertOrderRequest == null)
                    PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();

                PF_RequestObject.DigicertOrderRequest.WebServerType = objModel?.WebServerType;

                if (!string.IsNullOrEmpty(objModel?.IntermediateCAId))
                {
                    PF_RequestObject.DigicertOrderRequest.IntermediateCAId = objModel.IntermediateCAId;
                    var caItem = BLDigicert.GetDigicertAllowedCACerts(
                            PF_RequestObject.ProductDetail!.ProductId,
                            PF_RequestObject.CACredentialDetails?.GetDigicertCACredential())
                        .FirstOrDefault(m => m.Value == objModel.IntermediateCAId);
                    PF_RequestObject.DigicertOrderRequest.IntermediateCAText = caItem?.Text;
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.IntermediateCAText = PF_RequestObject.DigicertOrderRequest.IntermediateCAText;
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
        /// Same as old DigicertController.ContactInfo GET.
        /// Route: GET /api/SSLConfiguration/Digicert/ContactInfo?configurationToken=
        /// </summary>
        [HttpGet("ContactInfo")]
        public IActionResult ContactInfo_Get([FromQuery] string? configurationToken = null)
        {
            var response = new ContactInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                var objModel = new DigicertContactDetailDto();

                if (PF_RequestObject.DigicertOrderRequest != null)
                {
                    if (PF_RequestObject.DigicertOrderRequest.OrganizationContactInfo != null
                        && !string.IsNullOrEmpty(PF_RequestObject.DigicertOrderRequest.OrganizationContactInfo.Email))
                    {
                        objModel.AdminTitle = PF_RequestObject.DigicertOrderRequest.OrganizationContactInfo.Title;
                        objModel.AdminFirstName = PF_RequestObject.DigicertOrderRequest.OrganizationContactInfo.FirstName;
                        objModel.AdminLastName = PF_RequestObject.DigicertOrderRequest.OrganizationContactInfo.LastName;
                        objModel.AdminEmail = PF_RequestObject.DigicertOrderRequest.OrganizationContactInfo.Email;
                        objModel.ConfirmAdminEmail = PF_RequestObject.DigicertOrderRequest.OrganizationContactInfo.Email;
                        objModel.AdminPhoneNo = PF_RequestObject.DigicertOrderRequest.OrganizationContactInfo.PhoneNo;
                    }

                    if (PF_RequestObject.DigicertOrderRequest.TechnicalContactInfoRow != null
                        && !string.IsNullOrEmpty(PF_RequestObject.DigicertOrderRequest.TechnicalContactInfoRow.Email))
                    {
                        objModel.TechnicalTitle = PF_RequestObject.DigicertOrderRequest.TechnicalContactInfoRow.Title;
                        objModel.TechnicalFirstName = PF_RequestObject.DigicertOrderRequest.TechnicalContactInfoRow.FirstName;
                        objModel.TechnicalLastName = PF_RequestObject.DigicertOrderRequest.TechnicalContactInfoRow.LastName;
                        objModel.TechnicalEmail = PF_RequestObject.DigicertOrderRequest.TechnicalContactInfoRow.Email;
                        objModel.TechnicalPhoneNo = PF_RequestObject.DigicertOrderRequest.TechnicalContactInfoRow.PhoneNo;
                    }

                    if (PF_RequestObject.DigicertOrderRequest.EVApproverContactInfo != null
                        && !string.IsNullOrEmpty(PF_RequestObject.DigicertOrderRequest.EVApproverContactInfo.Email))
                    {
                        objModel.ApproverTitle = PF_RequestObject.DigicertOrderRequest.EVApproverContactInfo.Title;
                        objModel.ApproverFirstName = PF_RequestObject.DigicertOrderRequest.EVApproverContactInfo.FirstName;
                        objModel.ApproverLastName = PF_RequestObject.DigicertOrderRequest.EVApproverContactInfo.LastName;
                        objModel.ApproverEmail = PF_RequestObject.DigicertOrderRequest.EVApproverContactInfo.Email;
                        objModel.ConfirmApproverEmail = PF_RequestObject.DigicertOrderRequest.EVApproverContactInfo.Email;
                        objModel.ApproverPhoneNo = PF_RequestObject.DigicertOrderRequest.EVApproverContactInfo.PhoneNo;
                    }
                }

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.authenticationType = PF_RequestObject.ProductDetail?.AuthenticationType;
                response.contact = objModel;
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
        /// Same as old DigicertController.ContactInfo POST.
        /// Route: POST /api/SSLConfiguration/Digicert/ContactInfo
        /// </summary>
        [HttpPost("ContactInfo")]
        public IActionResult ContactInfo_Post([FromBody] DigicertContactDetailDto? objModel)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                if (PF_RequestObject.DigicertOrderRequest == null)
                    PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();

                PF_RequestObject.DigicertOrderRequest.OrganizationContactInfo = new SymantecContactInfo
                {
                    Title = objModel?.AdminTitle,
                    FirstName = objModel?.AdminFirstName,
                    LastName = objModel?.AdminLastName,
                    Email = objModel?.AdminEmail,
                    PhoneNo = objModel?.AdminPhoneNo
                };

                PF_RequestObject.DigicertOrderRequest.TechnicalContactInfoRow = new SymantecContactInfo
                {
                    Title = objModel?.TechnicalTitle,
                    FirstName = objModel?.TechnicalFirstName,
                    LastName = objModel?.TechnicalLastName,
                    Email = objModel?.TechnicalEmail,
                    PhoneNo = objModel?.TechnicalPhoneNo
                };

                if (PF_RequestObject.ProductDetail?.AuthenticationType == ConstantUtil.AuthenticationType_EV)
                {
                    PF_RequestObject.DigicertOrderRequest.EVApproverContactInfo = new SymantecContactInfo
                    {
                        Title = objModel?.ApproverTitle,
                        FirstName = objModel?.ApproverFirstName,
                        LastName = objModel?.ApproverLastName,
                        Email = objModel?.ApproverEmail,
                        PhoneNo = objModel?.ApproverPhoneNo
                    };
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
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
        /// Same as old DigicertController.OrganizationInfo GET.
        /// Route: GET /api/SSLConfiguration/Digicert/OrganizationInfo?configurationToken=
        /// </summary>
        [HttpGet("OrganizationInfo")]
        public IActionResult OrganizationInfo_Get([FromQuery] string? configurationToken = null)
        {
            var response = new OrganizationInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                var objModel = new DigicertOrganizationInformationDto();

                if (PF_RequestObject.DigicertOrderRequest?.OrgranisationInfoRow != null
                    && !string.IsNullOrEmpty(PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.LegalName))
                {
                    var org = PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow;
                    objModel.Orgname = org.LegalName;
                    objModel.DBAname = org.AssumedName;
                    objModel.Address1 = org.Address1;
                    objModel.Address2 = org.Address2;
                    objModel.PhoneNo = org.PhoneNo;
                    objModel.Duns = org.Duns;
                    objModel.City = org.City;
                    objModel.State = org.State;
                    objModel.CountryName = org.Country;
                    objModel.PostalCode = org.ZipCode;
                    objModel.Division = org.Division;
                    objModel.Fax = org.Fax;
                }

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.organization = objModel;
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();
                response.DigicertOrganizationId = PF_RequestObject.DigicertOrderRequest?.DigicertOrganizationId;
                response.IsOrgDetailChange = PF_RequestObject.DigicertOrderRequest?.IsOrgDetailChange;
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
        /// Same as old DigicertController.OrganizationInfo POST.
        /// Route: POST /api/SSLConfiguration/Digicert/OrganizationInfo
        /// </summary>
        [HttpPost("OrganizationInfo")]
        public IActionResult OrganizationInfo_Post([FromBody] DigicertOrganizationInformationDto? objModel)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                if (PF_RequestObject.DigicertOrderRequest == null)
                    PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();

                if (PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow == null)
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow = new SymantecOrganizationInfo();

                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.LegalName = objModel?.Orgname;
                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.AssumedName = objModel?.DBAname;
                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Address1 = objModel?.Address1;
                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Address2 = objModel?.Address2;
                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.PhoneNo = objModel?.PhoneNo;
                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Duns = objModel?.Duns;
                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.City = objModel?.City;
                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.State = objModel?.State;
                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Country = objModel?.CountryName;
                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.ZipCode = objModel?.PostalCode;
                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Division = objModel?.Division;
                PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Fax = objModel?.Fax;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old DigicertController.ValidateOrganization — DigicertAPIHelper.SearchOrganization via DLL.
        /// Route: POST /api/SSLConfiguration/Digicert/ValidateOrganization
        /// </summary>
        [HttpPost("ValidateOrganization")]
        public IActionResult ValidateOrganization([FromBody] ValidateOrganizationRequest? request)
        {
            var response = new ValidateOrganizationResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                if (string.IsNullOrEmpty(request?.orgName) || string.IsNullOrEmpty(request?.countryCode))
                {
                    response.IsSuccess = false;
                    response.Msg = "Please enter required field.";
                    response.configurationToken = resolved.token;
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                if (PF_RequestObject.DigicertOrderRequest == null)
                    PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();

                PF_RequestObject.DigicertOrderRequest.IsOrgDetailChange = false;

                var orgDetail = BLDigicert.GetOrganization(
                    request.orgName,
                    request.countryCode,
                    PF_RequestObject.CACredentialDetails?.GetDigicertCACredential());

                DigicertOrganizationInformationDto objModel;

                if (orgDetail != null)
                {
                    objModel = new DigicertOrganizationInformationDto
                    {
                        Orgname = request.orgName,
                        CountryName = request.countryCode,
                        Address1 = orgDetail.Address,
                        Address2 = orgDetail.Address2,
                        City = orgDetail.City,
                        PhoneNo = orgDetail.Telephone,
                        PostalCode = orgDetail.Zip,
                        State = orgDetail.State
                    };

                    PF_RequestObject.DigicertOrderRequest.DigicertOrganizationId = orgDetail.Id;

                    if (PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow == null)
                        PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow = new SymantecOrganizationInfo();

                    ApplyOrgDtoToDraft(PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow, objModel);
                }
                else
                {
                    objModel = new DigicertOrganizationInformationDto
                    {
                        Orgname = request.orgName,
                        CountryName = request.countryCode
                    };

                    PF_RequestObject.DigicertOrderRequest.DigicertOrganizationId = 0;

                    if (PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow == null)
                        PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow = new SymantecOrganizationInfo();

                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.LegalName = objModel.Orgname;
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Country = objModel.CountryName;
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.AssumedName = string.Empty;
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Address1 = string.Empty;
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Address2 = string.Empty;
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.PhoneNo = string.Empty;
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Duns = string.Empty;
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.City = string.Empty;
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.State = string.Empty;
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.ZipCode = string.Empty;
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Division = string.Empty;
                    PF_RequestObject.DigicertOrderRequest.OrgranisationInfoRow.Fax = string.Empty;
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.objModel = objModel;
                response.DigicertOrganizationId = PF_RequestObject.DigicertOrderRequest.DigicertOrganizationId;
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
        /// Same as old DigicertController.EditOrganization.
        /// Route: POST /api/SSLConfiguration/Digicert/EditOrganization
        /// </summary>
        [HttpPost("EditOrganization")]
        public IActionResult EditOrganization([FromBody] EditOrganizationRequest? request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token)
                    || resolved.request.DigicertOrderRequest == null)
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                resolved.request.DigicertOrderRequest.IsOrgDetailChange = true;
                _draftStore.Update(resolved.token, resolved.request);

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

        #endregion

        #region Phase 5 — Summary / PlaceOrder

        /// <summary>
        /// Same as old DigicertController.Summary GET (was PartialView) — returns draft summary JSON.
        /// Route: GET /api/SSLConfiguration/Digicert/Summary?configurationToken=
        /// </summary>
        [HttpGet("Summary")]
        public IActionResult Summary_Get([FromQuery] string? configurationToken = null)
        {
            var response = new SummaryGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = resolved.request;
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.domainName = pf.CSRDetailRow?.DomainName;
                response.productName = pf.ProductDetail?.ProductName ?? pf.StoreOrderDetail?.ProductName;
                response.webServerType = pf.DigicertOrderRequest?.WebServerType;
                response.approverEmail = pf.DigicertOrderRequest?.ApproverEmail;
                response.approvalMethod = pf.DigicertOrderRequest?.ApprovalMethod;
                response.authenticationType = pf.ProductDetail?.AuthenticationType;
                response.additionalDomainList = pf.DigicertOrderRequest?.AdditionalDomainList;
                response.additionalWildCardDomainList = pf.DigicertOrderRequest?.AdditionalWildCardDomainList;

                if (pf.DigicertOrderRequest?.OrgranisationInfoRow != null
                    && !string.IsNullOrEmpty(pf.DigicertOrderRequest.OrgranisationInfoRow.LegalName))
                {
                    var org = pf.DigicertOrderRequest.OrgranisationInfoRow;
                    response.organization = new DigicertOrganizationInformationDto
                    {
                        Orgname = org.LegalName,
                        DBAname = org.AssumedName,
                        Address1 = org.Address1,
                        Address2 = org.Address2,
                        PhoneNo = org.PhoneNo,
                        Duns = org.Duns,
                        City = org.City,
                        State = org.State,
                        CountryName = org.Country,
                        PostalCode = org.ZipCode,
                        Division = org.Division,
                        Fax = org.Fax
                    };
                }

                response.contact = new DigicertContactDetailDto
                {
                    WebServerType = pf.DigicertOrderRequest?.WebServerType,
                    IntermediateCAId = pf.DigicertOrderRequest?.IntermediateCAId,
                    IntermediateCAText = pf.DigicertOrderRequest?.IntermediateCAText,
                    AdminTitle = pf.DigicertOrderRequest?.OrganizationContactInfo?.Title,
                    AdminFirstName = pf.DigicertOrderRequest?.OrganizationContactInfo?.FirstName,
                    AdminLastName = pf.DigicertOrderRequest?.OrganizationContactInfo?.LastName,
                    AdminEmail = pf.DigicertOrderRequest?.OrganizationContactInfo?.Email,
                    AdminPhoneNo = pf.DigicertOrderRequest?.OrganizationContactInfo?.PhoneNo,
                    TechnicalTitle = pf.DigicertOrderRequest?.TechnicalContactInfoRow?.Title,
                    TechnicalFirstName = pf.DigicertOrderRequest?.TechnicalContactInfoRow?.FirstName,
                    TechnicalLastName = pf.DigicertOrderRequest?.TechnicalContactInfoRow?.LastName,
                    TechnicalEmail = pf.DigicertOrderRequest?.TechnicalContactInfoRow?.Email,
                    TechnicalPhoneNo = pf.DigicertOrderRequest?.TechnicalContactInfoRow?.PhoneNo,
                    ApproverTitle = pf.DigicertOrderRequest?.EVApproverContactInfo?.Title,
                    ApproverFirstName = pf.DigicertOrderRequest?.EVApproverContactInfo?.FirstName,
                    ApproverLastName = pf.DigicertOrderRequest?.EVApproverContactInfo?.LastName,
                    ApproverEmail = pf.DigicertOrderRequest?.EVApproverContactInfo?.Email,
                    ApproverPhoneNo = pf.DigicertOrderRequest?.EVApproverContactInfo?.PhoneNo
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
        /// Same as old DigicertController.Summary POST — PlaceOrder via VerisignUtil.GetProductObject + QuickDigicertOrder.
        /// Route: POST /api/SSLConfiguration/Digicert/Summary
        /// </summary>
        [HttpPost("Summary")]
        public IActionResult Summary_Post([FromBody] SummaryPostRequest? request)
        {
            var response = new SummaryPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                if (PF_RequestObject.DigicertOrderRequest == null)
                    PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();

                if (PF_RequestObject.DigicertOrderRequest.ApproverEmail == ConstantUtil.DCVMethod_FILE)
                    PF_RequestObject.DigicertOrderRequest.ApprovalMethod = (int)enmDCVMethod.FILE;
                else if (PF_RequestObject.DigicertOrderRequest.ApproverEmail == ConstantUtil.DCVMethod_DNS)
                    PF_RequestObject.DigicertOrderRequest.ApprovalMethod = (int)enmDCVMethod.DNS;
                else
                {
                    if (string.IsNullOrEmpty(Convert.ToString(PF_RequestObject.DigicertOrderRequest.ApprovalMethod))
                        || PF_RequestObject.DigicertOrderRequest.ApprovalMethod == 0)
                        PF_RequestObject.DigicertOrderRequest.ApprovalMethod = (int)enmDCVMethod.EMAIL;
                }

                _draftStore.Update(resolved.token, PF_RequestObject);

                PF_Response objPF_Response = DigicertPlaceOrder.PlaceOrder(PF_RequestObject);

                if (objPF_Response.ErrorCode == 0)
                {
                    string returnUrl;
                    if (BLGeneral.IsOVEVProduct_StoreOrder(PF_RequestObject.StoreOrderDetail!.StoreOrderId))
                        returnUrl = "/ManageOrder/Digicert/changeovdcvmethod";
                    else
                        returnUrl = "/ManageOrder/Digicert/changedcvmethod";

                    response.IsSuccess = true;
                    response.Msg = "Your order number is : " + objPF_Response.DigicertOrderNumber;
                    response.returnUrl = returnUrl;
                    response.DigicertOrderNumber = objPF_Response.DigicertOrderNumber;
                    response.DigicertCertificateId = objPF_Response.DigicertCertificateId;
                    response.configurationToken = resolved.token;

                    _draftStore.Remove(resolved.token);
                    return Ok(response);
                }

                response.IsSuccess = false;
                response.Msg = TranslateUtility.Translate(
                    "en",
                    PF_RequestObject.LanguageCode ?? "en",
                    objPF_Response.ErrorMessage ?? string.Empty);
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

        #endregion

        #region Phase 6 — CodeSign

        [HttpGet("CodeSign")]
        public IActionResult CodeSign([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry("CodeSign", pin, configurationToken);
            return Ok(response);
        }

        [HttpGet("CSRInfo_CodeSign")]
        public IActionResult CSRInfo_CodeSign_Get([FromQuery] string? configurationToken = null)
        {
            var response = new CodeSignCsrInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = resolved.request;
                if (pf.DigicertOrderRequest == null)
                    pf.DigicertOrderRequest = new PF_DigicertOrder();

                if (string.IsNullOrWhiteSpace(pf.DigicertOrderRequest.CodeSignProvisioningMethod)
                    && !string.IsNullOrEmpty(pf.StoreOrderDetail?.CodeSignProvisioningMethod))
                    pf.DigicertOrderRequest.CodeSignProvisioningMethod = pf.StoreOrderDetail.CodeSignProvisioningMethod;

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.CSR = pf.CSR;
                response.CodeSignProvisioningMethod = pf.DigicertOrderRequest.CodeSignProvisioningMethod;
                response.ServerHardwarePlatformId = pf.DigicertOrderRequest.ServerHardwarePlatformId;
                response.IntermediateCAId = pf.DigicertOrderRequest.IntermediateCAId;
                response.CodeSignProvisioningMethods = BLDigicert.GetDigicertCodeSignProvisingMethods().Select(ToSelectListItemDto).ToList();
                response.CodeSignServerHardwarePlatforms = BLDigicert.GetDigicertCodeSignServerHardwarePlatforms().Select(ToSelectListItemDto).ToList();
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();

                if (pf.DigicertOrderRequest.ShippingInformation != null)
                {
                    var s = pf.DigicertOrderRequest.ShippingInformation;
                    response.Shipping = new DigicertShippingInformationDto
                    {
                        ShippingName = s.ShippingName,
                        ShippingAddress1 = s.ShippingAddress1,
                        ShippingAddress2 = s.ShippingAddress2,
                        ShippingCity = s.ShippingCity,
                        ShippingState = s.ShippingState,
                        ShippingCountryCode = s.ShippingCountryCode,
                        ShippingCountryName = s.ShippingCountryName,
                        ShippingPostalCode = s.ShippingPostalCode
                    };
                }

                _draftStore.Update(resolved.token, pf);
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

        [HttpPost("CSRInfo_CodeSign")]
        public IActionResult CSRInfo_CodeSign_Post([FromBody] CodeSignCsrInfoPostRequest? objModel)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                if (objModel == null)
                {
                    response.IsSuccess = false;
                    response.Msg = "Please enter mandatory fields.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                if (PF_RequestObject.DigicertOrderRequest == null)
                    PF_RequestObject.DigicertOrderRequest = new PF_DigicertOrder();

                if (string.IsNullOrWhiteSpace(PF_RequestObject.DigicertOrderRequest.CodeSignProvisioningMethod)
                    && !string.IsNullOrEmpty(PF_RequestObject.StoreOrderDetail?.CodeSignProvisioningMethod))
                    PF_RequestObject.DigicertOrderRequest.CodeSignProvisioningMethod = PF_RequestObject.StoreOrderDetail.CodeSignProvisioningMethod;

                if (!string.IsNullOrWhiteSpace(objModel.CodeSignProvisioningMethod))
                    PF_RequestObject.DigicertOrderRequest.CodeSignProvisioningMethod = objModel.CodeSignProvisioningMethod;

                string method = (PF_RequestObject.DigicertOrderRequest.CodeSignProvisioningMethod ?? string.Empty).ToLower();

                if (method == "email")
                {
                    if (string.IsNullOrEmpty(objModel.CSR))
                    {
                        PF_RequestObject.CSR = string.Empty;
                        PF_RequestObject.CSRDetailRow = new CSRDetail();
                        response.IsSuccess = false;
                        response.Msg = "Please enter CSR Details.";
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }

                    ValidateAndParseCSRResponse objRes = BLGeneral.PasreCSR(objModel.CSR.Trim());
                    response.objCSRResJson = JsonConvert.SerializeObject(objRes);
                    if (objRes.error == null || objRes.error.ErrorCode == 0)
                    {
                        if ((objRes.DomainName ?? string.Empty).ToLower().StartsWith("*."))
                        {
                            response.IsSuccess = false;
                            response.Msg = "THE COMMON NAME (DOMAIN NAME) MAY NOT CONTAIN A * IN CSR";
                            response.configurationToken = resolved.token;
                            return Ok(response);
                        }

                        PF_RequestObject.CSRDetailRow = new CSRDetail
                        {
                            DomainName = objRes.DomainName,
                            Country = objRes.Country,
                            Locality = objRes.Locality,
                            Organisation = objRes.Organisation,
                            OrganisationUnit = objRes.OrganisationUnit,
                            State = objRes.State,
                            CSR = objModel.CSR
                        };
                        PF_RequestObject.CSR = objModel.CSR;
                        PF_RequestObject.DigicertOrderRequest.ShippingInformation = new DigicertShippingInformation();
                    }
                    else
                    {
                        response.IsSuccess = false;
                        response.Msg = TranslateUtility.Translate(
                            "en",
                            PF_RequestObject.LanguageCode ?? "en",
                            objRes.error?.ErrorMessage ?? string.Empty);
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }
                }
                else if (method == "ship_token")
                {
                    PF_RequestObject.CSR = string.Empty;
                    PF_RequestObject.CSRDetailRow = new CSRDetail();
                    PF_RequestObject.DigicertOrderRequest.ShippingInformation = new DigicertShippingInformation
                    {
                        ShippingName = objModel.Shipping?.ShippingName,
                        ShippingAddress1 = objModel.Shipping?.ShippingAddress1,
                        ShippingAddress2 = objModel.Shipping?.ShippingAddress2,
                        ShippingCity = objModel.Shipping?.ShippingCity,
                        ShippingState = objModel.Shipping?.ShippingState,
                        ShippingCountryCode = objModel.Shipping?.ShippingCountryCode,
                        ShippingCountryName = objModel.Shipping?.ShippingCountryName,
                        ShippingPostalCode = objModel.Shipping?.ShippingPostalCode
                    };
                }
                else if (method == "client_app")
                {
                    PF_RequestObject.CSR = string.Empty;
                    PF_RequestObject.CSRDetailRow = new CSRDetail();
                    PF_RequestObject.DigicertOrderRequest.ServerHardwarePlatformId = objModel.ServerHardwarePlatformId;
                    PF_RequestObject.DigicertOrderRequest.IntermediateCAId = objModel.IntermediateCAId;
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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

        [HttpGet("ServerTypeAdminEmail_CodeSign")]
        public IActionResult ServerTypeAdminEmail_CodeSign_Get([FromQuery] string? configurationToken = null)
        {
            var response = new ServerTypeAdminEmailGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = resolved.request;
                var allowedCACerts = BLDigicert.GetDigicertAllowedCACerts(
                    pf.ProductDetail!.ProductId,
                    pf.CACredentialDetails?.GetDigicertCACredential());

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.IntermediateCAId = pf.DigicertOrderRequest?.IntermediateCAId;
                response.IntermediateCAText = pf.DigicertOrderRequest?.IntermediateCAText;
                response.AllowedCACerts = allowedCACerts.Select(ToSelectListItemDto).ToList();
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

        [HttpPost("ServerTypeAdminEmail_CodeSign")]
        public IActionResult ServerTypeAdminEmail_CodeSign_Post([FromBody] DigicertContactDetailDto? objModel)
            => ServerTypeAdminEmail_Post(objModel);

        [HttpGet("ContactInfo_CodeSign")]
        public IActionResult ContactInfo_CodeSign_Get([FromQuery] string? configurationToken = null)
            => ContactInfo_Get(configurationToken);

        [HttpPost("ContactInfo_CodeSign")]
        public IActionResult ContactInfo_CodeSign_Post([FromBody] DigicertContactDetailDto? objModel)
        {
            // CodeSign always saves EV approver (unlike SSL ContactInfo which is EV-only)
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = resolved.request;
                if (pf.DigicertOrderRequest == null)
                    pf.DigicertOrderRequest = new PF_DigicertOrder();

                pf.DigicertOrderRequest.OrganizationContactInfo = new SymantecContactInfo
                {
                    Title = objModel?.AdminTitle,
                    FirstName = objModel?.AdminFirstName,
                    LastName = objModel?.AdminLastName,
                    Email = objModel?.AdminEmail,
                    PhoneNo = objModel?.AdminPhoneNo
                };
                pf.DigicertOrderRequest.TechnicalContactInfoRow = new SymantecContactInfo
                {
                    Title = objModel?.TechnicalTitle,
                    FirstName = objModel?.TechnicalFirstName,
                    LastName = objModel?.TechnicalLastName,
                    Email = objModel?.TechnicalEmail,
                    PhoneNo = objModel?.TechnicalPhoneNo
                };
                pf.DigicertOrderRequest.EVApproverContactInfo = new SymantecContactInfo
                {
                    Title = objModel?.ApproverTitle,
                    FirstName = objModel?.ApproverFirstName,
                    LastName = objModel?.ApproverLastName,
                    Email = objModel?.ApproverEmail,
                    PhoneNo = objModel?.ApproverPhoneNo
                };

                _draftStore.Update(resolved.token, pf);
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

        [HttpGet("OrganizationInfo_CodeSign")]
        public IActionResult OrganizationInfo_CodeSign_Get([FromQuery] string? configurationToken = null)
            => OrganizationInfo_Get(configurationToken);

        [HttpPost("OrganizationInfo_CodeSign")]
        public IActionResult OrganizationInfo_CodeSign_Post([FromBody] DigicertOrganizationInformationDto? objModel)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = resolved.request;
                if (pf.DigicertOrderRequest == null)
                    pf.DigicertOrderRequest = new PF_DigicertOrder();
                if (pf.DigicertOrderRequest.OrgranisationInfoRow == null)
                    pf.DigicertOrderRequest.OrgranisationInfoRow = new SymantecOrganizationInfo();

                pf.DigicertOrderRequest.OrgranisationInfoRow.LegalName = objModel?.Orgname;
                pf.DigicertOrderRequest.OrgranisationInfoRow.AssumedName = objModel?.DBAname;
                pf.DigicertOrderRequest.OrgranisationInfoRow.Address1 = objModel?.Address1;
                pf.DigicertOrderRequest.OrgranisationInfoRow.Address2 = objModel?.Address2;
                pf.DigicertOrderRequest.OrgranisationInfoRow.PhoneNo = objModel?.PhoneNo;
                pf.DigicertOrderRequest.OrgranisationInfoRow.Duns = objModel?.Duns;
                pf.DigicertOrderRequest.OrgranisationInfoRow.City = objModel?.City;
                pf.DigicertOrderRequest.OrgranisationInfoRow.State = objModel?.State;
                pf.DigicertOrderRequest.OrgranisationInfoRow.Country = objModel?.CountryName;
                pf.DigicertOrderRequest.OrgranisationInfoRow.ZipCode = objModel?.PostalCode;
                pf.DigicertOrderRequest.OrgranisationInfoRow.Division = objModel?.Division;
                pf.DigicertOrderRequest.OrgranisationInfoRow.Fax = objModel?.Fax;

                string method = (pf.DigicertOrderRequest.CodeSignProvisioningMethod ?? string.Empty).ToLower();
                if (method == "ship_token" || method == "client_app")
                {
                    pf.CSRDetailRow = new CSRDetail
                    {
                        DomainName = objModel?.Orgname,
                        Organisation = objModel?.Orgname,
                        OrganisationUnit = string.Empty,
                        State = objModel?.State,
                        Locality = string.Empty,
                        Country = objModel?.CountryName
                    };
                }

                _draftStore.Update(resolved.token, pf);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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

        [HttpPost("ValidateOrganization_CodeSign")]
        public IActionResult ValidateOrganization_CodeSign([FromBody] ValidateOrganizationRequest? request)
            => ValidateOrganization(request);

        [HttpGet("Summary_CodeSign")]
        public IActionResult Summary_CodeSign_Get([FromQuery] string? configurationToken = null)
            => Summary_Get(configurationToken);

        [HttpPost("Summary_CodeSign")]
        public IActionResult Summary_CodeSign_Post([FromBody] SummaryPostRequest? request)
        {
            var response = new SummaryPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Response objPF_Response = DigicertPlaceOrder.PlaceCodeSignOrder(resolved.request);
                if (objPF_Response.ErrorCode == 0)
                {
                    response.IsSuccess = true;
                    response.Msg = "Your order number is : " + objPF_Response.DigicertOrderNumber;
                    response.returnUrl = "/ManageOrder/Digicert/orderdetail";
                    response.DigicertOrderNumber = objPF_Response.DigicertOrderNumber;
                    response.DigicertCertificateId = objPF_Response.DigicertCertificateId;
                    response.configurationToken = resolved.token;
                    _draftStore.Remove(resolved.token);
                    return Ok(response);
                }

                response.IsSuccess = false;
                response.Msg = TranslateUtility.Translate(
                    "en",
                    resolved.request.LanguageCode ?? "en",
                    objPF_Response.ErrorMessage ?? string.Empty);
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

        #endregion

        #region Phase 7 — VMC

        [HttpGet("VMC")]
        public IActionResult VMC([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry("VMC", pin, configurationToken);
            return Ok(response);
        }

        [HttpGet("VMCInfo")]
        public IActionResult VMCInfo_Get([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new VMCInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                var vmc = resolved.request.DigicertOrderRequest?.VMCCertificateDetail;
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.DomainName = vmc?.DomainName;
                response.FileBase64 = vmc?.FileBase64;
                response.FileName = vmc?.FileName;
                response.Logo = vmc?.Logo;
                response.EnableHosting = vmc?.EnableHosting ?? false;
                response.MarkType = vmc?.MarkType;
                response.RegistrationNumber = vmc?.MarkTypeData?.RegistrationNumber;
                response.CountryCode = vmc?.MarkTypeData?.CountryCode;
                response.IsLogoExists = vmc?.IsLogoExists ?? false;
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

        [HttpPost("VMCInfo")]
        public IActionResult VMCInfo_Post([FromBody] VMCInfoPostRequest? objModel)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                if (objModel == null || string.IsNullOrEmpty(objModel.DomainName))
                {
                    response.IsSuccess = false;
                    response.Msg = "Please enter domain name.";
                    return Ok(response);
                }

                if (!BLGeneral.isValidSingleDomain(objModel.DomainName))
                {
                    response.IsSuccess = false;
                    response.Msg = "Please enter valid domain name.";
                    return Ok(response);
                }

                string strLogo = string.Empty;
                if (objModel.IsLogoExists)
                {
                    if (string.IsNullOrWhiteSpace(objModel.FileBase64))
                    {
                        response.IsSuccess = false;
                        response.Msg = "Please upload logo file(.svg).";
                        return Ok(response);
                    }

                    byte[] fileBytes;

                    try
                    {
                        fileBytes = Convert.FromBase64String(objModel.FileBase64);
                    }
                    catch (FormatException)
                    {
                        response.IsSuccess = false;
                        response.Msg = "Invalid logo file.";
                        return Ok(response);
                    }

                    if (fileBytes.Length == 0)
                    {
                        response.IsSuccess = false;
                        response.Msg = "Please upload logo file(.svg).";
                        return Ok(response);
                    }

                    string fileName = string.IsNullOrWhiteSpace(objModel.FileName)
                        ? "logo.svg"
                        : objModel.FileName;

                    if (!string.Equals(
                            Path.GetExtension(fileName),
                            ".svg",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        response.IsSuccess = false;
                        response.Msg = "The logo file must be in the SVG format.";
                        return Ok(response);
                    }

                    strLogo = System.Text.Encoding.UTF8.GetString(fileBytes);

                    strLogo = strLogo.Replace(
                        Environment.NewLine,
                        string.Empty);

                    DigicertErrorDetails objLogoResponse =
                        DigicertAPIHelper.ValidateVMCProductLogo(strLogo);

                    if (objLogoResponse != null &&
                        objLogoResponse.StatusCode < 0)
                    {
                        LogWriter.LogCARequestResponseObjectToDB(
                            resolved.request.StoreOrderDetail?.Pin,
                            objLogoResponse.CARequestObject,
                            objLogoResponse.ToString() ?? string.Empty,
                            "VMCInfo");

                        response.IsSuccess = false;
                        response.Msg =
                            "Invalid Logo Format. The logo you uploaded is not in a valid SVG format or is not supported by VMC.";

                        response.configurationToken = resolved.token;

                        return Ok(response);
                    }
                }

                PF_Request pf = resolved.request;
                if (pf.DigicertOrderRequest == null)
                    pf.DigicertOrderRequest = new PF_DigicertOrder();

                if (pf.DigicertOrderRequest.AdditionalDomainList != null
                    && pf.DigicertOrderRequest.AdditionalDomainList.Count > 0
                    && pf.DigicertOrderRequest.VMCCertificateDetail != null
                    && !string.IsNullOrEmpty(pf.DigicertOrderRequest.VMCCertificateDetail.DomainName))
                {
                    pf.DigicertOrderRequest.AdditionalDomainList.Remove(pf.DigicertOrderRequest.VMCCertificateDetail.DomainName);
                }

                pf.CSR = string.Empty;
                pf.CSRDetailRow = new CSRDetail { DomainName = objModel.DomainName };
                pf.DigicertOrderRequest.VMCCertificateDetail = new VMC_CertificateDetail
                {
                    DomainName = objModel.DomainName,
                    Logo = objModel.Logo,
                    EnableHosting = true,
                    MarkType = objModel.MarkType,
                    FileBase64 = objModel.FileBase64,
                    FileName = objModel.FileName,
                    strLogo = strLogo,
                    IsLogoExists = objModel.IsLogoExists,
                    MarkTypeData = new VmcMarkTypeData
                    {
                        RegistrationNumber = objModel.RegistrationNumber,
                        // same quirk as old controller: CountryCode assigned from RegistrationNumber field path
                        CountryCode = objModel.CountryCode ?? objModel.RegistrationNumber
                    }
                };

                _draftStore.Update(resolved.token, pf);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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

        [HttpGet("DCVInfo_VMC")]
        public IActionResult DCVInfo_VMC_Get([FromQuery] string? configurationToken = null)
        {
            var response = new DigicertJsonResponse { IsSuccess = true, configurationToken = configurationToken };
            var resolved = _authenticationService.ResolveDraft(configurationToken, null);
            if (!resolved.ok)
            {
                response.IsSuccess = false;
                response.Msg = resolved.errorMessage ?? "Session expired.";
            }
            else
                response.configurationToken = resolved.token;
            return Ok(response);
        }

        [HttpPost("DCVInfo_VMC")]
        public IActionResult DCVInfo_VMC_Post([FromBody] DCVInfoPostRequest? request)
            => DCVInfo_Post(request);

        [HttpGet("SANInfo_VMC")]
        public IActionResult SANInfo_VMC_Get([FromQuery] string? configurationToken = null)
        {
            // VMC uses ApprovalMethod.ToString() as primary SAN value (old behavior)
            var response = new SANInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = resolved.request;
                EnsureSanLists(pf);
                string domainName = pf.CSRDetailRow?.DomainName ?? string.Empty;
                pf.DigicertOrderRequest.AdditionalDomainList!.AddUniqueKey(domainName, pf.DigicertOrderRequest.ApprovalMethod.ToString());

                int noOfAdditionalDomains = pf.DigicertOrderRequest.NoOfAdditionalDomains == 0
                    ? BLGeneral.GetAddDomain(pf.StoreOrderDetail!.StoreOrderId, pf.StoreOrderDetail.ProductId)
                    : pf.DigicertOrderRequest.NoOfAdditionalDomains;
                int noOfWild = pf.DigicertOrderRequest.NoOfAdditionalWildCardDomains == 0
                    ? BLGeneral.GetWildcardSANCount(pf.StoreOrderDetail!.StoreOrderId, pf.StoreOrderDetail.ProductId)
                    : pf.DigicertOrderRequest.NoOfAdditionalWildCardDomains;

                _draftStore.Update(resolved.token, pf);
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.PrimaryDomainName = domainName;
                response.DomainName = domainName;
                response.NoOfAdditionalDomains = noOfAdditionalDomains;
                response.NoOfAdditionalWildCardDomains = noOfWild;
                response.TotalNoOfSANAllowed = noOfAdditionalDomains + noOfWild;
                response.AdditionalDomainList = pf.DigicertOrderRequest.AdditionalDomainList;
                response.AdditionalWildCardDomainList = pf.DigicertOrderRequest.AdditionalWildCardDomainList;
                response.isMultiDomain = pf.ProductDetail?.IsMultiDomain;
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

        [HttpPost("SANInfo_VMC")]
        public IActionResult SANInfo_VMC_Post([FromBody] SANInfoPostRequest? request)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = resolved.request;
                EnsureSanLists(pf);
                string domainName = pf.CSRDetailRow?.DomainName ?? string.Empty;
                pf.DigicertOrderRequest.ApproverEmail = pf.DigicertOrderRequest.AdditionalDomainList![domainName];
                _draftStore.Update(resolved.token, pf);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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

        [HttpPost("AddAdditionalDomains_VMC")]
        public IActionResult AddAdditionalDomains_VMC([FromBody] SanMutationRequest? request)
            => AddAdditionalDomains(request);

        [HttpGet("ContactInfo_VMC")]
        public IActionResult ContactInfo_VMC_Get([FromQuery] string? configurationToken = null)
            => ContactInfo_Get(configurationToken);

        [HttpPost("ContactInfo_VMC")]
        public IActionResult ContactInfo_VMC_Post([FromBody] DigicertContactDetailDto? objModel)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = resolved.request;
                if (pf.DigicertOrderRequest == null)
                    pf.DigicertOrderRequest = new PF_DigicertOrder();

                pf.DigicertOrderRequest.OrganizationContactInfo = new SymantecContactInfo
                {
                    Title = objModel?.AdminTitle,
                    FirstName = objModel?.AdminFirstName,
                    LastName = objModel?.AdminLastName,
                    Email = objModel?.AdminEmail,
                    PhoneNo = objModel?.AdminPhoneNo
                };
                pf.DigicertOrderRequest.TechnicalContactInfoRow = new SymantecContactInfo
                {
                    Title = objModel?.TechnicalTitle,
                    FirstName = objModel?.TechnicalFirstName,
                    LastName = objModel?.TechnicalLastName,
                    Email = objModel?.TechnicalEmail,
                    PhoneNo = objModel?.TechnicalPhoneNo
                };

                _draftStore.Update(resolved.token, pf);
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

        [HttpGet("OrganizationInfo_VMC")]
        public IActionResult OrganizationInfo_VMC_Get([FromQuery] string? configurationToken = null)
            => OrganizationInfo_Get(configurationToken);

        [HttpPost("OrganizationInfo_VMC")]
        public IActionResult OrganizationInfo_VMC_Post([FromBody] DigicertOrganizationInformationDto? objModel)
            => OrganizationInfo_Post(objModel);

        [HttpPost("ValidateOrganization_VMC")]
        public IActionResult ValidateOrganization_VMC([FromBody] ValidateOrganizationRequest? request)
            => ValidateOrganization(request);

        [HttpGet("Summary_VMC")]
        public IActionResult Summary_VMC_Get([FromQuery] string? configurationToken = null)
            => Summary_Get(configurationToken);

        [HttpPost("Summary_VMC")]
        public IActionResult Summary_VMC_Post([FromBody] SummaryPostRequest? request)
        {
            var response = new SummaryPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = resolved.request;

                // Persist SVG locally when logo exists (Core ContentRoot/Uploads/VMC)
                if (pf.DigicertOrderRequest?.VMCCertificateDetail != null
                    && pf.DigicertOrderRequest.VMCCertificateDetail.IsLogoExists
                    && !string.IsNullOrEmpty(pf.DigicertOrderRequest.VMCCertificateDetail.strLogo))
                {
                    try
                    {
                        string ext = string.IsNullOrWhiteSpace(pf.DigicertOrderRequest.VMCCertificateDetail.FileName)
                            ? ".svg"
                            : Path.GetExtension(pf.DigicertOrderRequest.VMCCertificateDetail.FileName);
                        if (string.IsNullOrWhiteSpace(ext))
                            ext = ".svg";

                        string fileName = Convert.ToString(pf.StoreOrderDetail!.SSLApiLinkId) + ext;
                        string dir = Path.Combine(LogWriter.ContentRoot, "Uploads", "VMC");
                        Directory.CreateDirectory(dir);
                        System.IO.File.WriteAllText(Path.Combine(dir, fileName), pf.DigicertOrderRequest.VMCCertificateDetail.strLogo);
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogErrorDetails(ex);
                    }
                }

                PF_Response objPF_Response = DigicertPlaceOrder.PlaceVmcOrder(pf);
                if (objPF_Response.ErrorCode == 0)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(pf.DigicertOrderRequest?.VMCCertificateDetail?.strLogo))
                        {
                            BLDigicert.UploadVMCProductLogo(
                                objPF_Response.DigicertOrderNumber!,
                                pf.DigicertOrderRequest.VMCCertificateDetail.strLogo,
                                pf.CACredentialDetails?.GetDigicertCACredential());
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogErrorDetails(ex);
                    }

                    string returnUrl = BLGeneral.IsOVEVProduct_StoreOrder(pf.StoreOrderDetail!.StoreOrderId)
                        ? "/ManageOrder/Digicert/changeovdcvmethod"
                        : "/ManageOrder/Digicert/changedcvmethod";

                    response.IsSuccess = true;
                    response.Msg = "Your order number is : " + objPF_Response.DigicertOrderNumber;
                    response.returnUrl = returnUrl;
                    response.DigicertOrderNumber = objPF_Response.DigicertOrderNumber;
                    response.DigicertCertificateId = objPF_Response.DigicertCertificateId;
                    response.configurationToken = resolved.token;
                    _draftStore.Remove(resolved.token);
                    return Ok(response);
                }

                response.IsSuccess = false;
                response.Msg = TranslateUtility.Translate(
                    "en",
                    pf.LanguageCode ?? "en",
                    objPF_Response.ErrorMessage ?? string.Empty);
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

        #endregion

        #region Phase 8 — X9

        [HttpGet("X9")]
        public IActionResult X9([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry("X9", pin, configurationToken);
            return Ok(response);
        }

        [HttpGet("ProfileKeyUsage")]
        public IActionResult ProfileKeyUsage_Get([FromQuery] string? configurationToken = null)
        {
            var response = new ProfileKeyUsageGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.KeyUsages = resolved.request.DigicertOrderRequest?.KeyUsages;
                response.ExtendedKeyUsages = resolved.request.DigicertOrderRequest?.ExtendedKeyUsages;
                response.KeyUsageList = BLDigicert.GetKeyUsage().Select(ToSelectListItemDto).ToList();
                response.ExtendedKeyUsageList = BLDigicert.GetExtendedKeyUsage().Select(ToSelectListItemDto).ToList();
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

        [HttpPost("ProfileKeyUsage")]
        public IActionResult ProfileKeyUsage_Post([FromBody] ProfileKeyUsagePostRequest? objModel)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                if (resolved.request.DigicertOrderRequest == null)
                    resolved.request.DigicertOrderRequest = new PF_DigicertOrder();

                resolved.request.DigicertOrderRequest.KeyUsages = objModel?.KeyUsages;
                resolved.request.DigicertOrderRequest.ExtendedKeyUsages = objModel?.ExtendedKeyUsages;
                _draftStore.Update(resolved.token, resolved.request);

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

        #endregion


        private static SelectListItemDto ToSelectListItemDto(SelectListItem i) =>
            new SelectListItemDto { Value = i.Value, Text = i.Text, Selected = i.Selected };

        private static void ApplyOrgDtoToDraft(SymantecOrganizationInfo org, DigicertOrganizationInformationDto objModel)
        {
            org.LegalName = objModel.Orgname;
            org.AssumedName = objModel.DBAname;
            org.Address1 = objModel.Address1;
            org.Address2 = objModel.Address2;
            org.PhoneNo = objModel.PhoneNo;
            org.Duns = objModel.Duns;
            org.City = objModel.City;
            org.State = objModel.State;
            org.Country = objModel.CountryName;
            org.ZipCode = objModel.PostalCode;
            org.Division = objModel.Division;
            org.Fax = objModel.Fax;
        }

        private IActionResult SanInfoGet(string? configurationToken)
        {
            var response = new SANInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureSanLists(PF_RequestObject);

                string domainName = PF_RequestObject.CSRDetailRow?.DomainName ?? string.Empty;
                bool useDns = PF_RequestObject.ProductDetail?.IsWildcardMultiDomain == true
                    || PF_RequestObject.ProductDetail?.IsWildcard == true;

                PF_RequestObject.DigicertOrderRequest.AdditionalDomainList!.AddUniqueKey(
                    domainName,
                    useDns ? ConstantUtil.Digicert_DCVMethod_DNS_TXT_TOKEN : ConstantUtil.Digicert_DCVMethod_HTTP_TOKEN);

                int noOfAdditionalDomains = PF_RequestObject.DigicertOrderRequest.NoOfAdditionalDomains == 0
                    ? BLGeneral.GetAddDomain(PF_RequestObject.StoreOrderDetail!.StoreOrderId, PF_RequestObject.StoreOrderDetail.ProductId)
                    : PF_RequestObject.DigicertOrderRequest.NoOfAdditionalDomains;

                int noOfAdditionalWildCardDomains = PF_RequestObject.DigicertOrderRequest.NoOfAdditionalWildCardDomains == 0
                    ? BLGeneral.GetWildcardSANCount(PF_RequestObject.StoreOrderDetail!.StoreOrderId, PF_RequestObject.StoreOrderDetail.ProductId)
                    : PF_RequestObject.DigicertOrderRequest.NoOfAdditionalWildCardDomains;

                _draftStore.Update(resolved.token, PF_RequestObject);

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.PrimaryDomainName = domainName;
                response.DomainName = domainName;
                response.NoOfAdditionalDomains = noOfAdditionalDomains;
                response.NoOfAdditionalWildCardDomains = noOfAdditionalWildCardDomains;
                response.TotalNoOfSANAllowed = noOfAdditionalDomains + noOfAdditionalWildCardDomains;
                response.AdditionalDomainList = PF_RequestObject.DigicertOrderRequest.AdditionalDomainList;
                response.AdditionalWildCardDomainList = PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList;
                response.isMultiDomain = PF_RequestObject.ProductDetail?.IsMultiDomain;
                response.isWildcard = PF_RequestObject.ProductDetail?.IsWildcard;
                response.isWildcardMultiDomain = PF_RequestObject.ProductDetail?.IsWildcardMultiDomain;
                response.isFlex = PF_RequestObject.ProductDetail?.IsFlex;
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

        private IActionResult SanInfoPost(string? configurationToken)
        {
            var response = new DigicertJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureSanLists(PF_RequestObject);

                foreach (var item in PF_RequestObject.DigicertOrderRequest.AdditionalDomainList!)
                {
                    if (item.Value == ConstantUtil.Digicert_DCVMethod_EMAIL)
                    {
                        response.IsSuccess = false;
                        response.Msg = "select email for " + item.Key;
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }
                }

                foreach (var item in PF_RequestObject.DigicertOrderRequest.AdditionalWildCardDomainList!)
                {
                    if (item.Value == ConstantUtil.Digicert_DCVMethod_EMAIL)
                    {
                        response.IsSuccess = false;
                        response.Msg = "select email for " + item.Key;
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }
                }

                string domainName = PF_RequestObject.CSRDetailRow?.DomainName ?? string.Empty;
                PF_RequestObject.DigicertOrderRequest.ApproverEmail =
                    PF_RequestObject.DigicertOrderRequest.AdditionalDomainList[domainName];

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.ApproverEmail= PF_RequestObject.DigicertOrderRequest.ApproverEmail;
                response.IsSuccess = true;
                response.Msg = string.Empty;
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

        private static void EnsureSanLists(PF_Request request)
        {
            if (request.DigicertOrderRequest == null)
                request.DigicertOrderRequest = new PF_DigicertOrder();

            if (request.DigicertOrderRequest.AdditionalDomainList == null)
                request.DigicertOrderRequest.AdditionalDomainList = new Dictionary<string, string>();

            if (request.DigicertOrderRequest.AdditionalWildCardDomainList == null)
                request.DigicertOrderRequest.AdditionalWildCardDomainList = new Dictionary<string, string>();
        }

        private IActionResult DcvInfoGet(string? configurationToken, bool isDv)
        {
            var response = new DCVInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                string domainName = PF_RequestObject.CSRDetailRow?.DomainName ?? string.Empty;

                var approvalEmailList = BLDigicert.GetDigicertApprovalEmailList(
                    domainName,
                    PF_RequestObject.CACredentialDetails?.GetDigicertCACredential());

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.domainName = domainName;
                response.authenticationType = PF_RequestObject.ProductDetail?.AuthenticationType;
                response.approvalMethod = PF_RequestObject.DigicertOrderRequest?.ApprovalMethod;
                response.approverEmail = PF_RequestObject.DigicertOrderRequest?.ApproverEmail;
                response.dcvScope = isDv ? PF_RequestObject.DigicertOrderRequest?.DCVScope : null;
                response.approvalEmailList = approvalEmailList.Select(i => new SelectListItemDto
                {
                    Value = i.Value,
                    Text = i.Text,
                    Selected = i.Selected
                }).ToList();

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
    }
}
