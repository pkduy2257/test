namespace LegacyVault.Application.Dtos.Requests;

/// <summary>
/// Tệp người dùng tải lên, ở dạng không phụ thuộc ASP.NET (IFormFile chỉ có ở tầng Api).
/// </summary>
public class UploadedFile
{
    public string FileName { get; set; } = string.Empty;

    /// <summary>Kích thước client khai báo; service vẫn tự đếm khi đọc.</summary>
    public long Length { get; set; }

    public Stream Content { get; set; } = Stream.Null;
}
