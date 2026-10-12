# Hướng dẫn đọc source code LegacyVault Backend

Tài liệu này dành cho người mới vào dự án, đặc biệt là người đã quen Java/Spring Boot. Đọc xong bạn sẽ biết: code nằm ở đâu, đọc theo thứ tự nào, một request đi qua những file nào, và muốn thêm tính năng thì đặt code vào đâu.

> Áp dụng cho nhánh `refactor/java-style` trở đi (cấu trúc Controller → Service → Repository → Entity).

---

## 1. Bức tranh tổng thể

Dự án là một **ASP.NET Core Web API (.NET 10)**, viết bằng C#, lưu dữ liệu trong **SQL Server** qua **EF Core**. Hiện có module **xác thực**: đăng nhập, xem phiên, làm mới phiên, đăng xuất.

Code chia thành **4 project**, mỗi project là một "lớp":

```
            ┌──────────────────────────┐
  HTTP ───► │  LegacyVault.Api         │  ④ Controller, cookie, chuẩn lỗi, Program.cs
            └──────┬─────────────┬─────┘
                   │             │
                   ▼             ▼
  ┌──────────────────────┐   ┌──────────────────────────────┐
  │ LegacyVault.         │◄──│ LegacyVault.Infrastructure   │  ③ EF Core, SQL Server, JWT, Argon2
  │ Application          │   └──────────────────────────────┘
  │ ② Service, DTO,      │
  │   interface, lỗi     │
  └──────────┬───────────┘
             ▼
  ┌──────────────────────┐
  │ LegacyVault.Domain   │  ① Entity, Enum (không phụ thuộc gì)
  └──────────────────────┘
```

Mũi tên nghĩa là "biết đến / tham chiếu". **Quy tắc duy nhất: lớp trong không bao giờ biết lớp ngoài.**

| Project | Chứa gì | Tương đương bên Spring Boot |
| --- | --- | --- |
| `LegacyVault.Domain` | Entity (chỉ có thuộc tính), Enum | package `entity`, `enums` |
| `LegacyVault.Application` | Service (luật nghiệp vụ), interface Repository, DTO, Exception | package `service`, `repository` (interface), `dto`, `exception` |
| `LegacyVault.Infrastructure` | Cài đặt Repository bằng EF Core, DbContext, Migration, JWT, băm mật khẩu | phần Spring Data JPA tự sinh + `security` + `config` cho DB |
| `LegacyVault.Api` | Controller, filter lỗi, cấu hình HTTP, ghi cookie, `Program.cs` | package `controller`, `@ControllerAdvice`, class `main` |

Vì sao tách interface Repository (ở Application) khỏi cài đặt (ở Infrastructure)? Để `AuthService` không biết dữ liệu nằm trong SQL Server. Đổi DB hay viết unit test với repository giả chỉ cần thay phần Infrastructure.

---

## 2. Cây thư mục chi tiết

