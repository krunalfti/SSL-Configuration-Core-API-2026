using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SSLConfiguration.Application;
using SSLConfiguration.Application.Services;
using SSLConfiguration.Contracts.SSLConfiguration.Comodo;
using SSLConfiguration.Contracts.SSLConfiguration.Digicert;
using SSLConfiguration.Contracts.SSLConfiguration.GlobalSign;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.Persistence;
using System.IO;
using System.Text;
using VerisignGateway;
using VerisignGateway.GS_MarkService;
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
using DigicertSelectListItemDto = SSLConfiguration.Contracts.SSLConfiguration.Digicert.SelectListItemDto;
using GlobalSignSelectListItemDto = SSLConfiguration.Contracts.SSLConfiguration.GlobalSign.SelectListItemDto;
using GlobalSignSummaryPostRequest = SSLConfiguration.Contracts.SSLConfiguration.GlobalSign.SummaryPostRequest;
using GlobalSignSummaryPostResponse = SSLConfiguration.Contracts.SSLConfiguration.GlobalSign.SummaryPostResponse;
using JsonSerializer = System.Text.Json.JsonSerializer;
namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration
{
    [ClientAuthorize]
    [ApiController]
    [Route("api/SSLConfiguration/[controller]")]
    public class PrimeSSLController : ControllerBase
    {
        private readonly ConfigurationDraftStore _draftStore;
        private readonly IAuthenticationService _authenticationService;

        public PrimeSSLController(IAuthenticationService authenticationService, ConfigurationDraftStore draftStore)
        {
            _authenticationService = authenticationService;
            _draftStore = draftStore;
        }

        #region DV / OV / EV
        /// <summary>
        /// Same as old PrimeSSLController
        /// Route: GET /api/SSLConfiguration/PrimeSSL/DV?pin=&amp;configurationToken=
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
        /// Same as old PrimeSSLController.CSRInfo GET — returns CSR draft fields (was PartialView).
        /// Route: GET /api/SSLConfiguration/PrimeSSL/CSRInfo?configurationToken=
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
                MapCsrDetailFields(pf.CSRDetailRow, response);
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
                    ApproverEmailListResponse objResponse = VerisignAPIHelper.GetComodoApproveremail(objRes.DomainName, comodoCredential);
                    response.objEmailListJson = JsonConvert.SerializeObject(objResponse);
                    if (objResponse.error != null && objResponse.error.ErrorCode < 0)
                    {
                        LogWriter.LogCARequestResponseObjectToDB(PF_RequestObject.StoreOrderDetail?.Pin ?? string.Empty, objResponse.CARequestObject, JsonSerializer.Serialize(objResponse.error), "GetComodoApproveremail");
                        response.IsSuccess = false;
                        // Prefer CA message (e.g. login/IP lock) over generic domain text.
                        response.Msg =  Resources.InvalidDomainNameInCSR;
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
                    response.CSRDetailJson = JsonConvert.SerializeObject(CSRDetail);

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
        /// Same as old PrimeSSLController.DCVInfo GET.
        /// Route: GET /api/SSLConfiguration/PrimeSSL/DCVInfo?configurationToken=
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
        /// Same as old PrimeSSLController.DCVInfo POST.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/DCVInfo
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
        /// Same as old PrimeSSLController.SANInfo GET.
        /// Route: GET /api/SSLConfiguration/PrimeSSL/SANInfo?configurationToken=
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
        /// Same as old PrimeSSLController.SANInfo POST — validates SAN DCV emails selected.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/SANInfo
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
        /// Same as old PrimeSSLController.GetApprovalEmailList.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/GetApprovalEmailList
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
        /// Same as old PrimeSSLController.AddAdditionalDomains.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/AddAdditionalDomains
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
        /// Same as old PrimeSSLController.DeleteAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/DeleteAdditionalDomain
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
        /// Same as old PrimeSSLController.UpdateMethodForAllAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/UpdateMethodForAllAdditionalDomain
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
        /// Same as old PrimeSSLController.UpdateMethodForAdditionalDomain.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/UpdateMethodForAdditionalDomain
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
        /// Same as old PrimeSSLController.AddWildcardSAN.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/AddWildcardSAN
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
                response.usedWildcardSan = usedWildcardSan;
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
        /// Same as old PrimeSSLController.DeleteWildcardSAN.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/DeleteWildcardSAN
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
        /// Same as old PrimeSSLController.UpdateMethodForAllAdditionalDomain_WildcardSAN.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/UpdateMethodForAllAdditionalDomain_WildcardSAN
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
        /// Same as old PrimeSSLController.UpdateMethodForAdditionalDomain_WildcardSAN.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/UpdateMethodForAdditionalDomain_WildcardSAN
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
        /// Same as old PrimeSSLController.ContactInfo GET.
        /// Route: GET /api/SSLConfiguration/PrimeSSL/ContactInfo?configurationToken=
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
        /// Same as old PrimeSSLController.ContactInfo POST.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/ContactInfo
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
        #endregion

        #region Organization Info
        /// <summary>
        /// Same as old PrimeSSLController.OrganizationInfo GET.
        /// Route: GET /api/SSLConfiguration/PrimeSSL/OrganizationInfo?configurationToken=
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
        /// Same as old PrimeSSLController.OrganizationInfo POST.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/OrganizationInfo
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
        /// Same as old PrimeSSLController.JurisdictionInfo GET.
        /// Route: GET /api/SSLConfiguration/PrimeSSL/JurisdictionInfo?configurationToken=
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
        /// Same as old PrimeSSLController.JurisdictionInfo POST.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/JurisdictionInfo
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
        /// Same as old PrimeSSLController.VerificationInfo GET (EV).
        /// Route: GET /api/SSLConfiguration/PrimeSSL/VerificationInfo?configurationToken=
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
        /// Same as old PrimeSSLController.VerificationInfo POST (EV).
        /// Route: POST /api/SSLConfiguration/PrimeSSL/VerificationInfo
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
        /// Same as old PrimeSSLController.Summary GET (was PartialView) — returns draft summary JSON.
        /// Route: GET /api/SSLConfiguration/PrimeSSL/Summary?configurationToken=
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
                response.csrDetail = ToCsrDetailDto(pf.CSRDetailRow);
                response.isMultiYearOrder = Convert.ToBoolean(pf.StoreOrderDetail?.IsSubscription)
                    && Convert.ToInt32(pf.StoreOrderDetail?.RemainingValidity ?? 0) > 0;

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
        /// Same as old PrimeSSLController.Summary POST — PlaceOrder via GetPrimeSSLProductObject + QuickComodoOrder.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/Summary
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

                PF_Response objPF_Response = PrimeSSLPlaceOrder.PlaceOrder(PF_RequestObject);

                // Send Mail if fail to set EV contact details with Comodo API (same as old ClickSSLController.Summary POST)
                if (objPF_Response.ErrorCode == 100)
                    MailUtil.SendEmail("billing@ssl2buy.com", "billing@ssl2buy.com", string.Empty, string.Empty, "Comodo EV Contact details fails to submit for Comodo Order# " + objPF_Response.VendorID, "Comodo EV Contact details fails to submit for Comodo Order# " + objPF_Response.VendorID + " and ErrorMessage: " + objPF_Response.ErrorMessage);

                response.ErrorCode = objPF_Response.ErrorCode;

                if (objPF_Response.ErrorCode == 0)
                {
                    response.IsSuccess = true;
                    response.Msg = "Your order number is : " + objPF_Response.VendorID;
                    response.returnUrl = "/ManageOrder/PrimeSSL/managedcv";
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
        /// Same as old PrimeSSLController.SetCSR — returns "success" / "error" string.
        /// Route: POST /api/SSLConfiguration/PrimeSSL/SetCSR
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

        #region VMC - Product
        /// <summary>
        /// Same as old PrimeSSLController.MarkCert — Entry for MarkCert products.
        /// Route: GET /api/SSLConfiguration/PrimeSSL/MarkCert?pin=&amp;configurationToken=
        /// </summary>
        [HttpGet("MarkCert")]
        public IActionResult MarkCert([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var response = _authenticationService.Entry(ConstantUtil.AuthenticationType_MarkCert, pin, configurationToken);
            return Ok(response);
        }

        #region VMC Info

        [HttpGet("VMCInfo")]
        public IActionResult VMCInfo_Get([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
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

                PF_Request pf = EnsureGlobalSignDraft(resolved.request, resolved.token);
                var trademarkData = LoadTrademarkData();
                var countryList = trademarkData.Select(x => x.Country)
                    .OrderByDescending(c => c!.All(char.IsUpper))
                    .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                response.TrademarkCountryList = countryList.Select(c => new GlobalSignSelectListItemDto { Text = c, Value = c }).ToList();
                response.TrademarkCountryOfficeData = JsonSerializer.Serialize(trademarkData);

                var vmc = pf.GlobalSignOrderRequest?.VMCCertificateDetail;
                if (vmc != null)
                {
                    response.DomainName = vmc.DomainName;
                    response.FileBase64 = vmc.FileBase64;
                    response.FileName = vmc.FileName;
                    response.Logo = !string.IsNullOrEmpty(vmc.strLogo) ? vmc.strLogo : vmc.Logo;
                    response.EnableHosting = vmc.EnableHosting;
                    response.MarkType = vmc.MarkType;
                    response.RegistrationNumber = vmc.MarkTypeData?.RegistrationNumber;
                    response.CountryCode = vmc.MarkTypeData?.CountryCode;
                    response.IsLogoExists = vmc.IsLogoExists;
                }

                if (pf.GlobalSignOrderRequest?.OrganisationInfoRow != null)
                {
                    response.TrademarkExpiryDate = pf.GlobalSignOrderRequest.OrganisationInfoRow.RegisteredMarkLicenseExpiryDate;
                    response.TrademarkIdentifier = pf.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkIdentifier;
                    response.CountryName = pf.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkCountryOrRegionName;
                    response.Trademarkoffice = pf.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkOfficeName;
                    response.TrademarkURL = pf.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkURL;
                }

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

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                string svgText = string.Empty;

                if (string.IsNullOrWhiteSpace(objModel?.FileBase64))
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

                svgText = System.Text.Encoding.UTF8.GetString(fileBytes);

                string base64Logo = objModel.FileBase64;
                ProductBase objProd = VerisignUtil.GetGSProductObject(VerisignGateway.ProductCode.PrimeSSLCommonMarkCertificate);
                QbV1ValidateLogoRequest validateLogoRequest = new QbV1ValidateLogoRequest
                {
                    OrderRequestHeader = new OrderRequestHeader
                    {
                        AuthToken = new AuthToken
                        {
                            UserName = PF_RequestObject.CACredentialDetails!.UserName,
                            Password = PF_RequestObject.CACredentialDetails.Password
                        }
                    },
                    SvgLogoBase64 = base64Logo
                };

                var objLogoResponse = objProd.ValidateLogoVMCCMC(validateLogoRequest);
                if (objLogoResponse != null && objLogoResponse.error.ErrorCode < 0)
                {
                    LogWriter.LogCARequestResponseObjectToDB(
                        PF_RequestObject.StoreOrderDetail?.Pin,
                        objLogoResponse.CARequestObject,
                        JsonSerializer.Serialize(objLogoResponse),
                        "VMCInfo");
                    response.IsSuccess = false;
                    response.Msg = "Invalid Logo Format. The logo you uploaded is not in a valid SVG format or is not supported by VMC. Please ensure that your logo is saved as an SVG Tiny Portable/Secure file and meets the specified requirements. For more instructions about the logo format, please <a href=\"https://support.globalsign.com/mark-certificate/set/how-convert-your-logos-bimi-guidelines\" target =\"_blank\">click here.</a>";
                    return Ok(response);
                }

                if (PF_RequestObject.ProductDetail!.ProductId != (int)VerisignGateway.ProductCode.PrimeSSLCommonMarkCertificate)
                {
                    if (objModel == null || !objModel.TrademarkExpiryDate.HasValue)
                    {
                        response.IsSuccess = false;
                        response.Msg = "Please select Registered Mark License Expiry Date.";
                        return Ok(response);
                    }

                    if (string.IsNullOrEmpty(objModel.TrademarkIdentifier))
                    {
                        response.IsSuccess = false;
                        response.Msg = "Please enter Trademark Identifier.";
                        return Ok(response);
                    }

                    if (string.IsNullOrEmpty(objModel.CountryName))
                    {
                        response.IsSuccess = false;
                        response.Msg = "Please select country.";
                        return Ok(response);
                    }

                    if (string.IsNullOrEmpty(objModel.Trademarkoffice))
                    {
                        response.IsSuccess = false;
                        response.Msg = "Please select office.";
                        return Ok(response);
                    }
                }

                PF_RequestObject.CSR = string.Empty;
                PF_RequestObject.CSRDetailRow = new CSRDetail();
                PF_RequestObject.GlobalSignOrderRequest!.VMCCertificateDetail = new VMC_CertificateDetail
                {
                    Logo = objModel.Logo,
                    EnableHosting = true,
                    MarkType = objModel.MarkType,
                    FileBase64 = objModel.FileBase64,
                    FileName = objModel.FileName,
                    strLogo = svgText,
                    IsLogoExists = true,
                    MarkTypeData = new VmcMarkTypeData
                    {
                        RegistrationNumber = objModel.RegistrationNumber,
                        CountryCode = objModel.CountryCode
                    }
                };

                if (PF_RequestObject.ProductDetail.ProductId == (int)VerisignGateway.ProductCode.PrimeSSLVerifiedMarkCertificate)
                {
                    if (PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow == null)
                        PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow = new GlobalSignOrganizationInfo();

                    PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.RegisteredMarkLicenseExpiryDate = objModel.TrademarkExpiryDate;
                    PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkIdentifier = objModel.TrademarkIdentifier;

                    var countrycode = BLGeneral.GetCountryList()
                        .Where(x => x.Text.Equals(objModel.CountryName, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.Value)
                        .FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(countrycode))
                    {
                        response.IsSuccess = false;
                        response.Msg = "Country code does not exist.";
                        return Ok(response);
                    }
                    response.countrycode = countrycode;
                    PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkCountryOrRegionName = countrycode;
                    PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkOfficeName = objModel.Trademarkoffice;
                    PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkURL = objModel.TrademarkURL;
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

        #endregion

        #region VMC CSR Info

        [HttpGet("GetVMCCSRInfo")]
        public IActionResult GetVMCCSRInfo_Get([FromQuery] string? configurationToken = null)
        {
            var response = new VmcCsrInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request pf = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.CountryList = BLGeneral.GetCountryList().Select(ToGlobalSignSelectListItemDto).ToList();

                if (pf.GlobalSignOrderRequest?.CSRDetailInfoRow != null)
                {
                    response.DomainName = pf.GlobalSignOrderRequest.CSRDetailInfoRow.DomainName;
                    response.Organisation = pf.GlobalSignOrderRequest.CSRDetailInfoRow.Organisation;
                    response.Locality = pf.GlobalSignOrderRequest.CSRDetailInfoRow.Locality;
                    response.State = pf.GlobalSignOrderRequest.CSRDetailInfoRow.State;
                    var countryMatch = BLGeneral.GetCountryList()
                        .FirstOrDefault(m => m.Value == pf.GlobalSignOrderRequest.CSRDetailInfoRow.Country);
                    response.DomainCountryName = countryMatch?.Text ?? pf.GlobalSignOrderRequest.CSRDetailInfoRow.Country;
                }

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

        [HttpPost("UpdateVMC_CSRInfo")]
        public IActionResult UpdateVMC_CSRInfo_Post([FromBody] VmcCsrInfoPostRequest? objModel)
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

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                if (string.IsNullOrEmpty(objModel?.DomainName))
                {
                    PF_RequestObject.CSRDetailRow = new CSRDetail();
                    response.IsSuccess = true;
                    response.Msg = "Please enter CSR Details.";
                    return Ok(response);
                }

                if (!BLGeneral.isValidSingleDomain(objModel.DomainName.Trim()))
                {
                    response.IsSuccess = false;
                    response.Msg = "Invalid domain name : " + objModel.DomainName.Trim().Replace(" ", "[space]");
                    return Ok(response);
                }

                CSRDetail objCSRInfo = new CSRDetail
                {
                    DomainName = objModel.DomainName,
                    Organisation = objModel.Organisation,
                    Locality = objModel.Locality,
                    State = objModel.State,
                    Country = objModel.DomainCountryName
                };

                PF_RequestObject.CSRDetailRow = objCSRInfo;
                PF_RequestObject.GlobalSignOrderRequest!.CSRDetailInfoRow = objCSRInfo;
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

        #region DCV Info - VMC

        [HttpGet("DCVInfo_VMC")]
        public IActionResult DCVInfo_VMC_Get([FromQuery] string? configurationToken = null)
        {
            var response = new DcvInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);

                if (!string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest!.ApprovalMethod) && PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod.ToUpper() == ConstantUtil.GlobalSign_DCVMethod_URL)
                    response.ApprovalMethod = 1;
                else if (!string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod) && PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod.ToUpper() == ConstantUtil.GlobalSign_DCVMethod_EMAIL)
                    response.ApprovalMethod = 2;
                else if (!string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod) && PF_RequestObject.GlobalSignOrderRequest.ApprovalMethod.ToUpper() == ConstantUtil.GlobalSign_DCVMethod_DNS)
                    response.ApprovalMethod = 3;
                else
                    response.ApprovalMethod = 0;

                response.ApprovalEmail = string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.ApprovalEmail)
                    ? string.Empty
                    : PF_RequestObject.GlobalSignOrderRequest.ApprovalEmail;

                var approvalEmailList = BLGeneral.GetApprovalEmailList(
                    PF_RequestObject.CSRDetailRow!.DomainName,
                    PF_RequestObject.StoreOrderDetail!.ProductId,
                    PF_RequestObject.CACredentialDetails!.GetGlobalSignCACredential()!);
                response.ApprovalEmailList = approvalEmailList.Select(ToGlobalSignSelectListItemDto).ToList();
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

        [HttpPost("DCVInfo_VMC")]
        public IActionResult DCVInfo_VMC_Post([FromBody] DcvInfoPostRequest? objModel)
        {
            var response = new DcvInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, null);
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
                    PF_RequestObject.GlobalSignOrderRequest!.ApprovalMethod = ConstantUtil.GlobalSign_DCVMethod_URL;
                else if (ApprovalMethod == ConstantUtil.DCVMethod_EMAIL)
                    PF_RequestObject.GlobalSignOrderRequest!.ApprovalMethod = ConstantUtil.GlobalSign_DCVMethod_EMAIL;
                else if (ApprovalMethod == ConstantUtil.DCVMethod_DNS)
                    PF_RequestObject.GlobalSignOrderRequest!.ApprovalMethod = ConstantUtil.GlobalSign_DCVMethod_DNS;
                else
                    PF_RequestObject.GlobalSignOrderRequest!.ApprovalMethod = ConstantUtil.GlobalSign_DCVMethod_EMAIL;

                PF_RequestObject.GlobalSignOrderRequest!.ApprovalEmail = ApprovalEmail;
                _draftStore.Update(resolved.token, PF_RequestObject);

                response.IsSuccess = true;
                response.ismulti = PF_RequestObject.ProductDetail?.IsMultiDomain ?? false;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = ex.Message;
                return Ok(response);
            }
        }

        #endregion

        #region SAN Info - VMC

        [HttpGet("SANInfo_VMC")]
        public IActionResult SANInfo_VMC_Get([FromQuery] string? configurationToken = null)
        {
            var response = new SanInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                if (PF_RequestObject.GlobalSignOrderRequest!.AdditionalDomainsList == null)
                    PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList = new Dictionary<string, string>();
                if (PF_RequestObject.GlobalSignOrderRequest.AdditionalWildCardDomainList == null)
                    PF_RequestObject.GlobalSignOrderRequest.AdditionalWildCardDomainList = new Dictionary<string, string>();

                string domainName = PF_RequestObject.CSRDetailRow?.DomainName ?? string.Empty;
                string primarydomainName = PF_RequestObject.CSRDetailRow?.PrimaryDomainName ?? string.Empty;
                PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList.AddUniqueKey(
                    domainName,
                    PF_RequestObject.GlobalSignOrderRequest.ApprovalMethods.ToString());

                int noOfAdditionalDomains = PF_RequestObject.GlobalSignOrderRequest.NoOfAdditionalDomains == 0
                    ? BLGeneral.GetAddDomain(PF_RequestObject.StoreOrderDetail!.StoreOrderId, PF_RequestObject.StoreOrderDetail.ProductId)
                    : PF_RequestObject.GlobalSignOrderRequest.NoOfAdditionalDomains;
                int noOfWild = PF_RequestObject.GlobalSignOrderRequest.NoOfAdditionalWildCardDomains == 0
                    ? BLGeneral.GetWildcardSANCount(PF_RequestObject.StoreOrderDetail!.StoreOrderId, PF_RequestObject.StoreOrderDetail.ProductId)
                    : PF_RequestObject.GlobalSignOrderRequest.NoOfAdditionalWildCardDomains;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.DomainName = domainName;
                response.NoOfAdditionalDomains = noOfAdditionalDomains;
                response.MaxWildcardSAN = noOfWild;
                response.TotalNoOfSANAllowed= noOfAdditionalDomains + noOfWild;
                response.primarydomainName = primarydomainName;
                response.isMultiDomain = PF_RequestObject.ProductDetail?.IsMultiDomain;
                response.isWildcardMultiDomain = PF_RequestObject.ProductDetail?.IsWildcardMultiDomain;
                response.AdditionalDomainList = PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList?.Keys.ToList();
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
        public IActionResult SANInfo_VMC_Post([FromBody] GlobalSignSummaryPostRequest? request)
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

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                string domainName = PF_RequestObject.CSRDetailRow?.DomainName ?? string.Empty;
                if (PF_RequestObject.GlobalSignOrderRequest!.AdditionalDomainsList != null &&
                    PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList.ContainsKey(domainName))
                {
                    PF_RequestObject.GlobalSignOrderRequest.ApproverEmail =
                        PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList[domainName];
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

        [HttpPost("AddAdditionalDomains_VMC")]
        public IActionResult AddAdditionalDomains_VMC([FromBody] SanMutationRequest? request)
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

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                response.configurationToken = resolved.token;

                if (PF_RequestObject.ProductDetail!.IsMultiDomain)
                {
                    var usedSan = BLGeneral.GetTotalAdditionalSANForSymantec(
                        PF_RequestObject.CSRDetailRow!.DomainName!,
                        PF_RequestObject.GlobalSignOrderRequest!.AdditionalDomainsList);
                    int allowedMaxSan = BLGeneral.GetAddDomain(
                        PF_RequestObject.StoreOrderDetail!.StoreOrderId,
                        PF_RequestObject.StoreOrderDetail.ProductId);

                    if (usedSan >= allowedMaxSan)
                    {
                        response.IsSuccess = false;
                        response.Msg = "Maximum allowed SAN : " + allowedMaxSan;
                        return Ok(response);
                    }

                    string? additionalDomains = request?.additionalDomains;
                    if (string.IsNullOrEmpty(additionalDomains))
                    {
                        response.IsSuccess = false;
                        response.Msg = "Enter san.";
                        return Ok(response);
                    }

                    string[] addDomainList = additionalDomains.Trim().Split('\n').Distinct().ToArray();
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
                        PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList!.AddUniqueKey(
                            san.Trim().ToLower(),
                            PF_RequestObject.GlobalSignOrderRequest.ApprovalMethods.ToString());
                    }

                    _draftStore.Update(resolved.token, PF_RequestObject);
                    response.IsSuccess = true;
                    response.Msg = string.Empty;
                    return Ok(response);
                }

                response.IsSuccess = false;
                response.Msg = "Additional SAN not allowed.";
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

        [HttpPost("DeleteAdditionalDomains_VMC")]
        public IActionResult DeleteAdditionalDomains_VMC([FromBody] SanMutationRequest? request)
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

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                string? sanDomainName = request?.sanDomainName;

                if (string.IsNullOrEmpty(sanDomainName))
                {
                    response.IsSuccess = false;
                    response.Msg = "Domain Name not available.";
                    return Ok(response);
                }

                if (PF_RequestObject.GlobalSignOrderRequest!.AdditionalDomainsList != null &&
                    PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList.ContainsKey(sanDomainName.ToLower()))
                {
                    PF_RequestObject.GlobalSignOrderRequest.AdditionalDomainsList.Remove(sanDomainName.ToLower());
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

        #endregion

        #region Organization Info - VMC

        [HttpGet("OrganizationInfo_VMC")]
        public IActionResult OrganizationInfo_VMC_Get([FromQuery] string? configurationToken = null)
        {
            var response = new OrganisationInfoGetResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                EnsureGlobalSignContactAndOrgRows(PF_RequestObject);

                var objModel = new OrganisationInfoDto();
                response.CountryList = BLGeneral.GetCountryList().Select(ToGlobalSignSelectListItemDto).ToList();

                if (PF_RequestObject.GlobalSignOrderRequest!.OrganisationInfoRow != null &&
                    !string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.LegalName))
                {
                    objModel.Orgname = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.LegalName;
                    objModel.JurictionCountryName = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry;
                    objModel.jurictionState = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState;
                    objModel.JurictionCity = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity;
                    objModel.CoRegistrationNumber = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.CorporateRegistrationNumber;
                    objModel.BusinessCategory = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.BusinessCategory;
                    objModel.Address1 = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Address1;
                    objModel.Address2 = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Address2;
                    objModel.Email = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Email;
                    objModel.PostalCode = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode;
                    objModel.PhoneNo = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo;
                    objModel.Fax = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Fax;
                    objModel.DBAname = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.AssumedName;
                    objModel.Duns = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Duns;
                }

                if (PF_RequestObject.GlobalSignOrderRequest.CSRDetailInfoRow != null)
                {
                    objModel.Orgname = PF_RequestObject.GlobalSignOrderRequest.CSRDetailInfoRow.Organisation;
                    objModel.City = PF_RequestObject.GlobalSignOrderRequest.CSRDetailInfoRow.Locality;
                    objModel.State = PF_RequestObject.GlobalSignOrderRequest.CSRDetailInfoRow.State;
                    var countryMatch = BLGeneral.GetCountryList()
                        .FirstOrDefault(m => m.Value == PF_RequestObject.GlobalSignOrderRequest.CSRDetailInfoRow.Country);
                    objModel.OrganizationCountryName = countryMatch?.Text;
                }

                response.organisation = objModel;
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

        [HttpPost("UpdateOrganizationInfo_VMC")]
        public IActionResult UpdateOrganizationInfo_VMC_Post([FromBody] OrganisationInfoPostRequest? objModel)
        {
            var response = new OrganisationInfoPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(objModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Message = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);
                if (PF_RequestObject.GlobalSignOrderRequest!.OrganisationInfoRow == null)
                    PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow = new GlobalSignOrganizationInfo();

                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.LegalName = objModel!.Orgname;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Email = objModel.Email;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.AssumedName = objModel.DBAname;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Address1 = objModel.Address1;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Address2 = objModel.Address2;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo = objModel.PhoneNo;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Duns = objModel.Duns;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.City = objModel.City;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.State = objModel.State;                
                if ((objModel.OrganizationCountryName?.Length ?? 0) > 2)
                {
                    var country = BLGeneral.GetCountryList()
                        .FirstOrDefault(c => c.Text.Equals(objModel.OrganizationCountryName, StringComparison.OrdinalIgnoreCase));
                    if (country != null)
                        PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Country = country.Value;
                }

                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode = objModel.PostalCode;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Division = objModel.Division;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Fax = objModel.Fax;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.BusinessCategory = objModel.BusinessCategory;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.CorporateRegistrationNumber = objModel.CoRegistrationNumber;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry = objModel.JurictionCountryName;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState = objModel.jurictionState;
                PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity = objModel.JurictionCity;

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.countryValue = PF_RequestObject.GlobalSignOrderRequest.OrganisationInfoRow.Country;
                response.Message = string.Empty;
                response.configurationToken = resolved.token;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Message = ex.Message;
                return Ok(response);
            }
        }

        #endregion

        #region Contact Info - VMC

        [HttpGet("ContactInfo_VMC")]
        public IActionResult ContactInfo_VMC_Get([FromQuery] string? configurationToken = null)
        {
            var response = new VmcContactInfoDto();
            var wrapper = new DigicertJsonResponse { IsSuccess = true };
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    wrapper.IsSuccess = false;
                    wrapper.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(wrapper);
                }

                PF_Request pf = EnsureGlobalSignDraft(resolved.request, resolved.token);
                wrapper.configurationToken = resolved.token;

                if (pf.GlobalSignOrderRequest?.RequestorInfoRow != null &&
                    !string.IsNullOrEmpty(pf.GlobalSignOrderRequest.RequestorInfoRow.Email))
                {
                    response.RequestorTitle = pf.GlobalSignOrderRequest.RequestorInfoRow.Title;
                    response.RequestorFirstName = pf.GlobalSignOrderRequest.RequestorInfoRow.FirstName;
                    response.RequestorLastName = pf.GlobalSignOrderRequest.RequestorInfoRow.LastName;
                    response.RequestorPhoneNo = pf.GlobalSignOrderRequest.RequestorInfoRow.PhoneNo;
                    response.RequestorEmail = pf.GlobalSignOrderRequest.RequestorInfoRow.Email;
                    response.RequestorCountryName = pf.GlobalSignOrderRequest.RequestorInfoRow.Country;
                    response.RequestorOrgName = pf.GlobalSignOrderRequest.RequestorInfoRow.OrganizationName;
                }

                if (pf.GlobalSignOrderRequest?.ApproverInfoRow != null &&
                    !string.IsNullOrEmpty(pf.GlobalSignOrderRequest.ApproverInfoRow.Email))
                {
                    response.ApproverTitle = pf.GlobalSignOrderRequest.ApproverInfoRow.Title;
                    response.ApproverFirstName = pf.GlobalSignOrderRequest.ApproverInfoRow.FirstName;
                    response.ApproverLastName = pf.GlobalSignOrderRequest.ApproverInfoRow.LastName;
                    response.ApproverPhoneNo = pf.GlobalSignOrderRequest.ApproverInfoRow.PhoneNo;
                    response.ApproverEmail = pf.GlobalSignOrderRequest.ApproverInfoRow.Email;
                    response.ApproverOrgName = pf.GlobalSignOrderRequest.ApproverInfoRow.OrganizationName;
                }

                if (pf.GlobalSignOrderRequest?.ContactInfoRow != null &&
                    !string.IsNullOrEmpty(pf.GlobalSignOrderRequest.ContactInfoRow.Email))
                {
                    response.ContactTitle = pf.GlobalSignOrderRequest.ContactInfoRow.Title;
                    response.ContactFirstName = pf.GlobalSignOrderRequest.ContactInfoRow.FirstName;
                    response.ContactLastName = pf.GlobalSignOrderRequest.ContactInfoRow.LastName;
                    response.ContactPhoneNo = pf.GlobalSignOrderRequest.ContactInfoRow.PhoneNo;
                    response.ContactEmail = pf.GlobalSignOrderRequest.ContactInfoRow.Email;
                    response.ContactOrgName = pf.GlobalSignOrderRequest.ContactInfoRow.OrganizationName;
                }

                return Ok(new
                {
                    IsSuccess = true,
                    configurationToken = resolved.token,
                    contact = response,
                    CountryList = BLGeneral.GetCountryList().Select(ToGlobalSignSelectListItemDto).ToList()
                });
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                wrapper.IsSuccess = false;
                wrapper.Msg = ex.Message;
                return Ok(wrapper);
            }
        }

        [HttpPost("ContactInfo_VMC")]
        public IActionResult ContactInfo_VMC_Post([FromBody] VmcContactInfoPostRequest? objModel)
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

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);

                PF_RequestObject.GlobalSignOrderRequest!.RequestorInfoRow = new GlobalSignContactInfo
                {
                    Title = objModel?.RequestorTitle,
                    FirstName = objModel?.RequestorFirstName,
                    LastName = objModel?.RequestorLastName,
                    PhoneNo = objModel?.RequestorPhoneNo,
                    Email = objModel?.RequestorEmail,
                    Country = objModel?.RequestorCountryName,
                    OrganizationName = objModel?.RequestorOrgName
                };

                PF_RequestObject.GlobalSignOrderRequest.ApproverInfoRow = new GlobalSignContactInfo
                {
                    Title = objModel?.ApproverTitle,
                    FirstName = objModel?.ApproverFirstName,
                    LastName = objModel?.ApproverLastName,
                    PhoneNo = objModel?.ApproverPhoneNo,
                    Email = objModel?.ApproverEmail,
                    OrganizationName = objModel?.ApproverOrgName
                };

                PF_RequestObject.GlobalSignOrderRequest.ContactInfoRow = new GlobalSignContactInfo
                {
                    Title = objModel?.ContactTitle,
                    FirstName = objModel?.ContactFirstName,
                    LastName = objModel?.ContactLastName,
                    PhoneNo = objModel?.ContactPhoneNo,
                    Email = objModel?.ContactEmail,
                    OrganizationName = objModel?.ContactOrgName
                };

                _draftStore.Update(resolved.token, PF_RequestObject);
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                return Ok(response);
            }
        }

        #endregion

        #region Summary - VMC

        [HttpGet("Summary_VMC")]
        public IActionResult Summary_VMC_Get([FromQuery] string? configurationToken = null)
        {
            return Ok(new DigicertJsonResponse
            {
                IsSuccess = true,
                configurationToken = configurationToken
            });
        }

        [HttpPost("Summary_VMC")]
        public IActionResult Summary_VMC_Post([FromBody] GlobalSignSummaryPostRequest? request)
        {
            var response = new GlobalSignSummaryPostResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(request?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(response);
                }

                PF_Request PF_RequestObject = EnsureGlobalSignDraft(resolved.request, resolved.token);

                if (PF_RequestObject.GlobalSignOrderRequest?.VMCCertificateDetail != null &&
                    !string.IsNullOrEmpty(PF_RequestObject.GlobalSignOrderRequest.VMCCertificateDetail.strLogo))
                {
                    try
                    {
                        string ext = string.IsNullOrWhiteSpace(PF_RequestObject.GlobalSignOrderRequest.VMCCertificateDetail.FileName)
                            ? ".svg"
                            : Path.GetExtension(PF_RequestObject.GlobalSignOrderRequest.VMCCertificateDetail.FileName);
                        if (string.IsNullOrWhiteSpace(ext))
                            ext = ".svg";

                        string fileName = Convert.ToString(PF_RequestObject.StoreOrderDetail!.SSLApiLinkId) + ext;
                        string dir = Path.Combine(LogWriter.ContentRoot, "Uploads", "VMC");
                        Directory.CreateDirectory(dir);
                        System.IO.File.WriteAllText(Path.Combine(dir, fileName), PF_RequestObject.GlobalSignOrderRequest.VMCCertificateDetail.strLogo);
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogErrorDetails(ex);
                    }
                }

                PF_Response objPF_Response = GlobalSignPlaceOrder.PlaceVmcOrder(
                    PF_RequestObject,
                    (VerisignGateway.ProductCode)PF_RequestObject.StoreOrderDetail!.ProductId);

                if (objPF_Response.ErrorCode == 0)
                {
                    response.IsSuccess = true;
                    response.Msg = "Your order number is : " + objPF_Response.VendorID;
                    response.returnUrl = "/ManageOrder/PrimeSSL/MarkCertorderdetail";
                    response.VendorID = objPF_Response.VendorID;
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

        #endregion

        #region CodeSign

        /// <summary>
        /// Same as old PrimeSSLController.CodeSign — Entry + require CodeSignProvisioningMethod.
        /// Route: GET /api/SSLConfiguration/PrimeSSL/CodeSign?pin=&amp;configurationToken=
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

                PF_Response objPF_Response = PrimeSSLPlaceOrder.PlaceCodeSignOrder(PF_RequestObject);
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
        private static DigicertSelectListItemDto ToSelectListItemDto(SelectListItem i) =>
            new DigicertSelectListItemDto { Value = i.Value, Text = i.Text, Selected = i.Selected };

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
                CountryName = src.CountryName,
                PostalCode = src.PostalCode,
                PhoneNo = src.PhoneNo,
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

        private static ComodoCsrDetailDto? ToCsrDetailDto(CSRDetail? row)
        {
            if (row == null || string.IsNullOrEmpty(row.DomainName)) return null;
            var dto = new ComodoCsrDetailDto
            {
                domainName = row.DomainName,
                organisation = row.Organisation,
                organisationUnit = row.OrganisationUnit,
                locality = row.Locality,
                state = row.State,
                country = row.Country,
                email = row.Email
            };
            if (!string.IsNullOrEmpty(row.Country))
            {
                dto.countryName = BLGeneral.GetCountryList()
                    .FirstOrDefault(m => m.Value == row.Country)?.Text ?? row.Country;
            }
            return dto;
        }

        private static void MapCsrDetailFields(CSRDetail? row, CSRInfoGetResponse response)
        {
            if (row == null) return;
            response.organisation = row.Organisation;
            response.organisationUnit = row.OrganisationUnit;
            response.locality = row.Locality;
            response.state = row.State;
            response.country = row.Country;
            response.email = row.Email;
            if (!string.IsNullOrEmpty(row.Country))
            {
                response.countryName = BLGeneral.GetCountryList()
                    .FirstOrDefault(m => m.Value == row.Country)?.Text ?? row.Country;
            }
        }

        private static GlobalSignSelectListItemDto ToGlobalSignSelectListItemDto(SelectListItem i) =>
            new GlobalSignSelectListItemDto { Value = i.Value, Text = i.Text, Selected = i.Selected };
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

            EnsureGlobalSignContactAndOrgRows(pf);
            _draftStore.Update(token, pf);
            return pf;
        }

        private static void EnsureGlobalSignContactAndOrgRows(PF_Request pf)
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

        private static string ParseLogoSvgContent(string logoSvgContent)
        {
            string strLogo = logoSvgContent.Trim();
            if (strLogo.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int comma = strLogo.IndexOf(',');
                if (comma > 0)
                    strLogo = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(strLogo[(comma + 1)..]));
            }
            else if (!strLogo.Contains('<') && !strLogo.Contains("svg", StringComparison.OrdinalIgnoreCase))
            {
                try { strLogo = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(strLogo)); }
                catch { /* already plain SVG text */ }
            }

            return strLogo.Replace(Environment.NewLine, "");
        }

        private static List<TrademarkCountryData> LoadTrademarkData()
        {
            string path = Path.Combine(LogWriter.ContentRoot, "Scripts", "TrademarksOffice", "trademarkscountrywiseoffice.json");
            var json = System.IO.File.ReadAllText(path);
            var jArray = JArray.Parse(json);
            var list = new List<TrademarkCountryData>();
            foreach (var item in jArray)
            {
                string country = item["Country"]?.ToString();
                var officeObj = item["Office"];
                var officeName = officeObj?["Name"]?.ToString();
                var offices = new List<TrademarkOfficeData>();
                if (!string.IsNullOrEmpty(officeName))
                {
                    var officeNames = officeName.Split('|');
                    foreach (var name in officeNames)
                    {
                        offices.Add(new TrademarkOfficeData
                        {
                            Name = name.Trim()
                        });
                    }
                }
                list.Add(new TrademarkCountryData
                {
                    Country = country,
                    Office = offices
                });
            }
            return list;
        }

        private sealed class TrademarkCountryJsonItem
        {
            public string? Country { get; set; }
            public TrademarkOfficeJsonItem? Office { get; set; }
        }

        private sealed class TrademarkOfficeJsonItem
        {
            public string? Name { get; set; }
        }

        private sealed class TrademarkCountryData
        {
            public string? Country { get; set; }
            public List<TrademarkOfficeData>? Office { get; set; }
        }

        private sealed class TrademarkOfficeData
        {
            public string? Name { get; set; }
        }

        #endregion
    }
}
