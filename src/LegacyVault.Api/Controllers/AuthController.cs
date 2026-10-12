using LegacyVault.Api.Helpers;
using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;
using LegacyVault.Application.Exceptions;
using LegacyVault.Application.Services;
using LegacyVault.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LegacyVault.Api.Controllers;

/// <summary>
/// Đăng nhập / làm mới phiên / xem phiên / đăng xuất (giống một @RestController bên Spring Boot).
/// URL khớp với FE: src/features/auth/api/authService.ts và src/app/providers/AuthBootstrap.tsx
/// </summary>
/// <remarks>
/// Controller nên MỎNG: nhận request → gọi 1 method service → trả HTTP response.
/// Lỗi nghiệp vụ (sai mật khẩu, bị khóa...) do service throw ApiException,
/// ApiExceptionFilter tự đổi thành JSON lỗi — controller không cần if/else.
/// </remarks>
[ApiController]
[Produces("application/json")]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AuthCookieWriter _cookieWriter;

    public AuthController(IAuthService authService, AuthCookieWriter cookieWriter)
    {
        _authService = authService;
        _cookieWriter = cookieWriter;
    }

    /// <summary>POST /api/v1/auth/login</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);

        _cookieWriter.Append(Response, result);
        return Ok(new ApiResponse<AuthSessionResponse>(result.Session));
    }

    /// <summary>
    /// POST /api/v1/auth/refresh — đổi refresh token (trong cookie) lấy cặp token mới.
    /// FE nên gọi khi một request bị 401, rồi thử lại request đó.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        // Không có cookie → null.
        var refreshToken = Request.Cookies[AuthCookieNames.RefreshToken];

        AuthResult result;
        try
        {
            result = await _authService.RefreshAsync(refreshToken, ct);
        }
        catch (SessionExpiredException)
        {
            _cookieWriter.Clear(Response); // phiên hỏng → xóa luôn cookie cũ
            throw;                         // ném tiếp cho ApiExceptionFilter trả JSON 401
        }

        _cookieWriter.Append(Response, result);
        return Ok(new ApiResponse<AuthSessionResponse>(result.Session));
    }

    /// <summary>GET /api/v1/auth/session — FE gọi khi tải trang để biết ai đang đăng nhập.</summary>
    [HttpGet("session")]
    [Authorize] // không có access token hợp lệ → middleware trả 401, không vào tới đây
    public async Task<IActionResult> Session(CancellationToken ct)
    {
        var user = await _authService.GetCurrentUserAsync(ct);

        var session = new CurrentSessionResponse { User = user };
        return Ok(new ApiResponse<CurrentSessionResponse>(session));
    }

    /// <summary>
    /// POST /api/v1/auth/logout — body không bắt buộc: { "revokeAllDevices": true }.
    /// </summary>
    /// <remarks>
    /// [AllowAnonymous] vì access token có thể đã hết hạn mà người dùng vẫn phải đăng xuất được;
    /// phiên được nhận diện qua refresh token trong cookie.
    /// </remarks>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LogoutRequest? request,
        CancellationToken ct)
    {
        var refreshToken = Request.Cookies[AuthCookieNames.RefreshToken];

        var revokeAllDevices = false;
        if (request != null)
        {
            revokeAllDevices = request.RevokeAllDevices;
        }

        await _authService.LogoutAsync(refreshToken, revokeAllDevices, ct);

        _cookieWriter.Clear(Response);
        return NoContent(); // 204: thành công, không có nội dung
    }
}
