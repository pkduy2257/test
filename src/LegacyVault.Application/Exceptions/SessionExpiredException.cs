namespace LegacyVault.Application.Exceptions;

/// <summary>401 - Phiên đăng nhập không còn hợp lệ, FE cần đăng nhập lại.</summary>
public class SessionExpiredException : ApiException
{
    public SessionExpiredException()
        : base(401, "SESSION_EXPIRED", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.")
    {
    }
}
