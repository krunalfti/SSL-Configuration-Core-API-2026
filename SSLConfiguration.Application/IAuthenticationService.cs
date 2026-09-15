using SSLConfiguration.Contracts.SSLConfiguration.Digicert;

namespace SSLConfiguration.Application
{
    public interface IAuthenticationService
    {
        DigicertEntryResponse Entry(
            string authenticationType,
            string? pin,
            string? configurationToken);

        /// <summary>
        /// Resolves draft by configurationToken or creates one from pin (shared across Digicert/Comodo/GlobalSign/etc).
        /// </summary>
        (bool ok, string? token, PF_Request? request, string? errorMessage) ResolveDraft(
            string? configurationToken,
            string? pin);
    }
}
