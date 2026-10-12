namespace LegacyVault.Infrastructure.Storage;

/// <summary>Cấu hình kho tệp cục bộ, đọc từ mục "FileStorage".</summary>
public class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Thư mục chứa bản mã. Đường dẫn tương đối tính từ thư mục project Api (ContentRoot).
    /// Thư mục này chỉ có bản mã nên lộ cũng không lộ nội dung, nhưng vẫn nằm ngoài git (.gitignore).
    /// </summary>
    public string RootPath { get; set; } = "App_Data/asset-blobs";
}
