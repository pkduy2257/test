using LegacyVault.Application.Repositories;
using LegacyVault.Infrastructure.Security;

namespace LegacyVault.Api.Helpers;

/// <summary>
/// AUTH-07: mỗi request đã đăng nhập ghi lại lần thao tác cuối của phiên.
/// Chỉ ghi DB tối đa 1 lần/phút cho mỗi phiên, nên thời gian chờ có thể sớm hơn tối đa 1 phút.
/// </summary>
public sealed class SessionActivityMiddleware
{
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);
    private readonly RequestDelegate _next;

    public SessionActivityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRefreshTokenRepository refreshTokens, TimeProvider timeProvider)
    {
        var sid = context.User.FindFirst(JwtClaimTypes.SessionId);
        if (sid != null && Guid.TryParse(sid.Value, out var familyId))
        {
            var now = timeProvider.GetUtcNow();
            await refreshTokens.TouchActivityAsync(familyId, now, now - TouchInterval, context.RequestAborted);
        }

        await _next(context);
    }
}
