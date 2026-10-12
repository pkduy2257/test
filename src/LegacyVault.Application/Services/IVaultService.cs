using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;

namespace LegacyVault.Application.Services;

/// <summary>Nghiệp vụ kho của Owner đang đăng nhập.</summary>
public interface IVaultService
{
    /// <summary>Tạo kho nháp. Đã có kho chưa hoàn tất → VaultAlreadyExistsException.</summary>
    Task<VaultResponse> CreateVaultAsync(CreateVaultRequest request, CancellationToken ct);

    /// <summary>Danh sách kho của mình, mới nhất trước.</summary>
    Task<List<VaultResponse>> GetMyVaultsAsync(CancellationToken ct);

    /// <summary>Một kho của mình. Không có / của người khác → VaultNotFoundException.</summary>
    Task<VaultResponse> GetVaultAsync(Guid vaultId, CancellationToken ct);
}
