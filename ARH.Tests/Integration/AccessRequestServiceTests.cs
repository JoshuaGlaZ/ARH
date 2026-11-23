using ARH.Data;
using ARH.Domain.Entities;
using ARH.Domain.Enums;
using ARH.DTOs;
using ARH.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ARH.Tests.Integration;

[Collection("MySQL integration")]
public class AccessRequestServiceTests
{
    private readonly MySqlFixture _fixture;

    public AccessRequestServiceTests(MySqlFixture fixture)
    {
        _fixture = fixture;
    }

    // ==========================================
    // HELPERS
    // ==========================================

    private static AccessRequestService CreateService(
        ArhDbContext db)
    {
        return new AccessRequestService(
            db,
            new WorkflowService());
    }

    private static CreateRequestDto CreateCommand(
        Guid applicationId,
        string clientRequestId = "test-request-001")
    {
        return new CreateRequestDto
        {
            ClientRequestId = clientRequestId,
            ApplicationId = applicationId,
            Environment = EnvironmentType.NonProduction,
            AccessLevel = AccessLevel.Read,
            Justification = "Required for business operations."
        };
    }

    private async Task<Guid> GetUserIdAsync(
        ArhDbContext db,
        string email)
    {
        return await db.Users
            .Where(x => x.Email == email)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private async Task<Guid> GetApplicationIdAsync(
        ArhDbContext db,
        string name)
    {
        return await db.Applications
            .Where(x => x.Name == name)
            .Select(x => x.Id)
            .SingleAsync();
    }

    // ==========================================
    // 1. CREATE REQUEST & IDEMPOTENCY
    // ==========================================

    [Fact]
    public async Task CreateAsync_ShouldCreatePendingManagerRequest()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var bobId = await GetUserIdAsync(
            db, "bob@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var result = await CreateService(db).CreateAsync(
            aliceId, CreateCommand(crmId));

        var request = result.Request;

        Assert.False(result.IsReplay);
        Assert.NotEqual(Guid.Empty, request.Id);

        Assert.Equal(
            RequestStatus.PendingManagerApproval,
            request.Status);

        Assert.Equal(RiskLevel.NonHigh, request.RiskLevel);
        Assert.Equal(bobId, request.CurrentApproverId);
        Assert.Equal(1, request.Version);

        Assert.Equal("Alice", request.Requester!.FullName);
        Assert.Equal("CRM", request.Application!.Name);
        Assert.Equal("Bob", request.CurrentApprover!.FullName);

        Assert.Equal(1, await db.AccessRequests.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateInitialAudit()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var result = await CreateService(db).CreateAsync(
            aliceId, CreateCommand(crmId));

        var audit = await db.AuditLogs.SingleAsync(
            x => x.AccessRequestId == result.Request.Id);

        Assert.Equal(aliceId, audit.ActorId);
        Assert.Equal("alice@example.local", audit.ActorEmailSnapshot);
        Assert.Equal("RequestCreated", audit.Action);
        Assert.Null(audit.FromStatus);

        Assert.Equal(
            RequestStatus.PendingManagerApproval.ToString(),
            audit.ToStatus);

        Assert.Equal(0, audit.PreviousVersion);
        Assert.Equal(1, audit.NewVersion);
        Assert.Equal("v1", audit.PolicyVersion);
    }

    [Fact]
    public async Task CreateAsync_DuplicateSamePayload_ShouldReturnExistingRequest()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var service = CreateService(db);
        var command = CreateCommand(crmId);

        var first = await service.CreateAsync(aliceId, command);
        var second = await service.CreateAsync(aliceId, command);

        Assert.False(first.IsReplay);
        Assert.True(second.IsReplay);

        Assert.Equal(first.Request.Id, second.Request.Id);

        Assert.Equal(
            1,
            await db.AccessRequests.CountAsync());

        Assert.Equal(
            1,
            await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_DuplicateDifferentPayload_ShouldThrow()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var financeId = await GetApplicationIdAsync(
            db, "Finance Portal");

        var service = CreateService(db);

        await service.CreateAsync(
            aliceId, CreateCommand(crmId));

        var differentCommand = CreateCommand(crmId);
        differentCommand.ApplicationId = financeId;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(
                aliceId, differentCommand));

        Assert.Equal(1, await db.AccessRequests.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RequesterWithoutManager_ShouldThrow()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var requesterId = Guid.NewGuid();

        db.Users.Add(new User
        {
            Id = requesterId,
            Email = "unassigned@example.local",
            FullName = "Unassigned User",
            Role = UserRole.Requester,
            ManagerId = null,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(
                requesterId, CreateCommand(crmId)));

        Assert.Equal(0, await db.AccessRequests.CountAsync());
        Assert.Equal(0, await db.AuditLogs.CountAsync());
    }

    // ==========================================
    // 2. MANAGER APPROVAL
    // ==========================================

    [Fact]
    public async Task ApproveAsync_ManagerNonHighRisk_ShouldApprove()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var bobId = await GetUserIdAsync(
            db, "bob@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var service = CreateService(db);

        var created = await service.CreateAsync(
            aliceId, CreateCommand(crmId));

        var result = await service.ApproveAsync(
            bobId, created.Request.Id, created.Request.Version);

        Assert.Equal(RequestStatus.Approved, result.Status);
        Assert.Null(result.CurrentApproverId);
        Assert.Equal(2, result.Version);

        Assert.Equal(
            2,
            await db.AuditLogs.CountAsync(
                x => x.AccessRequestId == created.Request.Id));
    }

    [Fact]
    public async Task ApproveAsync_ManagerHighRisk_ShouldRouteToSystemOwner()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var bobId = await GetUserIdAsync(
            db, "bob@example.local");

        var carolId = await GetUserIdAsync(
            db, "carol@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var command = CreateCommand(crmId);
        command.Environment = EnvironmentType.Production;

        var service = CreateService(db);

        var created = await service.CreateAsync(aliceId, command);

        var result = await service.ApproveAsync(
            bobId, created.Request.Id, created.Request.Version);

        Assert.Equal(
            RequestStatus.PendingSystemOwnerApproval,
            result.Status);

        Assert.Equal(carolId, result.CurrentApproverId);
        Assert.Equal(2, result.Version);
    }

    // ==========================================
    // 3. SYSTEM OWNER APPROVAL
    // ==========================================

    [Fact]
    public async Task ApproveAsync_SystemOwner_ShouldApproveHighRisk()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var bobId = await GetUserIdAsync(
            db, "bob@example.local");

        var carolId = await GetUserIdAsync(
            db, "carol@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var command = CreateCommand(crmId);
        command.Environment = EnvironmentType.Production;

        var service = CreateService(db);

        var created = await service.CreateAsync(aliceId, command);

        var managerResult = await service.ApproveAsync(
            bobId, created.Request.Id, 1);

        var ownerResult = await service.ApproveAsync(
            carolId, created.Request.Id, managerResult.Version);

        Assert.Equal(RequestStatus.Approved, ownerResult.Status);
        Assert.Null(ownerResult.CurrentApproverId);
        Assert.Equal(3, ownerResult.Version);

        Assert.Equal(
            3,
            await db.AuditLogs.CountAsync(
                x => x.AccessRequestId == created.Request.Id));
    }

    // ==========================================
    // 4. REJECTION
    // ==========================================

    [Fact]
    public async Task RejectAsync_Manager_ShouldRejectRequest()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var bobId = await GetUserIdAsync(
            db, "bob@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var service = CreateService(db);

        var created = await service.CreateAsync(
            aliceId, CreateCommand(crmId));

        const string reason =
            "Insufficient business justification.";

        var result = await service.RejectAsync(
            bobId, created.Request.Id,
            created.Request.Version, reason);

        Assert.Equal(RequestStatus.Rejected, result.Status);
        Assert.Equal(reason, result.RejectReason);
        Assert.Null(result.CurrentApproverId);
        Assert.Equal(2, result.Version);
    }

    [Fact]
    public async Task RejectAsync_SystemOwner_ShouldRejectHighRisk()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var bobId = await GetUserIdAsync(
            db, "bob@example.local");

        var carolId = await GetUserIdAsync(
            db, "carol@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var command = CreateCommand(crmId);
        command.Environment = EnvironmentType.Production;

        var service = CreateService(db);

        var created = await service.CreateAsync(aliceId, command);

        var managerResult = await service.ApproveAsync(
            bobId, created.Request.Id, 1);

        const string reason =
            "Production access is not approved.";

        var result = await service.RejectAsync(
            carolId,
            created.Request.Id,
            managerResult.Version,
            reason);

        Assert.Equal(RequestStatus.Rejected, result.Status);
        Assert.Null(result.CurrentApproverId);
        Assert.Equal(3, result.Version);
        Assert.Equal(reason, result.RejectReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectAsync_WithoutReason_ShouldThrow(
        string reason)
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var bobId = await GetUserIdAsync(
            db, "bob@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var service = CreateService(db);

        var created = await service.CreateAsync(
            aliceId, CreateCommand(crmId));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RejectAsync(
                bobId,
                created.Request.Id,
                created.Request.Version,
                reason));

        var persisted = await db.AccessRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Request.Id);

        Assert.Equal(
            RequestStatus.PendingManagerApproval,
            persisted.Status);

        Assert.Equal(1, persisted.Version);

        Assert.Equal(
            1,
            await db.AuditLogs.CountAsync(
                x => x.AccessRequestId == created.Request.Id));
    }

    // ==========================================
    // 5. AUTHORIZATION
    // ==========================================

    [Fact]
    public async Task ApproveAsync_WrongManager_ShouldBeRejected()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var danaId = await GetUserIdAsync(
            db, "dana@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var service = CreateService(db);

        var created = await service.CreateAsync(
            aliceId, CreateCommand(crmId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ApproveAsync(
                danaId,
                created.Request.Id,
                created.Request.Version));

        Assert.Equal(
            1,
            await db.AuditLogs.CountAsync(
                x => x.AccessRequestId == created.Request.Id));

        var persisted = await db.AccessRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Request.Id);

        Assert.Equal(
            RequestStatus.PendingManagerApproval,
            persisted.Status);
    }

    [Fact]
    public async Task ApproveAsync_WrongSystemOwner_ShouldBeRejected()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var bobId = await GetUserIdAsync(
            db, "bob@example.local");

        var carolId = await GetUserIdAsync(
            db, "carol@example.local");

        var financeId = await GetApplicationIdAsync(
            db, "Finance Portal");

        var command = CreateCommand(financeId);
        command.AccessLevel = AccessLevel.Admin;

        var service = CreateService(db);

        var created = await service.CreateAsync(aliceId, command);

        var managerResult = await service.ApproveAsync(
            bobId, created.Request.Id, 1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ApproveAsync(
                carolId,
                created.Request.Id,
                managerResult.Version));

        var persisted = await db.AccessRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Request.Id);

        Assert.Equal(
            RequestStatus.PendingSystemOwnerApproval,
            persisted.Status);

        Assert.Equal(2, persisted.Version);

        Assert.Equal(
            2,
            await db.AuditLogs.CountAsync(
                x => x.AccessRequestId == created.Request.Id));
    }

