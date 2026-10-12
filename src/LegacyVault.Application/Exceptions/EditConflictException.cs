namespace LegacyVault.Application.Exceptions;

/// <summary>
/// 409 - Bản ghi đã bị sửa sau khi client đọc (EDIT-07): ETag trong If-Match hoặc expectedVersionNumber đã cũ,
/// hoặc hai request cùng thay nội dung một tài sản. FE tải lại rồi cho Owner sửa trên bản mới.
/// </summary>
public class EditConflictException : ApiException
{
    public EditConflictException()
        : base(409, "EDIT_CONFLICT", "Dữ liệu đã được thay đổi ở nơi khác. Vui lòng tải lại rồi thử lại.")
    {
    }
}
