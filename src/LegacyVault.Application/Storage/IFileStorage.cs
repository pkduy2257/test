namespace LegacyVault.Application.Storage;

/// <summary>
/// Nơi lưu bản mã của tệp (đĩa cục bộ khi dev; sau này có thể là S3 / Azure Blob).
/// </summary>
/// <remarks>
/// Chỉ nhận và trả BẢN MÃ. Kho tệp không biết gì về khóa, nên lộ kho tệp không lộ nội dung.
/// path là đường dẫn tương đối do service tạo từ các Guid, ví dụ "{vaultId}/{assetId}/{versionId}.bin".
/// </remarks>
public interface IFileStorage
{
    Task SaveAsync(string path, byte[] content, CancellationToken ct);

    /// <summary>Không có tệp → FileNotFoundException.</summary>
    Task<byte[]> ReadAsync(string path, CancellationToken ct);

    /// <summary>Xóa nếu có; không có thì bỏ qua.</summary>
    Task DeleteAsync(string path, CancellationToken ct);
}
