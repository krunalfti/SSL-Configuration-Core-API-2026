namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same as old SSLConfiguration_Models.BaseRequest / BaseResponse / Errors (subset).
    /// </summary>
    public class BaseRequest
    {
        public int StoreId { get; set; }
        public string? Pin { get; set; }
        public int SSLApiLinkId { get; set; }
        public string? AuthKey { get; set; }
    }

    public class Errors
    {
        public int ErrorNumber;
        public string? ErrorField;
        public string? ErrorMessage;
    }

    public class BaseResponse
    {
        public Errors ErrorDetail;
        public int StatusCode { get; set; }
        public string? CredentialCode { get; set; }

        public BaseResponse()
        {
            ErrorDetail = new Errors();
            ErrorDetail.ErrorField = string.Empty;
            ErrorDetail.ErrorMessage = string.Empty;
        }
    }
}
