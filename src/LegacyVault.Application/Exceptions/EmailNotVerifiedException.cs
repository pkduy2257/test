namespace LegacyVault.Application.Exceptions;

/// <summary>403 - Email tài khoản chưa xác minh nên chưa được tạo kho (AUTH-04, AC-01).</summary>
public class EmailNotVerifiedException : ApiException
{
    public EmailNotVerifiedException()
        : base(403, "EMAIL_NOT_VERIFIED", "Vui lòng xác minh email trước khi tạo kho.")
    {
    }
}
