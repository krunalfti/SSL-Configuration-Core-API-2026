namespace SSLConfiguration.Contracts.ManageOrder.Digicert
{
    /// <summary>
    /// Request contract for ManageOrder DigicertController.UpdateDigicertVMCHostingFileSetting.
    /// </summary>
    public class UpdateDigicertVMCHostingFileSettingRequest
    {
        public string CAOrderNo { get; set; } = string.Empty;
        public bool EnableDigicertHost { get; set; }
        public int StoreOrderId { get; set; }
    }
}
