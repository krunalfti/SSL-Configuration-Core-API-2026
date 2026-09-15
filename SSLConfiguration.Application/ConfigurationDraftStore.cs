using Microsoft.Extensions.Caching.Memory;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Short-lived wizard draft store replacing MVC Session[PF_Request]
    /// for Digicert / Comodo / GlobalSign configuration APIs.
    /// </summary>
    public class ConfigurationDraftStore
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan DraftTtl = TimeSpan.FromHours(2);

        public ConfigurationDraftStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        public string Save(PF_Request request)
        {
            string token = Guid.NewGuid().ToString("N");
            _cache.Set(CacheKey(token), request, DraftTtl);
            return token;
        }

        public void Update(string configurationToken, PF_Request request)
        {
            _cache.Set(CacheKey(configurationToken), request, DraftTtl);
        }

        public PF_Request? Get(string? configurationToken)
        {
            if (string.IsNullOrWhiteSpace(configurationToken))
            {
                return null;
            }

            return _cache.TryGetValue(CacheKey(configurationToken), out PF_Request? request)
                ? request
                : null;
        }

        public void Remove(string? configurationToken)
        {
            if (!string.IsNullOrWhiteSpace(configurationToken))
            {
                _cache.Remove(CacheKey(configurationToken));
                _cache.Remove(PlaceOrderCacheKey(configurationToken));
            }
        }

        /// <summary>Same role as old Session[PF_Response] for Thanks after PlaceOrder.</summary>
        public void SavePlaceOrderResponse(string configurationToken, PF_Response response)
        {
            _cache.Set(PlaceOrderCacheKey(configurationToken), response, DraftTtl);
        }

        public PF_Response? GetPlaceOrderResponse(string? configurationToken)
        {
            if (string.IsNullOrWhiteSpace(configurationToken))
                return null;

            return _cache.TryGetValue(PlaceOrderCacheKey(configurationToken), out PF_Response? response)
                ? response
                : null;
        }

        private static string CacheKey(string token) => "pf-request:" + token;
        private static string PlaceOrderCacheKey(string token) => "pf-response:" + token;
    }
}
