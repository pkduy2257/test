using LegacyVault.Application.Repositories;
using LegacyVault.Domain.Entities;
using LegacyVault.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LegacyVault.Infrastructure.Repositories;

/// <summary>Cài đặt IVaultRepository bằng EF Core.</summary>
public sealed class VaultRepository : IVaultRepository
{
    private readonly AppDbContext _db;

    public VaultRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Vault?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        return _db.Vaults.SingleOrDefaultAsync(v => v.Id == id, ct);
    }

    public Task<List<Vault>> FindByOwnerIdAsync(Guid ownerId, CancellationToken ct)
    {
        return _db.Vaults.AsNoTracking()
            .Where(v => v.OwnerId == ownerId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(ct);
    }

    public void Add(Vault vault)
    {
        _db.Vaults.Add(vault);
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return _db.SaveChangesAsync(ct);
    }
}
