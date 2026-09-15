using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Core replacement for CaptchaMvc used by old HomeController.Index POST.
    /// UI flow: call GetCaptcha → show image → send captchaId + captchaCode on Index POST.
    /// </summary>
    public class CaptchaService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CaptchaTtl = TimeSpan.FromMinutes(5);

        public CaptchaService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public CaptchaChallenge Create()
        {
            string code = GenerateCode(5);
            string captchaId = Guid.NewGuid().ToString("N");

            _cache.Set(CacheKey(captchaId), code, CaptchaTtl);

            string svg = BuildSvg(code);
            string imageBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

            return new CaptchaChallenge
            {
                CaptchaId = captchaId,
                ImageBase64 = imageBase64,
                ContentType = "image/svg+xml"
            };
        }

        /// <summary>
        /// Same idea as old IsCaptchaValid — one-time use.
        /// </summary>
        public bool IsCaptchaValid(string? captchaId, string? captchaCode)
        {
            if (string.IsNullOrWhiteSpace(captchaId) || string.IsNullOrWhiteSpace(captchaCode))
            {
                return false;
            }

            string key = CacheKey(captchaId);
            if (!_cache.TryGetValue(key, out string? expected) || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            _cache.Remove(key);

            return string.Equals(expected, captchaCode.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string CacheKey(string captchaId) => "home-captcha:" + captchaId;

        private static string GenerateCode(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = RandomNumberGenerator.GetBytes(length);
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[bytes[i] % chars.Length];
            }

            return new string(result);
        }

        private static string BuildSvg(string code)
        {
            // Simple SVG captcha image for future UI <img src="data:image/svg+xml;base64,...">
            return
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"160\" height=\"50\">" +
                $"<rect width=\"100%\" height=\"100%\" fill=\"#f3f3f3\"/>" +
                $"<text x=\"20\" y=\"34\" font-size=\"28\" font-family=\"Arial, sans-serif\" fill=\"#333\">{code}</text>" +
                $"</svg>";
        }
    }

    public class CaptchaChallenge
    {
        public string CaptchaId { get; set; } = string.Empty;
        public string ImageBase64 { get; set; } = string.Empty;
        public string ContentType { get; set; } = "image/svg+xml";
    }
}
