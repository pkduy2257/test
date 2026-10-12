using LegacyVault.Api.Extensions;
using LegacyVault.Application;
using LegacyVault.Infrastructure;
using LegacyVault.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ===== 1. Đăng ký service vào DI container (theo từng lớp) =====
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

// ===== 2. Pipeline middleware: THỨ TỰ RẤT QUAN TRỌNG, request đi qua từ trên xuống =====
app.UseExceptionHandler();   // bọc ngoài cùng để bắt lỗi của mọi middleware phía sau
app.UseStatusCodePages();    // 401/403/404 rỗng → tự thành ProblemDetails JSON

// Tài liệu và giao diện thử API chỉ bật trong môi trường Development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "LegacyVault API v1");
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(ApiServiceExtensions.FrontendCorsPolicy); // trước Authentication để preflight OPTIONS không bị 401
app.UseAuthentication();     // đọc cookie/JWT → điền HttpContext.User
app.UseAuthorization();      // kiểm tra [Authorize]

app.MapControllers();

// ===== 3. Chuẩn bị DB cho môi trường dev =====
if (app.Environment.IsDevelopment())
{
    await app.Services.InitializeDatabaseAsync();
}

app.Run();

// Để sau này project test tích hợp (WebApplicationFactory<Program>) truy cập được.
public partial class Program;
