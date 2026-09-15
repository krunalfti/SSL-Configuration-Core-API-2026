using SSLConfiguration.Contracts.SSLConfiguration;
using SSLConfiguration.Contracts.SSLConfiguration.Digicert;


namespace SSLConfiguration.Application
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly ConfigurationDraftStore _draftStore;
        public AuthenticationService(ConfigurationDraftStore draftStore )
        {
            _draftStore = draftStore;
        }

        public DigicertEntryResponse Entry(string authenticationType, string? pin, string? configurationToken)
        {
            var response = new DigicertEntryResponse
            {
                authenticationType = authenticationType
            };
            try
            {
                var resolved = ResolveDraft(configurationToken, pin);

                if (!resolved.ok || resolved.request == null || string.IsNullOrEmpty(resolved.token))
                {
                    response.IsSuccess = false;
                    response.Msg = resolved.errorMessage ?? "Session expired.";

                    return response;
                }
                PF_Request pf = resolved.request;
                response.IsSuccess = true;
                response.configurationToken = resolved.token;
                response.storeOrderId = pf.StoreOrderDetail?.StoreOrderId;
                response.productId = pf.ProductDetail?.ProductId;
                response.isWildcard = pf.ProductDetail?.IsWildcard;
                response.isMultiDomain = pf.ProductDetail?.IsMultiDomain;
                response.isX9 = pf.ProductDetail?.IsX9;
                response.isFreeSANInclude = pf.DigicertOrderRequest?.IsFreeSANInclude;                
                return response;
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.IsSuccess = false;
                response.Msg = ex.Message;
                return response;
            }
        }
        public (bool ok, string? token, PF_Request? request, string? errorMessage) ResolveDraft(string? configurationToken, string? pin)
        {
            if (!string.IsNullOrWhiteSpace(configurationToken))
            {
                PF_Request? existing = _draftStore.Get(configurationToken);

                if (existing != null)
                {
                    CultureHelper.Apply(existing.LanguageCode);
                    return (true, configurationToken, existing, null);
                }

                return (false, null, null, "Session expired.");
            }

            if (!string.IsNullOrWhiteSpace(pin))
            {
                PF_Request? created = PF_RequestFactory.CreateFromPin(pin);

                if (created == null)
                {
                    return (false, null, null, Resources.Val_InvalidPIN);
                }

                CultureHelper.Apply(created.LanguageCode);
                string token = _draftStore.Save(created);

                return (true, token, created, null);
            }

            return (false, null, null, "configurationToken or pin is required.");
        }
    }
}