using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;
using LegacyVault.Application.Exceptions;
using LegacyVault.Application.Repositories;
using LegacyVault.Application.Security;
using LegacyVault.Domain.Entities;
using LegacyVault.Domain.Enums;

namespace LegacyVault.Application.Services;

/// <summary>
/// Nghiệp vụ đăng nhập / làm mới phiên / đăng xuất (giống một @Service bên Spring Boot).
/// </summary>
/// <remarks>
/// Mọi thứ service cần đều là INTERFACE, được DI container truyền vào constructor
/// → khi viết unit test có thể thay bằng bản giả.
/// Lỗi nghiệp vụ được báo bằng cách throw ApiException (xem thư mục Exceptions).
/// TimeProvider (có sẵn từ .NET 8) thay cho DateTime.UtcNow để test được "15 phút sau".
/// </remarks>
public sealed class AuthService : IAuthService
{
    /// <summary>Sai mật khẩu đủ số lần này thì khóa tài khoản.</summary>
    public const int MaxFailedLoginAttempts = 5;

    /// <summary>Thời gian khóa tài khoản.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Hash của một mật khẩu ngẫu nhiên, dùng để "giả vờ" kiểm tra khi email không tồn tại (xem LoginAsync).
    private static string? _dummyHash;

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Chuẩn hóa email để so sánh: bỏ khoảng trắng, chữ thường.
    /// "  An@Gmail.com " và "an@gmail.com" phải là cùng một tài khoản.
    /// </summary>
    public static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();
        var email = NormalizeEmail(request.Email);

        // 1. Tải dữ liệu.
        var user = await _userRepository.FindByEmailAsync(email, ct);

        if (user == null)
        {
            // Vẫn chạy Argon2 cho tốn đúng bấy nhiêu thời gian như khi email có thật.
            // Nếu bỏ dòng này, kẻ tấn công đo thời gian phản hồi là biết email nào đã đăng ký.
            if (_dummyHash == null)
            {
                _dummyHash = _passwordHasher.Hash(Guid.NewGuid().ToString());
            }
            _passwordHasher.Verify(request.Password, _dummyHash);
            throw new InvalidCredentialsException();
        }

        // 2. Kiểm tra quy tắc nghiệp vụ.
        if (IsLockedOut(user, now))
        {
            throw new AccountLockedException(GetLockoutRemainingSeconds(user, now));
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            var attemptsRemaining = RegisterFailedLogin(user, now);
            await _userRepository.SaveChangesAsync(ct); // lưu số lần sai
            if (attemptsRemaining == 0)
            {
                throw new AccountLockedException(GetLockoutRemainingSeconds(user, now));
            }

            throw new InvalidCredentialsException(attemptsRemaining);
        }

        // Kiểm tra trạng thái SAU khi mật khẩu đúng, để người lạ không dò được trạng thái tài khoản.
        if (user.Status != UserStatus.Active)
        {
            throw new AccountNotActiveException();
        }

        // 3. Đổi trạng thái user.
        RegisterSuccessfulLogin(user, now);

        // 4. Mở phiên mới: một family refresh token mới cho thiết bị này.
        var refresh = _tokenService.CreateRefreshToken();
        var newToken = AddRefreshToken(user, refresh.Hash, Guid.CreateVersion7(), request.RememberMe,
            now.Add(_tokenService.SessionLifetime), now);
        var result = BuildAuthResult(user, refresh.Value, newToken, now);

