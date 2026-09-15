using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSLConfiguration.Application;
using SSLConfiguration.Contracts.SSLConfiguration.Comodo;
using SSLConfiguration.Contracts.SSLConfiguration.Digicert;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.Persistence;
using VerisignGateway;
using JsonSerializer = System.Text.Json.JsonSerializer;
using ComodoContactInfoGetResponse = SSLConfiguration.Contracts.SSLConfiguration.Comodo.ContactInfoGetResponse;
using ComodoOrganizationInfoGetResponse = SSLConfiguration.Contracts.SSLConfiguration.Comodo.OrganizationInfoGetResponse;
using ComodoSummaryGetResponse = SSLConfiguration.Contracts.SSLConfiguration.Comodo.SummaryGetResponse;
using ComodoSummaryPostRequest = SSLConfiguration.Contracts.SSLConfiguration.Comodo.SummaryPostRequest;
using ComodoSummaryPostResponse = SSLConfiguration.Contracts.SSLConfiguration.Comodo.SummaryPostResponse;
namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration
{
    [Authorize]
    [ApiController]
    [Route("api/SSLConfiguration/[controller]")]
    public class ClickSSLController : ControllerBase
    {
        private readonly ConfigurationDraftStore _draftStore;
        private readonly IAuthenticationService _authenticationService;

        public ClickSSLController(IAuthenticationService authenticationService, ConfigurationDraftStore draftStore)
        {
            _authenticationService = authenticationService;
            _draftStore = draftStore;
        }
        #region DV / OV / EV
        /// <summary>
        /// Same as old ClickSSLController
        /// Route: GET /api/SSLConfiguration/ClickSSL/DV?pin=&amp;configurationToken=
        /// </summary>
        [HttpGet("DV")]
        public IActionResult DV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry("DV", pin, configurationToken);
            return Ok(response);
        }

        /// <summary>Same as old ClickSSLController.OV.</summary>
        [HttpGet("OV")]
        public IActionResult OV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry("OV", pin, configurationToken);
            return Ok(response);
        }

        /// <summary>Same as old ClickSSLController.EV.</summary>
        [HttpGet("EV")]
        public IActionResult EV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry("EV", pin, configurationToken);
            return Ok(response);
        }
        #endregion

        #region CSR Info
        /// <summary>
        /// Same as old ClickSSLController.CSRInfo GET — returns CSR draft fields (was PartialView).
        /// Route: GET /api/SSLConfiguration/ClickSSL/CSRInfo?configurationToken=
        /// </summary>
        [HttpGet("CSRInfo")]
        public ActionResult CSRInfoGet([FromQuery] string? configurationToken = null, [FromQuery] string? pin = null)
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
                    ApproverEmailListResponse objResponse = VerisignAPIHelper.GetComodoApproveremail(objRes.DomainName, comodoCredential);
                    if (objResponse.error != null && objResponse.error.ErrorCode < 0)
                    {
                        LogWriter.LogCARequestResponseObjectToDB(PF_RequestObject.StoreOrderDetail?.Pin ?? string.Empty, objResponse.CARequestObject, JsonSerializer.Serialize(objResponse.error), "GetComodoApproveremail");
                        response.IsSuccess = false;
                        // Prefer CA message (e.g. login/IP lock) over generic domain text.
                        response.Msg = !string.IsNullOrWhiteSpace(objResponse.error.ErrorMessage) ? objResponse.error.ErrorMessage : Resources.InvalidDomainNameInCSR;
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
                                    response.Msg = string.Format("Max SAN allowed : {0} (Normal SAN: {1}, Wildcard SAN: {2}).", allowedMaxSan + allowedMaxWildcardSan, allowedMaxSan, allowedMaxWildcardSan);
                                    return Ok(response);
                                }

                                if (wildcardSANList.Count > allowedMaxWildcardSan)
                                {
                                    response.IsSuccess = false;
                                    response.Msg = string.Format("Max SAN allowed : {0} (Normal SAN: {1}, Wildcard SAN: {2}).", allowedMaxSan + allowedMaxWildcardSan, allowedMaxSan, allowedMaxWildcardSan);
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
                                    response.Msg = "Maximum allowed " + (PF_RequestObject.ProductDetail.IsWildcardMultiDomain ? "Wildcard" : "") + " SAN : " + allowedMaxSanCount;
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
                    LogWriter.LogCARequestResponseObjectToDB(PF_RequestObject.StoreOrderDetail?.Pin ?? string.Empty, objRes.CARequestObject, JsonSerializer.Serialize(objRes.error), "ComodoParseCSR");
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

        #endregion

        #region DCV Info
        /// <summary>
        /// Same as old ClickSSLController.DCVInfo GET.
        /// Route: GET /api/SSLConfiguration/ClickSSL/DCVInfo?configurationToken=
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
        /// Same as old ClickSSLController.DCVInfo POST.
        /// Route: POST /api/SSLConfiguration/ClickSSL/DCVInfo
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
        #endregion

        #region SAN Info
        /// <summary>
        /// Same as old ClickSSLController.SANInfo GET.
        /// Route: GET /api/SSLConfiguration/ClickSSL/SANInfo?configurationToken=
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
                    PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.AddUniqueKey(domainName, PF_RequestObject.ProductDetail.IsWildcardMultiDomain || PF_RequestObject.ProductDetail.IsWildcard ? ConstantUtil.Comodo_DCVMethod_CnameCsrHash : ConstantUtil.Comodo_DCVMethod_HTTPCsrHash);
                }

                int storeOrderId = PF_RequestObject.StoreOrderDetail?.StoreOrderId ?? 0;
                int productId = PF_RequestObject.StoreOrderDetail?.ProductId ?? PF_RequestObject.ProductDetail?.ProductId ?? 0;

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

                if (!string.IsNullOrEmpty(primaryDomain) && PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail.ContainsKey(primaryDomain))
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
        /// Same as old ClickSSLController.GetApprovalEmailList.
        /// Route: POST /api/SSLConfiguration/ClickSSL/GetApprovalEmailList
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
                var approvalEmailList = BLGeneral.GetComodoApprovalEmailList(request?.sanDomainName ?? string.Empty, PF_RequestObject.CACredentialDetails?.GetComodoCACredential());

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
        #endregion

        #region SAN
        /// <summary>
        /// Same as old ClickSSLController.AddAdditionalDomains.
        /// Route: POST /api/SSLConfiguration/ClickSSL/AddAdditionalDomains
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
                int usedSan = BLGeneral.GetTotalAdditionalSANForComodo(domainName, PF_RequestObject.ComodoOrderRequest.SAN_ApprovalEmail);
                int storeOrderId = PF_RequestObject.StoreOrderDetail?.StoreOrderId ?? 0;
                int productId = PF_RequestObject.StoreOrderDetail?.ProductId ?? PF_RequestObject.ProductDetail.ProductId;
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
        /// Same as old ClickSSLController.DeleteAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/ClickSSL/DeleteAdditionalDomain
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
        /// Same as old ClickSSLController.UpdateMethodForAllAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/ClickSSL/UpdateMethodForAllAdditionalDomain
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
        /// Same as old ClickSSLController.UpdateMethodForAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/ClickSSL/UpdateMethodForAdditionalDomain
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
        #endregion

        #region Wildcard SAN
        /// <summary>
        /// Same as old ClickSSLController.AddWildcardSAN.
        /// Route: POST /api/SSLConfiguration/ClickSSL/AddWildcardSAN
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
                int usedWildcardSan = BLGeneral.GetTotalAdditionalSANForComodo(domainName, PF_RequestObject.ComodoOrderRequest.WildcardSAN_ApprovalEmail);
                int storeOrderId = PF_RequestObject.StoreOrderDetail?.StoreOrderId ?? 0;
                int productId = PF_RequestObject.StoreOrderDetail?.ProductId
                    ?? PF_RequestObject.ProductDetail.ProductId;
                int allowedMaxWildcardSan = BLGeneral.GetWildcardSANCount(storeOrderId, productId);

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
        /// Same as old ClickSSLController.DeleteWildcardSAN.
        /// Route: POST /api/SSLConfiguration/ClickSSL/DeleteWildcardSAN
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
        /// Same as old ClickSSLController.UpdateMethodForAllAdditionalDomain_WildcardSAN.
        /// Route: POST /api/SSLConfiguration/ClickSSL/UpdateMethodForAllAdditionalDomain_WildcardSAN
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
        /// Same as old ClickSSLController.UpdateMethodForAdditionalDomain_WildcardSAN.
        /// Route: POST /api/SSLConfiguration/ClickSSL/UpdateMethodForAdditionalDomain_WildcardSAN
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

        #region Contact Info
        /// <summary>
        /// Same as old ClickSSLController.ContactInfo GET.
        /// Route: GET /api/SSLConfiguration/ClickSSL/ContactInfo?configurationToken=
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
                PF_RequestObject.ComodoOrderRequest.ComodoOrderDetailRow.OrgEmail = string.IsNullOrWhiteSpace(objModel?.Email) ? "-" : objModel.Email;
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
        #endregion

        #region Organization Info
        /// <summary>
        /// Same as old ClickSSLController.OrganizationInfo GET.
        /// Route: GET /api/SSLConfiguration/ClickSSL/OrganizationInfo?configurationToken=
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
        /// Same as old ClickSSLController.OrganizationInfo POST.
        /// Route: POST /api/SSLConfiguration/ClickSSL/OrganizationInfo
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
        #endregion

        #region Jurisdiction Info
        /// <summary>
        /// Same as old ClickSSLController.JurisdictionInfo GET.
        /// Route: GET /api/SSLConfiguration/ClickSSL/JurisdictionInfo?configurationToken=
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
        /// Same as old ClickSSLController.JurisdictionInfo POST.
        /// Route: POST /api/SSLConfiguration/ClickSSL/JurisdictionInfo
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
        #endregion

        #region Verification Info
        /// <summary>
        /// Same as old ClickSSLController.VerificationInfo GET (EV).
        /// Route: GET /api/SSLConfiguration/ClickSSL/VerificationInfo?configurationToken=
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
        /// Same as old ClickSSLController.VerificationInfo POST (EV).
        /// Route: POST /api/SSLConfiguration/ClickSSL/VerificationInfo
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

        #region Summary
        /// <summary>
        /// Same as old ClickSSLController.Summary GET (was PartialView) — returns draft summary JSON.
        /// Route: GET /api/SSLConfiguration/ClickSSL/Summary?configurationToken=
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
        /// Same as old ClickSSLController.Summary POST — PlaceOrder via GetComodoProductObject + QuickComodoOrder.
        /// Route: POST /api/SSLConfiguration/ClickSSL/Summary
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

                // Send Mail if fail to set EV contact details with Comodo API (same as old ClickSSLController.Summary POST)
                if (objPF_Response.ErrorCode == 100)
                    MailUtil.SendEmail("billing@ssl2buy.com", "billing@ssl2buy.com", string.Empty, string.Empty, "Comodo EV Contact details fails to submit for Comodo Order# " + objPF_Response.VendorID, "Comodo EV Contact details fails to submit for Comodo Order# " + objPF_Response.VendorID + " and ErrorMessage: " + objPF_Response.ErrorMessage);

                response.ErrorCode = objPF_Response.ErrorCode;

                if (objPF_Response.ErrorCode == 0)
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
        /// <summary>
        /// Same as old ClickSSLController.SetCSR — returns "success" / "error" string.
        /// Route: POST /api/SSLConfiguration/ClickSSL/SetCSR
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
        #endregion

        #region private Used Classes

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
        private static SelectListItemDto ToSelectListItemDto(SelectListItem i) =>
            new SelectListItemDto { Value = i.Value, Text = i.Text, Selected = i.Selected };
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
        #endregion
    }
}
