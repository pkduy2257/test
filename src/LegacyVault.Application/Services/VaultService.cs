using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;
using LegacyVault.Application.Exceptions;
using LegacyVault.Application.Repositories;
using LegacyVault.Application.Security;
using LegacyVault.Domain.Entities;
using LegacyVault.Domain.Enums;

namespace LegacyVault.Application.Services;

/// <summary>
/// Tạo, xem và sửa tên/mô tả kho (F1, SRS v3.14.1 §5.2 bước 1; SRS v3.14.2 §5.5 EDIT-01).
/// </summary>
/// <remarks>
/// SRS tách ba kết quả: tạo kho chỉ tạo không gian lưu trữ; thanh toán chỉ cấp quyền dịch vụ;
/// kích hoạt mới bắt đầu điểm danh. Vì vậy kho mới luôn là: Vault OPEN + quyền dịch vụ FREE (20 MiB, 0 suất)
/// + kế hoạch DRAFT. Thanh toán (luồng P) và kích hoạt (SETUP-01) làm ở các bước sau, trên CÙNG kho này.
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

    public async Task<OpenVaultResult> OpenOrCreateMyVaultAsync(CreateVaultRequest? request, CancellationToken ct)
    {
        var userId = RequireUserId();

        // 1. Kiểm tra tài khoản. Access token sống 15 phút: tài khoản có thể vừa bị vô hiệu sau khi token được cấp.
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null || user.Status != UserStatus.Active)
        {
            throw new AccountNotActiveException();
        }

        // AC-01: chưa xác minh email thì không tạo kho; SETUP-01 kiểm tra lại khi kích hoạt.
        if (user.EmailVerifiedAt == null)
        {
            throw new EmailNotVerifiedException();
        }

        // 2. Đã có kho → mở lại đúng kho đó, kể cả kho CLOSED (SETUP-12, AC-104). Không bao giờ tạo kho thứ hai.
        var existing = await _vaultRepository.FindByOwnerIdAsync(userId, ct);
        if (existing != null)
        {
            return await BuildResultAsync(existing, false, ct);
        }

        // 3. Chưa có → tạo kho, kế hoạch và quyền dịch vụ trong CÙNG một lần lưu (một transaction).
        var vault = NewFreeVault(userId, request, _timeProvider.GetUtcNow());
        var inserted = await _vaultRepository.TryAddAsync(vault, ct);
        if (inserted)
        {
            return await BuildResultAsync(vault, true, ct);
        }

        // 4. Unique index trên OwnerId vừa chặn: một request khác (tab thứ hai, retry) đã tạo kho trước
        //    chúng ta một chút. Trả đúng kho đó như request bình thường (AC-89), không báo lỗi.
        var winner = await _vaultRepository.FindByOwnerIdAsync(userId, ct);
        if (winner == null)
        {
            throw new InvalidOperationException("DB báo Owner đã có kho nhưng không đọc lại được kho đó.");
        }

        return await BuildResultAsync(winner, false, ct);
    }

    public async Task<VaultResponse> GetMyVaultAsync(CancellationToken ct)
    {
        var userId = RequireUserId();
        var vault = await _vaultRepository.FindByOwnerIdAsync(userId, ct);
        if (vault == null)
        {
            throw new VaultNotFoundException();
        }

        return await BuildResponseAsync(vault, ct);
    }

    public async Task<VaultResponse> GetVaultAsync(Guid vaultId, CancellationToken ct)
    {
        var userId = RequireUserId();
        var vault = await _vaultRepository.FindByIdAsync(vaultId, ct);
        if (vault == null || vault.OwnerId != userId)
        {
            throw new VaultNotFoundException();
        }

        return await BuildResponseAsync(vault, ct);
    }

    public async Task<VaultResponse> UpdateVaultAsync(
        Guid vaultId, UpdateVaultRequest request, string? ifMatch, CancellationToken ct)
    {
        var userId = RequireUserId();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new PreconditionRequiredException();
        }
        if (!EntityTag.TryParse(ifMatch, out var expectedUpdatedAt))
        {
            throw new EditConflictException();
        }

        var vault = await _vaultRepository.FindByIdAsync(vaultId, ct);
        if (vault == null || vault.OwnerId != userId)
        {
            throw new VaultNotFoundException();
        }

        var now = _timeProvider.GetUtcNow();
        VaultEditPolicy.EnsureCanEdit(vault, now);

        // ETag đã cũ thì báo ngay, không cần gửi câu UPDATE.
        if (vault.UpdatedAt != expectedUpdatedAt)
        {
            throw new EditConflictException();
        }

        var name = vault.Name;
        if (request.Name != null)
        {
            name = request.Name.Trim();
            if (name.Length == 0)
            {
                throw new ValidationFailedException("name", "Tên kho không được để trống.");
            }
        }

        var description = vault.Description;
        if (request.Description != null)
        {
            description = NormalizeOptional(request.Description);
        }

        if (name == vault.Name && description == vault.Description)
        {
            return await BuildResponseAsync(vault, ct); // không có gì đổi → không ghi DB, ETag giữ nguyên
        }

        // EDIT-01: tên/mô tả kho không thuộc kế hoạch bàn giao nên KHÔNG tăng Plan.ConfigVersion.
        var updated = await _vaultRepository.TryUpdateDetailsAsync(vault.Id, expectedUpdatedAt, name, description, now, ct);
        if (!updated)
        {
            throw new EditConflictException();
        }

        vault.Name = name;
        vault.Description = description;
        vault.UpdatedAt = now;
        return await BuildResponseAsync(vault, ct);
    }

    // ===== Các bước nhỏ =====

    /// <summary>Kho mới theo §5.2 bước 1: Vault OPEN, quyền dịch vụ FREE, kế hoạch DRAFT.</summary>
    private static Vault NewFreeVault(Guid ownerId, CreateVaultRequest? request, DateTimeOffset now)
    {
        var name = VaultLimits.DefaultVaultName;
        string? description = null;
        if (request != null)
        {
            var requestedName = NormalizeOptional(request.Name);
            if (requestedName != null)
            {
                name = requestedName;
            }
            description = NormalizeOptional(request.Description);
        }

        var vault = new Vault
        {
            OwnerId = ownerId,
            Name = name,
            Description = description,
            Status = VaultStatus.Open,
            CreatedAt = now,
        };
        vault.Plan = new HandoverPlan
        {
            VaultId = vault.Id,
            Status = PlanStatus.Draft,
            ConfigVersion = 1,
            CreatedAt = now,
        };
        vault.Entitlement = new VaultEntitlement
        {
            VaultId = vault.Id,
            Tier = ServiceTier.Free,
            Status = EntitlementStatus.Free,
            StorageQuotaBytes = VaultLimits.FreeStorageQuotaBytes,
            IncludedBeneficiarySlots = VaultLimits.FreeBeneficiarySlots,
            ExtraBeneficiarySlots = 0,
            CreatedAt = now,
        };
        return vault;
    }

    private async Task<OpenVaultResult> BuildResultAsync(Vault vault, bool created, CancellationToken ct)
    {
        return new OpenVaultResult
        {
            Vault = await BuildResponseAsync(vault, ct),
            Created = created,
        };
    }

    private async Task<VaultResponse> BuildResponseAsync(Vault vault, CancellationToken ct)
    {
        var assetCount = await _assetRepository.CountByVaultIdAsync(vault.Id, ct);
        var usedBytes = await _assetRepository.SumCurrentSizeBytesAsync(vault.Id, ct);
        return VaultResponse.From(vault, assetCount, usedBytes);
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
