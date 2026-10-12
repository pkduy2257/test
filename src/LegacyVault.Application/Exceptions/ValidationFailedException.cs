namespace LegacyVault.Application.Exceptions;

/// <summary>
/// 400 - Dữ liệu sai mà attribute trên DTO không bắt được (ví dụ tên chỉ toàn khoảng trắng).
/// </summary>
/// <remarks>
/// Cùng dạng JSON với lỗi validate tự động của [ApiController] (ApiConfig): code VALIDATION_ERROR và
/// errors = { tênTrường: [thông báo] }, để FE xử lý một kiểu duy nhất.
/// </remarks>
public class ValidationFailedException : ApiException
{
    public ValidationFailedException(string field, string message)
        : base(400, "VALIDATION_ERROR", "Dữ liệu gửi lên không hợp lệ.")
    {
        ExtraData["errors"] = new Dictionary<string, string[]> { [field] = new[] { message } };
    }
}
