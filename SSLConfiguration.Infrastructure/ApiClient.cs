using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSLConfiguration.Infrastructure
{
    public class ApiClient
    {
        public int Id { get; set; }

        public Guid ClientGuid { get; set; }

        public string DomainName { get; set; } = string.Empty;

        public string? IpAddress { get; set; }

        public bool IsActive { get; set; }
    }
}
