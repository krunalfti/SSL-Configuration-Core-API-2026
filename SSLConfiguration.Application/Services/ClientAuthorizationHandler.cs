using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SSLConfiguration.Application.Services;
using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Application
{
    public class ClientAuthorizationHandler : AuthorizationHandler<ClientAuthorizationRequirement>
    {
        private readonly SSLConfigurationEntities _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ClientAuthorizationHandler(SSLConfigurationEntities db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ClientAuthorizationRequirement requirement)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            try
            {
                if (httpContext == null)
                {
                    LogWriter.LogAPICall("ClientAuth FAIL => HttpContext is null");
                    return;
                }
                var requestPath = httpContext.Request.Method + " " + httpContext.Request.Path + httpContext.Request.QueryString;
                var requestIp = httpContext.Connection.RemoteIpAddress?.ToString();
                LogWriter.LogAPICall("ClientAuth START => " + requestPath + " | IP: " + requestIp);
                // --------------------------------------------------
                // 1. Read GUID from header
                // --------------------------------------------------
                var guidHeader = httpContext.Request.Headers["X-Client-Guid"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(guidHeader))
                {
                    LogWriter.LogAPICall("ClientAuth FAIL => Missing X-Client-Guid header | " + requestPath);
                    return;
                }
                // --------------------------------------------------
                // 2. Validate GUID
                // --------------------------------------------------
                if (!Guid.TryParse(guidHeader, out Guid clientGuid))
                {
                    LogWriter.LogAPICall("ClientAuth FAIL => Invalid GUID format: " + guidHeader + " | " + requestPath);
                    return;
                }
                // --------------------------------------------------
                // 3. Find client in database
                // --------------------------------------------------            
                LogWriter.LogAPICall("ClientAuth DB START => GUID: " + clientGuid);
                var client = await _db.ApiClients.AsNoTracking().FirstOrDefaultAsync(x => x.ClientGuid == clientGuid && x.IsActive);
                LogWriter.LogAPICall("ClientAuth DB END => Client Found: " + (client != null));
                if (client == null)
                {
                    LogWriter.LogAPICall("ClientAuth FAIL => Client not found or inactive. GUID: " + clientGuid + " | " + requestPath);
                    return;
                }
                // --------------------------------------------------
                // 4. Get domain
                // --------------------------------------------------
                var host = httpContext.Request.Headers["X-Client-Domain"].FirstOrDefault();
                // --------------------------------------------------
                // 5. Validate domain
                // --------------------------------------------------
                if (!string.Equals(host, client.DomainName, StringComparison.OrdinalIgnoreCase))
                {
                    LogWriter.LogAPICall("ClientAuth FAIL => Domain mismatch. Header: [" + host + "] Expected: [" + client.DomainName + "] | GUID: " + clientGuid + " | " + requestPath);
                    return;
                }
                // --------------------------------------------------
                // 6. Validate IP if configured
                // --------------------------------------------------
                if (!string.IsNullOrWhiteSpace(client.IpAddress))
                {
                    if (!string.Equals(requestIp, client.IpAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        LogWriter.LogAPICall(
                            "ClientAuth FAIL => IP mismatch. Request IP: [" + requestIp + "] Expected: [" + client.IpAddress + "] | GUID: " + clientGuid + " | " + requestPath);
                        return;
                    }
                }
                // --------------------------------------------------
                // 7. Everything is valid
                // --------------------------------------------------
                LogWriter.LogAPICall("ClientAuth SUCCESS => GUID: " + clientGuid + " | Domain: " + host + " | IP: " + requestIp + " | " + requestPath);
                context.Succeed(requirement);
            }
            catch (Exception ex)
            {
                LogWriter.LogAPICall("ClientAuth DB EXCEPTION => " + ex.ToString());
                throw;
            }
        }
    }
}