using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSLConfiguration.Application;
using SSLConfiguration.Contracts.SSLConfiguration.Refund;
using SSLConfiguration.Infrastructure.Persistence;

namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration
{
    /// <summary>
    /// Ported from SSLConfiguration Controllers/RefundController.
    /// Admin JWT replaces Session["AdminUserName"]. Business rules unchanged.
    /// </summary>
    [ApiController]
    [Route("api/SSLConfiguration/[controller]")]
    public class RefundController : ControllerBase
    {
        private const int ConstPageSize = 25;
        private readonly JwtTokenService _jwtTokenService;

        public RefundController(JwtTokenService jwtTokenService)
        {
            _jwtTokenService = jwtTokenService;
        }

        /// <summary>
        /// Same as old RefundController.Login POST — validates AdminUserName/AdminPassword.
        /// Route: POST /api/SSLConfiguration/Refund/Login
        /// </summary>
        [AllowAnonymous]
        [HttpPost("Login")]
        public IActionResult Login([FromBody] RefundLoginRequest? request)
        {
            var response = new RefundLoginResponse();
            try
            {
                string? userName = AppConfig.AdminUserName;
                string? password = AppConfig.AdminPassword;

                if (request == null)
                {
                    response.success = false;
                    response.message = "Invalid request.";
                    return Ok(response);
                }

                if (string.IsNullOrWhiteSpace(request.email))
                {
                    response.success = false;
                    response.message = "Email is required.";
                    return Ok(response);
                }

                if (string.IsNullOrWhiteSpace(request.password))
                {
                    response.success = false;
                    response.message = "Password is required.";
                    return Ok(response);
                }

                if (userName == request.email && password == request.password)
                {
                    response.success = true;
                    response.accessToken = _jwtTokenService.CreateAdminToken(userName!);
                    response.message = string.Empty;
                    return Ok(response);
                }

                response.success = false;
                response.message = "Invalid username or password.";
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.success = false;
                response.message = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old Refund Index list/search (GetOrderDataUsingPaging + AdminGet list).
        /// Route: POST /api/SSLConfiguration/Refund/Index
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("Index")]
        public IActionResult Index([FromBody] RefundIndexRequest? request)
        {
            var response = new RefundIndexResponse();
            try
            {
                request ??= new RefundIndexRequest();
                int page = request.page <= 0 ? 1 : request.page;
                int pageSize = request.pageSize <= 0 ? ConstPageSize : request.pageSize;

                PagedMetadata pageMeta;
                var list = BLRefund.AdminGetSectigoRefundPaymentStatusList(
                    request.apiOrderNo,
                    request.refundStatus,
                    request.startDate,
                    request.endDate,
                    page,
                    pageSize,
                    out pageMeta);

                response.success = true;
                response.items = MapItems(list);
                response.pageMeta = MapPageMeta(pageMeta);
                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.success = false;
                response.message = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old RefundController.GetRefundStatus (single order refresh).
        /// Route: POST /api/SSLConfiguration/Refund/GetRefundStatus
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("GetRefundStatus")]
        public IActionResult GetRefundStatus([FromBody] GetRefundStatusRequest? request)
        {
            var response = new GetRefundStatusResponse();
            try
            {
                if (request == null)
                {
                    response.success = false;
                    response.message = "Invalid request.";
                    return Ok(response);
                }

                var result = BLRefund.GetSectigoPostPaymentStatus(request.storeOrderId, request.apiOrderNo ?? string.Empty);

                if (string.IsNullOrWhiteSpace(result.errorMessage) && result.errorCode == 0 && result.data != null)
                {
                    response.success = true;
                    response.message = "Record fetch successfully.";
                    response.refundStatus = result.data.orderStatus;
                    response.errorCode = result.errorCode;
                }
                else
                {
                    response.success = false;
                    response.message = result.errorMessage;
                    response.errorCode = result.errorCode;
                    response.refundStatus = result.data?.orderStatus;
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.success = false;
                response.message = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old Index "Get All Status" / GetAllRefundStatus — refresh non-Refunded rows.
        /// Route: POST /api/SSLConfiguration/Refund/GetAllRefundStatus
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("GetAllRefundStatus")]
        public IActionResult GetAllRefundStatus([FromBody] GetAllRefundStatusRequest? request)
        {
            var response = new GetAllRefundStatusResponse();
            try
            {
                request ??= new GetAllRefundStatusRequest();
                bool hasError = false;
                int page = request.page <= 0 ? 1 : request.page;
                int pagedSize = request.pageSize <= 0 ? int.MaxValue : request.pageSize;
                List<string> errorMessages = new List<string>();

                PagedMetadata pageMeta;
                var refundStatusList = BLRefund.AdminGetSectigoRefundPaymentStatusList(
                        request.apiOrderNo,
                        request.refundStatus,
                        request.startDate,
                        request.endDate,
                        page,
                        pagedSize,
                        out pageMeta)
                    ?.Where(x => x.RefundStatus != "Refunded")
                    .ToList();

                if (refundStatusList != null && refundStatusList.Any())
                {
                    foreach (var item in refundStatusList)
                    {
                        var result = BLRefund.GetSectigoPostPaymentStatus(
                            Convert.ToInt32(item.StoreOrderId),
                            item.ApiOrderNo ?? string.Empty);

                        if (result == null || result.errorCode != 0 || result.data == null)
                        {
                            hasError = true;
                            var msg = !string.IsNullOrWhiteSpace(result?.errorMessage)
                                ? result!.errorMessage
                                : $"Failed for Order: {item.ApiOrderNo}";
                            errorMessages.Add(msg);
                        }
                    }

                    response.processedCount = refundStatusList.Count;
                    if (hasError)
                    {
                        response.success = false;
                        response.message = errorMessages.Any()
                            ? string.Join(" | ", errorMessages.Distinct())
                            : "Some records failed to fetch.";
                    }
                    else
                    {
                        response.success = true;
                        response.message = $"All {refundStatusList.Count} records have been fetched successfully.";
                    }
                }
                else
                {
                    response.success = true;
                    response.processedCount = 0;
                    response.message = "No records found for get status.";
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                response.success = false;
                response.message = ex.Message;
                return Ok(response);
            }
        }

        /// <summary>
        /// Same as old Index "Download CSV" button.
        /// Route: POST /api/SSLConfiguration/Refund/DownloadCsv
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("DownloadCsv")]
        public IActionResult DownloadCsv([FromBody] DownloadCsvRequest? request)
        {
            try
            {
                request ??= new DownloadCsvRequest();
                PagedMetadata pageMeta;
                var list = BLRefund.AdminGetSectigoRefundPaymentStatusList(
                    request.apiOrderNo,
                    request.refundStatus,
                    request.startDate,
                    request.endDate,
                    1,
                    int.MaxValue,
                    out pageMeta);

                var storeNames = new Dictionary<int, string>
                {
                    { 1, "Click SSL" },
                    { 2, "SSL2 BUY" },
                    { 3, "Cheap SSLShop" },
                    { 5, "S2B Reseller" }
                };

                var sb = new StringBuilder();
                sb.AppendLine("StoreName,ApiOrderNo,CredentialCode,RefundStatus");

                if (list != null && list.Count > 0)
                {
                    foreach (var item in list)
                    {
                        string storename;
                        if (!item.StoreId.HasValue || !storeNames.TryGetValue((int)item.StoreId, out storename!))
                            storename = "-";

                        sb.AppendLine($"{storename},{item.ApiOrderNo},{item.CredentialCode},{item.RefundStatus}");
                    }
                }

                byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
                string fileName = "ComodoRefundStatus_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
                return File(buffer, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                return Ok(new { success = false, message = ex.Message });
            }
        }

        private static List<RefundListItemDto> MapItems(List<AdminGetSectigoRefundPaymentStatusList_SP_Result>? list)
        {
            if (list == null)
                return new List<RefundListItemDto>();

            return list.Select(x => new RefundListItemDto
            {
                rowNumber = x.RowNumber,
                sectigoRefundPaymentId = x.SectigoRefundPaymentId,
                storeId = x.StoreId,
                storeOrderId = x.StoreOrderId,
                apiOrderNo = x.ApiOrderNo,
                refundStatus = x.RefundStatus,
                createdDate = x.CreatedDate,
                updatedDate = x.UpdatedDate,
                credentialCode = x.CredentialCode,
                totalRecords = x.TotalRecords
            }).ToList();
        }

        private static PagedMetadataDto MapPageMeta(PagedMetadata pageMeta)
        {
            return new PagedMetadataDto
            {
                pageCount = pageMeta.PageCount,
                totalItemCount = pageMeta.TotalItemCount,
                pageNumber = pageMeta.PageNumber,
                pageSize = pageMeta.PageSize,
                hasPreviousPage = pageMeta.HasPreviousPage,
                hasNextPage = pageMeta.HasNextPage
            };
        }
    }
}