    [Fact]
    public async Task ApproveAsync_RequesterCannotApproveOwnRequest()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var service = CreateService(db);

        var created = await service.CreateAsync(
            aliceId, CreateCommand(crmId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ApproveAsync(
                aliceId,
                created.Request.Id,
                created.Request.Version));

        var persisted = await db.AccessRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Request.Id);

        Assert.Equal(
            RequestStatus.PendingManagerApproval,
            persisted.Status);

        Assert.Equal(1, persisted.Version);

        Assert.Equal(
            1,
            await db.AuditLogs.CountAsync(
                x => x.AccessRequestId == created.Request.Id));
    }

    // ==========================================
    // 6. STALE VERSION
    // ==========================================

    [Fact]
    public async Task ApproveAsync_StaleVersion_ShouldBeRejected()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var bobId = await GetUserIdAsync(
            db, "bob@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var service = CreateService(db);

        var created = await service.CreateAsync(
            aliceId, CreateCommand(crmId));

        await service.ApproveAsync(
            bobId, created.Request.Id, expectedVersion: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RejectAsync(
                bobId,
                created.Request.Id,
                expectedVersion: 1,
                reason: "Stale action"));

        var persisted = await db.AccessRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Request.Id);

        Assert.Equal(RequestStatus.Approved, persisted.Status);
        Assert.Equal(2, persisted.Version);

        Assert.Equal(
            2,
            await db.AuditLogs.CountAsync(
                x => x.AccessRequestId == created.Request.Id));
    }

    // ==========================================
    // 7. TERMINAL STATES
    // ==========================================

    [Fact]
    public async Task ApproveAsync_AlreadyApproved_ShouldBeRejected()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var bobId = await GetUserIdAsync(
            db, "bob@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var service = CreateService(db);

        var created = await service.CreateAsync(
            aliceId, CreateCommand(crmId));

        await service.ApproveAsync(
            bobId, created.Request.Id, created.Request.Version);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApproveAsync(
                bobId,
                created.Request.Id,
                expectedVersion: 2));

        Assert.Equal(
            2,
            await db.AuditLogs.CountAsync(
                x => x.AccessRequestId == created.Request.Id));
    }

    [Fact]
    public async Task RejectAsync_AlreadyRejected_ShouldBeRejected()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();

        var aliceId = await GetUserIdAsync(
            db, "alice@example.local");

        var bobId = await GetUserIdAsync(
            db, "bob@example.local");

        var crmId = await GetApplicationIdAsync(db, "CRM");

        var service = CreateService(db);

        var created = await service.CreateAsync(
            aliceId, CreateCommand(crmId));

        await service.RejectAsync(
            bobId,
            created.Request.Id,
            created.Request.Version,
            "Request rejected.");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApproveAsync(
                bobId,
                created.Request.Id,
                expectedVersion: 2));

        Assert.Equal(
            2,
            await db.AuditLogs.CountAsync(
                x => x.AccessRequestId == created.Request.Id));
    }

    // ==========================================
    // 8. CONCURRENT CREATE
    // ==========================================

    [Fact]
    public async Task CreateAsync_ConcurrentDuplicate_ShouldCreateOnlyOneRequest()
    {
        await _fixture.ResetDatabaseAsync();

        Guid aliceId;
        Guid crmId;

        await using (var db = _fixture.CreateDbContext())
        {
            aliceId = await GetUserIdAsync(
                db, "alice@example.local");

            crmId = await GetApplicationIdAsync(db, "CRM");
        }

        var command = CreateCommand(
            crmId, "concurrent-request-001");

        async Task<CreateRequestResult> CreateRequest()
        {
            await using var db = _fixture.CreateDbContext();

            return await CreateService(db).CreateAsync(
                aliceId, command);
        }

        var results = await Task.WhenAll(
            CreateRequest(),
            CreateRequest());

        Assert.Equal(
            results[0].Request.Id,
            results[1].Request.Id);

        Assert.Equal(
            1,
            results.Count(x => x.IsReplay));

        await using var verifyDb = _fixture.CreateDbContext();

        Assert.Equal(1, await verifyDb.AccessRequests.CountAsync());
        Assert.Equal(1, await verifyDb.AuditLogs.CountAsync());
    }

    // ==========================================
    // 9. CONCURRENT APPROVE / REJECT
    // ==========================================

    [Fact]
    public async Task ConcurrentApprovalAndRejection_OnlyOneShouldSucceed()
    {
        await _fixture.ResetDatabaseAsync();

        Guid aliceId;
        Guid bobId;
        Guid crmId;
        Guid requestId;

        await using (var db = _fixture.CreateDbContext())
        {
            aliceId = await GetUserIdAsync(
                db, "alice@example.local");

            bobId = await GetUserIdAsync(
                db, "bob@example.local");

            crmId = await GetApplicationIdAsync(db, "CRM");

            var created = await CreateService(db).CreateAsync(
                aliceId, CreateCommand(crmId));

            requestId = created.Request.Id;
        }

        async Task<bool> TryApprove()
        {
            await using var db = _fixture.CreateDbContext();

            try
            {
                await CreateService(db).ApproveAsync(
                    bobId, requestId, expectedVersion: 1);

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        async Task<bool> TryReject()
        {
            await using var db = _fixture.CreateDbContext();

            try
            {
                await CreateService(db).RejectAsync(
                    bobId,
                    requestId,
                    expectedVersion: 1,
                    reason: "Concurrent rejection test");

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(
            TryApprove(),
            TryReject());

        Assert.Equal(1, results.Count(x => x));
        Assert.Equal(1, results.Count(x => !x));

        await using var verifyDb = _fixture.CreateDbContext();

        var persisted = await verifyDb.AccessRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == requestId);

        Assert.Equal(2, persisted.Version);

        Assert.Contains(
            persisted.Status,
            new[]
            {
                RequestStatus.Approved,
                RequestStatus.Rejected
            });

        Assert.Equal(
            2,
            await verifyDb.AuditLogs.CountAsync(
                x => x.AccessRequestId == requestId));
    }

    // ==========================================
    // 10. ATOMIC AUDIT ROLLBACK
    // ==========================================

    [Fact]
    public async Task ApproveAsync_AuditInsertFails_ShouldRollbackStatus()
    {
        await _fixture.ResetDatabaseAsync();

        Guid aliceId;
        Guid bobId;
        Guid crmId;
        Guid requestId;

        await using (var db = _fixture.CreateDbContext())
        {
            aliceId = await GetUserIdAsync(
                db, "alice@example.local");

            bobId = await GetUserIdAsync(
                db, "bob@example.local");

            crmId = await GetApplicationIdAsync(db, "CRM");

            var created = await CreateService(db).CreateAsync(
                aliceId, CreateCommand(crmId));

            requestId = created.Request.Id;
        }

        const string triggerName = "trg_test_audit_failure";

        await using var setupDb = _fixture.CreateDbContext();

        try
        {
            await setupDb.Database.ExecuteSqlRawAsync(
                $"DROP TRIGGER IF EXISTS `{triggerName}`;");

            await setupDb.Database.ExecuteSqlRawAsync($"""
                CREATE TRIGGER `{triggerName}`
                BEFORE INSERT ON `AuditLogs`
                FOR EACH ROW
                SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Forced audit failure';
                """);

            await using var actionDb = _fixture.CreateDbContext();

            var service = CreateService(actionDb);

            await Assert.ThrowsAsync<DbUpdateException>(
                () => service.ApproveAsync(
                    bobId, requestId, expectedVersion: 1));

            await using var verifyDb = _fixture.CreateDbContext();

            var persisted = await verifyDb.AccessRequests
                .AsNoTracking()
                .SingleAsync(x => x.Id == requestId);

            Assert.Equal(
                RequestStatus.PendingManagerApproval,
                persisted.Status);

            Assert.Equal(1, persisted.Version);
            Assert.Equal(bobId, persisted.CurrentApproverId);

            Assert.Equal(
                1,
                await verifyDb.AuditLogs.CountAsync(
                    x => x.AccessRequestId == requestId));
        }
        finally
        {
            await setupDb.Database.ExecuteSqlRawAsync(
                $"DROP TRIGGER IF EXISTS `{triggerName}`;");
        }
    }
}
