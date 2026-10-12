# Cấu trúc lại backend theo kiểu Java: Kế hoạch triển khai

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sắp xếp lại 4 project C# theo mạch Controller → Service → Repository → Entity để người học Java đọc dễ, giữ nguyên 100% hành vi API.

**Architecture:** Làm theo từng lát cắt, mỗi task xong đều build được và qua bài so sánh hành vi:
1. Đổi `Result`/`Error` sang exception.
2. Thêm Repository.
3. Đơn giản hóa Entity.
4. Xếp lại thư mục, namespace và constructor.
5. README.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core (SQL Server), Argon2, JWT.

**Spec:** `docs/superpowers/specs/2026-10-12-java-style-refactor-design.md`

## Global Constraints

- Giữ 4 project và chiều phụ thuộc: Api → Infrastructure → Application → Domain.
- JSON, status, header `application/problem+json`, cookie (`lv_access` Path `/api`, `lv_refresh` Path `/api/v1/auth`, HttpOnly, SameSite=Lax), route `api/v1/auth/*` không đổi.
- Message lỗi giữ đúng từng chữ như `src/LegacyVault.Application/Features/Identity/AuthErrors.cs`.
- Schema DB không đổi; ID migration `20261011102914_InitialCreate` giữ nguyên.
- Quy ước code ở mục 3 của spec:
  - Không dùng primary constructor; mỗi class một file.
  - DTO là `class` có `{ get; set; }`.
  - Tránh `is { } x`, `??=`, `out`.
  - Comment tiếng Việt.
- Không thêm project test (spec: ngoài phạm vi). Kiểm chứng bằng `dotnet build` và bài so sánh hành vi ở Task 0.

## Review Focus

1. Sai mật khẩu sau khi lần khóa trước đã hết hạn: số đếm phải reset về 0 trước khi tăng (`attemptsRemaining` = 4, không phải 0). Không chờ 15 phút được trong bài so sánh, nên phần này kiểm bằng đối chiếu từng dòng ở Task 3 Step 3.
2. `refresh` bằng token cũ đã bị xoay: phải 401 **và** token mới cùng family cũng chết. Test bằng kịch bản `C13`.
3. `logout` không có body và `logout` hai lần: luôn 204. Test bằng `C15`, `C16`.
4. Login email viết hoa và có khoảng trắng (`"  OWNER@legacyvault.local "`): phải 200. Test bằng `C02`.
5. Exception lạ (không phải `ApiException`) vẫn ra 500 `INTERNAL_ERROR`, không lộ stack trace. Kiểm tra bằng cách đọc code `GlobalExceptionHandler` ở Task 1 Step 4 (không có kịch bản HTTP nào gây ra được lỗi này).

---

### Task 0: Bộ so sánh hành vi và chụp baseline

**Files:**
- Create: `<scratchpad>/compare/compare.py` (không commit, đây là công cụ kiểm tra)
- Output: `<scratchpad>/compare/baseline.json`

**Interfaces:**
- Produces: lệnh `python compare.py <out.json>` và `python compare.py --diff baseline.json <out.json>` (in `IDENTICAL` hoặc danh sách kịch bản khác nhau, exit code 1 nếu khác).

