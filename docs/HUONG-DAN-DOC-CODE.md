# Hướng dẫn đọc source code LegacyVault Backend

Tài liệu này dành cho người mới vào dự án, đặc biệt là người đã quen Java/Spring Boot. Đọc xong bạn sẽ biết: code nằm ở đâu, đọc theo thứ tự nào, một request đi qua những file nào, mỗi đoạn code ứng với yêu cầu nào trong SRS, và muốn thêm tính năng thì đặt code vào đâu.

> Căn cứ yêu cầu hiện hành: **SRS v3.14.1** (`LegacyVault-SRS-v3.14.1.md`). Comment trong code trích mã yêu cầu của bản này (SETUP-12, QUOTA-02, AC-89...). Gặp mã lạ, tìm đúng mã đó trong SRS.

---

## 1. Bức tranh tổng thể

Dự án là một **ASP.NET Core Web API (.NET 10)**, viết bằng C#, lưu dữ liệu trong **SQL Server** qua **EF Core**. Hiện có ba module:

| Module | Làm được gì | Service chính |
| --- | --- | --- |
| Xác thực | Đăng nhập, xem phiên, làm mới phiên, đăng xuất | `AuthService` |
| Kho | "Tạo kho của tôi", xem kho của mình | `VaultService` |
| Tài sản | Upload tệp (mã hóa phong bì), liệt kê, tải lại | `AssetService` |

Code chia thành **4 project**, mỗi project là một "lớp":

