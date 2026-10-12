using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LegacyVault.Api.Exceptions;

/// <summary>
/// Bắt mọi exception KHÔNG lường trước (mất kết nối DB, bug null...) ở một chỗ duy nhất.
/// (Lỗi nghiệp vụ ApiException đã được ApiExceptionFilter xử lý trước khi tới đây.)
/// </summary>
/// <remarks>
/// Ghi log đầy đủ ở server, nhưng chỉ trả cho FE thông báo chung chung + traceId.
/// Không bao giờ gửi stack trace ra ngoài: nó lộ cấu trúc code cho kẻ tấn công.
/// </remarks>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Lỗi không xử lý được tại {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Hệ thống đang gặp sự cố. Vui lòng thử lại sau.",
                Extensions = { ["code"] = "INTERNAL_ERROR" },
            },
        });
    }
}
