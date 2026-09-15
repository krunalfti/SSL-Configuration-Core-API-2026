using Microsoft.Extensions.DependencyInjection;

namespace SSLConfiguration.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddSingleton<CaptchaService>();
            services.AddSingleton<ConfigurationDraftStore>();
            services.AddSingleton<IAuthenticationService, AuthenticationService>();
            services.AddSingleton<JwtTokenService>();
            return services;
        }
    }
}
