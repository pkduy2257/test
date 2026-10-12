namespace LegacyVault.Application.Exceptions;

/// <summary>
/// 409 - Kho đã đủ số tài sản tối đa chống lạm dụng (NFR-14). Đây không phải giới hạn thương mại:
/// hạng dịch vụ chỉ giới hạn theo dung lượng (ASSET-04).
/// </summary>
public class AssetLimitExceededException : ApiException
{
    public AssetLimitExceededException(int maxAssets)
        : base(409, "ASSET_LIMIT_EXCEEDED", "Kho đã đạt số tài sản tối đa.")
    {
        ExtraData["maxAssets"] = maxAssets;
    }
}
