namespace LegacyVault.Application.Dtos.Requests;

/// <summary>Body (không bắt buộc) của POST /api/v1/auth/logout.</summary>
public class LogoutRequest
{
    /// <summary>true = đăng xuất khỏi mọi thiết bị.</summary>
    public bool RevokeAllDevices { get; set; }
}
