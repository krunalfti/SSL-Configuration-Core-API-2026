using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSLConfiguration.Infrastructure.Persistence
{
    public class AcmeSubcriptonError
    {
        public int AcmeSubcriptonErrorId { get; set; }
        public string? OrderNumber { get; set; }
        public int ExtensionDurationDays { get; set; }
        public int StoreId { get; set; }
        public int? StoreOrderId { get; set; }
        public string? RequestJson { get; set; }
        public int RetryCount { get; set; }
        public bool IsProcessed { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
