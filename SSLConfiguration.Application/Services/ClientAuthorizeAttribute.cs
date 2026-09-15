using Microsoft.AspNetCore.Authorization;

namespace SSLConfiguration.Application.Services
{
    public class ClientAuthorizeAttribute : AuthorizeAttribute
    {
        public ClientAuthorizeAttribute()
        {
            Policy = "ClientAuthorization";
        }
    }
}
