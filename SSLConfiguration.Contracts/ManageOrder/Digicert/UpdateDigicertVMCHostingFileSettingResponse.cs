namespace SSLConfiguration.Contracts.ManageOrder.Digicert
{
    /// <summary>
    /// Response contract matching old ManageOrder JSON shape.
    /// </summary>
    public class UpdateDigicertVMCHostingFileSettingResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
    }
}
