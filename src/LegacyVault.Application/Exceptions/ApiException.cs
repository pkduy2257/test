namespace LegacyVault.Application.Exceptions;

/// <summary>
/// Lớp cha của mọi lỗi nghiệp vụ "dự đoán được" (sai mật khẩu, tài khoản bị khóa...).
/// </summary>
/// <remarks>
/// Service chỉ cần: throw new InvalidCredentialsException();
/// GlobalExceptionHandler (project Api) sẽ bắt và đổi thành JSON lỗi gửi cho FE,
/// giống @ControllerAdvice + @ExceptionHandler bên Spring Boot.
/// </remarks>
public class ApiException : Exception
{
    public ApiException(int statusCode, string errorCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    /// <summary>HTTP status trả về, ví dụ 401.</summary>
    public int StatusCode { get; }

    /// <summary>Mã UPPER_SNAKE_CASE để FE so sánh, ví dụ INVALID_CREDENTIALS.</summary>
    public string ErrorCode { get; }

    /// <summary>Dữ liệu kèm theo, ví dụ attemptsRemaining. Sẽ nằm ở cấp cao nhất của JSON lỗi.</summary>
    public Dictionary<string, object?> ExtraData { get; } = new Dictionary<string, object?>();
}
