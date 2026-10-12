using System.Security.Claims;
using LegacyVault.Application.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace LegacyVault.Infrastructure.Security;

/// <summary>
/// Đọc người dùng hiện tại từ HttpContext.User — thứ mà middleware JwtBearer đã điền
/// sau khi kiểm tra chữ ký và hạn của token.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated
    {
        get
        {
            var principal = GetPrincipal();
            return principal != null
                && principal.Identity != null
                && principal.Identity.IsAuthenticated;
        }
    }

    public Guid? UserId
    {
        get
        {
            var principal = GetPrincipal();
            if (principal == null)
            {
                return null;
            }

            // Claim "sub" chứa userId (xem JwtTokenService.CreateAccessToken).
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (Guid.TryParse(sub, out var id))
            {
                return id;
            }

            return null;
        }
    }

    private ClaimsPrincipal? GetPrincipal()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        return httpContext.User;
    }
}
