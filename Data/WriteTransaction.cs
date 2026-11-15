using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ihouse.Data;

public static class WriteTransaction
{
    // Requests changing the same group wait for one another, even on different servers.
    // ReadCommitted makes validation see the changes committed by the previous request.
    public static async Task<IDbContextTransaction> BeginWriteAsync(
        this ApplicationDbContext db, int group)
    {
        var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(73421, {group})");
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public const int Rooms = 1;
    public const int Invoices = 2;
    public const int Tenants = 3;
    public const int Meters = 4;
    public const int Prices = 5;
    public const int Maintenance = 6;
}