        // 5. Lưu MỘT lần cho mọi thay đổi (user + refresh token) → cùng một transaction.
        //    Hai repository dùng chung một DbContext nên gọi SaveChanges ở đâu cũng lưu cả hai.
        await _userRepository.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new SessionExpiredException();
        }

        var now = _timeProvider.GetUtcNow();
        var hash = _tokenService.HashRefreshToken(refreshToken);

        var stored = await _refreshTokenRepository.FindByHashWithUserAsync(hash, ct);

        if (stored == null)
        {
            throw new SessionExpiredException();
        }

        if (stored.RevokedAt != null)
        {
            // Token đã bị xoay vòng/thu hồi mà vẫn có người dùng lại → nghi bị đánh cắp.
            // Thu hồi cả family để cả kẻ gian lẫn chủ thật đều phải đăng nhập lại.
            await RevokeFamilyAsync(stored.FamilyId, now, ct);
            await _refreshTokenRepository.SaveChangesAsync(ct);
            throw new SessionExpiredException();
        }

        var user = stored.User;
        var idleDeadline = stored.LastActivityAt.Add(_tokenService.GetIdleTimeout(user.Role));
        if (IsExpired(stored, now) || idleDeadline <= now
            || user.Status != UserStatus.Active || IsLockedOut(user, now))
        {
            RevokeToken(stored, now, null);
            await _refreshTokenRepository.SaveChangesAsync(ct);
            throw new SessionExpiredException();
        }

        // Xoay vòng: cấp token mới cùng family, thu hồi token cũ và ghi lại token thay thế.
        var refresh = _tokenService.CreateRefreshToken();
        var newToken = AddRefreshToken(user, refresh.Hash, stored.FamilyId, stored.IsPersistent,
            stored.ExpiresAt, now);   // giữ hạn chót cũ, KHÔNG cộng thêm 12 giờ
        var result = BuildAuthResult(user, refresh.Value, newToken, now);
        RevokeToken(stored, now, newToken.Id);

        await _refreshTokenRepository.SaveChangesAsync(ct);
        return result;
    }

    public async Task LogoutAsync(string? refreshToken, bool revokeAllDevices, CancellationToken ct)
    {
        // Đăng xuất luôn "thành công" với FE, kể cả khi token đã hết hạn hay không tồn tại.
        // (Idempotent: bấm đăng xuất 2 lần không được báo lỗi.)
        var now = _timeProvider.GetUtcNow();

        RefreshToken? stored = null;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = _tokenService.HashRefreshToken(refreshToken);
            stored = await _refreshTokenRepository.FindByHashAsync(hash, ct);
        }

        if (revokeAllDevices)
        {
            // Xác định người dùng từ refresh token, hoặc từ access token nếu còn hạn.
            Guid? userId = _currentUser.UserId;
            if (stored != null)
            {
                userId = stored.UserId;
            }

            if (userId != null)
            {
                var all = await _refreshTokenRepository.FindActiveByUserAsync(userId.Value, ct);
                foreach (var token in all)
                {
                    RevokeToken(token, now, null);
                }
            }
        }
        else if (stored != null)
        {
            await RevokeFamilyAsync(stored.FamilyId, now, ct);
        }

        await _refreshTokenRepository.SaveChangesAsync(ct);
    }

    public async Task<UserResponse> GetCurrentUserAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
        {
            throw new SessionExpiredException();
        }

        var user = await _userRepository.FindByIdAsync(userId.Value, ct);
        if (user == null || user.Status != UserStatus.Active)
        {
            throw new SessionExpiredException();
        }

        return UserResponse.From(user);
    }

    // ===== Luật khóa tài khoản =====

    private static bool IsLockedOut(User user, DateTimeOffset now)
    {
        return user.LockoutEndsAt != null && user.LockoutEndsAt.Value > now;
    }

    /// <summary>Số giây còn lại đến khi mở khóa, làm tròn lên (0 nếu không bị khóa).</summary>
    private static int GetLockoutRemainingSeconds(User user, DateTimeOffset now)
    {
        if (!IsLockedOut(user, now))
        {
            return 0;
        }

        return (int)Math.Ceiling((user.LockoutEndsAt!.Value - now).TotalSeconds);
    }

    /// <summary>
    /// Ghi nhận một lần nhập sai mật khẩu.
    /// </summary>
    /// <returns>Số lần thử còn lại. Trả 0 nghĩa là vừa bị khóa.</returns>
    private static int RegisterFailedLogin(User user, DateTimeOffset now)
    {
        // Lần khóa trước đã hết hạn → đếm lại từ đầu.
        if (user.LockoutEndsAt != null && user.LockoutEndsAt.Value <= now)
        {
            user.LockoutEndsAt = null;
            user.FailedLoginCount = 0;
        }

        user.FailedLoginCount++;
        user.UpdatedAt = now;

        if (user.FailedLoginCount >= MaxFailedLoginAttempts)
        {
            user.LockoutEndsAt = now.Add(LockoutDuration);
            return 0;
        }

        return MaxFailedLoginAttempts - user.FailedLoginCount;
    }

    private static void RegisterSuccessfulLogin(User user, DateTimeOffset now)
    {
        user.FailedLoginCount = 0;
        user.LockoutEndsAt = null;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
    }

    // ===== Luật refresh token =====

    private static bool IsExpired(RefreshToken token, DateTimeOffset now)
    {
        return token.ExpiresAt <= now;
    }

    /// <summary>Thu hồi token. Gọi nhiều lần vẫn an toàn (idempotent).</summary>
    private static void RevokeToken(RefreshToken token, DateTimeOffset now, Guid? replacedByTokenId)
    {
        if (token.RevokedAt != null)
        {
            return;
        }

        token.RevokedAt = now;
        token.ReplacedByTokenId = replacedByTokenId;
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct)
    {
        var family = await _refreshTokenRepository.FindActiveByFamilyAsync(familyId, ct);
        foreach (var token in family)
        {
            RevokeToken(token, now, null);
        }
    }

    /// <summary>Tạo bản ghi refresh token mới (chưa ghi DB, chỉ đánh dấu "sẽ INSERT" khi SaveChanges).</summary>
    private RefreshToken AddRefreshToken(
        User user, string tokenHash, Guid familyId, bool isPersistent,
        DateTimeOffset sessionExpiresAt, DateTimeOffset now)
    {
        var token = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            FamilyId = familyId,
            IsPersistent = isPersistent,
            CreatedAt = now,
            ExpiresAt = sessionExpiresAt,
            LastActivityAt = now,
        };
        _refreshTokenRepository.Add(token);
        return token;
    }

    /// <summary>Tạo access token và gom mọi thứ controller cần (token để ghi cookie + body trả FE).</summary>
    private AuthResult BuildAuthResult(User user, string refreshTokenValue, RefreshToken refreshToken, DateTimeOffset now)
    {
        var access = _tokenService.CreateAccessToken(user, refreshToken.FamilyId, refreshToken.ExpiresAt);

        var session = new AuthSessionResponse
        {
            UserId = user.Id,
            ExpiresIn = (int)(access.ExpiresAt - now).TotalSeconds,
            User = UserResponse.From(user),
        };

        return new AuthResult
        {
            AccessToken = access.Value,
            AccessTokenExpiresAt = access.ExpiresAt,
            RefreshToken = refreshTokenValue,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt,
            IsPersistent = refreshToken.IsPersistent,
            Session = session,
        };
    }
}
