using SSLConfiguration.Infrastructure.Persistence;
using VerisignGateway;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same as old SSLConfiguration_Models.GlobalSignOrderDetailRequest / Response (IssueCertificate subset).
    /// </summary>
    public class GlobalSignOrderDetailRequest : BaseRequest
    {
    }

    public class GlobalSignOrderDetailResponse : BaseResponse
    {
        public string? OrderStatus { get; set; }
        public string? RefundRemark { get; set; }
        public string? ApprovalEmail { get; set; }
        public GSOrderDetailInfo? GSOrderDetail { get; set; }
        public GlobalSignOrderDetail? GlobalSignOrderDetail { get; set; }
        public GlobalSignContactInfo? ContactDetails { get; set; }
        public GlobalSignContactInfo? RequestorDetails { get; set; }
        public GlobalSignContactInfo? ApprovalDetails { get; set; }
        public GlobalSignContactInfo? AuthorizedDetails { get; set; }
        public List<AdditionalDomain>? AdditionalDomainList { get; set; }
        public CSRDetail? EntityCSRDetail { get; set; }
        public GlobalSignOrganizationInfo? OrganizationDetails { get; set; }
        public GSCodeSignOrderDetailInfo? GSCodeSignOrderDetail { get; set; }
        public int StoreOrderId { get; set; }
        public string? CAContactName { get; set; }
        public string? CAContactEmail { get; set; }
        public string? Strlogo { get; set; }
        public string? DomainVerificationPage { get; set; }
        public bool? IsAllowIssueCertificate { get; set; }
        public int? CAOrderValidity { get; set; }
        public DateTime? CAOrderValidFrom { get; set; }
        public DateTime? CAOrderValidTo { get; set; }
        public DateTime? StoreOrderValidFrom { get; set; }
        public DateTime? StoreOrderValidTo { get; set; }
    }
}
