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
/// Thêm tài sản vào kho, cho Owner tải lại, sửa tên/mô tả và thay nội dung
/// (F1 §5.2 bước 2 và 6; ASSET-01..04; QUOTA-01/02; SEC-02/03; SRS v3.14.2 §5.5 EDIT-02..07).
/// </summary>
/// <remarks>
/// Đường đi của một tệp khi tạo tài sản:
///   tệp rõ ──► kiểm tra định dạng/kích thước ──► kiểm tra dung lượng kho ──► SHA-256 bản rõ
///          ──► IEnvelopeEncryptionService.Encrypt (DEK mới, AES-256-GCM, bọc DEK bằng KEK)
///          ──► IFileStorage lưu BẢN MÃ ──► DB lưu metadata + DEK đã bọc
/// Bản rõ chỉ tồn tại trong bộ nhớ của request này và bị ghi đè số 0 khi xong (SEC-03).
/// Thay nội dung đi cùng đường đó nhưng tạo phiên bản n+1 (DEK mới) và xóa DEK của bản n, xem SaveNewVersionAsync.
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
        VaultEditPolicy.EnsureCanEdit(vault, _timeProvider.GetUtcNow());

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
            await EnsureStorageQuotaAsync(vault, plaintext.Length, 0, ct);
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

    public async Task<AssetResponse> UpdateAssetDetailsAsync(
        Guid vaultId, Guid assetId, UpdateAssetRequest request, string? ifMatch, CancellationToken ct)
    {
        var userId = RequireUserId();
        var expectedUpdatedAt = ParseIfMatch(ifMatch);
        var vault = await LoadOwnedVaultAsync(vaultId, userId, ct);
        var now = _timeProvider.GetUtcNow();
        VaultEditPolicy.EnsureCanEdit(vault, now);

        var asset = await _assetRepository.FindByIdAsync(vault.Id, assetId, ct);
        if (asset == null)
        {
            throw new AssetNotFoundException();
        }

        // ETag đã cũ thì báo ngay, không cần gửi câu UPDATE.
        if (asset.UpdatedAt != expectedUpdatedAt)
        {
            throw new EditConflictException();
        }

        var name = asset.Name;
        if (request.Name != null)
        {
            name = request.Name.Trim();
            if (name.Length == 0)
            {
                throw new ValidationFailedException("name", "Tên tài sản không được để trống.");
            }
        }

        var description = asset.Description;
        if (request.Description != null)
        {
            description = NormalizeOptional(request.Description);
        }

        if (name == asset.Name && description == asset.Description)
        {
            return AssetResponse.From(asset); // không có gì đổi → không ghi DB, ETag giữ nguyên
        }

        // EDIT-06: khi có module Container, đổi tên tài sản thuộc Container chưa ARCHIVED phải tăng
        // vault.Plan.ConfigVersion trong cùng lần ghi. Hiện chưa có Container nên chưa tăng.
        var updated = await _assetRepository.TryUpdateDetailsAsync(
            vault.Id, asset.Id, expectedUpdatedAt, name, description, now, ct);
        if (!updated)
        {
            throw new EditConflictException();
        }

        asset.Name = name;
        asset.Description = description;
        asset.UpdatedAt = now;
        return AssetResponse.From(asset);
    }

    public async Task<AssetResponse> ReplaceAssetContentAsync(
        Guid vaultId, Guid assetId, int expectedVersionNumber, UploadedFile file, CancellationToken ct)
    {
        var userId = RequireUserId();
        var vault = await LoadOwnedVaultAsync(vaultId, userId, ct);
        var now = _timeProvider.GetUtcNow();
        VaultEditPolicy.EnsureCanEdit(vault, now);

        var asset = await _assetRepository.FindByIdAsync(vault.Id, assetId, ct);
        if (asset == null)
        {
            throw new AssetNotFoundException();
        }

        var current = asset.Versions.OrderByDescending(v => v.VersionNumber).First();
        if (current.VersionNumber != expectedVersionNumber)
        {
            throw new EditConflictException();
        }

        var fileName = AssetFileValidator.SanitizeFileName(file.FileName);
        var plaintext = await ReadWithLimitAsync(file, ct);
        try
        {
            // EDIT-03: tệp mới qua đủ kiểm tra như tạo mới, được phép khác loại với phiên bản trước.
            var contentType = AssetFileValidator.DetectContentType(fileName, plaintext);

            // EDIT-04: bản hiện hành sẽ bị thay nên không còn tính dung lượng, trừ khi đang được bảo vệ.
            var releasedBytes = IsProtectedVersion(current) ? 0 : current.SizeBytes;
            await EnsureStorageQuotaAsync(vault, plaintext.Length, releasedBytes, ct);

            return await SaveNewVersionAsync(vault, asset, current, userId, fileName, contentType, plaintext, now, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    // ===== Các bước nhỏ =====

    /// <summary>
    /// QUOTA-02 / EDIT-04: dung lượng đã dùng − byte của bản bị thay (releasedBytes) + tệp mới
    /// không được vượt dung lượng của hạng dịch vụ. Tạo tài sản mới thì releasedBytes = 0.
    /// Chưa có "byte đang giữ chỗ" cho upload dở dang, nên hai upload đồng thời gần đầy kho vẫn có thể
    /// cùng lọt qua; cần thêm cơ chế giữ chỗ (reserved_bytes) để đạt đủ AC-93.
    /// </summary>
    private async Task EnsureStorageQuotaAsync(Vault vault, long newBytes, long releasedBytes, CancellationToken ct)
    {
        var usedBytes = await _assetRepository.SumCurrentSizeBytesAsync(vault.Id, ct);
        var quotaBytes = vault.Entitlement.StorageQuotaBytes;
        if (usedBytes - releasedBytes + newBytes > quotaBytes)
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

        var ciphertext = EncryptInto(vault, asset, version, plaintext);
        asset.Versions.Add(version);

        // Ghi tệp trước, DB sau. DB lỗi → xóa tệp vừa ghi để không để lại bản mã mồ côi.
        await _fileStorage.SaveAsync(version.StoragePath, ciphertext, ct);
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

    /// <summary>
    /// EDIT-03/05: thêm phiên bản n+1 và cho bản n thôi dùng được, trong MỘT lần lưu DB.
    /// </summary>
    /// <remarks>
    /// Bản n không bị xóa dòng (giữ lịch sử, số phiên bản, checksum), nhưng bị xóa DEK đã bọc, nonce và tag
    /// ngay trong transaction lưu bản n+1. Mất DEK thì bản mã không giải mã được nữa (xóa bằng mật mã),
    /// nên dù bước xóa tệp sau đó có lỗi, nội dung cũ cũng không đọc lại được. WrappedDataKey rỗng = bản đã bị xóa.
    /// Khi có snapshot/grant/hold (IsProtectedVersion), bản được bảo vệ giữ nguyên khóa và tệp.
    /// </remarks>
    private async Task<AssetResponse> SaveNewVersionAsync(
        Vault vault, Asset asset, AssetVersion current, Guid userId,
        string fileName, string contentType, byte[] plaintext, DateTimeOffset now, CancellationToken ct)
    {
        var version = new AssetVersion
        {
            AssetId = asset.Id,
            VersionNumber = current.VersionNumber + 1,
            OriginalFileName = fileName,
            ContentType = contentType,
            SizeBytes = plaintext.Length,
            Sha256Checksum = ComputeSha256Hex(plaintext),
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        var ciphertext = EncryptInto(vault, asset, version, plaintext);
        _assetRepository.AddVersion(version); // INSERT, không phải UPDATE (xem IAssetRepository.AddVersion)
        asset.Versions.Add(version);
        asset.UpdatedAt = now; // ETag của tài sản đổi theo

        var eraseCurrent = !IsProtectedVersion(current);
        if (eraseCurrent)
        {
            current.WrappedDataKey = Array.Empty<byte>();
            current.Nonce = Array.Empty<byte>();
            current.AuthTag = Array.Empty<byte>();
        }

        await _fileStorage.SaveAsync(version.StoragePath, ciphertext, ct);
        bool saved;
        try
        {
            saved = await _assetRepository.TrySaveNewVersionAsync(ct);
        }
        catch
        {
            await _fileStorage.DeleteAsync(version.StoragePath, CancellationToken.None);
            throw;
        }

        if (!saved)
        {
            // Một request khác vừa lưu phiên bản n+1 trước (EDIT-07).
            await _fileStorage.DeleteAsync(version.StoragePath, CancellationToken.None);
            throw new EditConflictException();
        }

        if (eraseCurrent)
        {
            try
            {
                await _fileStorage.DeleteAsync(current.StoragePath, CancellationToken.None);
            }
            catch (IOException)
            {
                // Bản mã cũ đã mất DEK nên không đọc lại được; để lại tệp không làm lộ dữ liệu.
                // Không báo lỗi cho Owner vì phiên bản mới đã lưu thành công.
            }
            catch (UnauthorizedAccessException)
            {
                // Như trên.
            }
        }

        return AssetResponse.From(asset);
    }

    /// <summary>
    /// Mã hóa phong bì bản rõ cho một phiên bản (SEC-02): DEK mới, AAD gắn kho/tài sản/phiên bản.
    /// Ghi thông tin mã hóa và StoragePath vào version, trả bản mã để lưu vào kho tệp.
    /// </summary>
    private byte[] EncryptInto(Vault vault, Asset asset, AssetVersion version, byte[] plaintext)
    {
        var encrypted = _encryptionService.Encrypt(plaintext, BuildAssociatedData(vault.Id, asset.Id, version.Id));

        version.EncryptionAlgorithm = encrypted.Algorithm;
        version.Nonce = encrypted.Nonce;
        version.AuthTag = encrypted.AuthTag;
        version.WrappedDataKey = encrypted.WrappedDataKey;
        version.KeyId = encrypted.KeyId;
        // Đường dẫn chỉ ghép từ Guid → không có ký tự lạ, không path traversal, không lộ tên tệp gốc.
        version.StoragePath = vault.Id.ToString("N") + "/" + asset.Id.ToString("N") + "/" + version.Id.ToString("N") + ".bin";
        return encrypted.Ciphertext;
    }

    /// <summary>
    /// Phiên bản có đang được snapshot/grant/hold giữ không (QUOTA-01, EDIT-05).
    /// Các luồng đó chưa có nên luôn false; khi làm snapshot thì thay bằng truy vấn thật.
    /// </summary>
    private static bool IsProtectedVersion(AssetVersion version)
    {
        return false;
    }

    /// <summary>
    /// Header If-Match bắt buộc khi sửa tên/mô tả (EDIT-07). Thiếu → 428; sai định dạng → 409 để FE tải lại.
    /// </summary>
    private static DateTimeOffset? ParseIfMatch(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new PreconditionRequiredException();
        }

        if (!EntityTag.TryParse(ifMatch, out var expectedUpdatedAt))
        {
            throw new EditConflictException();
        }

        return expectedUpdatedAt;
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
