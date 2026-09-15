using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSLConfiguration.Contracts.SSLConfiguration.Refund
{
    public class DownloadCsvRequest
    {
        public string? apiOrderNo { get; set; }
        public string? refundStatus { get; set; }
        public DateTime? startDate { get; set; }
        public DateTime? endDate { get; set; }
    }
    public class GetAllRefundStatusRequest
    {
        public string? apiOrderNo { get; set; }
        public string? refundStatus { get; set; }
        public DateTime? startDate { get; set; }
        public DateTime? endDate { get; set; }
        public int page { get; set; } = 1;
        /// <summary>0 or less = int.MaxValue (same as old GetAllRefundStatus when no session page size).</summary>
        public int pageSize { get; set; } = 0;
    }
    public class GetAllRefundStatusResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public int processedCount { get; set; }
    }
    public class GetRefundStatusRequest
    {
        public int storeOrderId { get; set; }
        public string? apiOrderNo { get; set; }
    }
    public class GetRefundStatusResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public string? refundStatus { get; set; }
        public int errorCode { get; set; }
    }
    public class PagedMetadataDto
    {
        public int pageCount { get; set; }
        public int totalItemCount { get; set; }
        public int pageNumber { get; set; }
        public int pageSize { get; set; }
        public bool hasPreviousPage { get; set; }
        public bool hasNextPage { get; set; }
    }
    public class RefundIndexRequest
    {
        public string? apiOrderNo { get; set; }
        public string? refundStatus { get; set; }
        public DateTime? startDate { get; set; }
        public DateTime? endDate { get; set; }
        public int page { get; set; } = 1;
        public int pageSize { get; set; } = 25;
    }
    public class RefundIndexResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public List<RefundListItemDto>? items { get; set; }
        public PagedMetadataDto? pageMeta { get; set; }
    }
    public class RefundListItemDto
    {
        public long? rowNumber { get; set; }
        public int sectigoRefundPaymentId { get; set; }
        public int? storeId { get; set; }
        public int storeOrderId { get; set; }
        public string? apiOrderNo { get; set; }
        public string? refundStatus { get; set; }
        public DateTime createdDate { get; set; }
        public DateTime? updatedDate { get; set; }
        public string? credentialCode { get; set; }
        public int? totalRecords { get; set; }
    }
    public class RefundLoginRequest
    {
        public string? email { get; set; }
        public string? password { get; set; }
    }
    public class RefundLoginResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public string? accessToken { get; set; }
    }
}