- [ ] **Step 1: Viết `compare.py`**
  - **Khởi động:**
    - Xóa DB `LegacyVault_Compare` trên `(localdb)\MSSQLLocalDB` bằng `sqlcmd -Q "IF DB_ID('LegacyVault_Compare') IS NOT NULL BEGIN ALTER DATABASE LegacyVault_Compare SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE LegacyVault_Compare END"`.
    - Chạy `dotnet run --no-build --project src/LegacyVault.Api --launch-profile http` với các env: `ConnectionStrings__Default=Server=(localdb)\MSSQLLocalDB;Database=LegacyVault_Compare;Trusted_Connection=True;TrustServerCertificate=True` và `Jwt__SigningKey=<64 ký tự hex cố định>`.
    - Đợi `http://localhost:5000` trả lời (tối đa 90s). Chạy xong kịch bản thì tắt process.
  - **Mỗi kịch bản ghi lại:** `status`, `content-type`, body JSON đã chuẩn hóa, và danh sách `Set-Cookie` đã chuẩn hóa (tên, path, httponly, samesite, có/không `expires`, có phải lệnh xóa hay không).
  - **Chuẩn hóa:** chuỗi JWT/token → `<token>`, `traceId` → `<trace>`, GUID → `<guid>`, thời gian ISO → `<time>`, `remainingSeconds` → `"<int>"` nếu nằm trong 1..900, `expiresIn` → `"<int>"` nếu nằm trong 1..900.
  - **Cookie:** mỗi "phiên" là một `http.cookiejar.CookieJar` riêng (tôn trọng Path như trình duyệt).
  - **Kịch bản** (thứ tự cố định):

| ID | Phiên | Request | Kỳ vọng hiện tại |
|----|-------|---------|------------------|
| C01 | A | login owner `Password123!` rememberMe=true | 200 |
| C02 | B | login `"  OWNER@legacyvault.local "` rememberMe=false | 200, refresh cookie không có expires |
| C03 | A | GET session | 200 |
| C04 | mới | GET session không cookie | 401 |
| C05 | mới | login email không tồn tại | 401 INVALID_CREDENTIALS, không có attemptsRemaining |
| C06 | mới | login thiếu email `{ "password": "x" }` | 400 VALIDATION_ERROR |
| C07 | mới | login email sai định dạng | 400 |
| C08 | mới | login inactive@ đúng mật khẩu | 403 ACCOUNT_NOT_ACTIVE |
| C09 | mới | admin@ sai mật khẩu ×5 (ghi cả 5 response) | 401 attemptsRemaining 4,3,2,1 rồi 423 |
| C10 | mới | admin@ đúng mật khẩu khi đang khóa | 423 ACCOUNT_LOCKED |
| C11 | A | POST refresh | 200, cookie mới |
| C12 | mới | POST refresh không cookie | 401, cookie bị xóa |
| C13 | A' | copy cookie refresh **cũ** (trước C11) rồi POST refresh, sau đó phiên A POST refresh | cả hai 401 (cả family bị thu hồi) |
| C14 | B | logout `{ "revokeAllDevices": true }` | 204 |
| C15 | B | logout không body | 204 |
| C16 | B | logout lần nữa | 204 |
| C17 | B | GET session | 401 |

  - Review Focus #1 (reset bộ đếm khi lần khóa trước đã hết hạn) không thể chờ 15 phút. Bài so sánh không phủ được, nên Task 3 Step 3 phải đối chiếu từng dòng với `User.RegisterFailedLogin` cũ.

- [ ] **Step 2: Build và chụp baseline**

Run: `dotnet build` rồi `python <scratchpad>/compare/compare.py <scratchpad>/compare/baseline.json`
Expected: file baseline có đủ 17 kịch bản, status khớp cột "Kỳ vọng hiện tại". Chạy thêm lần nữa ra `baseline2.json`, rồi `--diff baseline.json baseline2.json` phải in `IDENTICAL` (chứng minh chuẩn hóa đủ ổn định).

---

### Task 1: Đổi `Result`/`Error` sang exception

**Files:**
- Create: `src/LegacyVault.Application/Exceptions/ApiException.cs`, `InvalidCredentialsException.cs`, `AccountLockedException.cs`, `AccountNotActiveException.cs`, `SessionExpiredException.cs`
- Modify: `src/LegacyVault.Application/Features/Identity/AuthService.cs` (throw thay vì return Error), `src/LegacyVault.Api/Controllers/V1/AuthController.cs`, `src/LegacyVault.Api/Common/GlobalExceptionHandler.cs`
- Delete: `src/LegacyVault.Domain/Common/Result.cs`, `Error.cs`, `src/LegacyVault.Application/Features/Identity/AuthErrors.cs`, `src/LegacyVault.Api/Common/ApiControllerBase.cs`

