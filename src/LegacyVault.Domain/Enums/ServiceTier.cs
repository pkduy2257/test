namespace LegacyVault.Domain.Enums;

/// <summary>
/// Hạng dịch vụ của kho (SRS v3.14.1 §8.1). SRS gọi là "hạng dịch vụ", không gọi tắt là "gói"
/// để khỏi nhầm với Container (gói bàn giao).
/// </summary>
public enum ServiceTier
{
    /// <summary>Owner Free: 20 MiB, 0 suất người nhận, chưa kích hoạt bàn giao được.</summary>
    Free = 0,
    /// <summary>Legacy Căn bản: 200 MiB, kèm 2 suất.</summary>
    Xs = 1,
    /// <summary>Legacy Cao cấp: 500 MiB, kèm 5 suất.</summary>
    XsMax = 2,
}
