using LegacyVault.Application.Repositories;
using LegacyVault.Domain.Entities;
using LegacyVault.Infrastructure.Data;
using LegacyVault.Infrastructure.Data.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LegacyVault.Infrastructure.Repositories;

/// <summary>Cài đặt IVaultRepository bằng EF Core.</summary>
public sealed class VaultRepository : IVaultRepository
{
    // Mã lỗi SQL Server khi chèn trùng: 2601 = unique index, 2627 = unique constraint.
    private const int SqlDuplicateKeyIndex = 2601;
    private const int SqlDuplicateKeyConstraint = 2627;

    private readonly AppDbContext _db;

    public VaultRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Vault?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        return _db.Vaults
            .Include(v => v.Plan)
            .Include(v => v.Entitlement)
            .SingleOrDefaultAsync(v => v.Id == id, ct);
    }

    public Task<Vault?> FindByOwnerIdAsync(Guid ownerId, CancellationToken ct)
    {
        return _db.Vaults
            .Include(v => v.Plan)
            .Include(v => v.Entitlement)
            .SingleOrDefaultAsync(v => v.OwnerId == ownerId, ct);
    }

    public async Task<bool> TryAddAsync(Vault vault, CancellationToken ct)
    {
        // Thêm Vault là EF thêm luôn Plan và Entitlement gắn với nó; SaveChanges chạy cả ba INSERT trong một transaction.
        _db.Vaults.Add(vault);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsOwnerAlreadyHasVault(ex))
        {
            // Bỏ theo dõi các bản ghi chèn hỏng, để lần SaveChanges sau trong request này không chèn lại chúng.
            _db.Entry(vault.Plan).State = EntityState.Detached;
            _db.Entry(vault.Entitlement).State = EntityState.Detached;
            _db.Entry(vault).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return _db.SaveChangesAsync(ct);
    }

    /// <summary>Lỗi do unique index UX_Vaults_OwnerId, không phải lỗi DB nào khác (lỗi khác vẫn ném tiếp ra 500).</summary>
    private static bool IsOwnerAlreadyHasVault(DbUpdateException ex)
    {
        var sqlException = ex.InnerException as SqlException;
        if (sqlException == null)
        {
            return false;
        }

        var isDuplicate = sqlException.Number == SqlDuplicateKeyIndex
            || sqlException.Number == SqlDuplicateKeyConstraint;
        return isDuplicate && sqlException.Message.Contains(VaultConfiguration.OwnerUniqueIndexName);
    }
}