```
legacyvault-be/
├── LegacyVault.sln                 Mở file này bằng Visual Studio / Rider
├── Directory.Build.props           Cấu hình chung cho 4 project (.NET 10, nullable…)
├── Directory.Packages.props        Version của MỌI thư viện NuGet khai báo ở đây (≈ dependencyManagement)
├── docker-compose.yml              SQL Server cho dev
├── http/auth.http                  Kịch bản gọi API bằng tay
├── docs/                           Tài liệu (spec, kế hoạch, file này)
└── src/
    ├── LegacyVault.Domain/
    │   ├── Entities/
    │   │   ├── BaseEntity.cs        Lớp cha, có Id (Guid)
    │   │   ├── User.cs              Bảng Users
    │   │   └── RefreshToken.cs      Bảng RefreshTokens (phiên đăng nhập)
    │   └── Enums/
    │       ├── UserRole.cs          Owner, Verifier, Admin
    │       └── UserStatus.cs        Pending, Active, Inactive
    │
    ├── LegacyVault.Application/
    │   ├── Services/
    │   │   ├── IAuthService.cs      Interface (controller chỉ biết cái này)
    │   │   └── AuthService.cs       ★ TOÀN BỘ NGHIỆP VỤ ĐĂNG NHẬP
    │   ├── Repositories/
    │   │   ├── IUserRepository.cs
    │   │   └── IRefreshTokenRepository.cs
    │   ├── Security/                Interface cho các thứ kỹ thuật service cần
    │   │   ├── IPasswordHasher.cs
    │   │   ├── ITokenService.cs     + AccessToken.cs, GeneratedRefreshToken.cs
    │   │   └── ICurrentUser.cs      Ai đang gọi API (đọc từ JWT)
    │   ├── Dtos/
    │   │   ├── Requests/            LoginRequest, LogoutRequest      (FE gửi lên)
    │   │   └── Responses/           UserResponse, AuthSessionResponse,
    │   │                            CurrentSessionResponse, AuthResult (trả về)
    │   ├── Exceptions/
    │   │   ├── ApiException.cs      Lớp cha: StatusCode + ErrorCode + ExtraData
    │   │   ├── InvalidCredentialsException.cs   401
    │   │   ├── AccountLockedException.cs        423
    │   │   ├── AccountNotActiveException.cs     403
    │   │   └── SessionExpiredException.cs       401
    │   └── ApplicationConfig.cs     Đăng ký AuthService vào DI
    │
    ├── LegacyVault.Infrastructure/
    │   ├── Repositories/
    │   │   ├── UserRepository.cs           Câu truy vấn EF Core cho Users
    │   │   └── RefreshTokenRepository.cs   Câu truy vấn EF Core cho RefreshTokens
    │   ├── Data/
    │   │   ├── AppDbContext.cs      Cửa ngõ EF Core (≈ EntityManager)
    │   │   ├── DbSeeder.cs          Dev: chạy migration + tạo 3 tài khoản demo
    │   │   ├── Configurations/      Cấu hình cột, index cho từng bảng (≈ annotation @Column, @Index)
    │   │   └── Migrations/          Lịch sử thay đổi schema (≈ Flyway/Liquibase)
    │   ├── Security/
    │   │   ├── Argon2PasswordHasher.cs   Băm mật khẩu
    │   │   ├── JwtTokenService.cs        Tạo JWT + refresh token
    │   │   ├── JwtSettings.cs            Đọc mục "Jwt" trong appsettings
    │   │   ├── JwtClaimTypes.cs
    │   │   ├── CurrentUser.cs            Đọc userId từ JWT
    │   │   └── AuthCookieNames.cs        Tên + Path của 2 cookie
    │   └── InfrastructureConfig.cs  Đăng ký DB, repository, JWT (≈ @Configuration)
    │
    └── LegacyVault.Api/
        ├── Program.cs               ★ ĐIỂM KHỞI ĐỘNG (≈ main + SpringApplication.run)
        ├── Controllers/
        │   └── AuthController.cs    4 endpoint /api/v1/auth/*
        ├── Exceptions/
        │   ├── ApiExceptionFilter.cs     ApiException → JSON lỗi (≈ @ControllerAdvice)
        │   └── GlobalExceptionHandler.cs Lỗi bất ngờ → 500
        ├── Config/
        │   ├── ApiConfig.cs         Controller + định dạng lỗi validate 400
        │   └── CorsConfig.cs        Cho phép FE localhost:5173 gọi API
        ├── Helpers/
        │   ├── ApiResponse.cs       Vỏ { "data": ... } cho response thành công
        │   └── AuthCookieWriter.cs  Ghi / xóa cookie HttpOnly
        ├── appsettings.json                 Cấu hình chung (≈ application.properties)
        └── appsettings.Development.json     Cấu hình dev (connection string, CORS)
```

---

## 3. Thứ tự đọc đề xuất

Đọc theo **đường đi của một request đăng nhập**, từ ngoài vào trong. Mỗi bước chỉ mở 1–2 file.

