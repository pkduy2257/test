using LegacyVault.Api.Helpers;
using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;
using LegacyVault.Application.Services;
using LegacyVault.Application.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegacyVault.Api.Controllers;

/// <summary>Tài sản trong một kho của Owner đang đăng nhập.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/vaults/{vaultId:guid}/assets")]
[Authorize(Roles = "OWNER")]
public sealed class AssetsController : ControllerBase
{
    // Rộng hơn giới hạn tệp 20 MiB một chút: tệp 20–25 MiB vẫn tới được service và nhận đúng lỗi
    // INVALID_ASSET_FILE / FILE_TOO_LARGE; request lớn hơn nữa bị chặn sớm, không đọc hết vào bộ nhớ.
    private const long MaxRequestBytes = AssetFileValidator.MaxFileSizeBytes + 5 * 1024 * 1024;

    private readonly IAssetService _assetService;

    public AssetsController(IAssetService assetService)
    {
        _assetService = assetService;
    }

    /// <summary>
    /// POST /api/v1/vaults/{vaultId}/assets — multipart/form-data gồm name, description (tùy chọn), file.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Create(
        Guid vaultId, [FromForm] CreateAssetRequest request, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var upload = new UploadedFile
        {
            FileName = file.FileName,
            Length = file.Length,
            Content = stream,
        };

        var asset = await _assetService.CreateAssetAsync(vaultId, request, upload, ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<AssetResponse>(asset));
    }

    /// <summary>GET /api/v1/vaults/{vaultId}/assets — metadata, không có nội dung.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid vaultId, CancellationToken ct)
    {
        var assets = await _assetService.GetAssetsAsync(vaultId, ct);
        return Ok(new ApiResponse<List<AssetResponse>>(assets));
    }

    /// <summary>GET /api/v1/vaults/{vaultId}/assets/{assetId}/content — Owner tải tệp đã giải mã (§5.2 bước 2).</summary>
    [HttpGet("{assetId:guid}/content")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> Download(Guid vaultId, Guid assetId, CancellationToken ct)
    {
        var file = await _assetService.DownloadAssetAsync(vaultId, assetId, ct);

        // Không cho trình duyệt/proxy lưu bản rõ vào cache; không cho đoán lại kiểu nội dung.
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        // Client so checksum sau khi tải để biết đã nhận đủ byte (DOWNLOAD-05).
        Response.Headers["X-Content-SHA256"] = file.Sha256Checksum;

        return File(file.Content, file.ContentType, file.FileName);
    }
}
