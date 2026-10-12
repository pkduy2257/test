# Thiết kế: Cấu trúc lại backend LegacyVault theo kiểu Java/Spring Boot

- Ngày: 2026-10-12
- Nhánh làm việc: `refactor/java-style` (tạo từ `test`)
- Trạng thái: chờ duyệt

## 1. Mục tiêu

Code hiện tại (khoảng 1.500 dòng C#, Clean Architecture 4 project) khó đọc với người mới học, nhất là người quen Java. Mục tiêu là sắp xếp lại code để người học Java đọc được ngay theo mạch quen thuộc **Controller → Service → Repository → Entity**.

### Ràng buộc đã chốt

| # | Ràng buộc |
|---|-----------|
| R1 | Giữ ngôn ngữ **C# / ASP.NET Core (.NET 10)**, giữ EF Core + SQL Server. |
| R2 | Giữ **4 project**: `Domain`, `Application`, `Infrastructure`, `Api`; giữ chiều phụ thuộc hiện tại (Api → Infrastructure → Application → Domain). |
| R3 | Hành vi API giữ nguyên **100%**: URL, status code, JSON thành công `{ "data": ... }`, JSON lỗi ProblemDetails (`type`, `title`, `status`, `code`, `attemptsRemaining`, `remainingSeconds`, `errors`, `traceId`), header `application/problem+json`, tên/Path/cờ cookie, schema DB. |
| R4 | Giữ nguyên toàn bộ luật nghiệp vụ và bảo mật (xem mục 5). |

### Ngoài phạm vi

- Thêm tính năng mới.
- Thêm project unit test.
- Đổi cấu hình deploy, Docker.

## 2. Cấu trúc thư mục mới

```
LegacyVault.Domain/
├── Entities/       BaseEntity.cs, User.cs, RefreshToken.cs
└── Enums/          UserRole.cs, UserStatus.cs

LegacyVault.Application/
├── Services/       IAuthService.cs, AuthService.cs
├── Repositories/   IUserRepository.cs, IRefreshTokenRepository.cs
├── Security/       IPasswordHasher.cs, ITokenService.cs, ICurrentUser.cs
├── Dtos/
│   ├── Requests/   LoginRequest.cs, LogoutRequest.cs
│   └── Responses/  UserResponse.cs, AuthSessionResponse.cs, CurrentSessionResponse.cs, AuthResult.cs
├── Exceptions/     ApiException.cs, InvalidCredentialsException.cs, AccountLockedException.cs,
│                   AccountNotActiveException.cs, SessionExpiredException.cs
└── ApplicationConfig.cs

LegacyVault.Infrastructure/
├── Repositories/   UserRepository.cs, RefreshTokenRepository.cs
├── Security/       Argon2PasswordHasher.cs, JwtTokenService.cs, CurrentUser.cs,
│                   JwtSettings.cs, AuthCookieNames.cs
├── Data/           AppDbContext.cs, DbSeeder.cs
│   ├── Configurations/  UserConfiguration.cs, RefreshTokenConfiguration.cs
│   └── Migrations/      (3 file migration hiện có)
└── InfrastructureConfig.cs

LegacyVault.Api/
├── Controllers/    AuthController.cs
├── Exceptions/     GlobalExceptionHandler.cs
├── Config/         ApiConfig.cs, CorsConfig.cs
├── Helpers/        AuthCookieWriter.cs, ApiResponse.cs
└── Program.cs
```

### Bảng chuyển đổi file

| File cũ | File mới | Ghi chú |
|---------|----------|---------|
| `Domain/Common/Entity.cs` | `Domain/Entities/BaseEntity.cs` | Đổi tên lớp `Entity` → `BaseEntity`. |
| `Domain/Common/Result.cs`, `Error.cs` | *(xóa)* | Thay bằng exception (mục 3). |
| `Domain/Identity/User.cs`, `RefreshToken.cs` | `Domain/Entities/` | Entity đơn giản (mục 4). |
| `Domain/Identity/UserRole.cs`, `UserStatus.cs` | `Domain/Enums/` | Giữ nguyên giá trị enum. |
| `Application/Common/Interfaces/IAppDbContext.cs` | *(xóa)* | Thay bằng 2 interface repository. |
| `Application/Common/Interfaces/I{PasswordHasher,TokenService,CurrentUser}.cs` | `Application/Security/` | |
| `Application/Features/Identity/AuthService.cs` | `Application/Services/IAuthService.cs` + `AuthService.cs` | Tách interface ra file riêng. |
| `Application/Features/Identity/AuthDtos.cs` | `Application/Dtos/Requests/*`, `Dtos/Responses/*` | Mỗi class một file; `UserDto` → `UserResponse`, `AuthSessionDto` → `AuthSessionResponse`, `CurrentSessionDto` → `CurrentSessionResponse`. **Tên property giữ nguyên** nên JSON không đổi. |
| `Application/Features/Identity/AuthErrors.cs` | `Application/Exceptions/*` | Mỗi lỗi một class exception. |
| `Application/DependencyInjection.cs` | `Application/ApplicationConfig.cs` | |
| `Infrastructure/Persistence/*` | `Infrastructure/Data/*` | `DbInitializer` → `DbSeeder`. |
| `Infrastructure/Security/JwtOptions.cs` | `Infrastructure/Security/JwtSettings.cs` | Section config vẫn là `"Jwt"`. |
| `Infrastructure/DependencyInjection.cs` | `Infrastructure/InfrastructureConfig.cs` | |
| `Api/Common/ApiControllerBase.cs` | *(xóa)* | Controller kế thừa thẳng `ControllerBase`. |
| `Api/Common/GlobalExceptionHandler.cs` | `Api/Exceptions/GlobalExceptionHandler.cs` | Thêm nhánh xử lý `ApiException`. |
| `Api/Common/ApiResponse.cs`, `AuthCookieWriter.cs` | `Api/Helpers/` | |
| `Api/Extensions/ApiServiceExtensions.cs` | `Api/Config/ApiConfig.cs` + `CorsConfig.cs` | |
| `Api/Controllers/V1/AuthController.cs` | `Api/Controllers/AuthController.cs` | Route vẫn là `api/v1/auth`. |

## 3. Quy ước viết code (cho người học Java)

- Constructor kiểu Java: field `private readonly` + constructor gán giá trị; **không dùng primary constructor**.
- Mỗi class/interface/enum một file; namespace khớp đường dẫn thư mục.
- Tránh cú pháp khó với người mới: pattern `is { } x`, `??=`, switch expression lồng, `out` parameter, `init`-only phức tạp. Dùng `if` / `== null` rõ ràng.
- DTO viết dạng `class` với property `{ get; set; }` (không dùng `record` positional).
- Comment tiếng Việt, giải thích **vì sao**, giữ các comment giải thích bảo mật đang có.
- Method async giữ hậu tố `Async` và tham số `CancellationToken` như hiện tại.

## 4. Xử lý lỗi bằng exception

### Lớp exception (project Application)

```csharp
public class ApiException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }
    public Dictionary<string, object?> ExtraData { get; } = new();
}
```

| Exception | Status | `code` | Message (`title`) | ExtraData |
|-----------|--------|--------|-------------------|-----------|
| `InvalidCredentialsException(int? attemptsRemaining = null)` | 401 | `INVALID_CREDENTIALS` | Email hoặc mật khẩu chưa đúng. | `attemptsRemaining` (chỉ khi có giá trị) |
| `AccountLockedException(int remainingSeconds)` | 423 | `ACCOUNT_LOCKED` | Tài khoản tạm khóa do nhập sai mật khẩu nhiều lần. Vui lòng thử lại sau. | `attemptsRemaining = 0`, `remainingSeconds` |
| `AccountNotActiveException()` | 403 | `ACCOUNT_NOT_ACTIVE` | Tài khoản chưa được kích hoạt hoặc đã bị vô hiệu hóa. | — |
| `SessionExpiredException()` | 401 | `SESSION_EXPIRED` | Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại. | — |

Nội dung message phải giữ **đúng từng chữ** như `AuthErrors.cs` hiện tại.

### `GlobalExceptionHandler` (project Api)

- **Gặp `ApiException`:** tạo ProblemDetails bằng `ProblemDetailsFactory.CreateProblemDetails(httpContext, statusCode, title: message)` để có `type` và `traceId` như bây giờ. Sau đó thêm `code` và từng mục trong `ExtraData`, set status code và trả `application/problem+json`. Không log ở mức Error (đây là lỗi nghiệp vụ, không phải bug).
- **Gặp exception khác:** giữ nguyên logic hiện tại (log Error, trả 500 `INTERNAL_ERROR`).

### Controller

- `Login`, `Session`: gọi service, trả về `Ok(new ApiResponse<T>(data))`. Không còn kiểm tra `IsFailure`.
- `Refresh`: bọc trong `try/catch (SessionExpiredException) { cookieWriter.Clear(Response); throw; }` để xóa cookie khi phiên hỏng, giống hành vi hiện tại.
- `Logout`: không đổi (luôn 204).

### Lỗi validate 400

`InvalidModelStateResponseFactory` giữ nguyên logic (camelCase tên field, `title = "Dữ liệu gửi lên không hợp lệ."`, `code = "VALIDATION_ERROR"`), chỉ chuyển sang `Api/Config/ApiConfig.cs`.

## 5. Entity và luật nghiệp vụ

### Entity: chỉ có thuộc tính get/set

```csharp
public class User : BaseEntity
{
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockoutEndsAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
```

- `BaseEntity.Id` vẫn mặc định `Guid.CreateVersion7()`.
- `RefreshToken` viết tương tự, giữ navigation `User`.
- Không còn factory `Create` hay method nghiệp vụ trong entity. `DbSeeder` dùng object initializer `new User { ... }`.

### Luật nghiệp vụ chuyển vào `AuthService` (giữ nguyên 100% hành vi)

Hằng số: `MaxFailedLoginAttempts = 5`, `LockoutDuration = 15 phút` (đặt trong `AuthService`).

| Luật | Hàm private trong `AuthService` |
|------|-------------------------------|
| Chuẩn hóa email: `Trim().ToLowerInvariant()` (dùng cho cả login và seeder) | `NormalizeEmail(string)`, public static để `DbSeeder` dùng chung |
| Bị khóa khi `LockoutEndsAt > now` | `IsLockedOut(User, now)` |
| Số giây còn khóa, làm tròn lên | `GetLockoutRemainingSeconds(User, now)` |
| Sai mật khẩu: nếu lần khóa trước đã hết thì reset; tăng đếm, cập nhật `UpdatedAt`; đủ 5 lần thì khóa 15 phút và trả 0, ngược lại trả số lần còn lại | `RegisterFailedLogin(User, now)` |
| Đăng nhập đúng: reset đếm/khóa, cập nhật `LastLoginAt`, `UpdatedAt` | `RegisterSuccessfulLogin(User, now)` |
| Thu hồi token idempotent, ghi `ReplacedByTokenId` | `RevokeToken(RefreshToken, now, Guid? replacedBy)` |
| Token hết hạn khi `ExpiresAt <= now` | `IsExpired(RefreshToken, now)` |

Các luật bảo mật phải giữ:

1. Email không tồn tại thì vẫn chạy Argon2 với hash giả (chống dò email qua thời gian phản hồi).
2. Kiểm tra trạng thái `Active` **sau** khi mật khẩu đúng.
3. Mỗi lần login tạo `FamilyId` mới (`Guid.CreateVersion7()`).
4. Refresh xoay vòng token: dùng lại token đã thu hồi thì thu hồi cả family.
5. Refresh khi token hết hạn, user không Active hoặc đang bị khóa: thu hồi token, trả `SESSION_EXPIRED`.
6. Logout luôn thành công; `revokeAllDevices` lấy userId từ refresh token, nếu không có thì từ access token.
7. Login lưu user + refresh token trong **một** lần `SaveChangesAsync` (một transaction).

## 6. Repository

```csharp
public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);           // AsNoTracking, chỉ đọc
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);           // không kèm User
    Task<RefreshToken?> FindByHashWithUserAsync(string tokenHash, CancellationToken ct);   // Include(User)
    Task<List<RefreshToken>> FindActiveByFamilyAsync(Guid familyId, CancellationToken ct); // RevokedAt == null
    Task<List<RefreshToken>> FindActiveByUserAsync(Guid userId, CancellationToken ct);     // RevokedAt == null
    void Add(RefreshToken token);
    Task SaveChangesAsync(CancellationToken ct);
}
```

- Hai repository được đăng ký `Scoped` và dùng chung **một** `AppDbContext` trong mỗi request. Gọi `SaveChangesAsync` ở repository nào cũng lưu mọi thay đổi đang chờ, nhờ đó giữ được luật 7. Điều này được ghi rõ bằng comment trong code.
- Câu truy vấn giữ đúng như hiện tại (`SingleOrDefaultAsync`, `Include`, `AsNoTracking`).

## 7. Database và migration

- Nội dung `UserConfiguration`, `RefreshTokenConfiguration` không đổi; chỉ cập nhật `using`/namespace.
- 3 file migration chuyển sang `Data/Migrations/`, đổi namespace và `using`. Giữ nguyên ID `20261011102914_InitialCreate` và `[DbContext(typeof(AppDbContext))]`.
- Tiêu chí: `dotnet ef migrations has-pending-model-changes` phải báo không có thay đổi.

## 8. Kiểm tra

1. `dotnet build` không có lỗi và không có cảnh báo mới.
2. `dotnet ef migrations has-pending-model-changes` không báo thay đổi.
3. **So sánh hành vi trước/sau.** Cần Docker SQL Server và `Jwt:SigningKey`.
   - Trước khi refactor, chạy bản cũ trên DB sạch và ghi lại status, body, header `Set-Cookie` của các kịch bản bên dưới.
   - Sau khi refactor, chạy lại trên DB sạch và so sánh. Chỉ cho phép khác ở giá trị ngẫu nhiên (token, `traceId`, `id`, thời gian).

   Các kịch bản:
   - login đúng, có và không có `rememberMe`
   - login sai mật khẩu (`attemptsRemaining` giảm dần)
   - sai lần thứ 5 (423, `remainingSeconds`); login khi đang bị khóa
   - login tài khoản inactive (403)
   - login email không tồn tại (401)
   - login thiếu email hoặc email sai định dạng (400 `VALIDATION_ERROR`)
   - `GET session` có cookie và không có cookie (401)
   - `refresh` hợp lệ; `refresh` lại bằng token cũ (401 và cả family bị thu hồi); `refresh` không có cookie
   - `logout`; `logout` với `revokeAllDevices: true`; `logout` hai lần liên tiếp

## 9. Git

1. Hoàn tất merge đang dở trên `test` (đã hỏi người dùng trước).
2. Tạo nhánh `refactor/java-style`.
3. Commit theo bước: spec → Domain → Application → Infrastructure → Api → README.
4. Cập nhật phần mô tả cấu trúc và đường dẫn file trong `README.md` cho khớp thư mục mới.
