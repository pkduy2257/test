using LegacyVault.Domain.Entities;

namespace LegacyVault.Application.Dtos.Responses;

/// <summary>Thông tin người dùng trả về cho FE (khớp interface User bên FE: src/entities/user/model/user.types.ts).</summary>
public class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Đổi entity → DTO. Map tay (nhóm không dùng AutoMapper): rõ ràng, dễ debug, IDE tìm được chỗ dùng.
    /// </summary>
    public static UserResponse From(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Role = user.Role.ToString().ToUpperInvariant(),     // Owner → "OWNER"
            Status = user.Status.ToString().ToUpperInvariant(), // Active → "ACTIVE"
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
        };
    }
}
