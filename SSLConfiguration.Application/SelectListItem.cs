namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same shape as System.Web.Mvc.SelectListItem / AspNetCore SelectListItem for approval email lists.
    /// </summary>
    public class SelectListItem
    {
        public string? Value { get; set; }
        public string? Text { get; set; }
        public bool Selected { get; set; }
    }
}
