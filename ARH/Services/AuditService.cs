using ARH.Data;
using ARH.Domain.Entities;
using ARH.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ARH.Services{

public class AuditService
{
    private readonly ArhDbContext _db;
    private readonly AccessRequestService _requestService;

    public AuditService(
        ArhDbContext db,
        AccessRequestService requestService)
    {
        _db = db;
        _requestService = requestService;
    }

    // Get audit history for an authorized request
    public async Task<List<AuditLog>> GetRequestHistoryAsync(
        Guid actorId,
        Guid requestId,
        CancellationToken ct = default)
    {
        // Reuse resource authorization
        await _requestService.GetRequestAsync(
            actorId,
            requestId,
            ct);

        return await _db.AuditLogs
            .AsNoTracking()
            .Include(x => x.Actor)
            .Where(x => x.AccessRequestId == requestId)
            .OrderBy(x => x.NewVersion)
            .ToListAsync(ct);
    }

        // Admin/Auditor can view all audit logs
        public async Task<List<AuditLog>> GetAuditLogsAsync(
            Guid actorId,
            CancellationToken ct = default)
        {
            var actor = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == actorId && x.IsActive,
                    ct);

            if (actor == null ||
                actor.Role != UserRole.AdminAuditor)
            {
                throw new UnauthorizedAccessException(
                    "Only Admin/Auditor can view all audit logs.");
            }

            return await _db.AuditLogs
                .AsNoTracking()
                .Include(x => x.Actor)
                .OrderByDescending(x => x.TimestampUtc)
                .ToListAsync(ct);
        }
}}