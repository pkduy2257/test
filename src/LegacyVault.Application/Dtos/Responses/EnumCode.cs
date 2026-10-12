using System.Text;

namespace LegacyVault.Application.Dtos.Responses;

/// <summary>Đổi tên enum sang mã UPPER_SNAKE_CASE cho FE: PendingVerification → "PENDING_VERIFICATION".</summary>
public static class EnumCode
{
    public static string From(Enum value)
    {
        var name = value.ToString();
        var builder = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                builder.Append('_');
            }
            builder.Append(char.ToUpperInvariant(name[i]));
        }

        return builder.ToString();
    }
}
