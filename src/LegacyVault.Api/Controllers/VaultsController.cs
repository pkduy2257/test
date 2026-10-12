using LegacyVault.Api.Helpers;
using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;
using LegacyVault.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegacyVault.Api.Controllers;

/// <summary>Tạo và xem kho của Owner đang đăng nhập.</summary>
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

    /// <summary>POST /api/v1/vaults — tạo kho nháp.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateVaultRequest request, CancellationToken ct)
    {
        var vault = await _vaultService.CreateVaultAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { vaultId = vault.Id }, new ApiResponse<VaultResponse>(vault));
    }

    /// <summary>GET /api/v1/vaults — các kho của tôi.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var vaults = await _vaultService.GetMyVaultsAsync(ct);
        return Ok(new ApiResponse<List<VaultResponse>>(vaults));
    }

    /// <summary>GET /api/v1/vaults/{vaultId}</summary>
    [HttpGet("{vaultId:guid}")]
    public async Task<IActionResult> GetById(Guid vaultId, CancellationToken ct)
    {
        var vault = await _vaultService.GetVaultAsync(vaultId, ct);
        return Ok(new ApiResponse<VaultResponse>(vault));
    }
}
