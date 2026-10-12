namespace LegacyVault.Application.Exceptions;

/// <summary>403 - Tài khoản chưa kích hoạt hoặc đã bị vô hiệu hóa.</summary>
public class AccountNotActiveException : ApiException
{
    public AccountNotActiveException()
        : base(403, "ACCOUNT_NOT_ACTIVE", "Tài khoản chưa được kích hoạt hoặc đã bị vô hiệu hóa.")
    {
    }
}
