namespace LegacyVault.Application.Dtos.Responses;

/// <summary>Body trả về khi đăng nhập / làm mới phiên thành công (khớp AuthSession bên FE, trừ token).</summary>
/// <remarks>
/// Token KHÔNG nằm trong body: chúng được đặt vào cookie HttpOnly,
/// JavaScript không đọc được → mã độc XSS không lấy cắp được token.
/// </remarks>
public class AuthSessionResponse
{
    public Guid UserId { get; set; }

    /// <summary>Số giây đến khi access token hết hạn.</summary>
    public int ExpiresIn { get; set; }

    public UserResponse User { get; set; } = null!;
}
