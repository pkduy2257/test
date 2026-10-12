namespace LegacyVault.Application.Exceptions;

/// <summary>404 - Không có tài sản này trong kho.</summary>
public class AssetNotFoundException : ApiException
{
    public AssetNotFoundException()
        : base(404, "ASSET_NOT_FOUND", "Không tìm thấy tài sản.")
    {
    }
}
