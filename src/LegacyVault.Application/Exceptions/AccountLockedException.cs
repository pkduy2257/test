namespace LegacyVault.Application.Exceptions;

/// <summary>423 - Tài khoản tạm khóa vì nhập sai mật khẩu quá nhiều lần.</summary>
public class AccountLockedException : ApiException
{
    public AccountLockedException(int remainingSeconds)
        : base(423, "ACCOUNT_LOCKED", "Tài khoản tạm khóa do nhập sai mật khẩu nhiều lần. Vui lòng thử lại sau.")
    {
        ExtraData["attemptsRemaining"] = 0;
        ExtraData["remainingSeconds"] = remainingSeconds;
    }
}
