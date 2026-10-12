namespace LegacyVault.Application.Exceptions;

/// <summary>
/// 428 - Lệnh sửa thiếu header If-Match (EDIT-07). Bắt buộc gửi ETag đang thấy để không ghi đè thay đổi của người khác.
/// </summary>
public class PreconditionRequiredException : ApiException
{
    public PreconditionRequiredException()
        : base(428, "PRECONDITION_REQUIRED", "Thiếu header If-Match chứa ETag của dữ liệu đang sửa.")
    {
    }
}