**Interfaces:**
- Produces:
  - `ApiException(int statusCode, string errorCode, string message)` với các property `int StatusCode`, `string ErrorCode`, `Dictionary<string, object?> ExtraData`. Bốn lớp con theo bảng mục 4 của spec.
  - `IAuthService`:
    - `Task<AuthResult> LoginAsync(LoginRequest, CancellationToken)`
    - `Task<AuthResult> RefreshAsync(string?, CancellationToken)`
    - `Task LogoutAsync(string?, bool, CancellationToken)`
    - `Task<UserDto> GetCurrentUserAsync(CancellationToken)`

- [ ] **Step 1: Tạo 5 class exception** với status/code/message/ExtraData đúng bảng mục 4 của spec. `InvalidCredentialsException` chỉ thêm `attemptsRemaining` khi tham số khác null.
- [ ] **Step 2: Sửa `AuthService`**: mọi `return AuthErrors.X` → `throw new XException(...)`, kiểu trả về bỏ `Result<>`.
- [ ] **Step 3: Sửa `AuthController`**:
  - Kế thừa `ControllerBase`, gắn `[ApiController]` và `[Produces("application/json")]`.
  - Trả về `Ok(new ApiResponse<T>(...))`.
  - `Refresh` dùng `try { ... } catch (SessionExpiredException) { cookies.Clear(Response); throw; }`.
- [ ] **Step 4: Sửa `GlobalExceptionHandler`**:
  - **Nhánh `ApiException`:** lấy `ProblemDetailsFactory` từ `httpContext.RequestServices`, gọi `CreateProblemDetails(httpContext, ex.StatusCode, title: ex.Message)`, thêm `code` và từng mục `ExtraData` vào `Extensions`. Set `Response.StatusCode`, ghi bằng `Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, "application/problem+json")`. Return true, không log Error.
  - **Nhánh còn lại:** giữ nguyên code cũ (500 `INTERNAL_ERROR`).
- [ ] **Step 5: Kiểm tra**

Run: `dotnet build` (0 lỗi), rồi `python compare.py after1.json` và `python compare.py --diff baseline.json after1.json`
Expected: `IDENTICAL`. Nếu khác ở field `type` hay thứ tự field thì sửa Step 4 cho tới khi khớp.

- [ ] **Step 6: Commit** `refactor: dùng exception thay cho Result/Error`

---

### Task 2: Thêm Repository, bỏ `IAppDbContext`

**Files:**
- Create: `src/LegacyVault.Application/Repositories/IUserRepository.cs`, `IRefreshTokenRepository.cs`; `src/LegacyVault.Infrastructure/Repositories/UserRepository.cs`, `RefreshTokenRepository.cs`
- Modify: `AuthService.cs` (dùng repository), `src/LegacyVault.Infrastructure/DependencyInjection.cs` (đăng ký Scoped)
- Delete: `src/LegacyVault.Application/Common/Interfaces/IAppDbContext.cs`
- Modify: `src/LegacyVault.Application/LegacyVault.Application.csproj`: bỏ package EF Core nếu Application không còn dùng (kiểm tra bằng build).

**Interfaces:**
- Produces: đúng chữ ký ở mục 6 của spec.
  - `IUserRepository`: `FindByEmailAsync`, `FindByIdAsync` (AsNoTracking), `SaveChangesAsync`.
  - `IRefreshTokenRepository`: `FindByHashAsync`, `FindByHashWithUserAsync`, `FindActiveByFamilyAsync`, `FindActiveByUserAsync`, `Add`, `SaveChangesAsync`.

- [ ] **Step 1: Tạo interface + implementation.** Constructor nhận `AppDbContext`. Câu truy vấn copy từ `AuthService` hiện tại (`SingleOrDefaultAsync`, `Include`, `Where(... RevokedAt == null)`).
- [ ] **Step 2: Sửa `AuthService`** dùng 2 repository thay `IAppDbContext`. Mỗi chỗ `db.SaveChangesAsync` cũ thay bằng **đúng một** lời gọi `SaveChangesAsync` của repository (giữ luật 7: login lưu một lần). Thêm comment giải thích việc hai repository dùng chung DbContext.
- [ ] **Step 3: Kiểm tra**

