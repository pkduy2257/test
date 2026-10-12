using LegacyVault.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace LegacyVault.Api.Exceptions;

/// <summary>
/// Đổi mọi ApiException (lỗi nghiệp vụ) thành JSON lỗi, ở MỘT chỗ duy nhất.
/// Giống @ControllerAdvice + @ExceptionHandler bên Spring Boot.
/// </summary>
/// <remarks>
/// Ví dụ JSON trả về:
/// { "status": 401, "title": "Email hoặc mật khẩu chưa đúng.", "code": "INVALID_CREDENTIALS", "attemptsRemaining": 3 }
///
/// Vì sao dùng filter của MVC mà không để GlobalExceptionHandler xử lý luôn?
/// GlobalExceptionHandler là middleware chạy SAU khi MVC đã bỏ cuộc: nó xóa sạch header
/// của response, kể cả lệnh xóa cookie mà controller vừa ghi (xem AuthController.Refresh).
/// Filter chạy bên trong MVC nên giữ nguyên các header đó.
/// Exception không phải ApiException (bug, mất DB...) vẫn đi tiếp tới GlobalExceptionHandler.
/// </remarks>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public ApiExceptionFilter(ProblemDetailsFactory problemDetailsFactory)
    {
        _problemDetailsFactory = problemDetailsFactory;
    }

    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception as ApiException;
        if (exception == null)
        {
            return; // không phải lỗi nghiệp vụ → để GlobalExceptionHandler xử lý
        }

        // Factory của ASP.NET tự thêm "type" và "traceId" (để tra log khi có sự cố).
        var problem = _problemDetailsFactory.CreateProblemDetails(
            context.HttpContext, exception.StatusCode, title: exception.Message);

        problem.Extensions["code"] = exception.ErrorCode;
        foreach (var item in exception.ExtraData)
        {
            problem.Extensions[item.Key] = item.Value;
        }

        context.Result = new ObjectResult(problem)
        {
            StatusCode = exception.StatusCode,
            // Giữ "application/json" giống response lỗi trước đây (FE đang nhận đúng kiểu này).
            ContentTypes = { "application/json" },
        };
        context.ExceptionHandled = true;
    }
}
