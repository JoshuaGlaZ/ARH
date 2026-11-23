using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ARH.Tests.Integration;

[Collection("MySQL integration")]
public class MySqlSmokeTests
{
    private readonly MySqlFixture _fixture;

    public MySqlSmokeTests(MySqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Database_ShouldContainFiveUsersAndTwoApplications()
    {
        // Reset before test
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var userCount = await db.Users.CountAsync();

        var applicationCount =
            await db.Applications.CountAsync();

        Assert.Equal(5, userCount);
        Assert.Equal(2, applicationCount);
    }

    [Fact]
    public async Task Database_ShouldHaveCorrectManagerAssignment()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var alice = await db.Users.SingleAsync(
            x => x.Email == "alice@example.local");

        var bob = await db.Users.SingleAsync(
            x => x.Email == "bob@example.local");

        Assert.Equal(bob.Id, alice.ManagerId);
    }

    [Fact]
    public async Task Database_ShouldHaveCorrectApplicationOwners()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var carol = await db.Users.SingleAsync(
            x => x.Email == "carol@example.local");

        var dana = await db.Users.SingleAsync(
            x => x.Email == "dana@example.local");

        var crm = await db.Applications.SingleAsync(
            x => x.Name == "CRM");

        var finance = await db.Applications.SingleAsync(
            x => x.Name == "Finance Portal");

        Assert.Equal(carol.Id, crm.SystemOwnerId);
        Assert.Equal(dana.Id, finance.SystemOwnerId);
    }
}