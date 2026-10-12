namespace LegacyVault.Application.Exceptions;

/// <summary>409 - Kho đã đủ số tài sản tối đa (M28).</summary>
public class AssetLimitExceededException : ApiException
{
    public AssetLimitExceededException(int maxAssets)
        : base(409, "ASSET_LIMIT_EXCEEDED", "Kho đã đạt số tài sản tối đa.")
    {
        ExtraData["maxAssets"] = maxAssets;
    }
}
