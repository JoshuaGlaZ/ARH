
using ARH.Data;
using Microsoft.EntityFrameworkCore;

namespace ARH.Tests.Integration;

public sealed class MySqlFixture : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=127.0.0.1;" +
        "Port=3306;" +
        "Database=arh_test;" +
        "User=root;" +
        "Password=;";

    public ArhDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ArhDbContext>()
                .UseMySql(
                    ConnectionString,
                    ServerVersion.AutoDetect(ConnectionString))
                .Options;

        return new ArhDbContext(options);
    }

    // Run once when the test collection starts.
    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();

        await db.Database.MigrateAsync();

        await ResetDatabaseAsync();
    }

    // Reset test data before each test.
    public async Task ResetDatabaseAsync()
    {
        await using var db = CreateDbContext();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        // Delete dependent records first.
        await db.AuditLogs.ExecuteDeleteAsync();

        await db.AccessRequests.ExecuteDeleteAsync();

        await db.Applications.ExecuteDeleteAsync();

        // Delete employees before their managers.
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM `Users` WHERE `ManagerId` IS NOT NULL;");

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM `Users`;");

        await transaction.CommitAsync();

        // Restore demo master data.
        await ArhDbSeeder.SeedAsync(db);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
