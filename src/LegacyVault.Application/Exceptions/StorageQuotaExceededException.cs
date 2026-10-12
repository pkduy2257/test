namespace LegacyVault.Application.Exceptions;

/// <summary>409 - Thêm tệp này sẽ vượt dung lượng của hạng dịch vụ (QUOTA-02, AC-05).</summary>
public class StorageQuotaExceededException : ApiException
{
    public StorageQuotaExceededException(long usedBytes, long quotaBytes)
        : base(409, "STORAGE_QUOTA_EXCEEDED", "Kho không còn đủ dung lượng cho tệp này.")
    {
        ExtraData["usedBytes"] = usedBytes;
        ExtraData["quotaBytes"] = quotaBytes;
    }
}
