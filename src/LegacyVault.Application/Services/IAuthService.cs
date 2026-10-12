using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;

namespace LegacyVault.Application.Services;

/// <summary>
/// Nghiệp vụ xác thực. Controller chỉ biết interface này, cài đặt nằm ở AuthService.
/// </summary>
public interface IAuthService
{
    /// <summary>Đăng nhập. Sai thông tin → InvalidCredentialsException / AccountLockedException / AccountNotActiveException.</summary>
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct);

    /// <summary>Đổi refresh token lấy cặp token mới. Phiên hỏng → SessionExpiredException.</summary>
    Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken ct);

    /// <summary>Đăng xuất, luôn thành công.</summary>
    Task LogoutAsync(string? refreshToken, bool revokeAllDevices, CancellationToken ct);

    /// <summary>Người dùng đang đăng nhập. Không hợp lệ → SessionExpiredException.</summary>
    Task<UserResponse> GetCurrentUserAsync(CancellationToken ct);
}
