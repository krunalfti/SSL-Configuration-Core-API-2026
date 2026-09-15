using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSLConfiguration.Infrastructure.Persistence
{
    public partial class SectigoRefundPaymentStatu
    {
        public int SectigoRefundPaymentId { get; set; }
        public int StoreOrderId { get; set; }
        public string? ApiOrderNo { get; set; }
        public string? RefundStatus { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string? CredentialCode { get; set; }
        public int? StoreId { get; set; }
    }
}
