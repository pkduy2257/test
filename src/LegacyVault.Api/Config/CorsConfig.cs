namespace LegacyVault.Api.Config;

/// <summary>
/// CORS: FE chạy ở localhost:5173 (Vite), API ở localhost:5000 → khác origin.
/// Trình duyệt chặn trừ khi API cho phép rõ ràng.
/// </summary>
public static class CorsConfig
{
    public const string FrontendCorsPolicy = "Frontend";

    public static IServiceCollection AddCorsConfig(this IServiceCollection services, IConfiguration configuration)
    {
        // Danh sách origin đọc từ appsettings: "Cors": { "AllowedOrigins": [ ... ] }
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins == null)
        {
            allowedOrigins = Array.Empty<string>();
        }

        // AllowCredentials để cookie được gửi kèm (FE đặt withCredentials: true).
        // Khi có AllowCredentials thì KHÔNG được dùng AllowAnyOrigin.
        services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }
}