Run: `dotnet build` + `python compare.py --diff baseline.json after2.json` (sau khi chụp `after2.json`)
Expected: `IDENTICAL`.

- [ ] **Step 4: Commit** `refactor: thêm UserRepository, RefreshTokenRepository`

---

### Task 3: Entity đơn giản, luật nghiệp vụ về `AuthService`

**Files:**
- Modify: `src/LegacyVault.Domain/Identity/User.cs`, `RefreshToken.cs`, `src/LegacyVault.Domain/Common/Entity.cs`
- Modify: `AuthService.cs`, `src/LegacyVault.Infrastructure/Persistence/DbInitializer.cs`, các chỗ khác đang gọi `User.Create`, `RefreshToken.Create`, `User.NormalizeEmail` (tìm bằng grep)

**Interfaces:**
- Produces:
  - Entity có property `{ get; set; }` theo mục 5 của spec. `BaseEntity.Id { get; set; } = Guid.CreateVersion7()` (đổi tên lớp ở Task 4).
  - `public static string AuthService.NormalizeEmail(string email)`.
  - Hằng `AuthService.MaxFailedLoginAttempts = 5`, `AuthService.LockoutDuration = TimeSpan.FromMinutes(15)`.
  - Các hàm private theo bảng mục 5 của spec.

- [ ] **Step 1: Đơn giản hóa entity:** bỏ constructor private, factory, method; setter public.
- [ ] **Step 2: Chuyển logic vào `AuthService`** dưới dạng hàm private: `IsLockedOut`, `GetLockoutRemainingSeconds` (dùng `Math.Ceiling`), `RegisterFailedLogin`, `RegisterSuccessfulLogin`, `RevokeToken` (idempotent), `IsExpired`. Tạo `RefreshToken` bằng object initializer với `ExpiresAt = now.Add(tokenService.RefreshTokenLifetime)`.
- [ ] **Step 3: Đối chiếu tay với code cũ** (`git show HEAD~2:src/LegacyVault.Domain/Identity/User.cs`). Từng nhánh `if` của `RegisterFailedLogin` phải giống: reset khi `LockoutEndsAt <= now`, tăng đếm, set `UpdatedAt`, đủ 5 lần thì khóa và trả 0.
- [ ] **Step 4: Sửa `DbInitializer`** dùng `new User { Email = AuthService.NormalizeEmail(...), FullName = ...Trim(), ... CreatedAt = now }`. Infrastructure đã tham chiếu Application nên gọi được.
- [ ] **Step 5: Kiểm tra**

Run:
- `dotnet build`
- `dotnet ef migrations has-pending-model-changes --project src/LegacyVault.Infrastructure --startup-project src/LegacyVault.Api`
- `compare.py --diff baseline.json after3.json`

Expected: build OK; EF báo "No changes have been made to the model"; `IDENTICAL`.

- [ ] **Step 6: Commit** `refactor: entity chỉ có thuộc tính, luật nghiệp vụ nằm trong AuthService`

---

### Task 4: Xếp lại thư mục, namespace, constructor kiểu Java

**Files:** làm theo **bảng chuyển đổi file** ở mục 2 của spec (dùng `git mv` để giữ lịch sử). Gồm:
- Domain: `Entities/`, `Enums/`; `Entity` → `BaseEntity`.
- Application:
  - `Services/`: tách `IAuthService` ra file riêng.
  - `Repositories/`, `Security/`.
  - `Dtos/Requests/`, `Dtos/Responses/`: `UserDto` → `UserResponse` (giữ `static From(User)`), `AuthSessionDto` → `AuthSessionResponse`, `CurrentSessionDto` → `CurrentSessionResponse`, `AuthResult`. Tất cả chuyển sang `class` với `{ get; set; }`, **tên property giữ nguyên**.
  - `Exceptions/`; `DependencyInjection.cs` → `ApplicationConfig.cs`.
