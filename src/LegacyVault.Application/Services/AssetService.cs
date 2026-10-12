using System.Security.Cryptography;
using System.Text;
using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;
using LegacyVault.Application.Exceptions;
using LegacyVault.Application.Repositories;
using LegacyVault.Application.Security;
using LegacyVault.Application.Storage;
using LegacyVault.Application.Validation;
using LegacyVault.Domain.Entities;
using LegacyVault.Domain.Enums;

namespace LegacyVault.Application.Services;

/// <summary>
/// Thêm tài sản vào kho và cho Owner tải lại (F1 §5.2 bước 2 và 6; ASSET-01..04; QUOTA-01/02; SEC-02/03).
/// </summary>
/// <remarks>
/// Đường đi của một tệp khi tạo tài sản:
///   tệp rõ ──► kiểm tra định dạng/kích thước ──► kiểm tra dung lượng kho ──► SHA-256 bản rõ
///          ──► IEnvelopeEncryptionService.Encrypt (DEK mới, AES-256-GCM, bọc DEK bằng KEK)
///          ──► IFileStorage lưu BẢN MÃ ──► DB lưu metadata + DEK đã bọc
/// Bản rõ chỉ tồn tại trong bộ nhớ của request này và bị ghi đè số 0 khi xong (SEC-03).
/// </remarks>
public sealed class AssetService : IAssetService
{
    private readonly IVaultRepository _vaultRepository;
    private readonly IAssetRepository _assetRepository;
    private readonly IEnvelopeEncryptionService _encryptionService;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public AssetService(
        IVaultRepository vaultRepository,
        IAssetRepository assetRepository,
        IEnvelopeEncryptionService encryptionService,
        IFileStorage fileStorage,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _vaultRepository = vaultRepository;
        _assetRepository = assetRepository;
        _encryptionService = encryptionService;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// AAD (dữ liệu xác thực kèm) gắn bản mã với đúng kho/tài sản/phiên bản (SEC-01).
    /// Có tiền tố + "v1" để sau này đổi định dạng vẫn phân biệt được.
    /// </summary>
    public static byte[] BuildAssociatedData(Guid vaultId, Guid assetId, Guid versionId)
    {
        var text = "legacyvault/asset-content/v1"
            + "|vault=" + vaultId.ToString("N")
            + "|asset=" + assetId.ToString("N")
            + "|version=" + versionId.ToString("N");
        return Encoding.UTF8.GetBytes(text);
    }

    public async Task<AssetResponse> CreateAssetAsync(
        Guid vaultId, CreateAssetRequest request, UploadedFile file, CancellationToken ct)
    {
        var userId = RequireUserId();
        var vault = await LoadOwnedVaultAsync(vaultId, userId, ct);
        EnsureCanAddContent(vault);

        // NFR-14: giới hạn chống lạm dụng. Hai upload đồng thời có thể cùng lọt qua ở tài sản thứ 2.000;
        // chấp nhận được vì đây không phải giới hạn tính tiền.
        var assetCount = await _assetRepository.CountByVaultIdAsync(vault.Id, ct);
        if (assetCount >= VaultLimits.MaxAssetsPerVault)
        {
            throw new AssetLimitExceededException(VaultLimits.MaxAssetsPerVault);
        }

        var fileName = AssetFileValidator.SanitizeFileName(file.FileName);
        var plaintext = await ReadWithLimitAsync(file, ct);
        try
        {
            var contentType = AssetFileValidator.DetectContentType(fileName, plaintext);
            await EnsureStorageQuotaAsync(vault, plaintext.Length, ct);
            return await EncryptAndSaveAsync(vault, userId, request, fileName, contentType, plaintext, ct);
        }
        finally
        {
            // Không để bản rõ nằm lại trong bộ nhớ chờ GC dọn.
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async Task<List<AssetResponse>> GetAssetsAsync(Guid vaultId, CancellationToken ct)
    {
        var userId = RequireUserId();
        var vault = await LoadOwnedVaultAsync(vaultId, userId, ct);

        var assets = await _assetRepository.FindByVaultIdAsync(vault.Id, ct);
        return assets.Select(AssetResponse.From).ToList();
    }

    public async Task<AssetFileContent> DownloadAssetAsync(Guid vaultId, Guid assetId, CancellationToken ct)
    {
        var userId = RequireUserId();
        var vault = await LoadOwnedVaultAsync(vaultId, userId, ct);

        var asset = await _assetRepository.FindByIdAsync(vault.Id, assetId, ct);
        if (asset == null)
        {
            throw new AssetNotFoundException();
        }

        var version = asset.Versions.OrderByDescending(v => v.VersionNumber).First();

        byte[] plaintext;
        try
        {
            var ciphertext = await _fileStorage.ReadAsync(version.StoragePath, ct);
            var encrypted = new EncryptedContent
            {
                Algorithm = version.EncryptionAlgorithm,
                Ciphertext = ciphertext,
                Nonce = version.Nonce,
                AuthTag = version.AuthTag,
                WrappedDataKey = version.WrappedDataKey,
                KeyId = version.KeyId,
            };
            plaintext = _encryptionService.Decrypt(encrypted, BuildAssociatedData(vault.Id, asset.Id, version.Id));
        }
        catch (CryptographicException)
        {
            // Bản mã/khóa bọc bị sửa, bị tráo hàng, hoặc KEK sai → GCM từ chối. Không bao giờ trả dữ liệu "gần đúng".
            throw new AssetIntegrityException();
        }
        catch (FileNotFoundException)
        {
            throw new AssetIntegrityException();
        }

        // Lớp kiểm tra thứ hai, độc lập với GCM: checksum lúc tải lên phải khớp.
        if (ComputeSha256Hex(plaintext) != version.Sha256Checksum)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new AssetIntegrityException();
        }

        return new AssetFileContent
        {
            Content = plaintext,
            FileName = version.OriginalFileName,
            ContentType = version.ContentType,
            Sha256Checksum = version.Sha256Checksum,
        };
    }

    // ===== Các bước nhỏ =====

    /// <summary>
    /// Owner chỉ được THÊM nội dung khi: kho còn mở (SETUP-18), kế hoạch không có hồ sơ chứng tử/bàn giao
    /// đang chạy (SETUP-05), và quyền dịch vụ là FREE hoặc PAID_ACTIVE (OPLAN-01 chặn thêm khi ân hạn/rà soát).
    /// </summary>
    private static void EnsureCanAddContent(Vault vault)
    {
        if (vault.Status != VaultStatus.Open)
        {
            throw new VaultNotEditableException(VaultNotEditableException.VaultClosed);
        }

        var plan = vault.Plan.Status;
        var planEditable = plan == PlanStatus.Draft
            || plan == PlanStatus.Active
            || plan == PlanStatus.CheckinOverdue
            || plan == PlanStatus.AwaitingCheck;
        if (!planEditable)
        {
            throw new VaultNotEditableException(VaultNotEditableException.PlanLocked);
        }

        var service = vault.Entitlement.Status;
        if (service != EntitlementStatus.Free && service != EntitlementStatus.PaidActive)
        {
            throw new VaultNotEditableException(VaultNotEditableException.ServiceReadOnly);
        }
    }

    /// <summary>
    /// QUOTA-02: dung lượng đã dùng + tệp mới không được vượt dung lượng của hạng dịch vụ.
    /// Chưa có "byte đang giữ chỗ" cho upload dở dang, nên hai upload đồng thời gần đầy kho vẫn có thể
    /// cùng lọt qua; cần thêm cơ chế giữ chỗ (reserved_bytes) để đạt đủ AC-93.
    /// </summary>
    private async Task EnsureStorageQuotaAsync(Vault vault, long newBytes, CancellationToken ct)
    {
        var usedBytes = await _assetRepository.SumCurrentSizeBytesAsync(vault.Id, ct);
        var quotaBytes = vault.Entitlement.StorageQuotaBytes;
        if (usedBytes + newBytes > quotaBytes)
        {
            throw new StorageQuotaExceededException(usedBytes, quotaBytes);
        }
    }

    private async Task<AssetResponse> EncryptAndSaveAsync(
        Vault vault, Guid userId, CreateAssetRequest request,
        string fileName, string contentType, byte[] plaintext, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();

        // Tạo object trước để có sẵn Id (Guid v7 sinh ngay trong code) — AAD cần các Id này.
        var asset = new Asset
        {
            VaultId = vault.Id,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            Status = AssetStatus.Locked,
            CreatedAt = now,
        };
        var version = new AssetVersion
        {
            AssetId = asset.Id,
            VersionNumber = 1,
            OriginalFileName = fileName,
            ContentType = contentType,
            SizeBytes = plaintext.Length,
            Sha256Checksum = ComputeSha256Hex(plaintext),
            CreatedByUserId = userId,
            CreatedAt = now,
        };

        var encrypted = _encryptionService.Encrypt(plaintext, BuildAssociatedData(vault.Id, asset.Id, version.Id));

        version.EncryptionAlgorithm = encrypted.Algorithm;
        version.Nonce = encrypted.Nonce;
        version.AuthTag = encrypted.AuthTag;
        version.WrappedDataKey = encrypted.WrappedDataKey;
        version.KeyId = encrypted.KeyId;
        // Đường dẫn chỉ ghép từ Guid → không có ký tự lạ, không path traversal, không lộ tên tệp gốc.
        version.StoragePath = vault.Id.ToString("N") + "/" + asset.Id.ToString("N") + "/" + version.Id.ToString("N") + ".bin";
        asset.Versions.Add(version);

        // Ghi tệp trước, DB sau. DB lỗi → xóa tệp vừa ghi để không để lại bản mã mồ côi.
        await _fileStorage.SaveAsync(version.StoragePath, encrypted.Ciphertext, ct);
        try
        {
            _assetRepository.Add(asset);
            await _assetRepository.SaveChangesAsync(ct);
        }
        catch
        {
            await _fileStorage.DeleteAsync(version.StoragePath, CancellationToken.None);
            throw;
        }

        return AssetResponse.From(asset);
    }

    /// <summary>Đọc tệp vào bộ nhớ nhưng dừng ngay khi vượt 20 MiB (không tin Length client khai báo).</summary>
    private static async Task<byte[]> ReadWithLimitAsync(UploadedFile file, CancellationToken ct)
    {
        if (file.Length > AssetFileValidator.MaxFileSizeBytes)
        {
            throw new InvalidAssetFileException("FILE_TOO_LARGE", "Tệp tối đa 20 MiB.");
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        try
        {
            int read;
            while ((read = await file.Content.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > AssetFileValidator.MaxFileSizeBytes)
                {
                    throw new InvalidAssetFileException("FILE_TOO_LARGE", "Tệp tối đa 20 MiB.");
                }
                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        finally
        {
            // ToArray() tạo bản sao; xóa bản rõ còn trong bộ đệm tạm.
            CryptographicOperations.ZeroMemory(buffer.GetBuffer());
            CryptographicOperations.ZeroMemory(chunk);
        }
    }

    private async Task<Vault> LoadOwnedVaultAsync(Guid vaultId, Guid userId, CancellationToken ct)
    {
        var vault = await _vaultRepository.FindByIdAsync(vaultId, ct);
        if (vault == null || vault.OwnerId != userId)
        {
            throw new VaultNotFoundException();
        }

        return vault;
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

    private static string ComputeSha256Hex(byte[] content)
    {
        return Convert.ToHexStringLower(SHA256.HashData(content));
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
