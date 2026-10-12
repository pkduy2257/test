namespace LegacyVault.Application.Dtos.Responses;

/// <summary>Body của GET /api/v1/auth/session (FE AuthBootstrap đọc data.user).</summary>
public class CurrentSessionResponse
{
    public UserResponse User { get; set; } = null!;
}
