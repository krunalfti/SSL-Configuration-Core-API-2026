using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SSLConfiguration.Application;
using SSLConfiguration.Application.Services;
using SSLConfiguration.Contracts.SSLConfiguration.Comodo;
using SSLConfiguration.Contracts.SSLConfiguration.Digicert;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.Persistence;
using System.Text;
using System.Text.Json;
using VerisignGateway;
using AppProductCode = SSLConfiguration.Application.ProductCode;
using ComodoCodeSignContactInfoDto = SSLConfiguration.Contracts.SSLConfiguration.Comodo.CodeSignContactInfoDto;
using ComodoCodeSignContactInfoGetResponse = SSLConfiguration.Contracts.SSLConfiguration.Comodo.CodeSignContactInfoGetResponse;
using ComodoCodeSignCsrInfoDto = SSLConfiguration.Contracts.SSLConfiguration.Comodo.CodeSignCsrInfoDto;
using ComodoCodeSignCsrInfoGetResponse = SSLConfiguration.Contracts.SSLConfiguration.Comodo.CodeSignCsrInfoGetResponse;
using ComodoCodeSignSummaryGetResponse = SSLConfiguration.Contracts.SSLConfiguration.Comodo.CodeSignSummaryGetResponse;
using ComodoContactInfoGetResponse = SSLConfiguration.Contracts.SSLConfiguration.Comodo.ContactInfoGetResponse;
using ComodoOrganizationInfoGetResponse = SSLConfiguration.Contracts.SSLConfiguration.Comodo.OrganizationInfoGetResponse;
using ComodoSummaryGetResponse = SSLConfiguration.Contracts.SSLConfiguration.Comodo.SummaryGetResponse;
using ComodoSummaryPostRequest = SSLConfiguration.Contracts.SSLConfiguration.Comodo.SummaryPostRequest;
using ComodoSummaryPostResponse = SSLConfiguration.Contracts.SSLConfiguration.Comodo.SummaryPostResponse;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;
namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration
{
    /// <summary>
    /// Ported from SSLConfiguration Controllers/ComodoController — Phase 0–6:
    /// Entry + CSR + DCV/SAN + Contact/Org/Jurisdiction/Verification + Summary + CodeSign + PAC.
    /// </summary>
    [ClientAuthorize]
    [ApiController]
    [Route("api/SSLConfiguration/[controller]")]
    public class ComodoController : ControllerBase
    {
        private readonly ConfigurationDraftStore _draftStore;
        private readonly IAuthenticationService _authenticationService;

        public ComodoController(IAuthenticationService authenticationService, ConfigurationDraftStore draftStore)
        {
            _authenticationService = authenticationService;
            _draftStore = draftStore;
        }

        /// <summary>
        /// Same as old ComodoController.DV — validates draft; ACME DV products redirect to ACME.
        /// Route: GET /api/SSLConfiguration/Comodo/DV?pin=&amp;configurationToken=
        /// </summary>
        [HttpGet("DV")]
        public IActionResult DV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            return EntryWithAcmeRouting("DV", pin, configurationToken, (int)AppProductCode.SectigoACMEDV, "DV", "ACMEInfo");
        }

        /// <summary>Same as old ComodoController.OV.</summary>
        [HttpGet("OV")]
        public IActionResult OV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            return EntryWithAcmeRouting("OV", pin, configurationToken, (int)AppProductCode.SectigoACMEOV, "OV", "ACMEOVInfo");
        }

        /// <summary>Same as old ComodoController.EV.</summary>
        [HttpGet("EV")]
        public IActionResult EV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry("EV", pin, configurationToken);
            return Ok(response);
        }

        /// <summary>
        /// Same as old ComodoController.CSRInfo GET — returns CSR draft fields (was PartialView).
        /// Route: GET /api/SSLConfiguration/Comodo/CSRInfo?configurationToken=
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
        /// Same as old ComodoController.CSRInfo POST — ComodoParseCSR + draft update.
        /// Route: POST /api/SSLConfiguration/Comodo/CSRInfo
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
                    // Do not wipe an existing draft CSR when client posts empty by mistake.
                    response.IsSuccess = false;
                    response.Msg = "Please enter CSR Details.";
                    response.configurationToken = resolved.token;
                    return Ok(response);
                }

                int productId = PF_RequestObject.StoreOrderDetail?.ProductId
                    ?? PF_RequestObject.ProductDetail?.ProductId
                    ?? 0;
                VerisignGateway.ProductCode pcode = BLGeneral.GetProductCodeByProductName(productId);
                var comodoCredential = PF_RequestObject.CACredentialDetails?.GetComodoCACredential();
                ValidateAndParseCSRResponse objRes = VerisignAPIHelper.ComodoParseCSR(pcode, objModel!.CSR, comodoCredential);
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

                    ApproverEmailListResponse objResponse = VerisignAPIHelper.GetComodoApproveremail(
                        objRes.DomainName,
                        comodoCredential);
                    response.objEmailListJson = JsonConvert.SerializeObject(objResponse);
                    if (objResponse.error != null && objResponse.error.ErrorCode < 0)
                    {
                        LogWriter.LogCARequestResponseObjectToDB(
                            PF_RequestObject.StoreOrderDetail?.Pin ?? string.Empty,
                            objResponse.CARequestObject,
                            JsonConvert.SerializeObject(objResponse.error),
                            "GetComodoApproveremail");

                        response.IsSuccess = false;
                        // Prefer CA message (e.g. login/IP lock) over generic domain text.
                        response.Msg = Resources.InvalidDomainNameInCSR;
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }

                    var CSRDetail = new CSRDetail
                    {
                        DomainName = (objRes.DomainName ?? string.Empty).Trim().ToLower(),
                        Country = objRes.Country,
                        Locality = objRes.Locality,
                        Organisation = objRes.Organisation,
                        OrganisationUnit = objRes.OrganisationUnit,
                        State = objRes.State,
                        Email = objRes.Email,
                        CSR = objModel.CSR
                    };
                    response.CSRDetailJson= JsonConvert.SerializeObject(CSRDetail);
                    PF_RequestObject.CSRDetailRow = CSRDetail;
                    PF_RequestObject.CSR = objModel.CSR;

                    if (PF_RequestObject.ComodoOrderRequest == null)
                        PF_RequestObject.ComodoOrderRequest = new PF_ComodoOrder();

                    if (PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow == null)
                        PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow = new ComodoOrderDetailDraft();

                    PF_RequestObject.ComodoOrderRequest.PrimaryDomain = CSRDetail.DomainName;
                    PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.CSR_MD5 =
                        string.IsNullOrEmpty(objRes.MD5) ? string.Empty : objRes.MD5;
                    // Same as old: SHA1 field stores SHA256 when SHA1Hash present
                    PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.CSR_SHA1 =
                        string.IsNullOrEmpty(objRes.SHA1Hash) ? string.Empty : objRes.SHA256;

                    if (PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail == null)
                        PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail = new Dictionary<string, string>();
                    else
                        PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.Clear();

                    if (PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail == null)
                        PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail = new Dictionary<string, string>();
                    else
                        PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail.Clear();

                    #region Manage SAN / Wildcard SAN in CSR

                    if (!string.IsNullOrEmpty(objRes.SAN) && objRes.SAN != PF_RequestObject.CSRDetailRow.DomainName)
                    {
                        string[] SANListInCSR = objRes.SAN.Split(',').Distinct().ToArray();

                        if (SANListInCSR != null && SANListInCSR.Length > 0)
                        {
                            if (PF_RequestObject.ProductDetail == null || !PF_RequestObject.ProductDetail.IsMultiDomain)
                            {
                                response.IsSuccess = false;
                                response.Msg = "CSR with SAN is not allowed.";
                                return Ok(response);
                            }

                            var normalSANList = new List<string>();
                            var wildcardSANList = new List<string>();

                            foreach (string san in SANListInCSR)
                            {
                                if (PF_RequestObject.CSRDetailRow.DomainName != san.ToLower())
                                {
                                    if (!BLGeneral.isValidSingleOrWildcardDomain(san.Trim()))
                                    {
                                        response.IsSuccess = false;
                                        response.Msg = "Invalid domain name : " + san.Trim().Replace(" ", "[space]");
                                        return Ok(response);
                                    }

                                    if (san.StartsWith("*."))
                                        wildcardSANList.Add(san.Trim().ToLower());
                                    else
                                        normalSANList.Add(san.Trim().ToLower());
                                }
                            }

                            if (PF_RequestObject.ProductDetail.IsWildcardMultiDomain)
                            {
                                if (normalSANList.Count > 0)
                                {
                                    response.IsSuccess = false;
                                    response.Msg = "Allowed wildcard SAN only.";
                                    return Ok(response);
                                }
                            }
                            else
                            {
                                if (!PF_RequestObject.ProductDetail.IsFlex && wildcardSANList.Count > 0)
                                {
                                    response.IsSuccess = false;
                                    response.Msg = "Wildcard SAN not allowed.";
                                    return Ok(response);
                                }
                            }

                            int storeOrderId = PF_RequestObject.StoreOrderDetail?.StoreOrderId ?? 0;
                            int storeProductId = PF_RequestObject.StoreOrderDetail?.ProductId
                                ?? PF_RequestObject.ProductDetail.ProductId;

                            PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.AddUniqueKey(
                                PF_RequestObject.CSRDetailRow.DomainName!,
                                PF_RequestObject.ProductDetail.IsWildcardMultiDomain || PF_RequestObject.ProductDetail.IsWildcard
                                    ? ConstantUtil.Comodo_DCVMethod_CnameCsrHash
                                    : ConstantUtil.Comodo_DCVMethod_HTTPCsrHash);
                            PF_RequestObject.ComodoOrderRequest.isCSRIncludeSAN = true;

                            if (PF_RequestObject.ProductDetail.IsFlex)
                            {
                                int allowedMaxSan = BLGeneral.GetAddDomain(storeOrderId, storeProductId);
                                int allowedMaxWildcardSan = BLGeneral.GetWildcardSANCount(storeOrderId, storeProductId);

                                if (normalSANList.Count > allowedMaxSan)
                                {
                                    response.IsSuccess = false;
                                    response.Msg = string.Format(
                                        "Max SAN allowed : {0} (Normal SAN: {1}, Wildcard SAN: {2}).",
                                        allowedMaxSan + allowedMaxWildcardSan,
                                        allowedMaxSan,
                                        allowedMaxWildcardSan);
                                    return Ok(response);
                                }

                                if (wildcardSANList.Count > allowedMaxWildcardSan)
                                {
                                    response.IsSuccess = false;
                                    response.Msg = string.Format(
                                        "Max SAN allowed : {0} (Normal SAN: {1}, Wildcard SAN: {2}).",
                                        allowedMaxSan + allowedMaxWildcardSan,
                                        allowedMaxSan,
                                        allowedMaxWildcardSan);
                                    return Ok(response);
                                }

                                foreach (string san in normalSANList)
                                    PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.AddUniqueKey(
                                        san, ConstantUtil.Comodo_DCVMethod_HTTPCsrHash);

                                foreach (string san in wildcardSANList)
                                    PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail.AddUniqueKey(
                                        san, ConstantUtil.Comodo_DCVMethod_CnameCsrHash);
                            }
                            else
                            {
                                int sanInCSRCount = PF_RequestObject.ProductDetail.IsWildcardMultiDomain
                                    ? wildcardSANList.Count
                                    : normalSANList.Count;
                                int allowedMaxSanCount = BLGeneral.GetAddDomain(storeOrderId, storeProductId);

                                if (sanInCSRCount > allowedMaxSanCount)
                                {
                                    response.IsSuccess = false;
                                    response.Msg = "Maximum allowed "
                                        + (PF_RequestObject.ProductDetail.IsWildcardMultiDomain ? "Wildcard" : "")
                                        + " SAN : " + allowedMaxSanCount;
                                    return Ok(response);
                                }

                                if (PF_RequestObject.ProductDetail.IsWildcardMultiDomain)
                                {
                                    foreach (string san in wildcardSANList)
                                        PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.AddUniqueKey(
                                            san, ConstantUtil.Comodo_DCVMethod_CnameCsrHash);
                                }
                                else
                                {
                                    foreach (string san in normalSANList)
                                        PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.AddUniqueKey(
                                            san, ConstantUtil.Comodo_DCVMethod_HTTPCsrHash);
                                }
                            }
                        }
                    }

                    #endregion

                    _draftStore.Update(resolved.token, PF_RequestObject);
                    response.IsSuccess = true;
                    response.Msg = string.Empty;
                    return Ok(response);
                }
                else
                {
                    LogWriter.LogCARequestResponseObjectToDB(
                        PF_RequestObject.StoreOrderDetail?.Pin ?? string.Empty,
                        objRes.CARequestObject,
                        JsonConvert.SerializeObject(objRes.error),
                        "ComodoParseCSR");

                    response.IsSuccess = false;
                    response.Msg = objRes.error?.ErrorMessage;
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
        /// Same as old ComodoController.SetCSR — returns "success" / "error" string.
        /// Route: POST /api/SSLConfiguration/Comodo/SetCSR
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

                PF_RequestObject.CSRDetailRow.IsCSRSaved = request!.isCSRSaved;
                PF_RequestObject.CSRDetailRow.CSR = PF_RequestObject.CSR;

                _draftStore.Update(resolved.token, PF_RequestObject);
                return Ok("success");
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                return Ok("error");
            }
        }

        #region Phase 2 — DCV + SAN

        /// <summary>
        /// Same as old ComodoController.DCVInfo GET.
        /// Route: GET /api/SSLConfiguration/Comodo/DCVInfo?configurationToken=
        /// </summary>
        [HttpGet("DCVInfo")]
        public IActionResult DCVInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new DCVInfoGetResponse();
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
                EnsureComodoOrder(pf);

                string? domainName = pf.CSRDetailRow?.DomainName;
                var approvalEmailList = BLGeneral.GetComodoApprovalEmailList(
                    domainName ?? string.Empty,
                    pf.CACredentialDetails?.GetComodoCACredential(),
                    pf.ComodoOrderRequest.ComodoOrderDetailRow?.ApprovalEmail);

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.domainName = domainName;
                response.approverEmail = pf.ComodoOrderRequest.ComodoOrderDetailRow?.ApprovalEmail;
                response.authenticationType = pf.ProductDetail?.AuthenticationType;
                response.approvalEmailList = approvalEmailList.Select(ToSelectListItemDto).ToList();
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
        /// Same as old ComodoController.DCVInfo POST.
        /// Route: POST /api/SSLConfiguration/Comodo/DCVInfo
        /// </summary>
        [HttpPost("DCVInfo")]
        public IActionResult DCVInfoPost([FromBody] DCVInfoPostRequest? request)
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
                EnsureComodoOrder(PF_RequestObject);

                string? ApprovalMethod = request?.ApprovalMethod;
                string? ApprovalEmail = request?.ApprovalEmail;

                if (ApprovalMethod == ConstantUtil.DCVMethod_EMAIL)
                {
                    PF_RequestObject.ComodoOrderRequest.ApprovalMethod = ConstantUtil.Comodo_DCVMethod_Email;
                    PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.ApprovalEmail = ApprovalEmail;
                }
                else if (ApprovalMethod == ConstantUtil.DCVMethod_FILE)
                {
                    PF_RequestObject.ComodoOrderRequest.ApprovalMethod = ConstantUtil.Comodo_DCVMethod_HTTPCsrHash;
                    PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.ApprovalEmail = ConstantUtil.Comodo_DCVMethod_HTTPCsrHash;
                }
                else if (ApprovalMethod == ConstantUtil.DCVMethod_DNS)
                {
                    PF_RequestObject.ComodoOrderRequest.ApprovalMethod = ConstantUtil.Comodo_DCVMethod_CnameCsrHash;
                    PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.ApprovalEmail = ConstantUtil.Comodo_DCVMethod_CnameCsrHash;
                }

                PF_RequestObject.ComodoOrderRequest.PrimaryDomainEmail = ApprovalEmail;

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
        /// Same as old ComodoController.SANInfo GET.
        /// Route: GET /api/SSLConfiguration/Comodo/SANInfo?configurationToken=
        /// </summary>
        [HttpGet("SANInfo")]
        public IActionResult SANInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new SANInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureComodoOrder(PF_RequestObject);

                string? domainName = PF_RequestObject.CSRDetailRow?.DomainName;
                if (!string.IsNullOrEmpty(domainName) && PF_RequestObject.ProductDetail != null)
                {
                    PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.AddUniqueKey(
                        domainName,
                        PF_RequestObject.ProductDetail.IsWildcardMultiDomain || PF_RequestObject.ProductDetail.IsWildcard
                            ? ConstantUtil.Comodo_DCVMethod_CnameCsrHash
                            : ConstantUtil.Comodo_DCVMethod_HTTPCsrHash);
                }

                int storeOrderId = PF_RequestObject.StoreOrderDetail?.StoreOrderId ?? 0;
                int productId = PF_RequestObject.StoreOrderDetail?.ProductId
                    ?? PF_RequestObject.ProductDetail?.ProductId
                    ?? 0;

                var sanList = new Dictionary<string, string>();
                foreach (var san in PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail)
                    sanList.AddUniqueKey(san.Key, san.Value);

                var wildcardList = new Dictionary<string, string>();
                foreach (var san in PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail)
                    wildcardList.AddUniqueKey(san.Key, san.Value);

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.DomainName = domainName;
                response.PrimaryDomainName = domainName;
                response.AdditionalDomainList = sanList;
                response.AdditionalWildCardDomainList = wildcardList;
                response.MaxSAN = BLGeneral.GetAddDomain(storeOrderId, productId);
                response.MaxWildcardSAN = BLGeneral.GetWildcardSANCount(storeOrderId, productId);
                response.TotalNoOfSANAllowed = response.MaxSAN;
                response.NoOfAdditionalDomains = BLGeneral.GetTotalAdditionalSANForComodo(domainName ?? string.Empty, sanList);
                response.NoOfAdditionalWildCardDomains = BLGeneral.GetTotalAdditionalSANForComodo(domainName ?? string.Empty, wildcardList);
                response.isMultiDomain = PF_RequestObject.ProductDetail?.IsMultiDomain;
                response.isWildcard = PF_RequestObject.ProductDetail?.IsWildcard;
                response.isWildcardMultiDomain = PF_RequestObject.ProductDetail?.IsWildcardMultiDomain;
                response.isFlex = PF_RequestObject.ProductDetail?.IsFlex;

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
        /// Same as old ComodoController.SANInfo POST — validates SAN DCV emails selected.
        /// Route: POST /api/SSLConfiguration/Comodo/SANInfo
        /// </summary>
        [HttpPost("SANInfo")]
        public IActionResult SANInfoPost([FromBody] SANInfoPostRequest? request)
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
                EnsureComodoOrder(PF_RequestObject);

                foreach (var item in PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail)
                {
                    if (item.Value == ConstantUtil.Comodo_DCVMethod_Email)
                    {
                        response.IsSuccess = false;
                        response.Msg = "select email for " + item.Key;
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }
                }

                foreach (var item in PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail)
                {
                    if (item.Value == ConstantUtil.Comodo_DCVMethod_Email)
                    {
                        response.IsSuccess = false;
                        response.Msg = "select email for " + item.Key;
                        response.configurationToken = resolved.token;
                        return Ok(response);
                    }
                }

                string? primaryDomain = PF_RequestObject.ComodoOrderRequest.PrimaryDomain
                    ?? PF_RequestObject.CSRDetailRow?.DomainName;

                if (!string.IsNullOrEmpty(primaryDomain)
                    && PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.ContainsKey(primaryDomain))
                {
                    PF_RequestObject.ComodoOrderRequest.PrimaryDomainEmail =
                        PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail[primaryDomain];
                    PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.ApprovalEmail =
                        PF_RequestObject.ComodoOrderRequest.PrimaryDomainEmail;
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
        /// Same as old ComodoController.GetApprovalEmailList.
        /// Route: POST /api/SSLConfiguration/Comodo/GetApprovalEmailList
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
                var approvalEmailList = BLGeneral.GetComodoApprovalEmailList(
                    request?.sanDomainName ?? string.Empty,
                    PF_RequestObject.CACredentialDetails?.GetComodoCACredential());

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.ApprovalEmailList = approvalEmailList.Select(ToSelectListItemDto).ToList();
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
        /// Same as old ComodoController.AddAdditionalDomains.
        /// Route: POST /api/SSLConfiguration/Comodo/AddAdditionalDomains
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
                EnsureComodoOrder(PF_RequestObject);
                response.configurationToken = resolved.token;

                if (PF_RequestObject.ProductDetail == null || !PF_RequestObject.ProductDetail.IsMultiDomain)
                {
                    response.IsSuccess = false;
                    response.Msg = "Additional SAN not allowed.";
                    return Ok(response);
                }

                string domainName = PF_RequestObject.CSRDetailRow?.DomainName ?? string.Empty;
                int usedSan = BLGeneral.GetTotalAdditionalSANForComodo(
                    domainName, PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail);
                int storeOrderId = PF_RequestObject.StoreOrderDetail?.StoreOrderId ?? 0;
                int productId = PF_RequestObject.StoreOrderDetail?.ProductId
                    ?? PF_RequestObject.ProductDetail.ProductId;
                int allowedMaxSan = BLGeneral.GetAddDomain(storeOrderId, productId);

                if (usedSan >= allowedMaxSan)
                {
                    response.IsSuccess = false;
                    response.Msg = "Maximum allowed SAN : " + allowedMaxSan;
                    return Ok(response);
                }

                if (string.IsNullOrEmpty(request?.additionalDomains))
                {
                    response.IsSuccess = false;
                    response.Msg = "Enter san.";
                    return Ok(response);
                }

                string[] addDomainList = request.additionalDomains.Trim().Split('\n').Distinct().ToArray();
                int newSan = 0;

                foreach (string san in addDomainList)
                {
                    if (PF_RequestObject.ProductDetail.IsWildcardMultiDomain)
                    {
                        if (!BLGeneral.isValidWildcardDomain(san.Trim()))
                        {
                            response.IsSuccess = false;
                            response.Msg = "Invalid domain name : " + san.Trim().Replace(" ", "[space]");
                            return Ok(response);
                        }
                    }
                    else
                    {
                        if (!BLGeneral.isValidSingleDomain(san.Trim()))
                        {
                            response.IsSuccess = false;
                            response.Msg = "Invalid domain name : " + san.Trim().Replace(" ", "[space]");
                            return Ok(response);
                        }
                    }

                    if (usedSan + newSan >= allowedMaxSan)
                    {
                        response.IsSuccess = false;
                        response.Msg = "Maximum SAN allowed : " + allowedMaxSan;
                        return Ok(response);
                    }

                    newSan++;
                }

                foreach (string san in addDomainList)
                {
                    PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.AddUniqueKey(
                        san.Trim().ToLower(),
                        PF_RequestObject.ProductDetail.IsWildcardMultiDomain
                            ? ConstantUtil.Comodo_DCVMethod_CnameCsrHash
                            : ConstantUtil.Comodo_DCVMethod_HTTPCsrHash);
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old ComodoController.DeleteAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/Comodo/DeleteAdditionalDomain
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
                EnsureComodoOrder(PF_RequestObject);
                response.configurationToken = resolved.token;

                if (string.IsNullOrEmpty(request?.sanDomainName))
                {
                    response.IsSuccess = false;
                    response.Msg = "Domain Name not available.";
                    return Ok(response);
                }

                string sanDomainName = request.sanDomainName;
                if (PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.ContainsKey(sanDomainName.ToLower()))
                    PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.Remove(sanDomainName);

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old ComodoController.UpdateMethodForAllAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/Comodo/UpdateMethodForAllAdditionalDomain
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
                EnsureComodoOrder(PF_RequestObject);
                response.configurationToken = resolved.token;

                if (string.IsNullOrEmpty(request?.approvalMethod))
                {
                    response.IsSuccess = false;
                    response.Msg = "Approval method not selected.";
                    return Ok(response);
                }

                foreach (string san in PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.Keys.ToList())
                    PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail[san] = request.approvalMethod;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old ComodoController.UpdateMethodForAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/Comodo/UpdateMethodForAdditionalDomain
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
                EnsureComodoOrder(PF_RequestObject);
                response.configurationToken = resolved.token;

                if (string.IsNullOrEmpty(request?.approvalMethodOrEmail))
                {
                    response.IsSuccess = false;
                    response.Msg = "Approval method not selected.";
                    return Ok(response);
                }

                string key = (request.sanDomainName ?? string.Empty).Trim();
                PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail[key] = request.approvalMethodOrEmail;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old ComodoController.AddWildcardSAN.
        /// Route: POST /api/SSLConfiguration/Comodo/AddWildcardSAN
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
                EnsureComodoOrder(PF_RequestObject);
                response.configurationToken = resolved.token;

                if (PF_RequestObject.ProductDetail == null || !PF_RequestObject.ProductDetail.IsFlex)
                {
                    response.IsSuccess = false;
                    response.Msg = "Product is not flex.";
                    return Ok(response);
                }

                string domainName = PF_RequestObject.CSRDetailRow?.DomainName ?? string.Empty;
                int usedWildcardSan = BLGeneral.GetTotalAdditionalSANForComodo(
                    domainName, PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail);
                response.usedWildcardSan = usedWildcardSan;
                int storeOrderId = PF_RequestObject.StoreOrderDetail?.StoreOrderId ?? 0;
                int productId = PF_RequestObject.StoreOrderDetail?.ProductId
                    ?? PF_RequestObject.ProductDetail.ProductId;
                int allowedMaxWildcardSan = BLGeneral.GetWildcardSANCount(storeOrderId, productId);
                response.allowedMaxWildcardSan = allowedMaxWildcardSan;
                if (usedWildcardSan >= allowedMaxWildcardSan)
                {
                    response.IsSuccess = false;
                    response.Msg = "Maximum Wildcard SAN allowed : " + allowedMaxWildcardSan;
                    return Ok(response);
                }

                if (string.IsNullOrEmpty(request?.additionalWildcardDomainNames))
                {
                    response.IsSuccess = false;
                    response.Msg = "Enter san.";
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
                        return Ok(response);
                    }

                    if (usedWildcardSan + newWildcardSan >= allowedMaxWildcardSan)
                    {
                        response.IsSuccess = false;
                        response.Msg = "Maximum Wildcard SAN allowed : " + allowedMaxWildcardSan;
                        return Ok(response);
                    }

                    newWildcardSan++;
                }

                foreach (string san in addDomainList)
                {
                    if (san.StartsWith("*."))
                    {
                        if (PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail.Count >= allowedMaxWildcardSan)
                        {
                            response.IsSuccess = false;
                            response.Msg = "Maximum Wildcard SAN allowed : " + allowedMaxWildcardSan;
                            return Ok(response);
                        }

                        PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail.AddUniqueKey(
                            san.Trim().ToLower(), ConstantUtil.Comodo_DCVMethod_CnameCsrHash);
                    }
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old ComodoController.DeleteWildcardSAN.
        /// Route: POST /api/SSLConfiguration/Comodo/DeleteWildcardSAN
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
                EnsureComodoOrder(PF_RequestObject);
                response.configurationToken = resolved.token;

                if (string.IsNullOrEmpty(request?.sanDomainName))
                {
                    response.IsSuccess = false;
                    response.Msg = "Domain Name not available.";
                    return Ok(response);
                }

                string sanDomainName = request.sanDomainName;
                if (PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail.ContainsKey(sanDomainName.ToLower()))
                    PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail.Remove(sanDomainName);

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old ComodoController.UpdateMethodForAllAdditionalDomain_WildcardSAN.
        /// Route: POST /api/SSLConfiguration/Comodo/UpdateMethodForAllAdditionalDomain_WildcardSAN
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
                EnsureComodoOrder(PF_RequestObject);
                response.configurationToken = resolved.token;

                if (string.IsNullOrEmpty(request?.approvalMethod))
                {
                    response.IsSuccess = false;
                    response.Msg = "Approval method not selected.";
                    return Ok(response);
                }

                foreach (string san in PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail.Keys.ToList())
                    PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail[san] = request.approvalMethod;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old ComodoController.UpdateMethodForAdditionalDomain_WildcardSAN.
        /// Route: POST /api/SSLConfiguration/Comodo/UpdateMethodForAdditionalDomain_WildcardSAN
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
                EnsureComodoOrder(PF_RequestObject);
                response.configurationToken = resolved.token;

                if (string.IsNullOrEmpty(request?.approvalMethodOrEmail))
                {
                    response.IsSuccess = false;
                    response.Msg = "Approval method not selected.";
                    return Ok(response);
                }

                string key = (request.sanDomainName ?? string.Empty).Trim();
                PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail[key] = request.approvalMethodOrEmail;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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

        #region Phase 3 — Contact / Org / Jurisdiction / Verification

        /// <summary>
        /// Same as old ComodoController.ContactInfo GET.
        /// Route: GET /api/SSLConfiguration/Comodo/ContactInfo?configurationToken=
        /// </summary>
        [HttpGet("ContactInfo")]
        public IActionResult ContactInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new ComodoContactInfoGetResponse();
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
                EnsureComodoOrder(pf);

                var objModel = new ComodoOrganizationInformationDto();
                if (!string.IsNullOrEmpty(pf.ComodoOrderRequest.FirstName))
                {
                    objModel.FirstName = pf.ComodoOrderRequest.FirstName;
                    objModel.LastName = pf.ComodoOrderRequest.LastName;
                    objModel.Address1 = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgAddress1;
                    objModel.Address2 = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgAddress2;
                    objModel.City = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgCity;
                    objModel.CountryName = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgCountry;
                    objModel.PostalCode = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgPostalCode;
                    objModel.State = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgState;
                    objModel.Email = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgEmail;
                    objModel.PhoneNo = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgPhone;
                    objModel.Fax = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgFax;
                }

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.contact = objModel;
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();
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
        /// Same as old ComodoController.ContactInfo POST.
        /// Route: POST /api/SSLConfiguration/Comodo/ContactInfo
        /// </summary>
        [HttpPost("ContactInfo")]
        public IActionResult ContactInfoPost([FromBody] ComodoOrganizationInformationDto? objModel)
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
                EnsureComodoOrder(PF_RequestObject);

                PF_RequestObject.ComodoOrderRequest.FirstName = string.IsNullOrWhiteSpace(objModel?.FirstName) ? "-" : objModel.FirstName;
                PF_RequestObject.ComodoOrderRequest.LastName = string.IsNullOrWhiteSpace(objModel?.LastName) ? "-" : objModel.LastName;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgAddress1 = string.IsNullOrWhiteSpace(objModel?.Address1) ? "-" : objModel.Address1;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgAddress2 = string.IsNullOrWhiteSpace(objModel?.Address2) ? "-" : objModel.Address2;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgCity = string.IsNullOrWhiteSpace(objModel?.City) ? "-" : objModel.City;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgCountry = string.IsNullOrWhiteSpace(objModel?.CountryName) ? "" : objModel.CountryName;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgPostalCode = string.IsNullOrWhiteSpace(objModel?.PostalCode) ? "-" : objModel.PostalCode;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgState = string.IsNullOrWhiteSpace(objModel?.State) ? "-" : objModel.State;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgEmail = objModel?.Email;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgPhone = string.IsNullOrWhiteSpace(objModel?.PhoneNo) ? "-" : objModel.PhoneNo;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgFax = string.IsNullOrWhiteSpace(objModel?.Fax) ? "-" : objModel.Fax;

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
        /// Same as old ComodoController.OrganizationInfo GET.
        /// Route: GET /api/SSLConfiguration/Comodo/OrganizationInfo?configurationToken=
        /// </summary>
        [HttpGet("OrganizationInfo")]
        public IActionResult OrganizationInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new ComodoOrganizationInfoGetResponse();
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
                EnsureComodoOrder(pf);

                var objModel = new ComodoOrganizationInformationDto();
                if (!string.IsNullOrEmpty(pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgName))
                {
                    objModel.OrganizationName = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgName;
                    objModel.Duns = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgDuns;
                    objModel.CompanyRegisterNumber = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgCompanyRegNumber;
                    objModel.Email = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgEmail;
                    objModel.Address1 = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgAddress1;
                    objModel.Address2 = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgAddress2;
                    objModel.City = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgCity;
                    objModel.State = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgState;
                    objModel.CountryCode = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgCountry;
                    objModel.CountryName = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgCountry;
                    objModel.PostalCode = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgPostalCode;
                    objModel.PhoneNo = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgPhone;
                    objModel.Fax = pf.ComodoOrderRequest.ComodoOrderDetailRow.OrgFax;
                }

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.organization = objModel;
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();
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
        /// Same as old ComodoController.OrganizationInfo POST.
        /// Route: POST /api/SSLConfiguration/Comodo/OrganizationInfo
        /// </summary>
        [HttpPost("OrganizationInfo")]
        public IActionResult OrganizationInfoPost([FromBody] ComodoOrganizationInformationDto? objModel)
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
                EnsureComodoOrder(PF_RequestObject);

                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgName = objModel?.OrganizationName;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgDuns = objModel?.Duns;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgCompanyRegNumber = objModel?.CompanyRegisterNumber;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgEmail = objModel?.Email;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgAddress1 = objModel?.Address1;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgAddress2 = objModel?.Address2;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgCity = objModel?.City;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgState = objModel?.State;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgCountry = objModel?.CountryName;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgPostalCode = objModel?.PostalCode;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgPhone = objModel?.PhoneNo;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgFax = objModel?.Fax;

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
        /// Same as old ComodoController.JurisdictionInfo GET.
        /// Route: GET /api/SSLConfiguration/Comodo/JurisdictionInfo?configurationToken=
        /// </summary>
        [HttpGet("JurisdictionInfo")]
        public IActionResult JurisdictionInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new JurisdictionInfoGetResponse();
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
                EnsureComodoOrder(pf);

                var objModel = new JurisdictionInfoDto();
                if (!string.IsNullOrEmpty(pf.ComodoOrderRequest.ComodoOrderDetailRow.JurictionCountry))
                {
                    objModel.JurictionCity = pf.ComodoOrderRequest.ComodoOrderDetailRow.JurictionCity;
                    objModel.jurictionState = pf.ComodoOrderRequest.ComodoOrderDetailRow.JurictionState;
                    objModel.JurictionCountryName = pf.ComodoOrderRequest.ComodoOrderDetailRow.JurictionCountry;
                    if (pf.ComodoOrderRequest.ComodoOrderDetailRow.DateOfIncorporation.HasValue)
                        objModel.DateOfIncorporation = pf.ComodoOrderRequest.ComodoOrderDetailRow.DateOfIncorporation.Value;
                    objModel.DoingBusinessAs = pf.ComodoOrderRequest.ComodoOrderDetailRow.DoingBusinessAs;
                }

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.jurisdiction = objModel;
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();
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
        /// Same as old ComodoController.JurisdictionInfo POST.
        /// Route: POST /api/SSLConfiguration/Comodo/JurisdictionInfo
        /// </summary>
        [HttpPost("JurisdictionInfo")]
        public IActionResult JurisdictionInfoPost([FromBody] JurisdictionInfoDto? objModel)
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
                EnsureComodoOrder(PF_RequestObject);

                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.JurictionCity = objModel?.JurictionCity;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.JurictionState = objModel?.jurictionState;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.JurictionCountry = objModel?.JurictionCountryName;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.DateOfIncorporation = objModel?.DateOfIncorporation;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.DoingBusinessAs = objModel?.DoingBusinessAs;

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
        /// Same as old ComodoController.VerificationInfo GET (EV).
        /// Route: GET /api/SSLConfiguration/Comodo/VerificationInfo?configurationToken=
        /// </summary>
        [HttpGet("VerificationInfo")]
        public IActionResult VerificationInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new VerificationInfoGetResponse();
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
                EnsureComodoOrder(pf);

                var objModel = new ComodoEVContactInfoDto
                {
                    InCorporationAgency = pf.ComodoOrderRequest.ComodoOrderDetailRow.InCorporationAgency,
                    InCorporationPhoneNumber = pf.ComodoOrderRequest.ComodoOrderDetailRow.InCorporationPhoneNo,
                    CertificateRequestorInfo = ToCommonContactDto(pf.ComodoOrderRequest.CertificateRequestorInfo),
                    CertificateApproverInfo = ToCommonContactDto(pf.ComodoOrderRequest.CertificateApproverInfo),
                    ContractSignerInfo = ToCommonContactDto(pf.ComodoOrderRequest.ContractSignerInfo)
                };

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.verification = objModel;
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();
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
        /// Same as old ComodoController.VerificationInfo POST (EV).
        /// Route: POST /api/SSLConfiguration/Comodo/VerificationInfo
        /// </summary>
        [HttpPost("VerificationInfo")]
        public IActionResult VerificationInfoPost([FromBody] ComodoEVContactInfoDto? objModel)
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
                EnsureComodoOrder(PF_RequestObject);

                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.InCorporationAgency = objModel?.InCorporationAgency;
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.InCorporationPhoneNo = objModel?.InCorporationPhoneNumber;

                ApplyCommonContactDto(PF_RequestObject.ComodoOrderRequest.CertificateRequestorInfo, objModel?.CertificateRequestorInfo);
                ApplyCommonContactDto(PF_RequestObject.ComodoOrderRequest.CertificateApproverInfo, objModel?.CertificateApproverInfo);
                ApplyCommonContactDto(PF_RequestObject.ComodoOrderRequest.ContractSignerInfo, objModel?.ContractSignerInfo);

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

        #region Phase 4 — Summary / PlaceOrder

        /// <summary>
        /// Same as old ComodoController.Summary GET (was PartialView) — returns draft summary JSON.
        /// Route: GET /api/SSLConfiguration/Comodo/Summary?configurationToken=
        /// </summary>
        [HttpGet("Summary")]
        public IActionResult SummaryGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new ComodoSummaryGetResponse();
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
                EnsureComodoOrder(pf);
                var detail = pf.ComodoOrderRequest.ComodoOrderDetailRow;

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.domainName = pf.CSRDetailRow?.DomainName;
                response.primaryDomain = pf.ComodoOrderRequest.PrimaryDomain ?? pf.CSRDetailRow?.DomainName;
                response.productName = pf.ProductDetail?.ProductName ?? pf.StoreOrderDetail?.ProductName;
                response.approvalMethod = pf.ComodoOrderRequest.ApprovalMethod;
                response.approverEmail = detail.ApprovalEmail;
                response.authenticationType = pf.ProductDetail?.AuthenticationType;
                response.firstName = pf.ComodoOrderRequest.FirstName;
                response.lastName = pf.ComodoOrderRequest.LastName;
                response.sanApprovalEmailList = pf.ComodoOrderRequest.SAN_ApprovalEmail;
                response.wildcardSanApprovalEmailList = pf.ComodoOrderRequest.WildcardSAN_ApprovalEmail;

                if (!string.IsNullOrEmpty(detail.OrgName) || !string.IsNullOrEmpty(detail.OrgEmail))
                {
                    response.organization = new ComodoOrganizationInformationDto
                    {
                        OrganizationName = detail.OrgName,
                        Duns = detail.OrgDuns,
                        CompanyRegisterNumber = detail.OrgCompanyRegNumber,
                        Email = detail.OrgEmail,
                        Address1 = detail.OrgAddress1,
                        Address2 = detail.OrgAddress2,
                        City = detail.OrgCity,
                        State = detail.OrgState,
                        CountryName = detail.OrgCountry,
                        CountryCode = detail.OrgCountry,
                        PostalCode = detail.OrgPostalCode,
                        PhoneNo = detail.OrgPhone,
                        Fax = detail.OrgFax,
                        FirstName = pf.ComodoOrderRequest.FirstName,
                        LastName = pf.ComodoOrderRequest.LastName
                    };
                }

                if (!string.IsNullOrEmpty(detail.JurictionCountry))
                {
                    response.jurisdiction = new JurisdictionInfoDto
                    {
                        JurictionCity = detail.JurictionCity,
                        jurictionState = detail.JurictionState,
                        JurictionCountryName = detail.JurictionCountry,
                        DateOfIncorporation = detail.DateOfIncorporation,
                        DoingBusinessAs = detail.DoingBusinessAs
                    };
                }

                if (pf.ProductDetail?.AuthenticationType == ConstantUtil.AuthenticationType_EV
                    || BLGeneral.IsEVProduct_StoreOrder(pf.StoreOrderDetail?.StoreOrderId ?? 0))
                {
                    response.verification = new ComodoEVContactInfoDto
                    {
                        InCorporationAgency = detail.InCorporationAgency,
                        InCorporationPhoneNumber = detail.InCorporationPhoneNo,
                        CertificateRequestorInfo = ToCommonContactDto(pf.ComodoOrderRequest.CertificateRequestorInfo),
                        CertificateApproverInfo = ToCommonContactDto(pf.ComodoOrderRequest.CertificateApproverInfo),
                        ContractSignerInfo = ToCommonContactDto(pf.ComodoOrderRequest.ContractSignerInfo)
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
        /// Same as old ComodoController.Summary POST — PlaceOrder via GetComodoProductObject + QuickComodoOrder.
        /// Route: POST /api/SSLConfiguration/Comodo/Summary
        /// </summary>
        [HttpPost("Summary")]
        public IActionResult SummaryPost([FromBody] ComodoSummaryPostRequest? request)
        {
            var response = new ComodoSummaryPostResponse();
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
                EnsureComodoOrder(PF_RequestObject);

                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.WebServer = "OTHER";
                PF_RequestObject.ComodoOrderRequest.WebServerCode = -1;
                _draftStore.Update(resolved.token, PF_RequestObject);

                PF_Response objPF_Response = ComodoPlaceOrder.PlaceOrder(PF_RequestObject);

                // Old: MailUtil on ErrorCode 100 (EV contact warning) — mail deferred in Core; surface ErrorCode                
                if (objPF_Response.ErrorCode == 100)
                    MailUtil.SendEmail("billing@ssl2buy.com", "billing@ssl2buy.com", string.Empty, string.Empty, "Comodo EV Contact details fails to submit for Comodo Order# " + objPF_Response.VendorID, "Comodo EV Contact details fails to submit for Comodo Order# " + objPF_Response.VendorID + " and ErrorMessage: " + objPF_Response.ErrorMessage);

                if (objPF_Response.ErrorCode == 0 )
                {
                    response.IsSuccess = true;
                    response.Msg = "Your order number is : " + objPF_Response.VendorID;
                    response.returnUrl = "/ManageOrder/comodo/managedcv";
                    response.VendorID = objPF_Response.VendorID;
                    response.configurationToken = resolved.token;
                    _draftStore.Remove(resolved.token);
                    return Ok(response);
                }

                response.IsSuccess = false;
                response.Msg = objPF_Response.ErrorMessage;
                response.returnUrl = string.Empty;
                response.configurationToken = resolved.token;
                return Ok(response);
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

        #endregion

        #region Phase 5 — CodeSign

        /// <summary>
        /// Same as old ComodoController.CodeSign — Entry + require CodeSignProvisioningMethod.
        /// Route: GET /api/SSLConfiguration/Comodo/CodeSign?pin=&amp;configurationToken=
        /// </summary>
        [HttpGet("CodeSign")]
        public IActionResult CodeSign([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry("CodeSign", pin, configurationToken);
            if (!response.IsSuccess || string.IsNullOrEmpty(response.configurationToken))
                return Ok(response);

            var resolved = _authenticationService.ResolveDraft(response.configurationToken, null);
            if (!resolved.ok || resolved.request == null)
            {
                response.IsSuccess = false;
                response.Msg = resolved.errorMessage ?? "Session expired.";
                return Ok(response);
            }

            if (string.IsNullOrEmpty(resolved.request.StoreOrderDetail?.CodeSignProvisioningMethod))
            {
                response.IsSuccess = false;
                response.Msg = "CodeSign provisioning method is required.";
            }

            return Ok(response);
        }

        /// <summary>
        /// Same as old ComodoController.CSRInfo_CodeSign GET.
        /// EV non-HSM → nextAction ContactInfo_CodeSign (old RedirectToAction).
        /// </summary>
        [HttpGet("CSRInfo_CodeSign")]
        public IActionResult CSRInfo_CodeSign_Get([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new ComodoCodeSignCsrInfoGetResponse();
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
                EnsureComodoOrder(pf);

                string method = (pf.StoreOrderDetail?.CodeSignProvisioningMethod ?? string.Empty).ToUpper();
                if (pf.ProductDetail?.AuthenticationType == ConstantUtil.AuthenticationType_EV
                    && !method.Contains("HSM"))
                {
                    response.IsSuccess = true;
                    response.configurationToken = resolved.token;
                    response.nextAction = "ContactInfo_CodeSign";
                    response.CodeSignProvisioningMethod = pf.StoreOrderDetail?.CodeSignProvisioningMethod;
                    response.authenticationType = pf.ProductDetail?.AuthenticationType;
                    return Ok(response);
                }

                var cs = pf.ComodoOrderRequest.ComodoCodeSignOrderInfo;
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.CodeSignProvisioningMethod = pf.StoreOrderDetail?.CodeSignProvisioningMethod;
                response.authenticationType = pf.ProductDetail?.AuthenticationType;
                response.ValidationTypeList = GetCodeSignValidationTypeList().Select(ToSelectListItemDto).ToList();
                response.HSMTypeList = BLGeneral.GetCodeSingHSMList().Select(ToSelectListItemDto).ToList();
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();
                response.csr = new ComodoCodeSignCsrInfoDto
                {
                    CSR = pf.CSR,
                    ValidationTypeId = pf.ComodoOrderRequest.ValidationTypeId,
                    DomainName = pf.CSRDetailRow?.DomainName,
                    Organisation = pf.CSRDetailRow?.Organisation,
                    OrganisationUnit = pf.CSRDetailRow?.OrganisationUnit,
                    Locality = pf.CSRDetailRow?.Locality,
                    State = pf.CSRDetailRow?.State,
                    Country = pf.CSRDetailRow?.Country,
                    KeyAttestation = cs.CodeSignKeyAttestation,
                    HSMType = cs.CodeSignHSMType
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
        /// Same as old ComodoController.CSRInfo_CodeSign POST — HSM path parses CSR via ComodoParseCSR (PositiveSSL product code).
        /// </summary>
        [HttpPost("CSRInfo_CodeSign")]
        public IActionResult CSRInfo_CodeSign_Post([FromBody] ComodoCodeSignCsrInfoDto? objModel)
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
                EnsureComodoOrder(PF_RequestObject);
                response.configurationToken = resolved.token;

                if (objModel == null)
                {
                    PF_RequestObject.CSR = string.Empty;
                    PF_RequestObject.CSRDetailRow = new CSRDetail();
                    PF_RequestObject.ComodoOrderRequest = new PF_ComodoOrder();
                    _draftStore.Update(resolved.token, PF_RequestObject);
                    response.IsSuccess = false;
                    response.Msg = "Please mandatory details.";
                    return Ok(response);
                }

                if (PF_RequestObject.ProductDetail?.AuthenticationType == ConstantUtil.AuthenticationType_OV
                    && string.IsNullOrEmpty(objModel.ValidationTypeId))
                {
                    response.IsSuccess = false;
                    response.Msg = "Please select validation type.";
                    return Ok(response);
                }

                string method = (PF_RequestObject.StoreOrderDetail?.CodeSignProvisioningMethod ?? string.Empty).ToUpper();
                if (method.Contains("HSM"))
                {
                    if (string.IsNullOrEmpty(objModel.CSR))
                    {
                        PF_RequestObject.CSR = string.Empty;
                        PF_RequestObject.CSRDetailRow = new CSRDetail();
                        PF_RequestObject.ComodoOrderRequest = new PF_ComodoOrder();
                        _draftStore.Update(resolved.token, PF_RequestObject);
                        response.IsSuccess = false;
                        response.Msg = "Please enter CSR Details.";
                        return Ok(response);
                    }

                    if (string.IsNullOrEmpty(objModel.HSMType))
                    {
                        response.IsSuccess = false;
                        response.Msg = "Please select HSM type.";
                        return Ok(response);
                    }

                    if (string.IsNullOrEmpty(objModel.KeyAttestation))
                    {
                        response.IsSuccess = false;
                        response.Msg = "Please enter Key attestation.";
                        return Ok(response);
                    }

                    VerisignGateway.ProductCode pcode = BLGeneral.GetProductCodeByProductName(
                        (int)VerisignGateway.ProductCode.ComodoPositiveSSL);
                    var comodoCredential = PF_RequestObject.CACredentialDetails?.GetComodoCACredential();
                    ValidateAndParseCSRResponse objRes = VerisignAPIHelper.ComodoParseCSR(
                        pcode,
                        objModel.CSR.Trim(),
                        comodoCredential);
                    response.objCSRResJson = JsonConvert.SerializeObject(objRes);

                    if (objRes.error == null || objRes.error.ErrorCode == 0)
                    {
                        if (!string.IsNullOrEmpty(objRes.DomainName) && objRes.DomainName.ToLower().StartsWith("*."))
                        {
                            response.IsSuccess = false;
                            response.Msg = "THE COMMON NAME (DOMAIN NAME) MAY NOT CONTAIN A * IN CSR";
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
                        response.CSRDetailJson = JsonConvert.SerializeObject(PF_RequestObject.CSRDetailRow);
                        PF_RequestObject.CSR = objModel.CSR;
                        PF_RequestObject.ComodoOrderRequest.ValidationTypeId = objModel.ValidationTypeId;
                        PF_RequestObject.ComodoOrderRequest.ComodoCodeSignOrderInfo.CodeSignKeyAttestation = objModel.KeyAttestation;
                        PF_RequestObject.ComodoOrderRequest.ComodoCodeSignOrderInfo.CodeSignHSMType = objModel.HSMType;
                    }
                    else
                    {
                        response.IsSuccess = false;
                        response.Msg = objRes.error?.ErrorMessage;
                        return Ok(response);
                    }
                }
                else if (PF_RequestObject.ProductDetail?.AuthenticationType == ConstantUtil.AuthenticationType_OV)
                {
                    PF_RequestObject.ComodoOrderRequest.ValidationTypeId = objModel.ValidationTypeId;
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
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
        /// Same as old ComodoController.GenerateKeyAttestation — LUNA / YUBIKEY / MARVELL_GOOGLE file → base64.
        /// Route: POST /api/SSLConfiguration/Comodo/GenerateKeyAttestation (multipart/form-data)
        /// </summary>
        [HttpPost("GenerateKeyAttestation")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> GenerateKeyAttestation([FromForm] GenerateKeyAttestationForm model)
        {
            var response = new GenerateKeyAttestationResponse { IsSuccess = true, keyAttestation = string.Empty };
            try
            {
                model ??= new GenerateKeyAttestationForm();
                if (string.IsNullOrEmpty(model.HSMType))
                    return Ok(response);

                string keyAttestation = string.Empty;
                string hsm = model.HSMType.ToUpper();

                if (hsm == "LUNA")
                {
                    if (model.AttestationP7B == null)
                        return Ok(response);

                    string strFileData = await ReadFormFileTextAsync(model.AttestationP7B);
                    if (!string.IsNullOrEmpty(strFileData))
                        keyAttestation = Convert.ToBase64String(Encoding.ASCII.GetBytes(strFileData));
                }
                else if (hsm == "YUBIKEY")
                {
                    if (model.AttestationCrt == null || model.IntermediateCrt == null)
                        return Ok(response);

                    var lines = new List<string>();
                    lines.AddRange(SplitLines(await ReadFormFileTextAsync(model.AttestationCrt)));
                    lines.AddRange(SplitLines(await ReadFormFileTextAsync(model.IntermediateCrt)));
                    string strFileData = string.Join(Environment.NewLine, lines);
                    if (!string.IsNullOrEmpty(strFileData))
                        keyAttestation = Convert.ToBase64String(Encoding.ASCII.GetBytes(strFileData));
                }
                else if (hsm == "MARVELL_GOOGLE")
                {
                    if (model.AttestationBin == null)
                        return Ok(response);

                    using var memoryStream = new MemoryStream();
                    await model.AttestationBin.CopyToAsync(memoryStream);
                    keyAttestation = Convert.ToBase64String(memoryStream.ToArray());
                }

                response.keyAttestation = keyAttestation;
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

        /// <summary>Same as old ComodoController.ContactInfo_CodeSign GET.</summary>
        [HttpGet("ContactInfo_CodeSign")]
        public IActionResult ContactInfo_CodeSign_Get([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new ComodoCodeSignContactInfoGetResponse();
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
                EnsureComodoOrder(pf);

                if (pf.ProductDetail?.AuthenticationType == ConstantUtil.AuthenticationType_EV)
                    pf.ComodoOrderRequest.ValidationTypeId = ConstantUtil.Comodo_CodeSignType_EV;

                _draftStore.Update(resolved.token, pf);

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.CodeSignProvisioningMethod = pf.StoreOrderDetail?.CodeSignProvisioningMethod;
                response.ValidationTypeId = pf.ComodoOrderRequest.ValidationTypeId;
                response.authenticationType = pf.ProductDetail?.AuthenticationType;
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();
                response.contact = ToCodeSignContactDto(pf.ComodoOrderRequest.ComodoCodeSignOrderInfo);

                string method = (pf.StoreOrderDetail?.CodeSignProvisioningMethod ?? string.Empty).ToUpper();
                if (method.Contains("HSM") && response.contact != null)
                {
                    response.contact.ShippingForename = null;
                    response.contact.ShippingSurname = null;
                    response.contact.ShippingStreetAddress1 = null;
                    response.contact.ShippingStreetAddress2 = null;
                    response.contact.ShippingStreetAddress3 = null;
                    response.contact.ShippingCity = null;
                    response.contact.ShippingState = null;
                    response.contact.ShippingCountryCode = null;
                    response.contact.ShippingCountryName = null;
                    response.contact.ShippingPostalCode = null;
                    response.contact.ShippingEmailAddress = null;
                    response.contact.ShippingPhoneNo = null;
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

        /// <summary>Same as old ComodoController.ContactInfo_CodeSign POST.</summary>
        [HttpPost("ContactInfo_CodeSign")]
        public IActionResult ContactInfo_CodeSign_Post([FromBody] ComodoCodeSignContactInfoDto? objModel)
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
                EnsureComodoOrder(PF_RequestObject);
                var cs = PF_RequestObject.ComodoOrderRequest.ComodoCodeSignOrderInfo;

                cs.OrganizationName = objModel.OrganizationName;
                cs.OrganizationUnit = objModel.OrganizationUnit;
                cs.OrganizationAddress1 = objModel.OrganizationAddress1;
                cs.OrganizationAddress2 = objModel.OrganizationAddress2;
                cs.OrganizationAddress3 = objModel.OrganizationAddress3;
                cs.City = objModel.City;
                cs.State = objModel.State;
                cs.CountryName = objModel.CountryName;
                cs.PostalCode = objModel.PostalCode;
                cs.PhoneNo = objModel.PhoneNo;
                cs.JurictionCity = objModel.JurictionCity;
                cs.jurictionState = objModel.jurictionState;
                cs.JurictionCountryName = objModel.JurictionCountryName;
                cs.AdminTitle = objModel.AdminTitle;
                cs.AdminFirstName = objModel.AdminFirstName;
                cs.AdminLastName = objModel.AdminLastName;
                cs.AdminEmail = objModel.AdminEmail;
                cs.AdminUserName = string.Empty;
                cs.AdminPassword = string.Empty;
                cs.AdminContactEmail = objModel.AdminContactEmail;
                cs.CodeSignPublisherEmail = objModel.CodeSignPublisherEmail;

                string method = (PF_RequestObject.StoreOrderDetail?.CodeSignProvisioningMethod ?? string.Empty).ToUpper();
                if (!method.Contains("HSM"))
                {
                    cs.ShippingForename = objModel.ShippingForename;
                    cs.ShippingSurname = objModel.ShippingSurname;
                    cs.ShippingStreetAddress1 = objModel.ShippingStreetAddress1;
                    cs.ShippingStreetAddress2 = objModel.ShippingStreetAddress2;
                    cs.ShippingStreetAddress3 = objModel.ShippingStreetAddress3;
                    cs.ShippingCity = objModel.ShippingCity;
                    cs.ShippingState = objModel.ShippingState;
                    cs.ShippingCountryCode = objModel.ShippingCountryCode;
                    cs.ShippingPostalCode = objModel.ShippingPostalCode;
                    cs.ShippingEmailAddress = objModel.ShippingEmailAddress;
                    cs.ShippingPhoneNo = objModel.ShippingPhoneNo;

                    if (!string.IsNullOrWhiteSpace(objModel.ShippingCountryCode))
                    {
                        var country = BLGeneral.GetCountryList()
                            .FirstOrDefault(m => m.Value == objModel.ShippingCountryCode);
                        cs.ShippingCountryName = country?.Text;
                    }
                }

                if (method != "HSM")
                {
                    PF_RequestObject.CSRDetailRow = new CSRDetail
                    {
                        DomainName = objModel.OrganizationName,
                        Country = objModel.CountryName,
                        Locality = string.Empty,
                        Organisation = objModel.OrganizationName,
                        OrganisationUnit = objModel.OrganizationUnit,
                        State = objModel.State
                    };
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.objccsshippingJson = JsonConvert.SerializeObject(cs);
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

        /// <summary>Same as old ComodoController.Summary_CodeSign GET.</summary>
        [HttpGet("Summary_CodeSign")]
        public IActionResult Summary_CodeSign_Get([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new ComodoCodeSignSummaryGetResponse();
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
                EnsureComodoOrder(pf);

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.productName = pf.ProductDetail?.ProductName ?? pf.StoreOrderDetail?.ProductName;
                response.authenticationType = pf.ProductDetail?.AuthenticationType;
                response.CodeSignProvisioningMethod = pf.StoreOrderDetail?.CodeSignProvisioningMethod;
                response.ValidationTypeId = pf.ComodoOrderRequest.ValidationTypeId;
                response.CSR = pf.CSR;
                response.DomainName = pf.CSRDetailRow?.DomainName;
                response.contact = ToCodeSignContactDto(pf.ComodoOrderRequest.ComodoCodeSignOrderInfo);
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
        /// Same as old ComodoController.Summary_CodeSign POST — PlaceCodeSignOrder.
        /// Success returnUrl: /ManageOrder/comodo/codesignvalidation
        /// </summary>
        [HttpPost("Summary_CodeSign")]
        public IActionResult Summary_CodeSign_Post([FromBody] ComodoSummaryPostRequest? request)
        {
            var response = new ComodoSummaryPostResponse();
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
                EnsureComodoOrder(PF_RequestObject);

                PF_Response objPF_Response = ComodoPlaceOrder.PlaceCodeSignOrder(PF_RequestObject);
                response.ErrorCode = objPF_Response.ErrorCode;

                if (objPF_Response.ErrorCode == 0)
                {
                    response.IsSuccess = true;
                    response.Msg = "Your order number is : " + objPF_Response.VendorID;
                    response.returnUrl = "/ManageOrder/comodo/codesignvalidation";
                    response.VendorID = objPF_Response.VendorID;
                    response.configurationToken = resolved.token;
                    _draftStore.Remove(resolved.token);
                    return Ok(response);
                }

                response.IsSuccess = false;
                response.Msg = objPF_Response.ErrorMessage;
                response.returnUrl = string.Empty;
                response.configurationToken = resolved.token;
                return Ok(response);
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

        #endregion

        #region Phase 6 — PAC

        /// <summary>
        /// Same as old ComodoController.PAC — Entry for PAC products.
        /// Route: GET /api/SSLConfiguration/Comodo/PAC?pin=&amp;configurationToken=
        /// </summary>
        [HttpGet("PAC")]
        public IActionResult PAC([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry(ConstantUtil.AuthenticationType_PAC, pin, configurationToken);
            return Ok(response);
        }

        /// <summary>Same as old ComodoController.CSRInfo_PAC GET.</summary>
        [HttpGet("CSRInfo_PAC")]
        public IActionResult CSRInfo_PAC_Get([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new PacCsrInfoGetResponse();
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
                EnsureComodoOrder(pf);
                var pac = pf.ComodoOrderRequest.ComodoPACOrderInfo;

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.domainName = pf.CSRDetailRow?.DomainName;
                response.pac = new PacOrderInfoDto
                {
                    IsNewCSR = pf.ComodoOrderRequest.IsNewCSRPAC,
                    FirstName = pac.FirstName,
                    LastName = pac.LastName,
                    Title = pac.Title,
                    Email = pf.CSRDetailRow?.Email ?? pac.Email,
                    CSR = pf.CSR
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
        /// Same as old ComodoController.CSRInfo_PAC POST — optional generate CSR + ComodoParseCSR.
        /// Returns FileName for DownloadKey when IsNewCSR.
        /// </summary>
        [HttpPost("CSRInfo_PAC")]
        public IActionResult CSRInfo_PAC_Post([FromBody] PacOrderInfoDto? objModel)
        {
            var response = new PacCsrInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, objModel?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    response.FileName = string.Empty;
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureComodoOrder(PF_RequestObject);
                response.configurationToken = resolved.token;

                if (objModel == null)
                {
                    response.IsSuccess = false;
                    response.Msg = "Please enter mandatory fields.";
                    response.FileName = string.Empty;
                    return Ok(response);
                }

                string privatekeyFileName = string.Empty;
                PF_RequestObject.ComodoOrderRequest.IsNewCSRPAC = objModel.IsNewCSR;

                if (objModel.IsNewCSR)
                {
                    var generated = PacCsrGenerator.Generate(objModel.FirstName, objModel.LastName, objModel.Email);
                    objModel.CSR = generated.csrPem;
                    objModel.PrivateKey = generated.privateKeyPem;
                    privatekeyFileName = PacCsrGenerator.ExportPrivateKeyFile(objModel.Email, objModel.PrivateKey);

                    PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.FirstName = objModel.FirstName;
                    PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.LastName = objModel.LastName;
                    PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.Title = objModel.Title;
                    PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.Email = objModel.Email;
                }
                else
                {
                    objModel.FirstName = string.Empty;
                    objModel.LastName = string.Empty;
                    objModel.Title = string.Empty;
                    objModel.Email = string.Empty;
                }

                if (string.IsNullOrWhiteSpace(objModel.CSR))
                {
                    response.IsSuccess = false;
                    response.Msg = "Please enter CSR Details.";
                    response.FileName = string.Empty;
                    return Ok(response);
                }

                VerisignGateway.ProductCode pcode = BLGeneral.GetProductCodeByProductName(
                    (int)VerisignGateway.ProductCode.ComodoPositiveSSL);
                var comodoCredential = PF_RequestObject.CACredentialDetails?.GetComodoCACredential();
                ValidateAndParseCSRResponse objRes = VerisignAPIHelper.ComodoParseCSR(
                    pcode,
                    objModel.CSR.Trim(),
                    comodoCredential);
                response.objCSRResJson = JsonConvert.SerializeObject(objRes);
                if (objRes.error == null || objRes.error.ErrorCode == 0)
                {
                    PF_RequestObject.CSRDetailRow = new CSRDetail
                    {
                        DomainName = objRes.DomainName,
                        Country = objRes.Country,
                        Locality = objRes.Locality,
                        Organisation = objRes.Organisation,
                        OrganisationUnit = objRes.OrganisationUnit,
                        State = objRes.State,
                        Email = objRes.Email,
                        CSR = objModel.CSR
                    };
                    PF_RequestObject.CSR = objModel.CSR;
                    response.CSRDetailJson = JsonConvert.SerializeObject(PF_RequestObject.CSRDetailRow);
                    if (!string.IsNullOrEmpty(objRes.DomainName) && objRes.DomainName.Split(' ').Length > 1)
                    {
                        string[] domainName = objRes.DomainName.Split(' ');
                        PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.FirstName = domainName[0];
                        PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.LastName = domainName[1];
                    }
                    else
                    {
                        PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.FirstName = objModel.FirstName;
                        PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.LastName = objModel.LastName;
                    }

                    PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.Title = objModel.Title;
                    PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.Email = objRes.Email;
                    PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.CSR = objModel.CSR;
                    PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.PrivateKey = objModel.PrivateKey;
                    PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo.IsNewCSR = objModel.IsNewCSR;
                }
                else
                {
                    response.IsSuccess = false;
                    response.Msg = objRes.error?.ErrorMessage;
                    response.FileName = string.Empty;
                    return Ok(response);
                }

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.Msg = string.Empty;
                response.FileName = privatekeyFileName;
                response.CSR = objModel.CSR;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                response.FileName = string.Empty;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old ComodoController.DownloadKey — download then delete temp private key file.
        /// Route: GET /api/SSLConfiguration/Comodo/DownloadKey?fileName=
        /// </summary>
        [HttpGet("DownloadKey")]
        public IActionResult DownloadKey([FromQuery] string? fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName)
                    || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                    || fileName.Contains("..")
                    || fileName.Contains('/')
                    || fileName.Contains('\\'))
                {
                    return BadRequest(new { IsSuccess = false, Msg = "Invalid file name." });
                }

                string fullPath = PacCsrGenerator.GetTempFilePath(fileName);
                if (!System.IO.File.Exists(fullPath))
                    return NotFound(new { IsSuccess = false, Msg = "File not found." });

                byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
                try { System.IO.File.Delete(fullPath); } catch { }

                return File(bytes, "text/plain", fileName);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                return StatusCode(500, new { IsSuccess = false, Msg = ex.Message });
            }
        }

        /// <summary>Same as old ComodoController.ContactInfo_PAC GET.</summary>
        [HttpGet("ContactInfo_PAC")]
        public IActionResult ContactInfo_PAC_Get([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new PacContactInfoGetResponse();
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
                EnsureComodoOrder(pf);
                var pac = pf.ComodoOrderRequest.ComodoPACOrderInfo;

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.contact = new PacOrderInfoDto
                {
                    Title = pac.Title,
                    FirstName = pac.FirstName,
                    LastName = pac.LastName,
                    Email = pac.Email
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

        /// <summary>Same as old ComodoController.ContactInfo_PAC POST.</summary>
        [HttpPost("ContactInfo_PAC")]
        public IActionResult ContactInfo_PAC_Post([FromBody] PacOrderInfoDto? objModel)
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

                if (objModel == null)
                {
                    response.IsSuccess = false;
                    response.Msg = "Please enter mandatory fields.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = resolved.request;
                EnsureComodoOrder(PF_RequestObject);
                var pac = PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo;

                pac.FirstName = string.IsNullOrEmpty(pac.FirstName) ? objModel.FirstName : pac.FirstName;
                pac.LastName = string.IsNullOrEmpty(pac.LastName) ? objModel.LastName : pac.LastName;
                pac.Title = string.IsNullOrEmpty(pac.Title) ? objModel.Title : pac.Title;
                pac.Email = objModel.Email;

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

        /// <summary>Same as old ComodoController.OrganizationInfo_PAC GET.</summary>
        [HttpGet("OrganizationInfo_PAC")]
        public IActionResult OrganizationInfo_PAC_Get([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new PacOrganizationInfoGetResponse();
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
                EnsureComodoOrder(pf);
                var detail = pf.ComodoOrderRequest.ComodoOrderDetailRow;
                var pac = pf.ComodoOrderRequest.ComodoPACOrderInfo;

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();
                response.organization = new ComodoOrganizationInformationDto
                {
                    OrganizationName = detail.OrgName,
                    Duns = detail.OrgDuns,
                    CompanyRegisterNumber = detail.OrgCompanyRegNumber,
                    Email = pac.Email ?? detail.OrgEmail,
                    Address1 = detail.OrgAddress1,
                    Address2 = detail.OrgAddress2,
                    City = detail.OrgCity,
                    State = detail.OrgState,
                    CountryCode = detail.OrgCountry,
                    CountryName = detail.OrgCountry,
                    PostalCode = detail.OrgPostalCode,
                    PhoneNo = detail.OrgPhone,
                    Fax = detail.OrgFax,
                    FirstName = pac.FirstName,
                    LastName = pac.LastName,
                    Title = pac.Title
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

        /// <summary>Same as old ComodoController.OrganizationInfo_PAC POST.</summary>
        [HttpPost("OrganizationInfo_PAC")]
        public IActionResult OrganizationInfo_PAC_Post([FromBody] ComodoOrganizationInformationDto? objModel)
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
                EnsureComodoOrder(PF_RequestObject);
                var detail = PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow;
                var pac = PF_RequestObject.ComodoOrderRequest.ComodoPACOrderInfo;

                detail.OrgName = objModel.OrganizationName;
                detail.OrgDuns = objModel.Duns;
                detail.OrgCompanyRegNumber = objModel.CompanyRegisterNumber;
                detail.OrgEmail = objModel.Email;
                detail.OrgAddress1 = objModel.Address1;
                detail.OrgAddress2 = objModel.Address2;
                detail.OrgCity = objModel.City;
                detail.OrgState = objModel.State;
                detail.OrgCountry = objModel.CountryName;
                detail.OrgPostalCode = objModel.PostalCode;
                detail.OrgPhone = objModel.PhoneNo;
                detail.OrgFax = objModel.Fax;

                pac.FirstName = string.IsNullOrEmpty(pac.FirstName) ? objModel.FirstName : pac.FirstName;
                pac.LastName = string.IsNullOrEmpty(pac.LastName) ? objModel.LastName : pac.LastName;
                pac.Title = string.IsNullOrEmpty(pac.Title) ? objModel.Title : pac.Title;
                pac.Email = objModel.Email;

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

        /// <summary>Same as old ComodoController.Summary_PAC GET.</summary>
        [HttpGet("Summary_PAC")]
        public IActionResult Summary_PAC_Get([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new PacSummaryGetResponse();
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
                EnsureComodoOrder(pf);
                var detail = pf.ComodoOrderRequest.ComodoOrderDetailRow;
                var pac = pf.ComodoOrderRequest.ComodoPACOrderInfo;
                var csrRow = pf.CSRDetailRow;

                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.productName = pf.ProductDetail?.ProductName ?? pf.StoreOrderDetail?.ProductName;
                response.CSR = pf.CSR;
                response.contact = new PacOrderInfoDto
                {
                    Title = pac.Title,
                    FirstName = pac.FirstName,
                    LastName = pac.LastName,
                    Email = pac.Email
                };
                response.organization = new ComodoOrganizationInformationDto
                {
                    OrganizationName = detail.OrgName,
                    Address1 = detail.OrgAddress1,
                    Address2 = detail.OrgAddress2,
                    City = detail.OrgCity,
                    State = detail.OrgState,
                    CountryName = detail.OrgCountry,
                    PostalCode = detail.OrgPostalCode,
                    PhoneNo = detail.OrgPhone,
                    Fax = detail.OrgFax,
                    Email = detail.OrgEmail,
                    Duns = detail.OrgDuns,
                    CompanyRegisterNumber = detail.OrgCompanyRegNumber
                };
                if (csrRow != null)
                {
                    response.csrDetail = new PacCsrDetailDto
                    {
                        DomainName = csrRow.DomainName,
                        Country = csrRow.Country,
                        Locality = csrRow.Locality,
                        Organisation = csrRow.Organisation,
                        OrganisationUnit = csrRow.OrganisationUnit,
                        State = csrRow.State,
                        Email = csrRow.Email,
                        CSR = pf.CSR
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
        /// Same as old ComodoController.Summary_PAC POST — PlacePACOrder.
        /// Success returnUrl: /manageorder/comodo/OrderDetails
        /// </summary>
        [HttpPost("Summary_PAC")]
        public IActionResult Summary_PAC_Post([FromBody] ComodoSummaryPostRequest? request)
        {
            var response = new ComodoSummaryPostResponse();
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
                EnsureComodoOrder(PF_RequestObject);

                PF_Response objPF_Response = ComodoPlaceOrder.PlacePACOrder(PF_RequestObject);
                response.ErrorCode = objPF_Response.ErrorCode;

                if (objPF_Response.ErrorCode == 0)
                {
                    response.IsSuccess = true;
                    response.Msg = "Your order number is : " + objPF_Response.VendorID;
                    response.returnUrl = "/manageorder/comodo/OrderDetails";
                    response.VendorID = objPF_Response.VendorID;
                    response.configurationToken = resolved.token;
                    _draftStore.Remove(resolved.token);
                    return Ok(response);
                }

                response.IsSuccess = false;
                response.Msg = objPF_Response.ErrorMessage;
                response.returnUrl = string.Empty;
                response.configurationToken = resolved.token;
                return Ok(response);
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

        #endregion

        /// <summary>
        /// Shared Entry + ACME product routing (old DV/OV redirects to ACME controller).
        /// Same as old ComodoController: empty AcmeAccountID → new action; else existing action.
        /// </summary>
        private IActionResult EntryWithAcmeRouting(
            string authenticationType,
            string? pin,
            string? configurationToken,
            int acmeProductId,
            string acmeNewAction,
            string acmeExistingAction)
        {
            var response = _authenticationService.Entry(authenticationType, pin, configurationToken);
            if (response.IsSuccess && response.productId == acmeProductId)
            {
                response.nextController = "ACME";
                response.nextArea = "SSLConfiguration";

                var storeOrderId = response.storeOrderId ?? 0;
                var objdetails = BLAcme.GetAcemeAccountDetails(storeOrderId);
                response.nextAction = string.IsNullOrEmpty(objdetails.AcmeAccountID)
                    ? acmeNewAction
                    : acmeExistingAction;
            }

            return Ok(response);
        }

        private static void EnsureComodoOrder(PF_Request pf)
        {
            if (pf.ComodoOrderRequest == null)
                pf.ComodoOrderRequest = new PF_ComodoOrder();
            if (pf.ComodoOrderRequest.ComodoOrderDetailRow == null)
                pf.ComodoOrderRequest.ComodoOrderDetailRow = new ComodoOrderDetailDraft();
            if (pf.ComodoOrderRequest.SAN_ApprovalEmail == null)
                pf.ComodoOrderRequest.SAN_ApprovalEmail = new Dictionary<string, string>();
            if (pf.ComodoOrderRequest.WildcardSAN_ApprovalEmail == null)
                pf.ComodoOrderRequest.WildcardSAN_ApprovalEmail = new Dictionary<string, string>();
            if (pf.ComodoOrderRequest.CertificateRequestorInfo == null)
                pf.ComodoOrderRequest.CertificateRequestorInfo = new ComodoContactInfoDraft();
            if (pf.ComodoOrderRequest.CertificateApproverInfo == null)
                pf.ComodoOrderRequest.CertificateApproverInfo = new ComodoContactInfoDraft();
            if (pf.ComodoOrderRequest.ContractSignerInfo == null)
                pf.ComodoOrderRequest.ContractSignerInfo = new ComodoContactInfoDraft();
            if (pf.ComodoOrderRequest.ComodoCodeSignOrderInfo == null)
                pf.ComodoOrderRequest.ComodoCodeSignOrderInfo = new ComodoCodeSignOrderInfoDraft();
            if (pf.ComodoOrderRequest.ComodoPACOrderInfo == null)
                pf.ComodoOrderRequest.ComodoPACOrderInfo = new ComodoPACOrderInfoDraft();
        }

        private static List<SelectListItem> GetCodeSignValidationTypeList() =>
            new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select --" },
                new SelectListItem { Value = ConstantUtil.Comodo_CodeSignType_Individual, Text = "Individual Codesign" },
                new SelectListItem { Value = ConstantUtil.Comodo_CodeSignType_Organization, Text = "Organization Codesign" }
            };

        private static ComodoCodeSignContactInfoDto ToCodeSignContactDto(ComodoCodeSignOrderInfoDraft src) =>
            new ComodoCodeSignContactInfoDto
            {
                AdminTitle = src.AdminTitle,
                AdminFirstName = src.AdminFirstName,
                AdminLastName = src.AdminLastName,
                AdminEmail = src.AdminEmail,
                AdminUserName = src.AdminUserName,
                AdminPassword = src.AdminPassword,
                AdminContactEmail = src.AdminContactEmail,
                OrganizationName = src.OrganizationName,
                OrganizationUnit = src.OrganizationUnit,
                OrganizationAddress1 = src.OrganizationAddress1,
                OrganizationAddress2 = src.OrganizationAddress2,
                OrganizationAddress3 = src.OrganizationAddress3,
                City = src.City,
                State = src.State,
                PostalCode = src.PostalCode,
                PhoneNo = src.PhoneNo,
                CountryName = src.CountryName,
                CountryCode = src.CountryCode,
                JurictionCity = src.JurictionCity,
                jurictionState = src.jurictionState,
                JurictionCountryName = src.JurictionCountryName,
                JurictionCountryCode = src.JurictionCountryCode,
                CodeSignPublisherEmail = src.CodeSignPublisherEmail,
                CodeSignHSMType = src.CodeSignHSMType,
                CodeSignKeyAttestation = src.CodeSignKeyAttestation,
                ShippingForename = src.ShippingForename,
                ShippingSurname = src.ShippingSurname,
                ShippingStreetAddress1 = src.ShippingStreetAddress1,
                ShippingStreetAddress2 = src.ShippingStreetAddress2,
                ShippingStreetAddress3 = src.ShippingStreetAddress3,
                ShippingCity = src.ShippingCity,
                ShippingState = src.ShippingState,
                ShippingCountryCode = src.ShippingCountryCode,
                ShippingCountryName = src.ShippingCountryName,
                ShippingPostalCode = src.ShippingPostalCode,
                ShippingEmailAddress = src.ShippingEmailAddress,
                ShippingPhoneNo = src.ShippingPhoneNo
            };

        private static async Task<string> ReadFormFileTextAsync(IFormFile file)
        {
            using var reader = new StreamReader(file.OpenReadStream());
            return await reader.ReadToEndAsync();
        }

        private static IEnumerable<string> SplitLines(string text) =>
            (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        private static ComodoCommonContactInfoDto ToCommonContactDto(ComodoContactInfoDraft src) =>
            new ComodoCommonContactInfoDto
            {
                Title = src.Title,
                FirstName = src.FirstName,
                LastName = src.LastName,
                Email = src.Email,
                Phone = src.Phone,
                Address1 = src.Address1,
                Address2 = src.Address2,
                City = src.City,
                Country = src.Country,
                PostalCode = src.PostalCode,
                RelationShip = src.Relationship,
                State = src.State
            };

        private static void ApplyCommonContactDto(ComodoContactInfoDraft target, ComodoCommonContactInfoDto? src)
        {
            if (src == null)
                return;

            target.Title = src.Title;
            target.FirstName = src.FirstName;
            target.LastName = src.LastName;
            target.Email = src.Email;
            target.Phone = src.Phone;
            target.Address1 = src.Address1;
            target.Address2 = src.Address2;
            target.City = src.City;
            target.Country = src.Country;
            target.PostalCode = src.PostalCode;
            target.Relationship = src.RelationShip;
            target.State = src.State;
        }

        private static SelectListItemDto ToSelectListItemDto(SelectListItem i) =>
            new SelectListItemDto { Value = i.Value, Text = i.Text, Selected = i.Selected };

        [HttpGet("CountryListNew")]
        public IActionResult GetCountryList([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
        {
            var response = new CountryListResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.CountryList = BLGeneral.GetCountryList().Select(ToSelectListItemDto).ToList();
                
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
        [HttpGet("GetComodoApprovalEmailListNew")]
        public IActionResult GetComodoApprovalEmailList([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null, [FromQuery] string? domainName = null, [FromQuery] string? approvalEmail = null)
        {
            var response = new ComodoApprovalEmailListResponse();
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
                EnsureComodoOrder(pf);

                var approvalEmailList = BLGeneral.GetComodoApprovalEmailList(domainName ?? string.Empty, pf.CACredentialDetails?.GetComodoCACredential(), approvalEmail ?? string.Empty);

                response.IsSuccess = true;
                response.configurationToken = resolved.token;                
                response.approvalEmailList = approvalEmailList.Select(ToSelectListItemDto).ToList();
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
