namespace LegacyVault.Application.Exceptions;

/// <summary>
/// 404 - Không có kho, hoặc kho không thuộc người đang đăng nhập.
/// </summary>
/// <remarks>Trả 404 cả khi kho của người khác (không phải 403) để không lộ việc kho đó có tồn tại.</remarks>
public class VaultNotFoundException : ApiException
{
    public VaultNotFoundException()
        : base(404, "VAULT_NOT_FOUND", "Không tìm thấy kho.")
    {
    }
}
