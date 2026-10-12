# LegacyVault Backend — Bước 1: Đăng nhập / Đăng xuất

ASP.NET Core Web API (.NET 10) theo kiến trúc 4 lớp của nhóm. Bước này chỉ làm **xác thực**: đăng nhập, xem phiên, làm mới phiên, đăng xuất. Các module khác (Vaults, Assets, …) sẽ thêm dần theo đúng khuôn này.

Đọc theo thứ tự: chạy được trước (mục 1) → hiểu cấu trúc (mục 2) → lần theo một request (mục 3) → tự làm bài tập (mục 6).

---

## 1. Cài lần đầu, kết nối database và chạy API

### 1.1. Chuẩn bị trên mỗi máy

- Cài [.NET 10 SDK](https://dotnet.microsoft.com/download), Git và Docker Desktop.
- Mở Docker Desktop, đợi Docker Engine sẵn sàng.
- Clone repository và lấy nhánh có thay đổi OpenAPI/Swagger. Nếu thay đổi còn nằm ở nhánh `feature/swagger-openAPI`, cần checkout nhánh đó sau khi nó đã được push; nếu đã merge vào `test`, checkout `test` và pull code mới.
- Mở terminal tại thư mục chứa `LegacyVault.sln` và `docker-compose.yml`. Các lệnh dưới đây dùng PowerShell.

Source đã có migration `InitialCreate` trong `src/LegacyVault.Infrastructure/Persistence/Migrations`. Khi lấy code lần đầu, **không tạo lại `InitialCreate`**. API tự áp dụng các migration chưa chạy khi khởi động ở Development.

### 1.2. Khởi động SQL Server Docker

```powershell
docker compose up -d
docker compose ps
docker compose logs --tail 30 sqlserver
```

Lần đầu Docker tải image SQL Server, nên có thể mất vài phút. Kiểm tra container `legacyvault-sql` có trạng thái `Up` và log có dòng `SQL Server is now ready for client connections` trước khi chạy API.

Cấu hình cổng trong `docker-compose.yml` là `14330:1433`:

- API hoặc SSMS chạy trên máy kết nối tới `127.0.0.1,14330`.
- SQL Server bên trong container vẫn dùng cổng `1433`.
- Chọn cổng máy `14330` để tránh trùng SQL Server Express thường dùng cổng `1433`.
- Nếu sau này API cũng chạy trong container cùng mạng Compose, địa chỉ SQL Server là `sqlserver,1433`.

### 1.3. Tạo khóa JWT và build (lần đầu trên mỗi máy)

```powershell
$jwtDevKey = [guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")
dotnet user-secrets set "Jwt:SigningKey" $jwtDevKey --project src/LegacyVault.Api

dotnet restore
dotnet build
```

Khóa JWT được lưu trong user-secrets trên máy bạn, không nằm trong source Git. Mỗi bạn tự cấu hình khóa này một lần; không cần tạo lại mỗi lần chạy project. Cấu hình database Development đã nằm trong `src/LegacyVault.Api/appsettings.Development.json` và khớp Docker Compose.

### 1.4. Chạy API bằng Visual Studio hoặc terminal

Trong Visual Studio:

1. Mở `LegacyVault.sln`.
2. Chuột phải `LegacyVault.Api` → **Set as Startup Project**.
3. Chọn profile **http** trên thanh chạy.
4. Bấm **F5** (debug) hoặc **Ctrl + F5** (chạy bình thường).

Hoặc chạy bằng PowerShell tại thư mục gốc:

```powershell
dotnet run --project src/LegacyVault.Api --launch-profile http
```

Chọn một cách chạy để tránh trùng cổng API `5000`. Khi terminal hiện `Now listening on: http://localhost:5000`, ứng dụng đã sẵn sàng. Visual Studio tự mở Swagger khi chạy profile `http`.

Ở Development, API tự tạo database `LegacyVault` nếu chưa có, áp dụng migration và thêm 3 tài khoản demo khi bảng `Users` chưa có dữ liệu. Mật khẩu chung là `Password123!`:

| Email | Role | Ghi chú |
| --- | --- | --- |
| `owner@legacyvault.local` | OWNER | Tài khoản thường |
| `admin@legacyvault.local` | ADMIN | Tài khoản quản trị |
| `inactive@legacyvault.local` | OWNER | Bị vô hiệu; đăng nhập nhận 403 |

### 1.5. Kết nối database bằng SSMS

Giữ Docker Desktop và container SQL Server đang chạy, rồi điền trong cửa sổ Connect của SSMS:

| Trường | Giá trị |
| --- | --- |
| Server Name | `127.0.0.1,14330` |
| Authentication | SQL Server Authentication |
| User Name | `sa` |
| Password | `LegacyVault_Dev123!` |
| Database Name | `LegacyVault`, hoặc giữ mặc định rồi chọn database sau khi kết nối |
| Encrypt | Mandatory |
| Trust Server Certificate | Tích chọn (cấu hình Development) |

Mật khẩu này chỉ dùng cho database Development của project. Nếu API chưa chạy lần đầu, database `LegacyVault` có thể chưa tồn tại; giữ Database Name mặc định để kết nối SQL Server trước.

Connection string Development:

```text
Server=127.0.0.1,14330;Database=LegacyVault;User Id=sa;Password=LegacyVault_Dev123!;TrustServerCertificate=True
```

### 1.6. Test API bằng Swagger hoặc file HTTP

- [Swagger UI](http://localhost:5000/swagger): xem endpoint và gọi thử bằng **Try it out** → **Execute**.
- [OpenAPI JSON](http://localhost:5000/openapi/v1.json): tài liệu mô tả API.
- OpenAPI và Swagger UI chỉ được bật trong môi trường Development.

Gọi `POST /api/v1/auth/login` với body:

```json
{
  "email": "owner@legacyvault.local",
  "password": "Password123!",
  "rememberMe": true
}
```

Sau đó test theo thứ tự:

| Request | Kết quả mong đợi |
| --- | --- |
| `POST /api/v1/auth/login` | 200, nhận cookie đăng nhập |
| `GET /api/v1/auth/session` | 200, thông tin người dùng |
| `POST /api/v1/auth/refresh` | 200, cookie mới |
| `POST /api/v1/auth/logout` | 204 |
| `GET /api/v1/auth/session` sau logout | 401 |

Token được trả qua cookie HttpOnly, không nằm trong JSON. Trình duyệt gửi cookie cho các request API cùng host; không cần dán Bearer token. Khi test, dùng nhất quán host `localhost` cho API.

Có thể mở `http/auth.http` bằng VS Code có extension REST Client, Rider hoặc Visual Studio rồi gửi từng request. Công cụ test cần lưu/gửi cookie giữa các request. Nếu dùng frontend, API đang cho phép origin `http://localhost:5173` và frontend cần gửi cookie (`withCredentials: true`). Frontend được chạy từ project frontend riêng.

### 1.7. Mỗi lần mở project và sau khi pull bản mới

Mỗi lần làm việc:

1. Mở Docker Desktop, đợi Docker sẵn sàng.
2. Chạy `docker compose up -d` tại thư mục project.
3. Chạy `LegacyVault.Api` với profile `http` bằng Visual Studio hoặc terminal.

Không cần tạo lại khóa JWT hay migration khi mở project. Khi nghỉ, dừng API và có thể dừng SQL Server bằng:

```powershell
docker compose stop
```

Lần sau dùng `docker compose up -d` để chạy lại. Dữ liệu được lưu trong volume `legacyvault-sql-data` và được giữ khi dừng hoặc tạo lại container. Lệnh `docker compose down -v` xóa volume và dữ liệu database; chỉ dùng khi muốn xóa dữ liệu Development.

Nếu đang dùng bản cũ với cổng `1433`, sau khi lấy code mới hãy chạy lại `docker compose up -d` để cập nhật container sang cổng máy `14330`, rồi build/chạy API. SSMS cũng cần đổi Server Name sang `127.0.0.1,14330`. Nếu có connection string riêng trong user-secrets hoặc biến môi trường, cập nhật cấu hình riêng đó vì nó có thể ghi đè cấu hình trong file.

### 1.8. Khi thay đổi cấu trúc database

Chỉ người thêm hoặc sửa entity/cấu hình bảng mới cần tạo migration mới. Cài công cụ EF Core phiên bản tương ứng project nếu chưa có:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.0
```

Ví dụ tạo migration cho một thay đổi mới (đổi `AddNewFeature` thành tên phù hợp):

```powershell
dotnet ef migrations add AddNewFeature --project src/LegacyVault.Infrastructure --startup-project src/LegacyVault.Api --output-dir Persistence/Migrations
```

Commit các file migration và model snapshot cùng thay đổi entity. Các thành viên khác pull migration đã tạo, rồi chạy API Development để áp dụng; không tạo lại migration cùng tên trên mỗi máy.

### 1.9. Lỗi hay gặp

- `docker-credential-desktop` không tìm thấy trong PATH hoặc terminal không nhận lệnh `docker`: đóng hẳn terminal/Visual Studio rồi mở lại để nhận PATH sau khi cài Docker; kiểm tra bằng `Get-Command docker` và `Get-Command docker-credential-desktop`.
- Docker không kết nối được Engine: mở Docker Desktop và đợi Engine sẵn sàng.
- Cổng `14330` đã bị chiếm: chọn cổng máy còn trống trong `docker-compose.yml` và sửa connection string Development cho khớp, sau đó chạy lại `docker compose up -d`.
- `Jwt:SigningKey phải dài ít nhất 32 ký tự`: cấu hình user-secrets theo mục 1.3.
- `No migrations were found`: kiểm tra đã lấy đầy đủ thư mục migrations; dừng API, **Build → Rebuild Solution**, rồi chạy lại. Source hiện đã có `InitialCreate`.
- `pending changes ... Add a new migration`: model có thay đổi chưa được ghi vào migration; kiểm tra thay đổi entity và tạo migration mới theo mục 1.8.
- `Login failed for user 'sa'`: kiểm tra đúng server `127.0.0.1,14330` và mật khẩu đã dùng khi khởi tạo SQL Server. Kiểm tra connection string riêng trong user-secrets/biến môi trường nếu có.
- Lỗi kết nối SQL hoặc cửa sổ API chờ lâu trước khi hiện `Now listening`: kiểm tra `docker compose ps`, log SQL Server và connection string. API chờ khởi tạo database trước khi bắt đầu nhận request.
- API báo cổng `5000` đã được dùng: dừng phiên API cũ trước khi chạy bằng Visual Studio hoặc terminal.

---

## 2. Cấu trúc

```
legacyvault-be/
├── Directory.Build.props        Cấu hình chung cho mọi project (.NET 10, nullable)
├── Directory.Packages.props     Version NuGet khai báo một chỗ
├── docker-compose.yml           SQL Server cho dev
├── http/auth.http               Kịch bản thử API bằng tay
└── src/
    ├── LegacyVault.Domain/              ① LÕI — không phụ thuộc gì
    │   ├── Common/                      Entity, Result, Error
    │   └── Identity/                    User, RefreshToken, UserRole, UserStatus
    ├── LegacyVault.Application/         ② USE CASE — chỉ biết Domain + interface
    │   ├── Common/Interfaces/           IAppDbContext, IPasswordHasher, ITokenService, ICurrentUser
    │   ├── Features/Identity/           AuthService, AuthDtos, AuthErrors
    │   └── DependencyInjection.cs
    ├── LegacyVault.Infrastructure/      ③ CÀI ĐẶT THẬT cho các interface
    │   ├── Persistence/                 AppDbContext, Configurations/, DbInitializer
    │   ├── Security/                    Argon2PasswordHasher, JwtTokenService, CurrentUser
    │   └── DependencyInjection.cs       Đăng ký DB, JWT
    └── LegacyVault.Api/                 ④ HTTP — controller mỏng, lắp ráp mọi thứ
        ├── Common/                      ApiControllerBase, ApiResponse, AuthCookieWriter, GlobalExceptionHandler
        ├── Controllers/V1/              AuthController
        ├── Extensions/                  Controller, CORS, chuẩn lỗi
        └── Program.cs
```

**Chiều phụ thuộc** (mũi tên = "biết đến"):

```
Api ──► Application ──► Domain
 │           ▲
 └──► Infrastructure
```

Quy tắc duy nhất cần nhớ: **lớp trong không bao giờ biết lớp ngoài.**
- Domain không biết có database hay HTTP. `User.RegisterFailedLogin()` chỉ là C# thuần — test được không cần gì.
- Application cần lưu dữ liệu nhưng chỉ biết `IAppDbContext` (interface). Không biết đó là SQL Server.
- Infrastructure cài đặt interface đó bằng EF Core + SQL Server. Đổi sang PostgreSQL chỉ sửa ở đây.
- Api là nơi duy nhất "lắp" mọi thứ lại (`Program.cs`).

Muốn biết một dòng code nên đặt ở đâu? Hỏi: *"Nó là luật nghiệp vụ, là điều phối, là chi tiết kỹ thuật, hay là chuyện HTTP?"* → Domain / Application / Infrastructure / Api.

---

## 3. Lần theo một request đăng nhập

Mở từng file theo thứ tự dưới đây khi đọc:

```
FE: POST /api/v1/auth/login { email, password, rememberMe }
 │
 ├─ Program.cs ............... CORS → Authentication → Authorization → Controller
 │
 ├─ AuthController.Login ..... [ApiController] đã tự kiểm tra [Required], [EmailAddress]
 │                             trong LoginRequest. Sai → 400 VALIDATION_ERROR, không vào tới đây.
 │
 ├─ AuthService.LoginAsync
 │    1. Tìm user theo email đã chuẩn hóa          (EF → SELECT … WHERE Email = @p)
 │    2. Hỏi entity: user.IsLockedOut(now)?       → 423 ACCOUNT_LOCKED
 │    3. passwordHasher.Verify(...)  sai?         → user.RegisterFailedLogin(now) → 401 + attemptsRemaining
 │    4. user.Status != Active?                    → 403 ACCOUNT_NOT_ACTIVE
 │    5. user.RegisterSuccessfulLogin(now)
 │    6. Tạo access token (JWT 15') + refresh token (7 ngày, lưu HASH vào DB)
 │    7. SaveChangesAsync() — MỘT lần cho mọi thay đổi
 │    → trả Result<AuthResult>
 │
 ├─ AuthController ........... Thất bại → Fail(error) → ProblemDetails
 │                             Thành công → AuthCookieWriter ghi 2 cookie HttpOnly
 │
 └─ Response 200 { "data": { userId, expiresIn, user } } + Set-Cookie: lv_access, lv_refresh
```

Các request sau đó: trình duyệt tự gửi cookie `lv_access` → JwtBearer (cấu hình ở `Infrastructure/DependencyInjection.cs`) đọc cookie, kiểm tra chữ ký + hạn → điền `HttpContext.User` → `CurrentUser.UserId` đọc ra.

### Vì sao trả `Result` thay vì `throw`?

Sai mật khẩu là chuyện **bình thường**, xảy ra hàng ngày. Exception dành cho chuyện **bất thường** (mất kết nối DB). `Result` buộc người gọi xử lý cả hai nhánh, và chữ ký method nói rõ "có thể thất bại". Exception thật sự bất ngờ thì `GlobalExceptionHandler` bắt hết và trả 500.

---

## 4. Xác thực hoạt động thế nào

| | Access token | Refresh token |
| --- | --- | --- |
| Là gì | JWT có chữ ký | Chuỗi ngẫu nhiên 64 byte |
| Sống | 15 phút | 7 ngày |
| Lưu ở server | Không | Có (chỉ lưu SHA-256) |
| Cookie | `lv_access`, path `/api` | `lv_refresh`, path `/api/v1/auth` |
| Thu hồi được? | Không (tự hết hạn) | Có — đó là cách đăng xuất |

**Vì sao cookie HttpOnly mà không để FE giữ token?** JavaScript không đọc được cookie HttpOnly, nên nếu trang bị chèn mã độc (XSS), mã đó cũng không lấy được token. FE của nhóm đã thiết kế theo hướng này (`withCredentials: true`, không đọc token).

**Xoay vòng refresh token:** mỗi lần `/auth/refresh`, token cũ bị thu hồi và thay bằng token mới. Nếu ai đó dùng lại token cũ — dấu hiệu bị đánh cắp — cả chuỗi phiên đó bị thu hồi. Bạn có thể tự thử: lưu cookie trước khi refresh, refresh, rồi gửi lại cookie cũ → 401, và cookie mới cũng chết theo.

**Giới hạn cần biết:** sau khi đăng xuất, access token cũ (nếu ai đó đã chép được) vẫn dùng được tối đa 15 phút vì server không lưu nó. Đó là lý do nó phải sống ngắn.

**Các biện pháp nhỏ đã có:** băm mật khẩu Argon2id; khóa 15 phút sau 5 lần sai; email không tồn tại vẫn chạy băm để không lộ qua thời gian phản hồi; kiểm tra trạng thái tài khoản *sau* khi mật khẩu đúng; so sánh hash thời gian cố định; khóa JWT nằm trong user-secrets.

---

## 5. Hợp đồng API

Thành công: `{ "data": … }`. Lỗi: ProblemDetails có `code`.

| Method | URL | Quyền | Kết quả |
| --- | --- | --- | --- |
| POST | `/api/v1/auth/login` | Ai cũng gọi được | 200 `{ data: { userId, expiresIn, user } }` + cookie |
| GET | `/api/v1/auth/session` | Cần đăng nhập | 200 `{ data: { user } }` / 401 |
| POST | `/api/v1/auth/refresh` | Cookie refresh | 200 như login + cookie mới / 401 `SESSION_EXPIRED` |
| POST | `/api/v1/auth/logout` | Ai cũng gọi được | 204, xóa cookie. Body tùy chọn `{ "revokeAllDevices": true }` |

| HTTP | `code` | Khi nào | Trường thêm |
| --- | --- | --- | --- |
| 400 | `VALIDATION_ERROR` | Thiếu/sai định dạng | `errors: { email: [...] }` |
| 401 | `INVALID_CREDENTIALS` | Sai email/mật khẩu | `attemptsRemaining` (chỉ khi email tồn tại) |
| 423 | `ACCOUNT_LOCKED` | Sai 5 lần | `attemptsRemaining: 0`, `remainingSeconds` |
| 403 | `ACCOUNT_NOT_ACTIVE` | Tài khoản bị vô hiệu | |
| 401 | `SESSION_EXPIRED` | Refresh token hết hạn/bị thu hồi | |
| 500 | `INTERNAL_ERROR` | Lỗi bất ngờ (xem log theo `traceId`) | |

### Ghi chú cho FE

1. `AuthSession` trong `auth.types.ts` khai báo `accessToken`, `refreshToken` bắt buộc — BE cố ý không trả hai trường này (đã nằm trong cookie). Nên đổi thành tùy chọn hoặc xóa.
2. Access token hết sau 15 phút. FE cần thêm vào response interceptor của axios: gặp 401 → gọi `POST /auth/refresh` một lần → thử lại request; refresh cũng 401 → về trang đăng nhập. Không có bước này, F5 sau 15 phút sẽ bị đăng xuất.
3. FE dùng role `NOTARY`, tài liệu kiến trúc dùng `VERIFIER`. BE đang theo tài liệu (`OWNER`, `VERIFIER`, `ADMIN`) — nhóm cần chốt một tên.

---

## 6. Học tiếp: tự làm theo khuôn

Mỗi tính năng mới đi đúng 6 bước (giống mục 7 tài liệu kiến trúc). Thử tự làm theo thứ tự, mỗi bài dựa trên bài trước:

**Bài 1 — Đăng ký (`POST /api/v1/auth/register`)**
FE đã có `RegisterRequest`. Gợi ý: thêm `RegisterRequest` vào `AuthDtos.cs` (kèm `[Compare]` cho `confirmPassword`), thêm `AuthErrors.EmailAlreadyExists` (409), thêm `RegisterAsync` vào `AuthService` dùng `User.Create(..., UserStatus.Pending, ...)`. Không cần migration mới vì bảng không đổi. Câu hỏi để tự nghĩ: nếu hai người đăng ký cùng email cùng lúc, cái gì chặn lại? (Gợi ý: xem `UserConfiguration`.)

**Bài 2 — Unit test cho Domain**
Tạo project `tests/LegacyVault.UnitTests` (xUnit), viết test cho `User.RegisterFailedLogin`: sai 4 lần còn 1 lượt; lần 5 bị khóa; sau 15 phút được thử lại. Domain không phụ thuộc gì nên test chỉ là `new` và `Assert`.

**Bài 3 — Phân quyền theo role**
Tạo `AdminController` với một endpoint `[Authorize(Roles = "ADMIN")]`. Đăng nhập bằng owner → 403; bằng admin → 200. Đọc lại `RoleClaimType` trong `Infrastructure/DependencyInjection.cs` để hiểu vì sao nó chạy.

**Bài 4 — Module đầu tiên có bảng mới**
Đây là lúc đi đủ 6 bước, kể cả `dotnet ef migrations add`: entity trong `Domain/<Module>/`, `<X>Configuration.cs`, thêm `DbSet` vào `IAppDbContext` và `AppDbContext`, service + DTO + errors, controller, migration.

Các phần còn lại của bảo mật (TOTP cho Admin, đăng nhập Google, rate limit) làm sau khi đã quen khuôn.
