using LegacyVault.Api.Helpers;
using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;
using LegacyVault.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LegacyVault.Api.Controllers;

/// <summary>Kho của Owner đang đăng nhập. Mỗi Owner có đúng một kho (SETUP-12).</summary>
/// <remarks>
/// Không có tham số ownerId: chủ kho luôn là người trong JWT (xem ICurrentUser).
/// Role OWNER: Admin/Verifier không có đường tạo hay đọc kho qua vai trò vận hành (SEC-05).
/// </remarks>
[ApiController]
[Produces("application/json")]
[Route("api/v1/vaults")]
[Authorize(Roles = "OWNER")]
public sealed class VaultsController : ControllerBase
{
    private readonly IVaultService _vaultService;

    public VaultsController(IVaultService vaultService)
    {
        _vaultService = vaultService;
    }

    /// <summary>
    /// POST /api/v1/vaults — nút "Tạo kho của tôi". Body không bắt buộc.
    /// 201 khi vừa tạo kho mới; 200 khi Owner đã có kho (trả lại đúng kho đó, không tạo thêm).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> OpenOrCreate(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateVaultRequest? request, CancellationToken ct)
    {
        var result = await _vaultService.OpenOrCreateMyVaultAsync(request, ct);
        var body = new ApiResponse<VaultResponse>(result.Vault);
        if (result.Created)
        {
            return CreatedAtAction(nameof(GetById), new { vaultId = result.Vault.Id }, body);
        }

        return Ok(body);
    }

    /// <summary>GET /api/v1/vaults/me — kho của tôi; chưa tạo kho → 404 VAULT_NOT_FOUND.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var vault = await _vaultService.GetMyVaultAsync(ct);
        return Ok(new ApiResponse<VaultResponse>(vault));
    }

    /// <summary>GET /api/v1/vaults/{vaultId}</summary>
    [HttpGet("{vaultId:guid}")]
    public async Task<IActionResult> GetById(Guid vaultId, CancellationToken ct)
    {
        var vault = await _vaultService.GetVaultAsync(vaultId, ct);
        return Ok(new ApiResponse<VaultResponse>(vault));
    }
}
