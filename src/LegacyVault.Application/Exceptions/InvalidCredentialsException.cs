namespace LegacyVault.Application.Exceptions;

/// <summary>401 - Sai email hoặc mật khẩu.</summary>
public class InvalidCredentialsException : ApiException
{
    public InvalidCredentialsException(int? attemptsRemaining = null)
        : base(401, "INVALID_CREDENTIALS", "Email hoặc mật khẩu chưa đúng.")
    {
        // FE (LoginForm.tsx) đọc error.response.data.attemptsRemaining để hiện "Bạn còn N lần thử".
        if (attemptsRemaining != null)
        {
            ExtraData["attemptsRemaining"] = attemptsRemaining;
        }
    }
}
