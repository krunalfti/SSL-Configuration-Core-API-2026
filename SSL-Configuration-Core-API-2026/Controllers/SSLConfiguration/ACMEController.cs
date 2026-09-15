using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSLConfiguration.Application;
using SSLConfiguration.Contracts.SSLConfiguration.ACME;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.Persistence;
using VerisignGateway;

namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration
{
    /// <summary>
    /// Ported from SSLConfiguration Controllers/ACMEController — DV/OV ACME flows.
    /// Session → ResolveDraft; View/PartialView/Redirect/TempData → Ok JSON.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/SSLConfiguration/[controller]")]
    public class ACMEController : ControllerBase
    {
        private readonly ConfigurationDraftStore _draftStore;
        private readonly IAuthenticationService _authenticationService;

        private readonly string apiUrl =
            System.Configuration.ConfigurationManager.AppSettings["AcemeApiUrl"]
            ?? AppConfig.AcemeApiUrl
            ?? "";

        private string request = "";
        private string response = "";

        /// <summary>Same role as old Session PF_Request — set after ResolveDraft for private helpers.</summary>
        public PF_Request? PF_RequestObject { get; set; }

        public ACMEController(IAuthenticationService authenticationService, ConfigurationDraftStore draftStore)
        {
            _authenticationService = authenticationService;
            _draftStore = draftStore;
        }

        #region Phase 1 — Entry + ACMEInfo DV

        /// <summary>Same as old ACMEController.DV.</summary>
        [HttpGet("DV")]
        public IActionResult DV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var entry = _authenticationService.Entry("DV", pin, configurationToken);
            return Ok(entry);
        }

        /// <summary>Same as old ACMEController.OV.</summary>
        [HttpGet("OV")]
        public IActionResult OV([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var entry = _authenticationService.Entry("OV", pin, configurationToken);
            return Ok(entry);
        }

        /// <summary>
        /// Same as old ACMEController.ACMEInfo GET — LISTSERVERS / PREREGISTER / accounts / domains.
        /// Route: GET /api/SSLConfiguration/ACME/ACMEInfo?pin=&amp;configurationToken=
        /// </summary>
        [HttpGet("ACMEInfo")]
        public IActionResult ACMEInfo([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var viewModel = new ACMEDetailViewModel();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    viewModel.IsSuccess = false;
                    viewModel.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(viewModel);
                }

                PF_RequestObject = resolved.request;
                viewModel.configurationToken = resolved.token;

                var username = PF_RequestObject.CACredentialDetails?.UserName;
                var password = PF_RequestObject.CACredentialDetails?.Password;
                var Eabkey = string.Empty;

                int SSLApiLinkId = PF_RequestObject.StoreOrderDetail!.SSLApiLinkId;
                string Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";
                int acmeyear = PF_RequestObject.StoreOrderDetail.Year;

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    viewModel.IsSuccess = false;
                    viewModel.Msg = "Missing CA credentials.";
                    viewModel.configurationToken = resolved.token;
                    return Ok(viewModel);
                }

                var acmeServerRequest = new VerisignGateway.ACMEBaseRequest
                {
                    loginName = username,
                    loginPassword = password,
                    action = "LISTSERVERS"
                };

                var logRequest = new VerisignGateway.ACMEBaseRequest
                {
                    loginName = "xxxxx",
                    loginPassword = "xxxxx",
                    action = "LISTSERVERS"
                };

                request = "Request Object: " + JsonSerializer.Serialize(logRequest);
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "LISTSERVERS");

                try
                {
                    var serverResponse = VerisignGateway.AcemeAPIHelper.GetServerUrl(acmeServerRequest);
                    if (serverResponse?.Servers != null && serverResponse.Servers.Any())
                    {
                        viewModel.ServerUrl = serverResponse.Servers.First().serverUrl;

                        response = "Response Object: " + JsonSerializer.Serialize(serverResponse);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "Get:LISTSERVERS");
                    }
                    else
                    {
                        string message = "No ACME servers found in response.";
                        response = "Response Object: " + JsonSerializer.Serialize(serverResponse);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, message);
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogErrorDetails(ex);
                    string msg = "Failed to retrieve ACME server URL:" + ex.Message;
                    response = "Response Object: ";
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, msg);
                    viewModel.IsSuccess = false;
                    viewModel.Msg = msg;
                    viewModel.configurationToken = resolved.token;
                    return Ok(viewModel);
                }

                var AccountDetails = BLAcme.GetAcemeDetail(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                if (AccountDetails == null && viewModel.EabId == null)
                {
                    #region API Call for Create EABAccount

                    var acmeAccountRequest = new VerisignGateway.AcemeAccountCreateRequest
                    {
                        loginName = username,
                        loginPassword = password,
                        action = "PREREGISTER",
                        serverUrl = viewModel.ServerUrl,
                        years = acmeyear
                    };

                    var logRequest1 = new VerisignGateway.AcemeAccountCreateRequest
                    {
                        loginName = "xxxxx",
                        loginPassword = "xxxxx",
                        action = "PREREGISTER",
                        serverUrl = viewModel.ServerUrl,
                        years = acmeyear
                    };

                    request = "Request Object: " + JsonSerializer.Serialize(logRequest1);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "EABAccount Create: PREREGISTER");

                    try
                    {
                        var EABAccountResponse = VerisignGateway.AcemeAPIHelper.GetEABAccountDetails(acmeAccountRequest);

                        if (EABAccountResponse?.Accounts != null && EABAccountResponse.Accounts.Any())
                        {
                            viewModel.EabId = EABAccountResponse.Accounts.First().EABID;
                            viewModel.EabKey = EABAccountResponse.Accounts.First().EABKey;
                            viewModel.EABAccountStatus = EABAccountResponse.Accounts.First().AccountStatus;
                            Eabkey = EABAccountResponse.Accounts.First().EABKey;

                            response = "Response Object: " + JsonSerializer.Serialize(EABAccountResponse);
                            LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "EABAccount Create SuccessFully.!");

                            BLAcme.SaveAcemeAccountDetailsInDB(PF_RequestObject, viewModel);

                            viewModel.OrderStatus = "InProcess";
                            BLAcme.SaveComodoOrderInDB(PF_RequestObject, viewModel);
                        }
                        else
                        {
                            string err = "EAB Details not found in API!";
                            response = "Response Object: " + JsonSerializer.Serialize(EABAccountResponse);
                            LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, err);
                            viewModel.IsSuccess = false;
                            viewModel.Msg = err;
                            viewModel.configurationToken = resolved.token;
                            return Ok(viewModel);
                        }
                    }
                    catch (Exception ex)
                    {
                        SSLApiLinkId = PF_RequestObject.StoreOrderDetail.SSLApiLinkId;
                        Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";
                        LogWriter.LogErrorDetails(ex);
                        string msg = "Failed to retrieve EAB Account Details URL: " + ex.Message;
                        response = "Response Object: ";
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, msg);
                        viewModel.IsSuccess = false;
                        viewModel.Msg = msg;
                        viewModel.configurationToken = resolved.token;
                        return Ok(viewModel);
                    }

                    #endregion
                }
                else
                {
                    viewModel.EabId = AccountDetails!.AcmeAccountID;
                    viewModel.ServerUrl = AccountDetails.ServerUrl;
                    viewModel.EabKey = Eabkey;
                }

                var accountDetails = Getaccountdetails(viewModel);

                if (accountDetails != null)
                {
                    viewModel.Accounts = accountDetails;
                    response = "Response Object: " + JsonSerializer.Serialize(accountDetails);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "Check Account Status");
                }
                else
                {
                    string msg = "No accounts details found.";
                    response = "Response Object: " + JsonSerializer.Serialize(accountDetails);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, msg);
                }

                viewModel.MaxSanUsed = BLAcme.GetSANAllowedCount(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                viewModel.MaxWildUsed = BLAcme.GetWildcardAllowedSANCount(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                viewModel.RemainingMaxWildcardSAN = BLAcme.GetMaxWildcardAllowedSAN(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                viewModel.RemainingMaxSAN = BLAcme.GetMaxAllowedSAN(
                    PF_RequestObject.StoreOrderDetail.StoreOrderId,
                    PF_RequestObject.ProductDetail?.IsWildcardMultiDomain ?? false);

                try
                {
                    var domainDetails = GetDomain(viewModel);
                    viewModel.Domains = domainDetails;
                }
                catch (Exception)
                {
                    viewModel.Domains = null;
                }

                viewModel.IsSuccess = true;
                viewModel.configurationToken = resolved.token;
                return Ok(viewModel);
            }
            catch (Exception ex)
            {
                int SSLApiLinkId = PF_RequestObject?.StoreOrderDetail?.SSLApiLinkId ?? 0;
                string Pin = PF_RequestObject?.StoreOrderDetail?.Pin ?? "";
                LogWriter.LogErrorDetails(ex);
                response = "Response Object: ";
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, ex.Message);
                viewModel.IsSuccess = false;
                viewModel.Msg = ex.Message;
                return Ok(viewModel);
            }
        }

        private List<AcemeDomains> GetDomain(ACMEDetailViewModel viewModel)
        {
            try
            {
                int SSLApiLinkId = PF_RequestObject!.StoreOrderDetail!.SSLApiLinkId;
                string Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";
                var AccountDetails = BLAcme.GetAcemeDetail(PF_RequestObject.StoreOrderDetail.StoreOrderId);

                var acmeGetDomainListRequest = new VerisignGateway.AcemeGetDomainListRequest
                {
                    loginName = PF_RequestObject.CACredentialDetails?.UserName,
                    loginPassword = PF_RequestObject.CACredentialDetails?.Password,
                    action = "LISTDOMAINS",
                    EABID = AccountDetails!.AcmeAccountID,
                };

                var logRequest5 = new VerisignGateway.AcemeGetDomainListRequest
                {
                    loginName = "xxxxx",
                    loginPassword = "xxxxx",
                    action = "LISTDOMAINS",
                    EABID = AccountDetails.AcmeAccountID
                };

                request = "Request Object: " + JsonSerializer.Serialize(logRequest5);
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "LISTDOMAINS");

                var Response = VerisignGateway.AcemeAPIHelper.GetAllDomainListByAcmeAccountID(acmeGetDomainListRequest);

                if (Response.Domains != null && Response.ErrorCode == 0)
                {
                    viewModel.Domains = Response.Domains
                        .Select(d => new AcemeDomains { domainName = d.DomainName })
                        .ToList();

                    response = "Response Object: " + JsonSerializer.Serialize(Response.Domains);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "Get: All Domains");
                }
                else
                {
                    viewModel.Domains = null;
                    response = "Response Object: " + JsonSerializer.Serialize(viewModel.Domains);
                    string msg = "No domains found.";
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, msg);
                    throw new Exception("No domains found.");
                }

                return viewModel.Domains!;
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                throw new Exception("No domains found.");
            }
        }

        private List<AccountInfo> Getaccountdetails(ACMEDetailViewModel viewModel)
        {
            try
            {
                int SSLApiLinkId = PF_RequestObject!.StoreOrderDetail!.SSLApiLinkId;
                string Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";

                var accountDetails = BLAcme.GetAcemeDetail(PF_RequestObject.StoreOrderDetail.StoreOrderId);

                var acmeServerRequest = new VerisignGateway.ACMEBaseRequest
                {
                    loginName = PF_RequestObject.CACredentialDetails?.UserName,
                    loginPassword = PF_RequestObject.CACredentialDetails?.Password,
                    action = "LISTACCOUNTS",
                    EABKeyID = accountDetails!.AcmeAccountID,
                    includeAdditionalAccounts = "Y"
                };

                var logRequest6 = new VerisignGateway.ACMEBaseRequest
                {
                    loginName = "xxxxx",
                    loginPassword = "xxxxx",
                    action = "LISTACCOUNTS",
                    EABKeyID = accountDetails.AcmeAccountID,
                    includeAdditionalAccounts = "Y"
                };

                request = "Request Object: " + JsonSerializer.Serialize(logRequest6);
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "Get Accounts Details: LISTACCOUNTS");

                var Response = VerisignGateway.AcemeAPIHelper.GetAcemeAllAccountList(acmeServerRequest);

                if (Response.Accounts == null || !Response.Accounts.Any())
                {
                    // same as old TempData message path
                }

                if (Response.Accounts != null)
                {
                    viewModel.Accounts = Response.Accounts
                        .Select(d => new AccountInfo
                        {
                            EABID = d.EABID,
                            EABKey = d.EABKey,
                            AccountStatus = d.AccountStatus,
                            IpAddress = d.IpAddress,
                            LastActivity = ParseLastActivity(d.LastActivity),
                            UserAgent = d.UserAgent
                        }).ToList();

                    response = "Response Object: " + JsonSerializer.Serialize(Response.Accounts);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "Get: Acme Account Details.");
                }
                else
                {
                    string msg = "No accounts details found. ";
                    response = "Response Object: " + JsonSerializer.Serialize(Response.Accounts);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, msg);
                    throw new Exception("No accounts details found.");
                }

                return viewModel.Accounts!;
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                throw new Exception("No accounts details found.");
            }
        }

        #endregion

        #region Phase 2 — CheckDomain / AddDomain / UpdateAccountStatus

        /// <summary>Same as old CheckDomainAvailable — QuoteOnly ADDDOMAIN.</summary>
        [HttpGet("CheckDomainAvailable")]
        public IActionResult CheckDomainAvailable(
            [FromQuery] string? domainName,
            [FromQuery] string? pin = null,
            [FromQuery] string? configurationToken = null)
        {
            var result = new AcmeJsonResponse();
            var viewModel = new ACMEDetailViewModel();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    result.IsSuccess = false;
                    result.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(result);
                }

                PF_RequestObject = resolved.request;
                result.configurationToken = resolved.token;

                int SSLApiLinkId = PF_RequestObject.StoreOrderDetail!.SSLApiLinkId;
                string Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";

                var orderDetail = BLStoreOrder.GetStoreOrderDetailByPIN(Pin.Trim());
                if (orderDetail != null && orderDetail.IsCancel)
                {
                    result.IsSuccess = false;
                    result.Msg = Resources.Val_InvalidPIN;
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }

                domainName ??= "";
                var isWildcard = domainName.StartsWith("*.");

                if (!IsDomainQuotaAvailable(isWildcard))
                {
                    result.IsSuccess = false;
                    result.Msg = $"Domain quota exceeded. You cannot add more {(isWildcard ? "Wildcard" : "Standard")} domains.";
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }

                var AccountDetails = BLAcme.GetAcemeDetail(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                var acmeAddDomainRequest = new VerisignGateway.GetDomainAvailableRequest
                {
                    loginName = PF_RequestObject.CACredentialDetails?.UserName,
                    loginPassword = PF_RequestObject.CACredentialDetails?.Password,
                    action = "ADDDOMAIN",
                    EABID = AccountDetails!.AcmeAccountID,
                    DomainName = domainName,
                    QuoteOnly = "Y",
                    AddAssociatedFQDN = "Y"
                };

                var logRequest2 = new VerisignGateway.GetDomainAvailableRequest
                {
                    loginName = "xxxxx",
                    loginPassword = "xxxxx",
                    action = "ADDDOMAIN",
                    EABID = AccountDetails.AcmeAccountID,
                    DomainName = domainName,
                    QuoteOnly = "Y",
                    AddAssociatedFQDN = "Y"
                };

                request = "Request Object: " + JsonSerializer.Serialize(logRequest2);
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "Check: Domain Availability");

                var Response = VerisignGateway.AcemeAPIHelper.GetDomainAvailable(acmeAddDomainRequest);

                if (Response.Success == true && Response.Domains != null)
                {
                    var alldomains = Response.Domains.Select(d => d.DomainName).ToList();
                    viewModel.domains = alldomains
                        .Select(d => new GetDomains { domainName = d })
                        .ToList();
                    if (alldomains.Count > 1)
                        viewModel.domainName = alldomains[1];
                    else
                        viewModel.domainName = alldomains[0];

                    response = "Response Object: " + JsonSerializer.Serialize(Response);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "Get: FQDN Domain");

                    result.IsSuccess = true;
                    result.domainName = viewModel.domainName;
                    result.domains = viewModel.domains;
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
                else
                {
                    result.IsSuccess = false;
                    result.Msg = Response.ErrorDetail;
                    response = "Response Object: " + JsonSerializer.Serialize(Response);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, result.Msg ?? "");
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                result.IsSuccess = false;
                result.Msg = ex.Message;
                return Ok(result);
            }
        }

        /// <summary>Same as old AddDomain.</summary>
        [HttpPost("AddDomain")]
        public IActionResult AddDomain([FromBody] AddDomainRequest body)
        {
            var result = new AcmeJsonResponse();
            var viewModel = new ACMEDetailViewModel();
            try
            {
                var resolved = _authenticationService.ResolveDraft(body?.configurationToken, body?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    result.IsSuccess = false;
                    result.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(result);
                }

                PF_RequestObject = resolved.request;
                result.configurationToken = resolved.token;

                string domainname = body?.domainname ?? "";
                string status = body?.status ?? "";

                var orderDetail = BLStoreOrder.GetStoreOrderDetailByPIN(PF_RequestObject.StoreOrderDetail!.Pin!.Trim());
                if (orderDetail != null && orderDetail.IsCancel)
                {
                    result.IsSuccess = false;
                    result.Msg = Resources.Val_InvalidPIN;
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }

                var AccountDetails = BLAcme.GetAcemeDetail(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                var isWildcard = domainname.StartsWith("*.");

                if (!IsDomainQuotaAvailable(isWildcard))
                {
                    result.IsSuccess = false;
                    result.Msg = $"Domain quota exceeded. You cannot add more {(isWildcard ? "Wildcard" : "Standard")} domains.";
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }

                int SSLApiLinkId = PF_RequestObject.StoreOrderDetail.SSLApiLinkId;
                string Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";

                var AddDomainRequestApi = new VerisignGateway.AcemeAddDomainRequest
                {
                    loginName = PF_RequestObject.CACredentialDetails?.UserName,
                    loginPassword = PF_RequestObject.CACredentialDetails?.Password,
                    action = "ADDDOMAIN",
                    EABID = AccountDetails!.AcmeAccountID,
                    DomainName = domainname,
                    AddAssociatedFQDNStatus = status == "Y" ? "Y" : "N"
                };

                var logRequest3 = new VerisignGateway.AcemeAddDomainRequest
                {
                    loginName = "xxxxx",
                    loginPassword = "xxxxx",
                    action = "ADDDOMAIN",
                    EABID = AccountDetails.AcmeAccountID,
                    DomainName = domainname,
                    AddAssociatedFQDNStatus = status == "Y" ? "Y" : "N"
                };

                request = "Request Object: " + JsonSerializer.Serialize(logRequest3);
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "ADDDOMAIN");

                var Response = VerisignGateway.AcemeAPIHelper.AddDomain(AddDomainRequestApi);
                if (Response.Success && Response.Domains != null)
                {
                    viewModel.Domains = Response.Domains
                        .Select(d => new AcemeDomains { domainName = d.DomainName })
                        .ToList();

                    viewModel.domainName = viewModel.Domains.First().domainName;

                    string response1 = "Response Object: " + JsonSerializer.Serialize(Response);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response1, "Get Insert Domain");

                    int DomainwithholdCount = BLAcme.GetCountByDomainWithHoldorNot(
                        PF_RequestObject.StoreOrderDetail.StoreOrderId, viewModel.domainName!);

                    if (DomainwithholdCount == 1)
                    {
                        result.IsSuccess = false;
                        result.Msg = "The domain name is already present in the subscription.";
                        response = "Response Object: " + JsonSerializer.Serialize(viewModel.Domains);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, result.Msg);
                        result.configurationToken = resolved.token;
                        return Ok(result);
                    }

                    int DomainCount = BLAcme.GetAdditionalDomainCount(PF_RequestObject.StoreOrderDetail.StoreOrderId);

                    var subscriptionDateRequest = new VerisignGateway.GetSubscriptionDateRequest
                    {
                        loginName = PF_RequestObject.CACredentialDetails?.UserName,
                        loginPassword = PF_RequestObject.CACredentialDetails?.Password,
                        action = "LISTTRANSACTIONS",
                        EABID = AccountDetails.AcmeAccountID,
                        DomainName = domainname
                    };

                    if (DomainCount == 0)
                    {
                        var logRequest4 = new VerisignGateway.GetSubscriptionDateRequest
                        {
                            loginName = "xxxxx",
                            loginPassword = "xxxxx",
                            action = "LISTTRANSACTIONS",
                            EABID = AccountDetails.AcmeAccountID,
                            DomainName = domainname
                        };

                        request = "Request Object: " + JsonSerializer.Serialize(logRequest4);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "LISTTRANSACTIONS");

                        var subscriptionResponse = VerisignGateway.AcemeAPIHelper.GetDomainSubscriptionsDateDetails(subscriptionDateRequest);
                        if (subscriptionResponse.ErrorCode == 0 && subscriptionResponse.subscriptions.Count > 0)
                        {
                            viewModel.SubscriptionStartDate = DateTime.Parse(subscriptionResponse.subscriptions[0].subscriptionStartDate, null, DateTimeStyles.AdjustToUniversal).Date;
                            viewModel.SubscriptionEndDate = DateTime.Parse(subscriptionResponse.subscriptions[0].subscriptionEndDate, null, DateTimeStyles.AdjustToUniversal).Date;

                            long orderNumber = 0;
                            var subscription = subscriptionResponse.subscriptions.FirstOrDefault();
                            if (subscription != null)
                            {
                                var transaction = subscription.transactions
                                    .FirstOrDefault(t => t.domains.Any(d => d.name.Equals(domainname, StringComparison.OrdinalIgnoreCase)));

                                if (transaction != null)
                                    orderNumber = transaction.orderNumber;
                            }
                            viewModel.AcmeOrderNo = orderNumber;
                        }
                        string response2 = "Response Object: " + JsonSerializer.Serialize(subscriptionResponse);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response2, "Get: Subscription StartDate and EndDate and AcmeorderNumber ");

                        BLAcme.SaveAcemeDomainDetailsInDB(PF_RequestObject, viewModel);

                        viewModel.OrderStatus = "Completed";
                        viewModel.EabId = AccountDetails.AcmeAccountID;
                        BLAcme.SaveComodoOrderInDB(PF_RequestObject, viewModel);
                    }
                    else
                    {
                        var subscriptionResponse = VerisignGateway.AcemeAPIHelper.GetDomainSubscriptionsDateDetails(subscriptionDateRequest);
                        if (subscriptionResponse.ErrorCode == 0 && subscriptionResponse.subscriptions.Count > 0)
                        {
                            long orderNumber = 0;
                            var subscription = subscriptionResponse.subscriptions.FirstOrDefault();
                            if (subscription != null)
                            {
                                var transaction = subscription.transactions
                                    .FirstOrDefault(t => t.domains.Any(d => d.name.Equals(domainname, StringComparison.OrdinalIgnoreCase)));

                                if (transaction != null)
                                    orderNumber = transaction.orderNumber;
                            }
                            viewModel.AcmeOrderNo = orderNumber;
                        }
                        string response2 = "Response Object: " + JsonSerializer.Serialize(subscriptionResponse);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response2, "Get: AcmeorderNumber Insert");
                        BLAcme.SaveAcemeDomainDetailsInDB(PF_RequestObject, viewModel);
                    }

                    viewModel.MaxWildcardSAN = BLAcme.GetMaxWildcardAllowedSAN(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                    viewModel.MaxSAN = BLAcme.GetMaxAllowedSAN(
                        PF_RequestObject.StoreOrderDetail.StoreOrderId,
                        PF_RequestObject.ProductDetail?.IsWildcardMultiDomain ?? false);
                    viewModel.MaxSanUsed = BLAcme.GetSANAllowedCount(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                    viewModel.MaxWildUsed = BLAcme.GetWildcardAllowedSANCount(PF_RequestObject.StoreOrderDetail.StoreOrderId);

                    string successMsg = "Domain Added Successfully!";
                    response = "Response Object: " + JsonSerializer.Serialize(viewModel.Domains);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, successMsg);

                    result.IsSuccess = true;
                    result.Msg = successMsg;
                    result.MaxSAN = viewModel.MaxSAN;
                    result.MaxWildcardSAN = viewModel.MaxWildcardSAN;
                    result.SanUsed = viewModel.MaxSanUsed;
                    result.WildUsed = viewModel.MaxWildUsed;
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
                else
                {
                    result.IsSuccess = false;
                    result.Msg = "Failed to add domain.";
                    response = "Response Object: " + JsonSerializer.Serialize(viewModel.Domains);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, result.Msg);
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                result.IsSuccess = false;
                result.Msg = ex.Message;
                return Ok(result);
            }
        }

        /// <summary>Same as old UpdateAccountStatus.</summary>
        [HttpPost("UpdateAccountStatus")]
        public IActionResult UpdateAccountStatus([FromBody] UpdateAccountStatusRequest body)
        {
            var result = new AcmeJsonResponse();
            try
            {
                var resolved = _authenticationService.ResolveDraft(body?.configurationToken, body?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    result.IsSuccess = false;
                    result.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(result);
                }

                PF_RequestObject = resolved.request;
                result.configurationToken = resolved.token;

                int SSLApiLinkId = PF_RequestObject.StoreOrderDetail!.SSLApiLinkId;
                string Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";

                var orderDetail = BLStoreOrder.GetStoreOrderDetailByPIN(Pin.Trim());
                if (orderDetail != null && orderDetail.IsCancel)
                {
                    result.IsSuccess = false;
                    result.Msg = Resources.Val_InvalidPIN;
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }

                string? accountId = body?.accountId;
                string actionStatus = body?.actionStatus ?? "";

                if (actionStatus == "suspend")
                    actionStatus = "SUSPENDACCOUNT";
                else if (actionStatus == "unsuspend")
                    actionStatus = "UNSUSPENDACCOUNT";
                else if (actionStatus == "deactivated")
                    actionStatus = "DEACTIVATEACCOUNT";

                var AcemeAccountUpdateRequest = new VerisignGateway.AcemeAccountUpdateRequest
                {
                    loginName = PF_RequestObject.CACredentialDetails?.UserName,
                    loginPassword = PF_RequestObject.CACredentialDetails?.Password,
                    EABID = accountId,
                    action = actionStatus,
                    IncludeAdditionalAccounts = "N"
                };

                var logRequest7 = new VerisignGateway.AcemeAccountUpdateRequest
                {
                    loginName = "xxxxx",
                    loginPassword = "xxxxx",
                    EABID = accountId,
                    action = actionStatus,
                    IncludeAdditionalAccounts = "N"
                };

                request = "Request Object: " + JsonSerializer.Serialize(logRequest7);
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "Update Account Status");

                var Response = VerisignGateway.AcemeAPIHelper.AccountUpdateStatus(AcemeAccountUpdateRequest);

                if (Response.ErrorCode == 0 && Response.Success == true)
                {
                    result.IsSuccess = true;
                    result.Msg = "ACME Status Updated Successfully";
                    response = "Response Object: " + JsonSerializer.Serialize(Response);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, result.Msg);
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
                else
                {
                    result.IsSuccess = false;
                    result.Msg = "Failed to update ACME status.";
                    response = "Response Object: " + JsonSerializer.Serialize(Response);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, result.Msg);
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                result.IsSuccess = false;
                result.Msg = ex.Message;
                return Ok(result);
            }
        }

        #endregion

        #region Phase 3 — Partials + AcmeAccountDetails

        /// <summary>Same as old GetAcmeAccountDetailsPartial — returns model JSON.</summary>
        [HttpGet("GetAcmeAccountDetailsPartial")]
        public IActionResult GetAcmeAccountDetailsPartial(
            [FromQuery] string? pin = null,
            [FromQuery] string? configurationToken = null)
        {
            var model = new ACMEDetailViewModel();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    model.IsSuccess = false;
                    model.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(model);
                }

                PF_RequestObject = resolved.request;
                model.configurationToken = resolved.token;

                try
                {
                    model.Accounts = Getaccountdetails(model);
                }
                catch (Exception ex)
                {
                    LogWriter.LogErrorDetails(ex);
                    model.Msg = ex.Message;
                }

                model.IsSuccess = true;
                model.configurationToken = resolved.token;
                return Ok(model);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                model.IsSuccess = false;
                model.Msg = ex.Message;
                return Ok(model);
            }
        }

        /// <summary>Same as old GetAcmeDomainDetailsPartial — returns model JSON.</summary>
        [HttpGet("GetAcmeDomainDetailsPartial")]
        public IActionResult GetAcmeDomainDetailsPartial(
            [FromQuery] string? pin = null,
            [FromQuery] string? configurationToken = null)
        {
            var model = new ACMEDetailViewModel();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    model.IsSuccess = false;
                    model.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(model);
                }

                PF_RequestObject = resolved.request;
                model.configurationToken = resolved.token;

                try
                {
                    model.Domains = GetDomain(model);
                }
                catch (Exception ex)
                {
                    LogWriter.LogErrorDetails(ex);
                    model.Msg = ex.Message;
                    model.Domains = null;
                }

                model.IsSuccess = true;
                model.configurationToken = resolved.token;
                return Ok(model);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                model.IsSuccess = false;
                model.Msg = ex.Message;
                return Ok(model);
            }
        }

        /// <summary>Same as old AcmeAccountDetails — returns model JSON (was PartialView).</summary>
        [HttpPost("AcmeAccountDetails")]
        public IActionResult AcmeAccountDetails([FromBody] ACMEDetailViewModel viewModel)
        {
            try
            {
                var resolved = _authenticationService.ResolveDraft(viewModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    var fail = viewModel ?? new ACMEDetailViewModel();
                    fail.IsSuccess = false;
                    fail.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(fail);
                }

                PF_RequestObject = resolved.request;
                viewModel ??= new ACMEDetailViewModel();
                viewModel.IsSuccess = true;
                viewModel.configurationToken = resolved.token;
                return Ok(viewModel);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                viewModel ??= new ACMEDetailViewModel();
                viewModel.IsSuccess = false;
                viewModel.Msg = ex.Message;
                return Ok(viewModel);
            }
        }

        #region check Domain count Allowed or not

        private bool IsDomainQuotaAvailable(bool isWildcard)
        {
            int used = isWildcard
                ? BLAcme.GetWildcardSANbyStore(PF_RequestObject!.StoreOrderDetail!.StoreOrderId, PF_RequestObject.StoreOrderDetail.ProductId)
                : BLAcme.GetSANbyStore(PF_RequestObject!.StoreOrderDetail!.StoreOrderId, PF_RequestObject.StoreOrderDetail.ProductId);

            int allowed = isWildcard
                ? BLAcme.GetWildcardAllowedSANCount(PF_RequestObject.StoreOrderDetail.StoreOrderId)
                : BLAcme.GetSANAllowedCount(PF_RequestObject.StoreOrderDetail.StoreOrderId);

            if (used <= allowed)
                return false;
            else
                return true;
        }

        #endregion

        #endregion

        #region Phase 4 — ACME OV

        /// <summary>
        /// Same as old ACMEOVInfo GET — Servers[1] for OV server URL.
        /// </summary>
        [HttpGet("ACMEOVInfo")]
        public IActionResult ACMEOVInfo([FromQuery] string? pin = null, [FromQuery] string? configurationToken = null)
        {
            var viewModel = new ACMEDetailViewModel();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    viewModel.IsSuccess = false;
                    viewModel.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(viewModel);
                }

                PF_RequestObject = resolved.request;
                viewModel.configurationToken = resolved.token;

                var username = PF_RequestObject.CACredentialDetails?.UserName;
                var password = PF_RequestObject.CACredentialDetails?.Password;
                var Eabkey = string.Empty;

                int SSLApiLinkId = PF_RequestObject.StoreOrderDetail!.SSLApiLinkId;
                string Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";
                int acmeyear = PF_RequestObject.StoreOrderDetail.Year;

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    viewModel.IsSuccess = false;
                    viewModel.Msg = "Missing CA credentials.";
                    viewModel.configurationToken = resolved.token;
                    return Ok(viewModel);
                }

                var acmeServerRequest = new VerisignGateway.ACMEBaseRequest
                {
                    loginName = username,
                    loginPassword = password,
                    action = "LISTSERVERS"
                };

                var logRequest = new VerisignGateway.ACMEBaseRequest
                {
                    loginName = "xxxxx",
                    loginPassword = "xxxxx",
                    action = "LISTSERVERS"
                };

                request = "Request Object: " + JsonSerializer.Serialize(logRequest);
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "LISTSERVERS");

                try
                {
                    var serverResponse = VerisignGateway.AcemeAPIHelper.GetServerUrl(acmeServerRequest);
                    if (serverResponse?.Servers != null && serverResponse.Servers.Any())
                    {
                        viewModel.ServerUrl = serverResponse.Servers[1].serverUrl;

                        response = "Response Object: " + JsonSerializer.Serialize(serverResponse);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "Get:LISTSERVERS");
                    }
                    else
                    {
                        string message = "No ACME servers found in response.";
                        response = "Response Object: " + JsonSerializer.Serialize(serverResponse);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, message);
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogErrorDetails(ex);
                    string msg = "Failed to retrieve ACME server URL:" + ex.Message;
                    response = "Response Object: ";
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, msg);
                    viewModel.IsSuccess = false;
                    viewModel.Msg = msg;
                    viewModel.configurationToken = resolved.token;
                    return Ok(viewModel);
                }

                var AccountDetails = BLAcme.GetAcemeDetail(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                if (AccountDetails == null && viewModel.EabId == null)
                {
                    #region API Call for Create EABAccount

                    var acmeAccountRequest = new VerisignGateway.AcemeAccountCreateRequest
                    {
                        loginName = username,
                        loginPassword = password,
                        action = "PREREGISTER",
                        serverUrl = viewModel.ServerUrl,
                        years = acmeyear
                    };

                    var logRequest1 = new VerisignGateway.AcemeAccountCreateRequest
                    {
                        loginName = "xxxxx",
                        loginPassword = "xxxxx",
                        action = "PREREGISTER",
                        serverUrl = viewModel.ServerUrl,
                        years = acmeyear
                    };

                    request = "Request Object: " + JsonSerializer.Serialize(logRequest1);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "EABAccount Create: PREREGISTER");

                    try
                    {
                        var EABAccountResponse = VerisignGateway.AcemeAPIHelper.GetEABAccountDetails(acmeAccountRequest);

                        if (EABAccountResponse?.Accounts != null && EABAccountResponse.Accounts.Any())
                        {
                            viewModel.EabId = EABAccountResponse.Accounts.First().EABID;
                            viewModel.EabKey = EABAccountResponse.Accounts.First().EABKey;
                            viewModel.EABAccountStatus = EABAccountResponse.Accounts.First().AccountStatus;
                            Eabkey = EABAccountResponse.Accounts.First().EABKey;

                            response = "Response Object: " + JsonSerializer.Serialize(EABAccountResponse);
                            LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "EABAccount Create SuccessFully.!");

                            BLAcme.SaveAcemeAccountDetailsInDB(PF_RequestObject, viewModel);

                            viewModel.OrderStatus = "InProcess";
                            BLAcme.SaveComodoOrderInDB(PF_RequestObject, viewModel);
                        }
                        else
                        {
                            string err = "EAB Details not found in API!";
                            response = "Response Object: " + JsonSerializer.Serialize(EABAccountResponse);
                            LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, err);
                            viewModel.IsSuccess = false;
                            viewModel.Msg = err;
                            viewModel.configurationToken = resolved.token;
                            return Ok(viewModel);
                        }
                    }
                    catch (Exception ex)
                    {
                        SSLApiLinkId = PF_RequestObject.StoreOrderDetail.SSLApiLinkId;
                        Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";
                        LogWriter.LogErrorDetails(ex);
                        string msg = "Failed to retrieve EAB Account Details URL: " + ex.Message;
                        response = "Response Object: ";
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, msg);
                        viewModel.IsSuccess = false;
                        viewModel.Msg = msg;
                        viewModel.configurationToken = resolved.token;
                        return Ok(viewModel);
                    }

                    #endregion
                }
                else
                {
                    viewModel.EabId = AccountDetails!.AcmeAccountID;
                    viewModel.ServerUrl = AccountDetails.ServerUrl;
                    viewModel.EabKey = Eabkey;
                }

                try
                {
                    var accountDetails = GetAcmeOVaccountdetails(viewModel);
                    if (accountDetails != null)
                    {
                        viewModel.Accounts = accountDetails;
                        response = "Response Object: " + JsonSerializer.Serialize(accountDetails);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "Check Account Status");
                    }
                    else
                    {
                        string msg = "No accounts details found.";
                        response = "Response Object: " + JsonSerializer.Serialize(accountDetails);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, msg);
                    }
                }
                catch (Exception)
                {
                    viewModel.Accounts = null;
                }

                viewModel.MaxSanUsed = BLAcme.GetSANAllowedCount(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                viewModel.MaxWildUsed = BLAcme.GetWildcardAllowedSANCount(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                viewModel.RemainingMaxWildcardSAN = BLAcme.GetMaxWildcardAllowedSAN(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                viewModel.RemainingMaxSAN = BLAcme.GetMaxAllowedSAN(
                    PF_RequestObject.StoreOrderDetail.StoreOrderId,
                    PF_RequestObject.ProductDetail?.IsWildcardMultiDomain ?? false);

                viewModel.OrganisationList = new List<OrganisationListItem>();
                var orgResponse = BLAcme.GetOrganisationDetails(Pin, PF_RequestObject.StoreOrderDetail.StoreId);
                if (orgResponse.StatusCode == 0 && orgResponse.ACMEOrganisationList != null && orgResponse.ACMEOrganisationList.Count > 0)
                {
                    viewModel.OrganisationList = new List<OrganisationListItem>
                    {
                        new OrganisationListItem
                        {
                            Text = "Select Organization",
                            Value = "-1",
                            Selected = true
                        }
                    };

                    viewModel.OrganisationList.AddRange(orgResponse.ACMEOrganisationList.Select(org => new OrganisationListItem
                    {
                        Text = $"{org.OrgName} ({org.SectigoOrgID})",
                        Value = org.SectigoOrgID
                    }));

                    viewModel.FullOrganisationData = orgResponse.ACMEOrganisationList;
                }
                else
                {
                    viewModel.OrganisationList = new List<OrganisationListItem>
                    {
                        new OrganisationListItem
                        {
                            Text = "Select Organization",
                            Value = "-1",
                            Selected = true
                        }
                    };
                }

                viewModel.CountryList = BLGeneral.GetCountryList()
                    .Select(c => new OrganisationListItem
                    {
                        Text = c.Text,
                        Value = c.Value,
                        Selected = c.Selected
                    }).ToList();

                try
                {
                    var domainDetails = GetAcmeOvDomainList(viewModel);
                    viewModel.AcmeOvDomains = domainDetails;
                }
                catch (Exception)
                {
                    viewModel.AcmeOvDomains = null;
                }

                viewModel.IsSuccess = true;
                viewModel.configurationToken = resolved.token;
                return Ok(viewModel);
            }
            catch (Exception ex)
            {
                int SSLApiLinkId = PF_RequestObject?.StoreOrderDetail?.SSLApiLinkId ?? 0;
                string Pin = PF_RequestObject?.StoreOrderDetail?.Pin ?? "";
                LogWriter.LogErrorDetails(ex);
                response = "Response Object: ";
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, ex.Message);
                viewModel.IsSuccess = false;
                viewModel.Msg = ex.Message;
                return Ok(viewModel);
            }
        }

        private List<AcemeOvDomains> GetAcmeOvDomainList(ACMEDetailViewModel viewModel)
        {
            try
            {
                int SSLApiLinkId = PF_RequestObject!.StoreOrderDetail!.SSLApiLinkId;
                string Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";
                var AccountDetails = BLAcme.GetAcemeDetail(PF_RequestObject.StoreOrderDetail.StoreOrderId);

                var acmeGetDomainListRequest = new VerisignGateway.AcemeGetDomainListRequest
                {
                    loginName = PF_RequestObject.CACredentialDetails?.UserName,
                    loginPassword = PF_RequestObject.CACredentialDetails?.Password,
                    action = "LISTDOMAINS",
                    EABID = AccountDetails!.AcmeAccountID,
                };

                var logRequest5 = new VerisignGateway.AcemeGetDomainListRequest
                {
                    loginName = "xxxxx",
                    loginPassword = "xxxxx",
                    action = "LISTDOMAINS",
                    EABID = AccountDetails.AcmeAccountID
                };

                request = "Request Object: " + JsonSerializer.Serialize(logRequest5);
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "LISTDOMAINS");

                var Response = VerisignGateway.AcemeAPIHelper.GetAllDomainListByAcmeAccountID(acmeGetDomainListRequest);

                if (Response.Domains != null && Response.ErrorCode == 0)
                {
                    viewModel.AcmeOvDomains = Response.Domains
                        .Select(d => new AcemeOvDomains
                        {
                            domainName = d.DomainName,
                            organisationNumber = d.OvAnchorOrderNumber,
                            expireDate = Convert.ToDateTime(d.ExpiresAt)
                        }).ToList();

                    response = "Response Object: " + JsonSerializer.Serialize(Response.Domains);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "Get: All Domains");
                }
                else
                {
                    viewModel.AcmeOvDomains = null;
                    response = "Response Object: " + JsonSerializer.Serialize(viewModel.AcmeOvDomains);
                    string msg = "No domains found.";
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, msg);
                    throw new Exception("No domains found.");
                }

                var orgResponse = BLAcme.GetOrganisationDetails(Pin, PF_RequestObject.StoreOrderDetail.StoreId);
                if (orgResponse.StatusCode == 0 && orgResponse.ACMEOrganisationList != null && orgResponse.ACMEOrganisationList.Count > 0)
                {
                    var organisationList = orgResponse.ACMEOrganisationList;
                    foreach (var domain in viewModel.AcmeOvDomains!)
                    {
                        var org = organisationList
                            .FirstOrDefault(o => o.SectigoOrgID == domain.organisationNumber);

                        if (org != null)
                            domain.organisationNumber = $"{org.OrgName} ({org.SectigoOrgID})";
                    }
                }

                return viewModel.AcmeOvDomains!;
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                throw new Exception("No domains found.");
            }
        }

        private List<AccountInfo> GetAcmeOVaccountdetails(ACMEDetailViewModel viewModel)
        {
            try
            {
                int SSLApiLinkId = PF_RequestObject!.StoreOrderDetail!.SSLApiLinkId;
                string Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";

                var accountDetails = BLAcme.GetAcemeDetail(PF_RequestObject.StoreOrderDetail.StoreOrderId);

                var acmeServerRequest = new VerisignGateway.ACMEBaseRequest
                {
                    loginName = PF_RequestObject.CACredentialDetails?.UserName,
                    loginPassword = PF_RequestObject.CACredentialDetails?.Password,
                    action = "LISTACCOUNTS",
                    EABKeyID = accountDetails!.AcmeAccountID,
                    includeAdditionalAccounts = "Y"
                };

                var logRequest6 = new VerisignGateway.ACMEBaseRequest
                {
                    loginName = "xxxxx",
                    loginPassword = "xxxxx",
                    action = "LISTACCOUNTS",
                    EABKeyID = accountDetails.AcmeAccountID,
                    includeAdditionalAccounts = "Y"
                };

                request = "Request Object: " + JsonSerializer.Serialize(logRequest6);
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "Get Accounts Details: LISTACCOUNTS");

                var Response = VerisignGateway.AcemeAPIHelper.GetAcemeAllAccountList(acmeServerRequest);

                if (Response.Accounts == null || !Response.Accounts.Any())
                {
                    // same as old
                }

                if (Response.Accounts != null)
                {
                    viewModel.Accounts = Response.Accounts
                        .Select(d => new AccountInfo
                        {
                            EABID = d.EABID,
                            EABKey = d.EABKey,
                            AccountStatus = d.AccountStatus,
                            IpAddress = d.IpAddress,
                            LastActivity = ParseLastActivity(d.LastActivity),
                            UserAgent = d.UserAgent
                        }).ToList();

                    response = "Response Object: " + JsonSerializer.Serialize(Response.Accounts);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "Get: Acme Account Details.");
                }
                else
                {
                    string msg = "No accounts details found. ";
                    response = "Response Object: " + JsonSerializer.Serialize(Response.Accounts);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, msg);
                    throw new Exception("No accounts details found.");
                }

                return viewModel.Accounts!;
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                throw new Exception("No accounts details found.");
            }
        }

        /// <summary>Same as old AddDomainOV.</summary>
        [HttpPost("AddDomainOV")]
        public IActionResult AddDomainOV([FromBody] AddDomainRequest body)
        {
            var result = new AcmeJsonResponse();
            var viewModel = new ACMEDetailViewModel();
            try
            {
                var resolved = _authenticationService.ResolveDraft(body?.configurationToken, body?.pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    result.IsSuccess = false;
                    result.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(result);
                }

                PF_RequestObject = resolved.request;
                result.configurationToken = resolved.token;

                string domainname = body?.domainname ?? "";
                string status = body?.status ?? "";
                string acmeOrgId = body?.acmeOrgId ?? "";
                string acmeOrgName = body?.acmeOrgName ?? "";

                var orderDetail = BLStoreOrder.GetStoreOrderDetailByPIN(PF_RequestObject.StoreOrderDetail!.Pin!.Trim());
                if (orderDetail != null && orderDetail.IsCancel)
                {
                    result.IsSuccess = false;
                    result.Msg = Resources.Val_InvalidPIN;
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }

                #region check acmeOrgId is exist or not
                var orgResponse = BLAcme.GetOrganisationDetails(
                    PF_RequestObject.StoreOrderDetail.Pin.Trim(),
                    PF_RequestObject.StoreOrderDetail.StoreId);

                bool isMatched = orgResponse.ACMEOrganisationList != null
                    && orgResponse.ACMEOrganisationList
                        .Any(x => x.SectigoOrgID != null && x.SectigoOrgID.Trim() == acmeOrgId);

                if (!isMatched)
                {
                    result.IsSuccess = false;
                    result.Msg = "The Organisation is invalid.";
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
                #endregion

                var AccountDetails = BLAcme.GetAcemeDetail(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                var isWildcard = domainname.StartsWith("*.");

                if (!IsDomainQuotaAvailable(isWildcard))
                {
                    result.IsSuccess = false;
                    result.Msg = $"Domain quota exceeded. You cannot add more {(isWildcard ? "Wildcard" : "Standard")} domains.";
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }

                int SSLApiLinkId = PF_RequestObject.StoreOrderDetail.SSLApiLinkId;
                string Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";

                var AddDomainRequestApi = new VerisignGateway.AcemeAddDomainRequest
                {
                    loginName = PF_RequestObject.CACredentialDetails?.UserName,
                    loginPassword = PF_RequestObject.CACredentialDetails?.Password,
                    action = "ADDDOMAIN",
                    EABID = AccountDetails!.AcmeAccountID,
                    DomainName = domainname,
                    AddAssociatedFQDNStatus = status == "Y" ? "Y" : "N",
                    OvAnchorOrderNumber = acmeOrgId
                };

                var logRequest3 = new VerisignGateway.AcemeAddDomainRequest
                {
                    loginName = "xxxxx",
                    loginPassword = "xxxxx",
                    action = "ADDDOMAIN",
                    EABID = AccountDetails.AcmeAccountID,
                    DomainName = domainname,
                    AddAssociatedFQDNStatus = status == "Y" ? "Y" : "N",
                    OvAnchorOrderNumber = acmeOrgId
                };

                request = "Request Object: " + JsonSerializer.Serialize(logRequest3);
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "ADDDOMAIN");

                var Response = VerisignGateway.AcemeAPIHelper.AddDomain(AddDomainRequestApi);
                if (Response.Success && Response.Domains != null)
                {
                    viewModel.AcmeOvDomains = Response.Domains
                        .Select(d => new AcemeOvDomains { domainName = d.DomainName })
                        .ToList();

                    viewModel.domainName = viewModel.AcmeOvDomains.First().domainName;

                    string response1 = "Response Object: " + JsonSerializer.Serialize(Response);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response1, "Get Insert Domain");

                    int DomainwithholdCount = BLAcme.GetCountByDomainWithHoldorNot(
                        PF_RequestObject.StoreOrderDetail.StoreOrderId, viewModel.domainName!);

                    if (DomainwithholdCount == 1)
                    {
                        result.IsSuccess = false;
                        result.Msg = "The domain name is already present in the subscription.";
                        response = "Response Object: " + JsonSerializer.Serialize(viewModel.Domains);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, result.Msg);
                        result.configurationToken = resolved.token;
                        return Ok(result);
                    }

                    int DomainCount = BLAcme.GetAdditionalDomainCount(PF_RequestObject.StoreOrderDetail.StoreOrderId);

                    var subscriptionDateRequest = new VerisignGateway.GetSubscriptionDateRequest
                    {
                        loginName = PF_RequestObject.CACredentialDetails?.UserName,
                        loginPassword = PF_RequestObject.CACredentialDetails?.Password,
                        action = "LISTTRANSACTIONS",
                        EABID = AccountDetails.AcmeAccountID,
                        DomainName = domainname
                    };

                    if (DomainCount == 0)
                    {
                        var logRequest4 = new VerisignGateway.GetSubscriptionDateRequest
                        {
                            loginName = "xxxxx",
                            loginPassword = "xxxxx",
                            action = "LISTTRANSACTIONS",
                            EABID = AccountDetails.AcmeAccountID,
                            DomainName = domainname
                        };

                        request = "Request Object: " + JsonSerializer.Serialize(logRequest4);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "LISTTRANSACTIONS");

                        var subscriptionResponse = VerisignGateway.AcemeAPIHelper.GetDomainSubscriptionsDateDetails(subscriptionDateRequest);
                        if (subscriptionResponse.ErrorCode == 0 && subscriptionResponse.subscriptions.Count > 0)
                        {
                            viewModel.SubscriptionStartDate = DateTime.Parse(subscriptionResponse.subscriptions[0].subscriptionStartDate, null, DateTimeStyles.AdjustToUniversal).Date;
                            viewModel.SubscriptionEndDate = DateTime.Parse(subscriptionResponse.subscriptions[0].subscriptionEndDate, null, DateTimeStyles.AdjustToUniversal).Date;

                            long orderNumber = 0;
                            var subscription = subscriptionResponse.subscriptions.FirstOrDefault();
                            if (subscription != null)
                            {
                                var transaction = subscription.transactions
                                    .FirstOrDefault(t => t.domains.Any(d => d.name.Equals(domainname, StringComparison.OrdinalIgnoreCase)));

                                if (transaction != null)
                                    orderNumber = transaction.orderNumber;
                            }
                            viewModel.AcmeOrderNo = orderNumber;
                            viewModel.SelectedOrgID = acmeOrgId;
                            viewModel.OrgName = acmeOrgName;
                        }
                        string response2 = "Response Object: " + JsonSerializer.Serialize(subscriptionResponse);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response2, "Get: Subscription StartDate and EndDate and AcmeorderNumber or AcmeOrgName.");

                        BLAcme.SaveAcemeOVDomainDetailsInDB(PF_RequestObject, viewModel);

                        viewModel.OrderStatus = "Completed";
                        viewModel.EabId = AccountDetails.AcmeAccountID;
                        BLAcme.SaveComodoOrderInDB(PF_RequestObject, viewModel);
                    }
                    else
                    {
                        var subscriptionResponse = VerisignGateway.AcemeAPIHelper.GetDomainSubscriptionsDateDetails(subscriptionDateRequest);
                        if (subscriptionResponse.ErrorCode == 0 && subscriptionResponse.subscriptions.Count > 0)
                        {
                            long orderNumber = 0;
                            var subscription = subscriptionResponse.subscriptions.FirstOrDefault();
                            if (subscription != null)
                            {
                                var transaction = subscription.transactions
                                    .FirstOrDefault(t => t.domains.Any(d => d.name.Equals(domainname, StringComparison.OrdinalIgnoreCase)));

                                if (transaction != null)
                                    orderNumber = transaction.orderNumber;
                            }

                            viewModel.AcmeOrderNo = orderNumber;
                            viewModel.SelectedOrgID = acmeOrgId;
                            viewModel.OrgName = acmeOrgName;
                        }
                        string response2 = "Response Object: " + JsonSerializer.Serialize(subscriptionResponse);
                        LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response2, "Get: AcmeorderNumber or AcmeOrgName Insert.");
                        BLAcme.SaveAcemeOVDomainDetailsInDB(PF_RequestObject, viewModel);
                    }

                    string successMsg = "Domain Added Successfully!";
                    viewModel.MaxWildcardSAN = BLAcme.GetMaxWildcardAllowedSAN(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                    viewModel.MaxSAN = BLAcme.GetMaxAllowedSAN(
                        PF_RequestObject.StoreOrderDetail.StoreOrderId,
                        PF_RequestObject.ProductDetail?.IsWildcardMultiDomain ?? false);
                    viewModel.MaxSanUsed = BLAcme.GetSANAllowedCount(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                    viewModel.MaxWildUsed = BLAcme.GetWildcardAllowedSANCount(PF_RequestObject.StoreOrderDetail.StoreOrderId);

                    response = "Response Object: " + JsonSerializer.Serialize(viewModel.Domains);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, successMsg);

                    result.IsSuccess = true;
                    result.Msg = successMsg;
                    result.MaxSAN = viewModel.MaxSAN;
                    result.MaxWildcardSAN = viewModel.MaxWildcardSAN;
                    result.SanUsed = viewModel.MaxSanUsed;
                    result.WildUsed = viewModel.MaxWildUsed;
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
                else
                {
                    result.IsSuccess = false;
                    result.Msg = "Failed to add domain.";
                    response = "Response Object: " + JsonSerializer.Serialize(viewModel.Domains);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, result.Msg);
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                result.IsSuccess = false;
                result.Msg = ex.Message;
                return Ok(result);
            }
        }

        /// <summary>Same as old CheckAcmeOVDomainAvailable.</summary>
        [HttpGet("CheckAcmeOVDomainAvailable")]
        public IActionResult CheckAcmeOVDomainAvailable(
            [FromQuery] string? domainName,
            [FromQuery] string? ovOrgID,
            [FromQuery] string? pin = null,
            [FromQuery] string? configurationToken = null)
        {
            var result = new AcmeJsonResponse();
            var viewModel = new ACMEDetailViewModel();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    result.IsSuccess = false;
                    result.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(result);
                }

                PF_RequestObject = resolved.request;
                result.configurationToken = resolved.token;

                int SSLApiLinkId = PF_RequestObject.StoreOrderDetail!.SSLApiLinkId;
                string Pin = PF_RequestObject.StoreOrderDetail.Pin ?? "";

                var orderDetail = BLStoreOrder.GetStoreOrderDetailByPIN(Pin.Trim());
                if (orderDetail != null && orderDetail.IsCancel)
                {
                    result.IsSuccess = false;
                    result.Msg = Resources.Val_InvalidPIN;
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }

                #region check acmeOrgId is exist or not
                var orgResponse = BLAcme.GetOrganisationDetails(
                    PF_RequestObject.StoreOrderDetail.Pin!.Trim(),
                    PF_RequestObject.StoreOrderDetail.StoreId);

                bool isMatched = orgResponse.ACMEOrganisationList != null
                    && orgResponse.ACMEOrganisationList
                        .Any(x => x.SectigoOrgID != null && x.SectigoOrgID.Trim() == ovOrgID);

                if (!isMatched)
                {
                    result.IsSuccess = false;
                    result.Msg = "The Organisation is invalid.";
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
                #endregion

                domainName ??= "";
                var isWildcard = domainName.StartsWith("*.");

                if (!IsDomainQuotaAvailable(isWildcard))
                {
                    result.IsSuccess = false;
                    result.Msg = $"Domain quota exceeded. You cannot add more {(isWildcard ? "Wildcard" : "Standard")} domains.";
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }

                var AccountDetails = BLAcme.GetAcemeDetail(PF_RequestObject.StoreOrderDetail.StoreOrderId);
                var acmeAddDomainRequest = new VerisignGateway.GetDomainAvailableRequest
                {
                    loginName = PF_RequestObject.CACredentialDetails?.UserName,
                    loginPassword = PF_RequestObject.CACredentialDetails?.Password,
                    action = "ADDDOMAIN",
                    EABID = AccountDetails!.AcmeAccountID,
                    DomainName = domainName,
                    QuoteOnly = "Y",
                    AddAssociatedFQDN = "Y",
                    OvAnchorOrderNumber = ovOrgID
                };

                var logRequest2 = new VerisignGateway.GetDomainAvailableRequest
                {
                    loginName = "xxxxx",
                    loginPassword = "xxxxx",
                    action = "ADDDOMAIN",
                    EABID = AccountDetails.AcmeAccountID,
                    DomainName = domainName,
                    QuoteOnly = "Y",
                    AddAssociatedFQDN = "Y",
                    OvAnchorOrderNumber = ovOrgID
                };

                request = "Request Object: " + JsonSerializer.Serialize(logRequest2);
                LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, request, "Check: Domain Availability");

                var Response = VerisignGateway.AcemeAPIHelper.GetDomainAvailable(acmeAddDomainRequest);

                if (Response.Success == true && Response.Domains != null)
                {
                    var alldomains = Response.Domains.Select(d => d.DomainName).ToList();
                    viewModel.domains = alldomains
                        .Select(d => new GetDomains { domainName = d })
                        .ToList();
                    if (alldomains.Count > 1)
                        viewModel.domainName = alldomains[1];
                    else
                        viewModel.domainName = alldomains[0];

                    response = "Response Object: " + JsonSerializer.Serialize(Response);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, "Get: FQDN Domain");

                    result.IsSuccess = true;
                    result.domainName = viewModel.domainName;
                    result.domains = viewModel.domains;
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
                else
                {
                    result.IsSuccess = false;
                    result.Msg = Response.ErrorDetail;
                    response = "Response Object: " + JsonSerializer.Serialize(Response);
                    LogWriter.LogAcmeAPIRequest(SSLApiLinkId, Pin, apiUrl, response, result.Msg ?? "");
                    result.configurationToken = resolved.token;
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                result.IsSuccess = false;
                result.Msg = ex.Message;
                return Ok(result);
            }
        }

        /// <summary>Same as old GetAcmeOVAccountDetailsPartial — returns model JSON.</summary>
        [HttpGet("GetAcmeOVAccountDetailsPartial")]
        public IActionResult GetAcmeOVAccountDetailsPartial(
            [FromQuery] string? pin = null,
            [FromQuery] string? configurationToken = null)
        {
            var model = new ACMEDetailViewModel();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    model.IsSuccess = false;
                    model.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(model);
                }

                PF_RequestObject = resolved.request;
                model.configurationToken = resolved.token;

                try
                {
                    model.Accounts = GetAcmeOVaccountdetails(model);
                }
                catch (Exception ex)
                {
                    LogWriter.LogErrorDetails(ex);
                    model.Msg = ex.Message;
                }

                model.IsSuccess = true;
                model.configurationToken = resolved.token;
                return Ok(model);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                model.IsSuccess = false;
                model.Msg = ex.Message;
                return Ok(model);
            }
        }

        /// <summary>Same as old GetAcmeOVDomainDetailsPartial — returns model JSON.</summary>
        [HttpGet("GetAcmeOVDomainDetailsPartial")]
        public IActionResult GetAcmeOVDomainDetailsPartial(
            [FromQuery] string? pin = null,
            [FromQuery] string? configurationToken = null)
        {
            var model = new ACMEDetailViewModel();
            try
            {
                var resolved = _authenticationService.ResolveDraft(configurationToken, pin);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    model.IsSuccess = false;
                    model.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(model);
                }

                PF_RequestObject = resolved.request;
                model.configurationToken = resolved.token;

                try
                {
                    model.AcmeOvDomains = GetAcmeOvDomainList(model);
                }
                catch (Exception ex)
                {
                    LogWriter.LogErrorDetails(ex);
                    model.Msg = ex.Message;
                    model.AcmeOvDomains = null;
                }

                model.IsSuccess = true;
                model.configurationToken = resolved.token;
                return Ok(model);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                model.IsSuccess = false;
                model.Msg = ex.Message;
                return Ok(model);
            }
        }

        /// <summary>Same as old AcmeOVAccountDetails — returns model JSON (was PartialView).</summary>
        [HttpPost("AcmeOVAccountDetails")]
        public IActionResult AcmeOVAccountDetails([FromBody] ACMEDetailViewModel viewModel)
        {
            try
            {
                var resolved = _authenticationService.ResolveDraft(viewModel?.configurationToken, null);
                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    var fail = viewModel ?? new ACMEDetailViewModel();
                    fail.IsSuccess = false;
                    fail.Msg = resolved.errorMessage ?? "Session expired.";
                    return Ok(fail);
                }

                PF_RequestObject = resolved.request;
                viewModel ??= new ACMEDetailViewModel();
                viewModel.IsSuccess = true;
                viewModel.configurationToken = resolved.token;
                return Ok(viewModel);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                viewModel ??= new ACMEDetailViewModel();
                viewModel.IsSuccess = false;
                viewModel.Msg = ex.Message;
                return Ok(viewModel);
            }
        }

        #endregion

        /// <summary>Maps gateway LastActivity (string/DateTime/object) to AccountInfo.DateTime?.</summary>
        private static DateTime? ParseLastActivity(object? value)
        {
            if (value == null)
                return null;

            if (value is DateTime dt)
                return dt;

            if (value is DateTimeOffset dto)
                return dto.UtcDateTime;

            if (DateTime.TryParse(Convert.ToString(value), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces, out var parsed))
                return parsed;

            return null;
        }
    }
}
