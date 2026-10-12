namespace LegacyVault.Application.Exceptions;

/// <summary>400 - Tệp tải lên không hợp lệ (rỗng, quá lớn, sai định dạng...).</summary>
/// <remarks>reason là mã ngắn để FE hiển thị đúng thông báo, ví dụ FILE_TOO_LARGE.</remarks>
public class InvalidAssetFileException : ApiException
{
    public InvalidAssetFileException(string reason, string message)
        : base(400, "INVALID_ASSET_FILE", message)
    {
        ExtraData["reason"] = reason;
    }
}
