using System.Text;
using LegacyVault.Application.Repositories;
using LegacyVault.Application.Security;
using LegacyVault.Application.Storage;
using LegacyVault.Infrastructure.Data;
using LegacyVault.Infrastructure.Repositories;
using LegacyVault.Infrastructure.Security;
using LegacyVault.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LegacyVault.Infrastructure;

/// <summary>
/// Đăng ký các class của lớp Infrastructure (DB, repository, bảo mật) vào DI container.
/// Giống một class @Configuration với các @Bean bên Spring Boot.
/// Program.cs chỉ cần gọi builder.Services.AddInfrastructure(builder.Configuration).
/// </summary>
public static class InfrastructureConfig
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ---- Đồng hồ hệ thống (test có thể thay bằng đồng hồ giả) ----
        services.AddSingleton(TimeProvider.System);

        AddDatabase(services, configuration);
        AddRepositories(services);
        AddSecurity(services);
        AddEncryptionAndStorage(services, configuration);
        AddJwtAuthentication(services, configuration);

        return services;
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (connectionString == null)
        {
            throw new InvalidOperationException("Thiếu ConnectionStrings:Default trong appsettings.");
        }

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
    }

    private static void AddRepositories(IServiceCollection services)
    {
        // Scoped (mỗi request một instance) để các repository dùng chung AppDbContext của request đó.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IVaultRepository, VaultRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
    }

    private static void AddSecurity(IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
    }

    private static void AddEncryptionAndStorage(IServiceCollection services, IConfiguration configuration)
    {
        // Kiểm tra KEK ngay lúc khởi động, giống Jwt:SigningKey: thiếu/sai khóa thì app dừng với hướng dẫn rõ ràng.
        services.AddOptions<EncryptionSettings>()
            .Bind(configuration.GetSection(EncryptionSettings.SectionName))
            .Validate(
                settings => settings.ActiveKeyId.Length > 0
                    && settings.Keys.ContainsKey(settings.ActiveKeyId)
                    && settings.Keys.Values.All(AesGcmEnvelopeEncryptionService.IsValidKey),
                "Thiếu khóa chủ mã hóa tài sản. Mỗi khóa trong Encryption:Keys phải là 32 byte Base64 và phải có khóa " +
                "trùng Encryption:ActiveKeyId. Xem README mục 1.3 để tạo bằng dotnet user-secrets.")
            .ValidateOnStart();

        services.AddOptions<FileStorageSettings>()
            .Bind(configuration.GetSection(FileStorageSettings.SectionName));

        services.AddSingleton<IEnvelopeEncryptionService, AesGcmEnvelopeEncryptionService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
    }

    private static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        // Options pattern + kiểm tra ngay lúc khởi động: thiếu khóa thì app dừng với thông báo rõ ràng,
        // thay vì chạy được rồi mới lỗi khi có người đăng nhập.
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(
                settings => Encoding.UTF8.GetByteCount(settings.SigningKey) >= 32,
                "Jwt:SigningKey phải dài ít nhất 32 ký tự. Chạy trong thư mục src/LegacyVault.Api: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"<chuỗi ngẫu nhiên dài>\"")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Cấu hình JwtBearer SAU khi đã có JwtSettings (lấy qua DI, không đọc config thủ công).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>(ConfigureJwtBearer);

        services.AddAuthorization();
    }

    private static void ConfigureJwtBearer(JwtBearerOptions bearer, IOptions<JwtSettings> jwtSettings)
    {
        var jwt = jwtSettings.Value;

        // Giữ nguyên tên claim "sub", "role" (mặc định .NET đổi sang URL dài khó dùng).
        bearer.MapInboundClaims = false;

        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtTokenService.CreateSigningKey(jwt.SigningKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30), // mặc định 5 phút là quá rộng
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = JwtClaimTypes.Role, // để [Authorize(Roles = "ADMIN")] hoạt động
        };

        bearer.Events = new JwtBearerEvents
        {
            OnMessageReceived = ReadAccessTokenFromCookie,
        };
    }

    /// <summary>
    /// Mặc định JwtBearer đọc header "Authorization: Bearer xxx".
    /// FE của nhóm dùng cookie HttpOnly nên ta lấy token từ cookie
    /// (vẫn ưu tiên header nếu có, tiện khi thử bằng Postman).
    /// </summary>
    private static Task ReadAccessTokenFromCookie(MessageReceivedContext context)
    {
        var hasHeader = context.Request.Headers.Authorization.Count > 0;
        var cookieToken = context.Request.Cookies[AuthCookieNames.AccessToken];
        if (!hasHeader && cookieToken != null)
        {
            context.Token = cookieToken;
        }

        return Task.CompletedTask;
    }
}
