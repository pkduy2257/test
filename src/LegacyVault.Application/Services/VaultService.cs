using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;
using LegacyVault.Application.Exceptions;
using LegacyVault.Application.Repositories;
using LegacyVault.Application.Security;
using LegacyVault.Domain.Entities;
using LegacyVault.Domain.Enums;

namespace LegacyVault.Application.Services;

/// <summary>
/// Tạo và xem kho (Luồng 1, bước 3–4).
/// </summary>
/// <remarks>
/// Kho mới luôn ở Draft. Kích hoạt (SETUP-01: cần Executor đã chấp nhận, cấu hình điểm danh...) làm ở bước sau.
/// </remarks>
public sealed class VaultService : IVaultService
{
    private readonly IVaultRepository _vaultRepository;
    private readonly IAssetRepository _assetRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public VaultService(
        IVaultRepository vaultRepository,
        IAssetRepository assetRepository,
        IUserRepository userRepository,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _vaultRepository = vaultRepository;
        _assetRepository = assetRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<VaultResponse> CreateVaultAsync(CreateVaultRequest request, CancellationToken ct)
    {
        var userId = RequireUserId();

        // Access token sống 15 phút: tài khoản có thể vừa bị vô hiệu sau khi token được cấp.
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null || user.Status != UserStatus.Active)
        {
            throw new AccountNotActiveException();
        }

        // M20: mỗi Owner tối đa một kho chưa hoàn tất. Hai request đồng thời vẫn lọt qua được bước này,
        // nên DB có thêm filtered unique index làm chốt chặn cuối (xem VaultConfiguration).
        var existing = await _vaultRepository.FindByOwnerIdAsync(userId, ct);
        var openVault = existing.FirstOrDefault(v => v.Status != VaultStatus.Completed);
        if (openVault != null)
        {
            throw new VaultAlreadyExistsException(openVault.Id);
        }

        var vault = new Vault
        {
            OwnerId = userId,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            Status = VaultStatus.Draft,
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        _vaultRepository.Add(vault);
        await _vaultRepository.SaveChangesAsync(ct);

        return VaultResponse.From(vault, 0);
    }

    public async Task<List<VaultResponse>> GetMyVaultsAsync(CancellationToken ct)
    {
        var userId = RequireUserId();
        var vaults = await _vaultRepository.FindByOwnerIdAsync(userId, ct);

        var result = new List<VaultResponse>();
        foreach (var vault in vaults)
        {
            var assetCount = await _assetRepository.CountByVaultIdAsync(vault.Id, ct);
            result.Add(VaultResponse.From(vault, assetCount));
        }

        return result;
    }

    public async Task<VaultResponse> GetVaultAsync(Guid vaultId, CancellationToken ct)
    {
        var userId = RequireUserId();
        var vault = await _vaultRepository.FindByIdAsync(vaultId, ct);
        if (vault == null || vault.OwnerId != userId)
        {
            throw new VaultNotFoundException();
        }

        var assetCount = await _assetRepository.CountByVaultIdAsync(vault.Id, ct);
        return VaultResponse.From(vault, assetCount);
    }

    private Guid RequireUserId()
    {
        var userId = _currentUser.UserId;
        if (userId == null)
        {
            throw new SessionExpiredException();
        }

        return userId.Value;
    }

    /// <summary>Chuỗi rỗng/toàn khoảng trắng → null.</summary>
    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