- Infrastructure:
  - `Persistence/` → `Data/`, bao gồm `Configurations/` và `Migrations/`; `DbInitializer` → `DbSeeder`.
  - `JwtOptions` → `JwtSettings`, `SectionName = "Jwt"` giữ nguyên.
  - `DependencyInjection.cs` → `InfrastructureConfig.cs`.
- Api:
  - `Controllers/AuthController.cs` (bỏ thư mục `V1`).
  - `Exceptions/GlobalExceptionHandler.cs`.
  - `Helpers/AuthCookieWriter.cs`, `ApiResponse.cs`.
  - `Extensions/ApiServiceExtensions.cs` tách thành `Config/ApiConfig.cs` (Controllers + InvalidModelStateResponseFactory + ProblemDetails + ExceptionHandler + AuthCookieWriter) và `Config/CorsConfig.cs` (const `FrontendCorsPolicy`, đăng ký CORS).
- `Program.cs` sửa theo tên mới.

**Interfaces:**
- Produces:
  - Namespace khớp thư mục, ví dụ `LegacyVault.Application.Services`, `LegacyVault.Infrastructure.Data`.
  - Extension method: `AddApplication()`, `AddInfrastructure(IConfiguration)`, `AddApiConfig()`, `AddCorsConfig(IConfiguration)`, `InitializeDatabaseAsync()`.

- [ ] **Step 1: Di chuyển file bằng `git mv`, sửa namespace và `using`.** Với 3 file migration: chỉ đổi `namespace` và `using`, giữ `[DbContext(typeof(AppDbContext))]` và `[Migration("20261011102914_InitialCreate")]`.
- [ ] **Step 2: Đổi mọi primary constructor sang field `private readonly` + constructor.** Thay `is { } x`, `??=`, `out` bằng code `if` rõ ràng. Riêng overload `IssueTokens(..., out RefreshToken)` đổi thành hàm trả về object `RefreshToken` vừa tạo, hoặc tạo token trước rồi truyền vào. Chọn cách nào cũng được miễn không dùng `out`.
- [ ] **Step 3: Tách `AuthDtos.cs` thành từng file** như danh sách ở phần Files. Giữ các comment hiện có.
- [ ] **Step 4: Kiểm tra**

Run:
- `dotnet build`
- `dotnet ef migrations has-pending-model-changes ...` (như Task 3)
- `compare.py --diff baseline.json after4.json`
- `grep -rnE "\(\s*I[A-Z]\w+ \w+[,)].*\)\s*(:|$)" --include=*.cs src | grep -E "class .*\("` để kiểm tra không còn primary constructor

Expected: build OK; EF không có thay đổi; `IDENTICAL`; grep rỗng.

- [ ] **Step 5: Commit** `refactor: xếp lại thư mục và namespace theo kiểu Java`

---

### Task 5: README và kiểm tra cuối

**Files:**
- Modify: `README.md`, gồm các mục:
  - "2. Cấu trúc": cây thư mục mới.
  - "3. Lần theo một request đăng nhập": đường đi Controller → Service → Repository.
  - Thay mục "Vì sao trả `Result` thay vì `throw`?" bằng "Lỗi nghiệp vụ được xử lý thế nào" (ApiException + GlobalExceptionHandler).
  - Sửa mọi đường dẫn file cũ (`Persistence/Migrations` → `Data/Migrations`, …) và mục 6 bài tập nếu nhắc tên cũ.

- [ ] **Step 1: Cập nhật README.** Grep `Persistence|Features/Identity|Result|AuthErrors|ApiControllerBase|DependencyInjection` trong README phải không còn tham chiếu sai.
- [ ] **Step 2: Kiểm tra cuối**

Run: `dotnet build -c Release` + `compare.py --diff baseline.json final.json`
Expected: 0 lỗi, 0 cảnh báo mới; `IDENTICAL`.

- [ ] **Step 3: Commit** `docs: cập nhật README theo cấu trúc mới`