```
            ┌──────────────────────────┐
  HTTP ───► │  LegacyVault.Api         │  ④ Controller, cookie, chuẩn lỗi, Program.cs
            └──────┬─────────────┬─────┘
                   │             │
                   ▼             ▼
  ┌──────────────────────┐   ┌──────────────────────────────┐
  │ LegacyVault.         │◄──│ LegacyVault.Infrastructure   │  ③ EF Core, SQL Server, JWT, Argon2,
  │ Application          │   └──────────────────────────────┘     AES-GCM, lưu tệp
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
| `LegacyVault.Application` | Service (luật nghiệp vụ), interface Repository, DTO, Exception, hạn mức | package `service`, `repository` (interface), `dto`, `exception` |
| `LegacyVault.Infrastructure` | Cài đặt Repository bằng EF Core, DbContext, Migration, JWT, băm mật khẩu, mã hóa, lưu tệp | phần Spring Data JPA tự sinh + `security` + `config` |
| `LegacyVault.Api` | Controller, filter lỗi, cấu hình HTTP, ghi cookie, `Program.cs` | package `controller`, `@ControllerAdvice`, class `main` |

Vì sao tách interface Repository (ở Application) khỏi cài đặt (ở Infrastructure)? Để service không biết dữ liệu nằm trong SQL Server. Ví dụ `VaultService` chỉ gọi `IVaultRepository.TryAddAsync` và nhận `true/false`; chuyện "SQL Server báo lỗi trùng khóa số 2601" nằm gọn trong `VaultRepository`.

---

## 2. Cây thư mục chi tiết

```
legacyvault-be/
├── LegacyVault.sln                 Mở file này bằng Visual Studio / Rider
├── LegacyVault-SRS-v3.14.1.md      Đặc tả yêu cầu hiện hành
├── Directory.Build.props           Cấu hình chung cho 4 project (.NET 10, nullable…)
├── Directory.Packages.props        Version của MỌI thư viện NuGet (≈ dependencyManagement)
├── docker-compose.yml              SQL Server cho dev
├── http/
│   ├── auth.http                   Kịch bản gọi API đăng nhập
│   └── vaults.http                 Kịch bản gọi API kho + tài sản
├── docs/                           Tài liệu (spec, kế hoạch, file này)
└── src/
    ├── LegacyVault.Domain/
    │   ├── Entities/
    │   │   ├── BaseEntity.cs        Lớp cha, có Id (Guid v7)
    │   │   ├── User.cs              Bảng Users (có EmailVerifiedAt)
    │   │   ├── RefreshToken.cs      Bảng RefreshTokens (phiên đăng nhập)
    │   │   ├── Vault.cs             ★ Bảng Vaults: một Owner một kho
    │   │   ├── HandoverPlan.cs      Bảng HandoverPlans: kế hoạch bàn giao, 1–1 với Vault
    │   │   ├── VaultEntitlement.cs  Bảng VaultEntitlements: quyền dịch vụ + hạn mức, 1–1 với Vault
    │   │   ├── Asset.cs             Bảng Assets: tên, mô tả tài sản
    │   │   └── AssetVersion.cs      Bảng AssetVersions: một phiên bản tệp + thông tin mã hóa
    │   └── Enums/
    │       ├── UserRole.cs          Owner, Verifier, Admin
    │       ├── UserStatus.cs        Pending, Active, Inactive
    │       ├── VaultStatus.cs       Open, Closed                 (trạng thái KHO)
    │       ├── PlanStatus.cs        Draft, Active, ... Closed    (trạng thái KẾ HOẠCH)
    │       ├── ServiceTier.cs       Free, Xs, XsMax              (hạng dịch vụ)
    │       ├── EntitlementStatus.cs Free, PaidActive, GraceReadOnly, ExpiredReview
    │       └── AssetStatus.cs
    │
    ├── LegacyVault.Application/
    │   ├── Services/
    │   │   ├── IAuthService.cs / AuthService.cs     Nghiệp vụ đăng nhập
    │   │   ├── IVaultService.cs / VaultService.cs   ★ Nghiệp vụ tạo/xem kho
    │   │   ├── IAssetService.cs / AssetService.cs   Nghiệp vụ tài sản + mã hóa
    │   │   └── VaultLimits.cs                       Mọi con số hạn mức (20 MiB, 2.000 tài sản...)
    │   ├── Repositories/            IUserRepository, IRefreshTokenRepository, IVaultRepository, IAssetRepository
    │   ├── Security/                IPasswordHasher, ITokenService, ICurrentUser, IEnvelopeEncryptionService
    │   ├── Storage/                 IFileStorage (lưu bản mã)
    │   ├── Validation/              AssetFileValidator (kiểm tra loại tệp bằng magic number)
    │   ├── Dtos/
    │   │   ├── Requests/            LoginRequest, LogoutRequest, CreateVaultRequest, CreateAssetRequest, UploadedFile
    │   │   └── Responses/           UserResponse, AuthResult, VaultResponse, OpenVaultResult, AssetResponse, ...
    │   ├── Exceptions/              ApiException + mỗi lỗi nghiệp vụ một class (xem mục 5)
    │   └── ApplicationConfig.cs     Đăng ký service vào DI
    │
    ├── LegacyVault.Infrastructure/
    │   ├── Repositories/            Câu truy vấn EF Core cho từng bảng
    │   ├── Data/
    │   │   ├── AppDbContext.cs      Cửa ngõ EF Core (≈ EntityManager)
    │   │   ├── DbSeeder.cs          Dev: chạy migration + tạo 4 tài khoản demo
    │   │   ├── Configurations/      Cấu hình cột, index cho từng bảng (≈ @Column, @Index)
    │   │   └── Migrations/          Lịch sử thay đổi schema (≈ Flyway/Liquibase)
    │   ├── Security/                Argon2, JWT, AES-GCM, CurrentUser
    │   ├── Storage/                 LocalFileStorage (bản mã nằm ở App_Data/asset-blobs)
    │   └── InfrastructureConfig.cs  Đăng ký DB, repository, JWT, mã hóa (≈ @Configuration)
    │
    └── LegacyVault.Api/
        ├── Program.cs               ★ ĐIỂM KHỞI ĐỘNG (≈ main + SpringApplication.run)
        ├── Controllers/
        │   ├── AuthController.cs    /api/v1/auth/*
        │   ├── VaultsController.cs  /api/v1/vaults, /vaults/me, /vaults/{id}
        │   └── AssetsController.cs  /api/v1/vaults/{id}/assets/*
        ├── Exceptions/              ApiExceptionFilter (≈ @ControllerAdvice), GlobalExceptionHandler (500)
        ├── Config/                  ApiConfig (lỗi validate 400), CorsConfig
        ├── Helpers/                 ApiResponse { data }, AuthCookieWriter
        └── appsettings*.json        Cấu hình (≈ application.properties)
```

---

## 3. Thứ tự đọc đề xuất

Đọc theo **đường đi của request**, từ ngoài vào trong. Làm xong chặng A rồi mới sang B, C. Mỗi bước chỉ mở 1–2 file.

### Chặng A: Khung chung và đăng nhập (khoảng 1 giờ 30 phút)

| Bước | File | Đọc để hiểu | Thời gian |
| --- | --- | --- | --- |
| A1 | `Api/Program.cs` | App khởi động: đăng ký service theo từng lớp, thứ tự middleware | 5' |
| A2 | `Api/Controllers/AuthController.cs` | Mỗi hàm: nhận request → gọi service → ghi cookie → trả `Ok(...)` | 10' |
| A3 | `Application/Services/AuthService.cs` | ★ `LoginAsync` → `RefreshAsync` → `LogoutAsync`. Hàm private cuối file là luật nghiệp vụ | 30' |
| A4 | `Application/Repositories/IUserRepository.cs` → `Infrastructure/Repositories/UserRepository.cs` | Interface và câu truy vấn EF thật | 10' |
| A5 | `Domain/Entities/User.cs`, `RefreshToken.cs` | Dữ liệu trong DB | 10' |
| A6 | `Application/Exceptions/ApiException.cs` → `Api/Exceptions/ApiExceptionFilter.cs` | Lỗi được throw ở đâu, biến thành JSON ở đâu | 10' |
| A7 | `Infrastructure/InfrastructureConfig.cs` | DI: interface nào gắn class nào; JWT đọc token từ cookie | 15' |

### Chặng B: Tạo kho (khoảng 1 giờ)

Đọc chặng này cạnh **SRS §5.2 bước 1, SETUP-12, SETUP-18 và §11.1**.

| Bước | File | Đọc để hiểu | Thời gian |
| --- | --- | --- | --- |
| B1 | `Domain/Enums/VaultStatus.cs`, `PlanStatus.cs`, `EntitlementStatus.cs` | Vì sao có **ba** trạng thái riêng: kho, kế hoạch, quyền dịch vụ | 10' |
| B2 | `Domain/Entities/Vault.cs`, `HandoverPlan.cs`, `VaultEntitlement.cs` | Một kho luôn kéo theo một kế hoạch và một quyền dịch vụ | 10' |
| B3 | `Api/Controllers/VaultsController.cs` | `POST /vaults` trả 201 hay 200 tùy cờ `Created` | 5' |
| B4 | `Application/Services/VaultService.cs` | ★ `OpenOrCreateMyVaultAsync` theo 4 bước có đánh số trong code | 15' |
| B5 | `Infrastructure/Repositories/VaultRepository.cs` | `TryAddAsync`: bắt lỗi trùng khóa của SQL Server, trả `false` | 10' |
| B6 | `Infrastructure/Data/Configurations/VaultConfiguration.cs` | Unique index `UX_Vaults_OwnerId`: chốt chặn cuối ở DB | 5' |
| B7 | `Application/Services/VaultLimits.cs` | Các con số: 20 MiB, 0 suất, 2.000 tài sản | 3' |

### Chặng C: Tài sản và mã hóa (khoảng 1 giờ)

| Bước | File | Đọc để hiểu | Thời gian |
| --- | --- | --- | --- |
| C1 | `Api/Controllers/AssetsController.cs` | Nhận multipart, giới hạn kích thước request | 5' |
| C2 | `Application/Services/AssetService.cs` | ★ `CreateAssetAsync`: kiểm tra quyền thêm → giới hạn → đọc tệp → dung lượng → mã hóa → lưu | 25' |
| C3 | `Application/Validation/AssetFileValidator.cs` | Kiểm tra loại tệp bằng byte đầu, không tin đuôi tệp | 10' |
| C4 | `Domain/Entities/AssetVersion.cs` → `Infrastructure/Security/AesGcmEnvelopeEncryptionService.cs` | Mã hóa phong bì: DEK riêng mỗi phiên bản, bọc bằng KEK | 20' |

**Mẹo khi đọc trong IDE:**
- Đặt con trỏ vào tên interface (vd. `IVaultRepository`) rồi bấm **Ctrl+F12** (Visual Studio) hoặc **Ctrl+Alt+B** (Rider) để nhảy tới class cài đặt.
- **Shift+F12** để xem mọi chỗ dùng một hàm.
- Đặt breakpoint trong `VaultService.OpenOrCreateMyVaultAsync`, chạy F5, chạy request 2 trong `http/vaults.http` hai lần để thấy nhánh "tạo mới" và nhánh "mở lại".

---

## 4. Một request đi qua những đâu

### 4.1. Đăng nhập: `POST /api/v1/auth/login`

```
FE ──► Program.cs pipeline: ExceptionHandler → CORS → Authentication → Authorization
        │
        ▼
   AuthController.Login(LoginRequest)
        │  [ApiController] tự validate LoginRequest. Sai → 400 VALIDATION_ERROR, không vào hàm.
        ▼
   AuthService.LoginAsync
        1. email = NormalizeEmail(...)                     "  An@X.com " → "an@x.com"
        2. userRepository.FindByEmailAsync(email)
           └─ không có → vẫn chạy Argon2 giả (chống dò email) → InvalidCredentialsException (401)
        3. IsLockedOut(user)?                              → AccountLockedException (423)
        4. passwordHasher.Verify(...) sai?                 → đếm lần sai, đủ 5 thì khóa 15 phút
        5. user.Status != Active?                          → AccountNotActiveException (403)
        6. Tạo refresh token (lưu HASH) + access token JWT, SaveChangesAsync() MỘT lần
        ▼
   AuthController: ghi 2 cookie HttpOnly lv_access, lv_refresh
        ▼
   200 { "data": { "userId", "expiresIn", "user": {...} } }
```

Các request sau: trình duyệt tự gửi cookie `lv_access` → JwtBearer (`InfrastructureConfig.ReadAccessTokenFromCookie`) kiểm tra chữ ký và hạn → `CurrentUser.UserId` đọc claim `sub`.

### 4.2. "Tạo kho của tôi": `POST /api/v1/vaults`

SRS §5.2 phân biệt ba kết quả: **tạo kho** chỉ tạo không gian lưu trữ, **thanh toán** chỉ cấp quyền dịch vụ, **kích hoạt** mới bắt đầu điểm danh. Vì vậy tạo kho luôn ra cùng một hình dạng: Vault `OPEN` + kế hoạch `DRAFT` + quyền dịch vụ `FREE` 20 MiB.

```
FE ──► VaultsController.OpenOrCreate(CreateVaultRequest? request)
        │  [Authorize(Roles = "OWNER")]: Admin/Verifier → 403
        │  Body không bắt buộc (EmptyBodyBehavior.Allow); name > 200 ký tự → 400
        ▼
   VaultService.OpenOrCreateMyVaultAsync
        1. Kiểm tra tài khoản
           user không có / Status != Active               → AccountNotActiveException (403)
           user.EmailVerifiedAt == null                    → EmailNotVerifiedException (403, AC-01)
        2. vaultRepository.FindByOwnerIdAsync(userId)
           └─ đã có kho (kể cả CLOSED)                     → trả kho đó, Created = false   (SETUP-12)
        3. NewFreeVault(...) rồi vaultRepository.TryAddAsync(vault)
           └─ INSERT Vaults + HandoverPlans + VaultEntitlements trong MỘT transaction
           └─ thành công                                   → trả kho mới, Created = true
        4. TryAddAsync trả false (unique index UX_Vaults_OwnerId chặn,
           vì một request khác vừa tạo kho trước)          → đọc lại kho đó, Created = false (AC-89)
        ▼
   VaultsController: Created ? 201 Created (kèm Location) : 200 OK
        ▼
   { "data": { id, name, status: "OPEN", planStatus: "DRAFT", serviceTier: "FREE",
               serviceStatus: "FREE", storageQuotaBytes: 20971520, storageUsedBytes, assetCount, ... } }
```

**Vì sao cần cả bước 2 lẫn bước 4?** Bước 2 xử lý trường hợp thường gặp (bấm lại nút, F5). Nhưng hai tab bấm cùng lúc thì cả hai đều qua bước 2 với kết quả "chưa có kho". Khi đó unique index trong DB chỉ cho một INSERT thành công; request còn lại rơi vào bước 4 và trả về kho của request thắng. Người dùng không bao giờ thấy lỗi, và DB không bao giờ có hai kho cho một Owner.

**Chỗ duy nhất biết về SQL Server** là `VaultRepository.IsOwnerAlreadyHasVault`: nó nhận ra lỗi 2601/2627 mà thông báo có tên `UX_Vaults_OwnerId`. Lỗi DB khác vẫn ném tiếp ra 500, không bị nuốt.

### 4.3. Thêm tài sản: `POST /api/v1/vaults/{vaultId}/assets`

```
AssetService.CreateAssetAsync
  1. LoadOwnedVaultAsync          kho không có / của người khác       → 404 VAULT_NOT_FOUND
  2. EnsureCanAddContent          kho CLOSED                          → 409 VAULT_NOT_EDITABLE (VAULT_CLOSED)
                                  kế hoạch đang xét chứng tử/bàn giao → 409 (PLAN_LOCKED, SETUP-05)
                                  quyền dịch vụ đang ân hạn/rà soát   → 409 (SERVICE_READ_ONLY, OPLAN-01)
  3. Đếm tài sản ≥ 2.000          → 409 ASSET_LIMIT_EXCEEDED (NFR-14)
  4. ReadWithLimitAsync           đọc tệp, dừng ngay khi vượt 20 MiB  → 400 INVALID_ASSET_FILE
  5. DetectContentType            kiểm tra magic number               → 400 INVALID_ASSET_FILE
  6. EnsureStorageQuotaAsync      đã dùng + tệp mới > quota           → 409 STORAGE_QUOTA_EXCEEDED
  7. EncryptAndSaveAsync          DEK mới → AES-256-GCM → bọc DEK bằng KEK → ghi bản mã → ghi DB
  finally: ZeroMemory(bản rõ)
```

### 4.4. Khi có lỗi

Mọi bước **throw ApiException** → `ApiExceptionFilter` trả:

```json
{ "type": "...", "title": "Kho không còn đủ dung lượng cho tệp này.", "status": 409,
  "traceId": "...", "code": "STORAGE_QUOTA_EXCEEDED", "usedBytes": 29, "quotaBytes": 20971520 }
```

Exception khác (bug, mất DB) đi tới `GlobalExceptionHandler` → 500 `INTERNAL_ERROR`, không lộ stack trace.

---

## 5. Bảng tra: code ↔ SRS v3.14.1

| Yêu cầu | Ý nghĩa ngắn | Code |
| --- | --- | --- |
| §5.2 bước 1 | "Tạo kho của tôi": có thì mở lại, chưa có thì tạo FREE + DRAFT | `VaultService.OpenOrCreateMyVaultAsync` |
| SETUP-12, DATA-04 | Một Owner tối đa một kho, kể cả kho đã đóng; chống tạo trùng | `VaultConfiguration` (unique index), `VaultRepository.TryAddAsync` |
| SETUP-13 | Mỗi kho một kế hoạch | `HandoverPlan`, `HandoverPlanConfiguration` (unique VaultId) |
| SETUP-18 | Vault chỉ CLOSED khi kế hoạch CLOSED và payload đã xóa | `VaultStatus.Closed` (chưa có luồng tự chuyển) |
| §11.1 | Trạng thái kho / kế hoạch / quyền dịch vụ tách riêng | `VaultStatus`, `PlanStatus`, `EntitlementStatus` |
| AUTH-04, AC-01 | Chưa xác minh email thì không tạo kho | `User.EmailVerifiedAt`, `EmailNotVerifiedException` |
| AC-89 | Hai tab, retry vẫn ra một kho | Bước 4 trong `OpenOrCreateMyVaultAsync` |
| AC-104 | Kho CLOSED: gọi tạo kho không sinh kho thứ hai | Bước 2 trả cả kho CLOSED |
| §8.1, QUOTA-01 | Free 20 MiB, 0 suất; 1 MiB = 1.048.576 byte | `VaultLimits` |
| QUOTA-02, AC-05 | Không vượt dung lượng khi commit | `AssetService.EnsureStorageQuotaAsync` |
| SETUP-05, OPLAN-01 | Chặn thêm nội dung khi đang xét chứng tử hoặc ân hạn | `AssetService.EnsureCanAddContent` |
| NFR-14 | 2.000 tài sản/kho (chống lạm dụng) | `VaultLimits.MaxAssetsPerVault` |
| SEC-02, SEC-03 | Mã hóa phong bì, xóa bản rõ khỏi bộ nhớ | `AssetService`, `AesGcmEnvelopeEncryptionService` |

### Những gì SRS yêu cầu nhưng CHƯA làm

Đọc code mà không thấy những phần dưới là đúng; đây là việc của các bước sau:

- **Container (gói bàn giao)**: bảng, trạng thái `DRAFT/READY/ARCHIVED`, giới hạn 50 Container (SETUP-03/04/16/17, NFR-14).
- **Thanh toán và kích hoạt kế hoạch**: `VaultEntitlement` và `HandoverPlan` mới chỉ được tạo ở trạng thái ban đầu.
- **Giữ chỗ dung lượng** cho upload đồng thời (`reserved_bytes`, QUOTA-02); giới hạn 60 upload/giờ (NFR-14).
- **Loại tệp WEBP/XLSX/ZIP** (NFR-07).
- **Vai trò Owner theo SETUP-15**: hiện mọi user thường mặc định role `OWNER`.

---

## 6. Bảng tra nhanh C# ↔ Java

| C# / ASP.NET Core | Java / Spring Boot |
| --- | --- |
| `[ApiController]`, `[Route("api/v1/vaults")]` | `@RestController`, `@RequestMapping("/api/v1/vaults")` |
| `[HttpPost]`, `[HttpGet("me")]` | `@PostMapping`, `@GetMapping("/me")` |
| `[Authorize(Roles = "OWNER")]` | `@PreAuthorize("hasRole('OWNER')")` |
| `[Required]`, `[MaxLength]` | `@NotBlank`, `@Size` |
| `[FromBody(EmptyBodyBehavior = Allow)]` | `@RequestBody(required = false)` |
| `Ok(...)`, `CreatedAtAction(...)` | `ResponseEntity.ok(...)`, `ResponseEntity.created(uri)` |
| `services.AddScoped<IVaultService, VaultService>()` | `@Service` + constructor injection |
| `IExceptionFilter` (`ApiExceptionFilter`) | `@ControllerAdvice` + `@ExceptionHandler` |
| `DbContext`, `DbSet<Vault>` | `EntityManager`, `JpaRepository<Vault, UUID>` |
| `Include(v => v.Plan)` | `JOIN FETCH` |
| `AsNoTracking()` | `@Transactional(readOnly = true)` |
| `SaveChangesAsync()` | `flush()` / commit transaction |
| `catch (DbUpdateException ex) when (...)` | `catch (DataIntegrityViolationException e)` |
| `HasOne(...).WithOne(...)` | `@OneToOne` |
| `IEntityTypeConfiguration<Vault>` | annotation `@Table`, `@Column`, `@Index` |
| EF Core Migrations | Flyway / Liquibase |
| `IOptions<JwtSettings>` | `@ConfigurationProperties(prefix = "jwt")` |
| `async Task<T>` / `await` | `CompletableFuture<T>` (ở đây chỉ cần hiểu là "chờ kết quả") |
| `CancellationToken ct` | Tín hiệu hủy khi client ngắt kết nối; cứ truyền tiếp xuống |
| `string?`, `Vault?` | Kiểu có thể null (≈ `@Nullable`, `Optional`) |
| `Guid`, `DateTimeOffset` | `UUID`, `OffsetDateTime` |
| LINQ `.Where(...).Select(...).SumAsync()` | Stream `.filter(...).map(...)` / JPQL `SUM` |

---

## 7. Quy ước code trong dự án

- **Constructor kiểu Java:** field `private readonly` + constructor gán giá trị. Không dùng primary constructor.
- **Mỗi class / interface / enum một file**, namespace khớp thư mục (`Application/Services/` → `LegacyVault.Application.Services`).
- **DTO** là `class` có `{ get; set; }`, không dùng `record`.
- **Kiểm tra null rõ ràng:** `if (user == null)`, không dùng `?.`, `??`, `is { } x`.
- **Lỗi nghiệp vụ:** `throw new XxxException(...)`. Không trả `null` hay mã lỗi.
- **Entity chỉ chứa dữ liệu**; luật nghiệp vụ nằm trong Service.
- **Con số hạn mức** gom ở `VaultLimits`, không rải số cứng trong service.
- **Comment tiếng Việt**, giải thích *vì sao*, kèm mã yêu cầu SRS khi có.
- **Enum lưu DB dạng chữ** (`HasConversion<string>()`); trả FE dạng `UPPER_SNAKE_CASE` qua `EnumCode.From`.

---

## 8. Thêm một tính năng mới: đặt code ở đâu?

Ví dụ thêm **Container** `POST /api/v1/vaults/{vaultId}/containers`:

| Bước | Việc | File |
| --- | --- | --- |
| 1 | Entity + enum trạng thái | `Domain/Entities/HandoverContainer.cs`, `Domain/Enums/ContainerStatus.cs` |
| 2 | Cấu hình bảng | `Infrastructure/Data/Configurations/HandoverContainerConfiguration.cs` + `DbSet` trong `AppDbContext` |
| 3 | Repository | `Application/Repositories/IContainerRepository.cs` + `Infrastructure/Repositories/ContainerRepository.cs`, đăng ký trong `InfrastructureConfig.AddRepositories` |
| 4 | DTO + lỗi | `Dtos/Requests/CreateContainerRequest.cs`, `Dtos/Responses/ContainerResponse.cs`, exception mới nếu cần |
| 5 | Nghiệp vụ | `IContainerService` + `ContainerService` (Free chỉ được tạo/đổi tên/lưu trữ — SETUP-17), đăng ký trong `ApplicationConfig` |
| 6 | Endpoint | `Api/Controllers/ContainersController.cs` |
| 7 | Migration | lệnh bên dưới |
| 8 | Thử | thêm request vào `http/vaults.http` |

```powershell
dotnet ef migrations add <TenMigration> --project src/LegacyVault.Infrastructure --startup-project src/LegacyVault.Api --output-dir Data/Migrations
```

Mở file migration vừa sinh ra và đọc lại trước khi chạy. EF có thể đoán sai, ví dụ đổi tên một cột thay vì xóa cột cũ rồi thêm cột mới. Migration `OneVaultPerOwnerWithPlanAndEntitlement` là ví dụ đã được sửa tay để chuyển dữ liệu cũ cho đúng nghĩa.

**Câu hỏi để chọn chỗ đặt code:** *"Đây là dữ liệu, luật nghiệp vụ, chi tiết kỹ thuật (DB, JWT, mã hóa), hay chuyện HTTP (route, cookie, status)?"* → Domain / Application / Infrastructure / Api.

---

## 9. Chạy thử để đọc code dễ hơn

**Cách 1: LocalDB (không cần Docker, có sẵn khi cài Visual Studio)**

```powershell
cd src/LegacyVault.Api
dotnet user-secrets set "Jwt:SigningKey" ([guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N"))
$kek = New-Object byte[] 32; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($kek)
dotnet user-secrets set "Encryption:Keys:kek-2026-10" ([Convert]::ToBase64String($kek))
dotnet user-secrets set "ConnectionStrings:Default" "Server=(localdb)\MSSQLLocalDB;Database=LegacyVault;Trusted_Connection=True;TrustServerCertificate=True"
cd ../..
dotnet run --project src/LegacyVault.Api --launch-profile http
```

**Cách 2: SQL Server bằng Docker:** xem README mục 1.

Mở `http://localhost:5000/swagger` hoặc chạy `http/vaults.http`. Tài khoản demo (mật khẩu chung `Password123!`):

| Email | Ghi chú |
| --- | --- |
| `owner@legacyvault.local` | Tài khoản thường, tạo kho được |
| `admin@legacyvault.local` | Quản trị, gọi API kho nhận 403 |
| `inactive@legacyvault.local` | Bị vô hiệu → 403 |
| `unverified@legacyvault.local` | Email chưa xác minh → tạo kho nhận 403 `EMAIL_NOT_VERIFIED` |

Muốn xem dữ liệu sau khi tạo kho:

```sql
SELECT v.Name, v.Status, p.Status AS PlanStatus, e.Tier, e.Status AS ServiceStatus, e.StorageQuotaBytes
FROM Vaults v
JOIN HandoverPlans p ON p.VaultId = v.Id
JOIN VaultEntitlements e ON e.VaultId = v.Id;
```

---

## 10. Tài liệu liên quan

- `LegacyVault-SRS-v3.14.1.md`: yêu cầu hiện hành; bảng "Thay đổi so với v3.14.0" ở đầu file tóm tắt các quyết định mới.
- `README.md`: cài đặt, chạy, hợp đồng API, bài tập tự làm.
- `docs/superpowers/specs/2026-10-12-java-style-refactor-design.md`: vì sao code được tổ chức như hiện nay.
- `http/auth.http`, `http/vaults.http`: kịch bản gọi API mẫu.
- `docs/diagrams/luong-kho-legacyvault.drawio`: sơ đồ luồng tạo kho, F1 tổng thể và thêm tài sản (mở bằng draw.io).
