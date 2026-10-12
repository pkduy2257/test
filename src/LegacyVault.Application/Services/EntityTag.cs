using System.Globalization;

namespace LegacyVault.Application.Services;

/// <summary>
/// ETag cho kho/tài sản, lấy từ cột UpdatedAt sẵn có (EDIT-07: client gửi lại bản đang thấy qua If-Match).
/// </summary>
/// <remarks>
/// Không thêm cột rowversion để khỏi phải đổi cấu trúc bảng. Mỗi lần sửa đều ghi UpdatedAt = giờ server,
/// nên UpdatedAt đổi ⇔ bản ghi đã bị sửa. SQL Server lưu datetimeoffset(7) đúng tới 100 ns = 1 tick của .NET,
/// nên giá trị đọc ra khớp tuyệt đối với giá trị đã ghi.
/// Dạng ETag: "0" khi chưa sửa lần nào (UpdatedAt null), ngược lại là số tick UTC, ví dụ "639117213712345678".
/// Câu UPDATE luôn có điều kiện WHERE UpdatedAt = giá trị client gửi, nên hai request cùng ETag chỉ một cái thắng.
/// </remarks>
public static class EntityTag
{
    public static string From(DateTimeOffset? updatedAt)
    {
        if (updatedAt == null)
        {
            return "\"0\"";
        }

        return "\"" + updatedAt.Value.UtcTicks.ToString(CultureInfo.InvariantCulture) + "\"";
    }

    /// <summary>
    /// Đọc giá trị header If-Match. Trả false khi không đọc được (sai định dạng, nhiều giá trị, "*").
    /// expectedUpdatedAt = null nghĩa là client đang thấy bản chưa sửa lần nào.
    /// </summary>
    public static bool TryParse(string ifMatch, out DateTimeOffset? expectedUpdatedAt)
    {
        expectedUpdatedAt = null;

        var value = ifMatch.Trim();
        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value.Substring(2);
        }
        value = value.Trim('"');

        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
        {
            return false;
        }

        if (ticks == 0)
        {
            return true;
        }

        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return false;
        }

        expectedUpdatedAt = new DateTimeOffset(ticks, TimeSpan.Zero);
        return true;
    }
}
