using System.Text;
using LegacyVault.Application.Exceptions;

namespace LegacyVault.Application.Validation;

/// <summary>
/// Kiểm tra tệp chính của tài sản: PDF/DOCX/JPG/PNG/TXT, tối đa 20 MiB mỗi tệp (NFR-07, FILE-01).
/// </summary>
/// <remarks>
/// NFR-07 của SRS v3.14.1 còn cho phép WEBP/XLSX/ZIP; ba loại này chưa được hỗ trợ ở đây.
/// </remarks>
/// <remarks>
/// Không tin đuôi tệp hay Content-Type do client gửi (đổi tên virus.exe thành virus.pdf là xong).
/// Ta đọc vài byte đầu ("magic number") để chắc tệp đúng loại, rồi tự quyết định ContentType.
/// Đây chỉ là kiểm tra định dạng, KHÔNG phải quét virus — muốn quét thật cần ClamAV/Defender (ngoài phạm vi MVP).
/// </remarks>
public static class AssetFileValidator
{
    public const long MaxFileSizeBytes = 20L * 1024 * 1024; // 20 MiB
    public const int MaxFileNameLength = 255;

    private static readonly byte[] PdfSignature = Encoding.ASCII.GetBytes("%PDF-");
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] ZipSignature = { 0x50, 0x4B, 0x03, 0x04 }; // DOCX là một tệp ZIP

    /// <summary>Tên tệp an toàn để lưu/hiển thị: bỏ phần thư mục ("../../x.pdf" → "x.pdf").</summary>
    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
        if (name.Length == 0)
        {
            throw new InvalidAssetFileException("FILE_NAME_INVALID", "Tên tệp không hợp lệ.");
        }
        if (name.Length > MaxFileNameLength)
        {
            throw new InvalidAssetFileException("FILE_NAME_TOO_LONG", "Tên tệp tối đa 255 ký tự.");
        }

        return name;
    }

    /// <summary>Kiểm tra nội dung khớp đuôi tệp. Hợp lệ → trả ContentType chuẩn; không → InvalidAssetFileException.</summary>
    public static string DetectContentType(string fileName, byte[] content)
    {
        if (content.Length == 0)
        {
            throw new InvalidAssetFileException("FILE_EMPTY", "Tệp rỗng.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        switch (extension)
        {
            case ".pdf":
                RequireSignature(content, PdfSignature);
                return "application/pdf";
            case ".png":
                RequireSignature(content, PngSignature);
                return "image/png";
            case ".jpg":
            case ".jpeg":
                RequireSignature(content, JpegSignature);
                return "image/jpeg";
            case ".docx":
                RequireSignature(content, ZipSignature);
                return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            case ".txt":
                RequirePlainText(content);
                return "text/plain; charset=utf-8";
            default:
                throw new InvalidAssetFileException(
                    "FILE_TYPE_NOT_ALLOWED", "Chỉ chấp nhận tệp PDF, DOCX, JPG, PNG hoặc TXT.");
        }
    }

    private static void RequireSignature(byte[] content, byte[] signature)
    {
        if (!content.AsSpan().StartsWith(signature))
        {
            throw new InvalidAssetFileException(
                "FILE_CONTENT_MISMATCH", "Nội dung tệp không đúng với định dạng của đuôi tệp.");
        }
    }

    /// <summary>TXT không có magic number: yêu cầu UTF-8 hợp lệ và không có byte 0 (dấu hiệu tệp nhị phân).</summary>
    private static void RequirePlainText(byte[] content)
    {
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            strictUtf8.GetCharCount(content);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidAssetFileException("FILE_CONTENT_MISMATCH", "Tệp TXT phải là văn bản UTF-8.");
        }

        if (Array.IndexOf(content, (byte)0) >= 0)
        {
            throw new InvalidAssetFileException("FILE_CONTENT_MISMATCH", "Tệp TXT phải là văn bản UTF-8.");
        }
    }
}
