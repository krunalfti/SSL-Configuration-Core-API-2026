namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Same entity as old SSLConfiguration_DatabaseObjects.StoreMaster (callback fields).
    /// </summary>
    public partial class StoreMaster
    {
        public int StoreMasterId { get; set; }
        public string? StoreName { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public bool IsLogEnable { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string? CallBackURL { get; set; }
    }
}
