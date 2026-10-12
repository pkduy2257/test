using LegacyVault.Application.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LegacyVault.Infrastructure.Storage;

/// <summary>
/// Lưu bản mã xuống ổ đĩa của máy chạy API. Đủ cho dev/demo; production thay bằng S3/Azure Blob
/// bằng cách viết một class khác cài IFileStorage, service không phải sửa.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IOptions<FileStorageSettings> settings, IHostEnvironment environment)
    {
        _rootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, settings.Value.RootPath));
    }

    public async Task SaveAsync(string path, byte[] content, CancellationToken ct)
    {
        var fullPath = ResolvePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        // Ghi ra tệp tạm rồi đổi tên: nếu API sập giữa chừng, không bao giờ có tệp ghi dở mang tên thật.
        var tempPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(tempPath, content, ct);
            File.Move(tempPath, fullPath, overwrite: false); // phiên bản không bao giờ bị ghi đè
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public Task<byte[]> ReadAsync(string path, CancellationToken ct)
    {
        return File.ReadAllBytesAsync(ResolvePath(path), ct);
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        var fullPath = ResolvePath(path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>Chặn path traversal: đường dẫn cuối cùng bắt buộc nằm trong thư mục gốc.</summary>
    private string ResolvePath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, path));
        if (!fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Đường dẫn lưu trữ không hợp lệ.");
        }

        return fullPath;
    }
}
