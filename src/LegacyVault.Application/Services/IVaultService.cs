using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;

namespace LegacyVault.Application.Services;

/// <summary>Nghiệp vụ kho của Owner đang đăng nhập.</summary>
public interface IVaultService
{
    /// <summary>
    /// "Tạo kho của tôi" (§5.2 bước 1, SETUP-12): đã có kho thì mở lại kho đó (kể cả CLOSED),
    /// chưa có thì tạo đúng một kho FREE với kế hoạch DRAFT. Gọi lặp hoặc song song vẫn ra cùng một kho.
    /// Lỗi: AccountNotActiveException, EmailNotVerifiedException.
    /// </summary>
    Task<OpenVaultResult> OpenOrCreateMyVaultAsync(CreateVaultRequest? request, CancellationToken ct);

    /// <summary>Kho của mình. Chưa tạo kho → VaultNotFoundException.</summary>
    Task<VaultResponse> GetMyVaultAsync(CancellationToken ct);

    /// <summary>Một kho theo Id. Không có / của người khác → VaultNotFoundException.</summary>
    Task<VaultResponse> GetVaultAsync(Guid vaultId, CancellationToken ct);
}
