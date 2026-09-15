namespace SSLConfiguration.Application
{
    /// <summary>
    /// Core-adjusted LogWriter (same method names as old SSLConfiguration_CommonUtility.LogWriter).
    /// Old code used System.Web HttpContext.Current.Server.MapPath — adapted for ASP.NET Core paths.
    /// </summary>
    public static class LogWriter
    {
        public static string ContentRoot { get; set; } = AppContext.BaseDirectory;
        public static Func<string?>? GetClientIp { get; set; }

        public static void LogError(string message)
        {
            try
            {
                string logDirectoryPath = Path.Combine(ContentRoot, "ErrorLog");
                if (!Directory.Exists(logDirectoryPath))
                    Directory.CreateDirectory(logDirectoryPath);

                string logFileName = Path.Combine(logDirectoryPath, Guid.NewGuid().ToString() + ".txt");
                File.AppendAllText(logFileName, "DateTime : " + DateTime.Now + Environment.NewLine + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        public static void LogErrorDetails(Exception ex)
        {
            string logDirectoryPath = Path.Combine(ContentRoot, "ErrorLog");
            string logFileName = Path.Combine(logDirectoryPath, Guid.NewGuid().ToString() + ".txt");

            if (!Directory.Exists(logDirectoryPath))
            {
                Directory.CreateDirectory(logDirectoryPath);
            }

            try
            {
                File.AppendAllText(
                    logFileName,
                    "DateTime : " + DateTime.Now + Environment.NewLine +
                    "Message : " + ex.Message + Environment.NewLine +
                    "StackTrace : " + ex.StackTrace + Environment.NewLine +
                    (ex.InnerException != null ? "InnerException : " + ex.InnerException.Message + Environment.NewLine : string.Empty));
            }
            catch
            {
            }
        }

        public static void LogStoreOrderIPAddress(int StoreOrderId)
        {
            string logDirectoryPath = Path.Combine(ContentRoot, "PinHistory");
            string logFileName = Path.Combine(logDirectoryPath, StoreOrderId + ".txt");

            if (!Directory.Exists(logDirectoryPath))
            {
                Directory.CreateDirectory(logDirectoryPath);
            }

            try
            {
                string ip = GetClientIp?.Invoke() ?? string.Empty;
                File.AppendAllText(
                    logFileName,
                    "DateTime : " + DateTime.Now + " | IP Address : " + ip + Environment.NewLine);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Same method name as old LogWriter.LogCARequestResponseObjectToDB (file/DB logging deferred; keep call sites).
        /// </summary>
        public static void LogCARequestResponseObjectToDB(string pin, object? requestObject, string response, string actionName)
        {
            try
            {
                string logDirectoryPath = Path.Combine(ContentRoot, "CARequestLog");
                if (!Directory.Exists(logDirectoryPath))
                {
                    Directory.CreateDirectory(logDirectoryPath);
                }

                string logFileName = Path.Combine(logDirectoryPath, (string.IsNullOrWhiteSpace(pin) ? "unknown" : pin) + "-" + actionName + ".txt");
                File.AppendAllText(
                    logFileName,
                    "DateTime : " + DateTime.Now + Environment.NewLine +
                    "Action : " + actionName + Environment.NewLine +
                    "Response : " + response + Environment.NewLine);
            }
            catch
            {
            }
        }

        public static void LogCARequestResponseObjectToDB(string? pin, object? caResponse)
        {
            LogCARequestResponseObjectToDB(pin ?? string.Empty, caResponse, caResponse?.ToString() ?? string.Empty, "DigicertPlaceOrder");
        }

        public static void LogDigicertRequestObjects(int sslApiLinkId, string? pin, string requestJson)
        {
            try
            {
                string logDirectoryPath = Path.Combine(ContentRoot, "DigicertRequestLog");
                if (!Directory.Exists(logDirectoryPath))
                    Directory.CreateDirectory(logDirectoryPath);

                string logFileName = Path.Combine(logDirectoryPath, sslApiLinkId + "-" + (pin ?? "unknown") + ".txt");
                File.AppendAllText(logFileName, "DateTime : " + DateTime.Now + Environment.NewLine + requestJson + Environment.NewLine);
            }
            catch
            {
            }
        }

        public static void LogDigicertOrderRequestObjectsToDB(PF_Request objPFRequest)
        {
            try
            {
                LogDigicertRequestObjects(
                    objPFRequest.StoreOrderDetail?.SSLApiLinkId ?? 0,
                    objPFRequest.StoreOrderDetail?.Pin,
                    "PlaceOrder error snapshot");
            }
            catch
            {
            }
        }

        public static void LogComodoOrderRequestObjectsToDB(PF_Request objPFRequest)
        {
            try
            {
                string logDirectoryPath = Path.Combine(ContentRoot, "ComodoRequestLog");
                if (!Directory.Exists(logDirectoryPath))
                    Directory.CreateDirectory(logDirectoryPath);

                string pin = objPFRequest.StoreOrderDetail?.Pin ?? "unknown";
                string logFileName = Path.Combine(logDirectoryPath, (objPFRequest.StoreOrderDetail?.SSLApiLinkId ?? 0) + "-" + pin + ".txt");
                File.AppendAllText(logFileName, "DateTime : " + DateTime.Now + Environment.NewLine + "PlaceOrder error snapshot" + Environment.NewLine);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Same method name as old LogWriter.LogGlobalSignOrderRequestObjectsToDB (file snapshot; DB logging deferred).
        /// </summary>
        public static void LogGlobalSignOrderRequestObjectsToDB(PF_Request objPFRequest)
        {
            try
            {
                string logDirectoryPath = Path.Combine(ContentRoot, "GlobalSignRequestLog");
                if (!Directory.Exists(logDirectoryPath))
                    Directory.CreateDirectory(logDirectoryPath);

                string pin = objPFRequest.StoreOrderDetail?.Pin ?? "unknown";
                string logFileName = Path.Combine(logDirectoryPath, (objPFRequest.StoreOrderDetail?.SSLApiLinkId ?? 0) + "-" + pin + ".txt");
                File.AppendAllText(logFileName, "DateTime : " + DateTime.Now + Environment.NewLine + "PlaceOrder error snapshot" + Environment.NewLine);
            }
            catch
            {
            }
        }

        public static void LogComodoCodeSignAuthorizationError(string? errorMessage, int storeOrderId)
        {
            try
            {
                string logDirectoryPath = Path.Combine(ContentRoot, "ComodoCodeSignAuthLog");
                if (!Directory.Exists(logDirectoryPath))
                    Directory.CreateDirectory(logDirectoryPath);

                string logFileName = Path.Combine(logDirectoryPath, storeOrderId + ".txt");
                File.AppendAllText(logFileName, "DateTime : " + DateTime.Now + Environment.NewLine + (errorMessage ?? string.Empty) + Environment.NewLine);
            }
            catch
            {
            }
        }

        public static void LogRequestResponseObjectToDB(PF_Request objPFRequest, Exception ex)
        {
            LogErrorDetails(ex);
        }

        public static void LogHandlerError(string? storeName, int sslApiLinkId, string message)
        {
            try
            {
                string logDirectoryPath = Path.Combine(ContentRoot, "HandlerError");
                if (!Directory.Exists(logDirectoryPath))
                    Directory.CreateDirectory(logDirectoryPath);

                string logFileName = Path.Combine(logDirectoryPath, (storeName ?? "store") + "-" + sslApiLinkId + ".txt");
                File.AppendAllText(logFileName, "DateTime : " + DateTime.Now + " | " + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        /// <summary>Same as old LogWriter.LogAcmeAPIRequest (file log under AcmeRequestLog).</summary>
        public static void LogAcmeAPIRequest(int sslapiLinkId, string pin, string apiUrl, string objrequest, string logMessage)
        {
            try
            {
                string logDirectoryPath = Path.Combine(ContentRoot, "AcmeRequestLog");
                if (!Directory.Exists(logDirectoryPath))
                    Directory.CreateDirectory(logDirectoryPath);

                string logFileName = Path.Combine(logDirectoryPath, sslapiLinkId + "-" + pin + ".txt");
                File.AppendAllText(
                    logFileName,
                    "---------------------------------------------------------------------------------------------------" + Environment.NewLine
                    + "DateTime : " + DateTime.Now + Environment.NewLine
                    + "IP Address : " + (GetClientIp?.Invoke() ?? string.Empty) + Environment.NewLine
                    + "API Url: " + apiUrl + Environment.NewLine
                    + objrequest + Environment.NewLine
                    + "Message : " + logMessage + Environment.NewLine);
            }
            catch
            {
            }
        }
        public static void LogAPICall(string apiCallMessage)
        {
            try
            {
                string logDirectoryPath = Path.Combine(ContentRoot, "APICallLog");
                if (!Directory.Exists(logDirectoryPath))
                    Directory.CreateDirectory(logDirectoryPath);
                // One file per day (easier to read than GUID per call)
                string logFileName = Path.Combine(logDirectoryPath, "ClientAuth_" + DateTime.Now.ToString("dd-MM-yyyy") + ".txt");
                File.AppendAllText(
                    logFileName,
                    "---------------------------------------------------------------------------------------------------" + Environment.NewLine +
                    "DateTime : " + DateTime.Now + Environment.NewLine +
                    "API Call Message: " + apiCallMessage + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