| Bước | File | Đọc để hiểu | Thời gian |
| --- | --- | --- | --- |
| 1 | `Api/Program.cs` | App khởi động: đăng ký service theo từng lớp, rồi thứ tự middleware | 5' |
| 2 | `Api/Controllers/AuthController.cs` | 4 endpoint. Mỗi hàm: nhận request → gọi service → ghi cookie → trả `Ok(...)` | 10' |
| 3 | `Application/Dtos/Requests/LoginRequest.cs` | FE gửi gì, validate bằng `[Required]`, `[EmailAddress]` | 3' |
| 4 | `Application/Services/IAuthService.cs` | Service làm được những gì | 3' |
| 5 | `Application/Services/AuthService.cs` | ★ Đọc `LoginAsync` trước → `RefreshAsync` → `LogoutAsync`. Các hàm private ở cuối file là luật nghiệp vụ | 30' |
| 6 | `Application/Repositories/IUserRepository.cs` → `Infrastructure/Repositories/UserRepository.cs` | Interface và câu truy vấn EF thật | 10' |
| 7 | `Domain/Entities/User.cs`, `RefreshToken.cs` | Dữ liệu trong DB; đọc comment trong `RefreshToken.cs` để hiểu "xoay vòng token" | 10' |
| 8 | `Application/Exceptions/*` → `Api/Exceptions/ApiExceptionFilter.cs` | Lỗi được throw ở đâu, biến thành JSON ở đâu | 10' |
| 9 | `Api/Helpers/AuthCookieWriter.cs` | Cookie HttpOnly / SameSite / Path | 5' |
| 10 | `Infrastructure/InfrastructureConfig.cs` | DI: interface nào gắn với class nào; JWT đọc token từ cookie | 15' |
| 11 | `Infrastructure/Data/Configurations/*`, `Data/DbSeeder.cs` | Cấu hình bảng, dữ liệu demo | 10' |

**Mẹo khi đọc trong IDE:**
- Đặt con trỏ vào tên interface (vd. `IUserRepository`) rồi bấm **Ctrl+F12** (Visual Studio) hoặc **Ctrl+Alt+B** (Rider) để nhảy tới class cài đặt.
- **Shift+F12** để xem mọi chỗ dùng một hàm.
- Đặt breakpoint trong `AuthService.LoginAsync`, chạy F5, gọi login trong Swagger để xem từng dòng chạy.

---

## 4. Một request đi qua những đâu

### 4.1. Đăng nhập: `POST /api/v1/auth/login`

```
FE ──► Program.cs pipeline: ExceptionHandler → CORS → Authentication → Authorization
        │
        ▼
   AuthController.Login(LoginRequest)
        │  [ApiController] tự validate LoginRequest. Sai → 400 VALIDATION_ERROR (ApiConfig), không vào hàm.
        ▼
   AuthService.LoginAsync
        1. email = NormalizeEmail(...)                     "  An@X.com " → "an@x.com"
        2. userRepository.FindByEmailAsync(email)          SELECT … WHERE Email = @email
           └─ không có → vẫn chạy Argon2 giả (chống dò email) → throw InvalidCredentialsException
        3. IsLockedOut(user)?                              → throw AccountLockedException (423)
        4. passwordHasher.Verify(...) sai?
           └─ RegisterFailedLogin → lưu DB → throw InvalidCredentials (401 + attemptsRemaining)
                                              hoặc AccountLocked nếu vừa đủ 5 lần
        5. user.Status != Active?                          → throw AccountNotActiveException (403)
        6. RegisterSuccessfulLogin
        7. Tạo refresh token (lưu HASH) + access token JWT
        8. SaveChangesAsync() — MỘT lần cho cả user lẫn token
        │
        ▼
   AuthController: AuthCookieWriter.Append → 2 cookie lv_access, lv_refresh
        │
        ▼
   200 { "data": { "userId", "expiresIn", "user": {...} } }
```

Nếu bất kỳ bước nào **throw ApiException** → `ApiExceptionFilter` trả:

```json
{ "type": "...", "title": "Email hoặc mật khẩu chưa đúng.", "status": 401,
  "traceId": "...", "code": "INVALID_CREDENTIALS", "attemptsRemaining": 3 }
```

### 4.2. Các request sau đó (cần đăng nhập)

```
Trình duyệt tự gửi cookie lv_access
  → JwtBearer (cấu hình ở InfrastructureConfig.ReadAccessTokenFromCookie) đọc token từ cookie
  → kiểm tra chữ ký + hạn → điền HttpContext.User
  → [Authorize] cho qua → CurrentUser.UserId đọc claim "sub"
```

### 4.3. Làm mới phiên: `POST /api/v1/auth/refresh`

```
AuthController.Refresh ── đọc cookie lv_refresh
  → AuthService.RefreshAsync
      token không tồn tại                 → SessionExpired
      token ĐÃ bị thu hồi (bị dùng lại)  → thu hồi CẢ family (nghi bị đánh cắp) → SessionExpired
      hết hạn / user bị khóa / inactive   → thu hồi token này → SessionExpired
      hợp lệ → tạo token mới cùng family, thu hồi token cũ (ReplacedByTokenId = token mới)
  → SessionExpired? controller xóa cookie rồi ném tiếp cho ApiExceptionFilter (401)
```

### 4.4. Đăng xuất: `POST /api/v1/auth/logout`

Luôn trả **204**, kể cả khi token đã hết hạn hoặc gọi 2 lần. Body `{ "revokeAllDevices": true }` thì thu hồi mọi phiên của user.

---

## 5. Bảng tra nhanh C# ↔ Java

| C# / ASP.NET Core | Java / Spring Boot |
| --- | --- |
| `[ApiController]`, `[Route("api/v1/auth")]` | `@RestController`, `@RequestMapping("/api/v1/auth")` |
| `[HttpPost("login")]` | `@PostMapping("/login")` |
| `[Authorize]` / `[AllowAnonymous]` | `@PreAuthorize` / `permitAll()` |
| `[Required]`, `[EmailAddress]`, `[MaxLength]` | `@NotBlank`, `@Email`, `@Size` |
| `IActionResult`, `Ok(...)`, `NoContent()` | `ResponseEntity.ok(...)`, `ResponseEntity.noContent()` |
| `services.AddScoped<IAuthService, AuthService>()` | `@Service` + `@Autowired` (bean phạm vi request) |
| `AddSingleton` / `AddScoped` | singleton bean / request-scoped bean |
| Constructor nhận interface (DI) | Constructor injection |
| `IExceptionFilter` (`ApiExceptionFilter`) | `@ControllerAdvice` + `@ExceptionHandler` |
| `DbContext`, `DbSet<User>` | `EntityManager`, `JpaRepository<User, UUID>` |
| `SingleOrDefaultAsync(u => u.Email == email)` | `findByEmail(email)` trả `Optional` |
| `Include(t => t.User)` | `JOIN FETCH` |
| `AsNoTracking()` | `@Transactional(readOnly = true)` |
| `SaveChangesAsync()` | `flush()` / commit transaction |
| `IEntityTypeConfiguration<User>` | annotation `@Table`, `@Column`, `@Index` |
| EF Core Migrations | Flyway / Liquibase |
| `appsettings.json` | `application.properties` / `.yml` |
| `IOptions<JwtSettings>` | `@ConfigurationProperties(prefix = "jwt")` |
| `async Task<T>` / `await` | `CompletableFuture<T>` (ở đây chỉ cần hiểu là "chờ kết quả") |
| `CancellationToken ct` | Tín hiệu hủy khi client ngắt kết nối; cứ truyền tiếp xuống |
| `string?` | Kiểu có thể null (≈ `@Nullable`) |
| `public string Email { get; set; }` | field `private String email` + getter/setter |
| `Guid` | `UUID` |
| `DateTimeOffset` | `OffsetDateTime` |
| `var x = ...` | `var x = ...` (Java 10+) |
| LINQ `.Where(...).Select(...).ToList()` | Stream `.filter(...).map(...).toList()` |

