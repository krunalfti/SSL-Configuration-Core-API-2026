using System.Globalization;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Replaces old BaseController Session CurrentCulture for Core API requests.
    /// </summary>
    public static class CultureHelper
    {
        private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
        {
            "en", "zh", "fr", "de", "hu", "pl", "es"
        };

        public static string Normalize(string? culture)
        {
            culture = (culture ?? "en").Trim();
            if (culture.Length == 0)
                return "en";

            // Accept "zh-CN" → "zh"
            string primary = culture.Split('-', '_')[0];
            return Supported.Contains(primary) ? primary.ToLowerInvariant() : "en";
        }

        public static void Apply(string? culture)
        {
            var ci = new CultureInfo(Normalize(culture));
            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;
        }
    }
}
