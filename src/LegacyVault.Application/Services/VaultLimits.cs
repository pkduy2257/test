namespace LegacyVault.Application.Services;

/// <summary>
/// Các con số hạn mức của kho theo SRS v3.14.1, gom một chỗ để dễ tra và dễ đổi.
/// </summary>
/// <remarks>
/// NFR-14 yêu cầu giới hạn chống lạm dụng là cấu hình có phiên bản. Khi có màn hình hạn mức,
/// chuyển các hằng số này ra appsettings và lưu phiên bản cấu hình.
/// </remarks>
public static class VaultLimits
{
    /// <summary>QUOTA-01: 1 MiB = 1.048.576 byte.</summary>
    public const long BytesPerMiB = 1024L * 1024;

    /// <summary>§8.1: hạng Owner Free được 20 MiB.</summary>
    public const long FreeStorageQuotaBytes = 20 * BytesPerMiB;

    /// <summary>§8.1: hạng Free không kèm suất người nhận nào.</summary>
    public const int FreeBeneficiarySlots = 0;

    /// <summary>NFR-14: tối đa 2.000 tài sản mỗi kho (chống lạm dụng, không phải giới hạn thương mại).</summary>
    public const int MaxAssetsPerVault = 2000;

    /// <summary>NFR-14: tối đa 50 Container chưa lưu trữ mỗi kho. Dùng khi làm module Container.</summary>
    public const int MaxActiveContainersPerVault = 50;

    /// <summary>Tên kho khi Owner bấm "Tạo kho của tôi" mà không nhập tên (§5.2 bước 1 chỉ có một nút).</summary>
    public const string DefaultVaultName = "Kho của tôi";
}
