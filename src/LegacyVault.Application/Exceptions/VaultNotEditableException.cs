namespace LegacyVault.Application.Exceptions;

/// <summary>
/// 409 - Kho đang không cho Owner thêm nội dung. <c>reason</c> cho FE biết vì sao:
/// VAULT_CLOSED (SETUP-18), PLAN_LOCKED (có hồ sơ chứng tử/đang bàn giao, SETUP-05),
/// SERVICE_READ_ONLY (đang ân hạn hoặc rà soát, OPLAN-01).
/// </summary>
public class VaultNotEditableException : ApiException
{
    public const string VaultClosed = "VAULT_CLOSED";
    public const string PlanLocked = "PLAN_LOCKED";
    public const string ServiceReadOnly = "SERVICE_READ_ONLY";

    public VaultNotEditableException(string reason)
        : base(409, "VAULT_NOT_EDITABLE", "Kho đang ở trạng thái không cho phép thêm nội dung.")
    {
        ExtraData["reason"] = reason;
    }
}