---

## 6. Quy ước code trong dự án

- **Constructor kiểu Java:** field `private readonly` + constructor gán giá trị. Không dùng primary constructor `class X(IFoo foo)`.
- **Mỗi class / interface / enum một file**, namespace khớp đường dẫn thư mục (`Application/Services/` → `LegacyVault.Application.Services`).
- **DTO** là `class` có `{ get; set; }`, không dùng `record`.
- **Kiểm tra null rõ ràng:** `if (user == null)`, không dùng `?.`, `??`, `is { } x`.
- **Lỗi nghiệp vụ:** `throw new XxxException(...)`. Không trả `null` hay mã lỗi.
- **Entity chỉ chứa dữ liệu**; luật nghiệp vụ đặt trong Service (hàm `private static` ở cuối `AuthService`).
- **Comment tiếng Việt**, giải thích *vì sao*, không lặp lại code làm gì.

---

## 7. Thêm một tính năng mới: đặt code ở đâu?

Ví dụ thêm **đăng ký tài khoản** `POST /api/v1/auth/register`:

| Bước | Việc | File |
| --- | --- | --- |
| 1 | DTO đầu vào | `Application/Dtos/Requests/RegisterRequest.cs` |
| 2 | Lỗi mới (409) | `Application/Exceptions/EmailAlreadyExistsException.cs` kế thừa `ApiException` |
| 3 | Hàm repository cần thêm | `IUserRepository.Add(User)` + cài đặt trong `UserRepository` |
| 4 | Nghiệp vụ | `IAuthService.RegisterAsync` + cài đặt trong `AuthService` |
| 5 | Endpoint | `AuthController.Register` |
| 6 | Thử | `http/auth.http` hoặc Swagger |

Nếu tính năng cần **bảng mới**, thêm các bước:

1. Entity trong `Domain/Entities/`.
2. `Data/Configurations/<X>Configuration.cs`.
3. `DbSet<X>` trong `AppDbContext`.
4. Interface trong `Application/Repositories/` + class trong `Infrastructure/Repositories/`, đăng ký trong `InfrastructureConfig.AddRepositories`.
5. Service mới thì đăng ký trong `ApplicationConfig`.
6. Tạo migration:

```powershell
dotnet ef migrations add <TenMigration> --project src/LegacyVault.Infrastructure --startup-project src/LegacyVault.Api --output-dir Data/Migrations
```

**Câu hỏi để chọn chỗ đặt code:** *"Đây là dữ liệu, luật nghiệp vụ, chi tiết kỹ thuật (DB, JWT, mật khẩu), hay chuyện HTTP (route, cookie, status)?"* → Domain / Application / Infrastructure / Api.

---

## 8. Chạy thử để đọc code dễ hơn

**Cách 1: LocalDB (không cần Docker, có sẵn khi cài Visual Studio)**

```powershell
cd src/LegacyVault.Api
dotnet user-secrets set "Jwt:SigningKey" ([guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N"))
dotnet user-secrets set "ConnectionStrings:Default" "Server=(localdb)\MSSQLLocalDB;Database=LegacyVault;Trusted_Connection=True;TrustServerCertificate=True"
cd ../..
dotnet run --project src/LegacyVault.Api --launch-profile http
```

**Cách 2: SQL Server bằng Docker:** xem README mục 1.

Mở `http://localhost:5000/swagger`. Tài khoản demo (mật khẩu chung `Password123!`):

| Email | Ghi chú |
| --- | --- |
| `owner@legacyvault.local` | Tài khoản thường |
| `admin@legacyvault.local` | Quản trị |
| `inactive@legacyvault.local` | Bị vô hiệu → 403 |

---

## 9. Tài liệu liên quan

- `README.md`: cài đặt, chạy, hợp đồng API, bài tập tự làm.
- `docs/superpowers/specs/2026-10-12-java-style-refactor-design.md`: vì sao code được tổ chức như hiện nay.
- `http/auth.http`: kịch bản gọi API mẫu.
