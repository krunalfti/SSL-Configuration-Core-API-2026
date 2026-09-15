using System.Text.Json;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same behavior as old SSLConfiguration_Translation.TranslateUtility (Google Translate API).
    /// </summary>
    public static class TranslateUtility
    {
        public static string Translate(string sourceLanguage, string targetLanguage, string msgToTranslate)
        {
            if (string.IsNullOrWhiteSpace(msgToTranslate))
                return msgToTranslate ?? string.Empty;

            targetLanguage = CultureHelper.Normalize(targetLanguage);
            if (targetLanguage.Equals("en", StringComparison.OrdinalIgnoreCase))
                return msgToTranslate;

            try
            {
                string? apiUrl = AppConfig.GoogleTranslateApiUrl;
                string? apiKey = AppConfig.GoogleTranslateApiKey;
                if (string.IsNullOrWhiteSpace(apiUrl) || string.IsNullOrWhiteSpace(apiKey))
                    return msgToTranslate;

                string url =
                    apiUrl
                    + "?key=" + Uri.EscapeDataString(apiKey)
                    + "&source=" + Uri.EscapeDataString(sourceLanguage ?? "en")
                    + "&target=" + Uri.EscapeDataString(targetLanguage)
                    + "&q=" + Uri.EscapeDataString(msgToTranslate);

                using var client = new HttpClient();
                string json = client.GetStringAsync(url).GetAwaiter().GetResult();

                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement translated = doc.RootElement
                    .GetProperty("data")
                    .GetProperty("translations")[0]
                    .GetProperty("translatedText");

                return translated.GetString() ?? msgToTranslate;
            }
            catch
            {
                return msgToTranslate;
            }
        }
    }
}
