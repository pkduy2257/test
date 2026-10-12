using System.Text.Json;
using LegacyVault.Api.Exceptions;
using LegacyVault.Api.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LegacyVault.Api.Config;

/// <summary>
/// Đăng ký những thứ thuộc tầng HTTP: controller, chuẩn lỗi, ghi cookie.
/// </summary>
public static class ApiConfig
{
    public static IServiceCollection AddApiConfig(this IServiceCollection services)
    {
        // ApiExceptionFilter: đổi lỗi nghiệp vụ (ApiException) thành JSON lỗi cho mọi controller.
        services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>())
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = CreateValidationErrorResponse;
            });

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddScoped<AuthCookieWriter>();

        return services;
    }

    /// <summary>
    /// Khi DTO sai (thiếu email...), [ApiController] tự trả 400 bằng hàm này.
    /// Ta thêm "code" để lỗi validate cũng theo đúng chuẩn chung của nhóm.
    /// </summary>
    private static IActionResult CreateValidationErrorResponse(ActionContext context)
    {
        // Đổi tên trường "Email" → "email" cho khớp tên field bên FE.
        var errors = context.ModelState
            .Where(entry => entry.Value != null && entry.Value.Errors.Count > 0)
            .ToDictionary(
                entry => JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dữ liệu gửi lên không hợp lệ.",
        };
        problem.Extensions["code"] = "VALIDATION_ERROR";

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }
}
