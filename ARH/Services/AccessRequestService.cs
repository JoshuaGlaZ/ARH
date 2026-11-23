
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ARH.Data;
using ARH.Domain.Entities;
using ARH.Domain.Enums;
using ARH.DTOs;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ARH.Services
{
    public class AccessRequestService
    {
        private const string CurrentPolicyVersion = "v1";

        private readonly ArhDbContext _db;
        private readonly WorkflowService _workflow;

        public AccessRequestService(
            ArhDbContext db,
            WorkflowService workflow)
        {
            _db = db;
            _workflow = workflow;
        }

        // CREATE REQUEST

        public async Task<CreateRequestResult> CreateAsync(
            Guid requesterId,
            CreateRequestDto command,
            CancellationToken ct = default)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(command.ClientRequestId))
                throw new ArgumentException(
                    "ClientRequestId is required.");

            var clientRequestId = command.ClientRequestId.Trim();

            if (clientRequestId.Length > 100)
                throw new ArgumentException(
                    "ClientRequestId cannot exceed 100 characters.");

            if (command.ApplicationId == Guid.Empty)
                throw new ArgumentException(
                    "ApplicationId is required.");

            if (!Enum.IsDefined(command.Environment))
                throw new ArgumentException("Invalid environment.");

            if (!Enum.IsDefined(command.AccessLevel))
                throw new ArgumentException("Invalid access level.");

            if (string.IsNullOrWhiteSpace(command.Justification))
                throw new ArgumentException(
                    "Justification is required.");

            var justification = command.Justification.Trim();

            if (justification.Length > 1000)
                throw new ArgumentException(
                    "Justification cannot exceed 1000 characters.");

            // Generate payload hash
            var payloadHash = CalculatePayloadHash(
                command.ApplicationId,
                command.Environment,
                command.AccessLevel,
                justification);

            // Validate requester
            var requester = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == requesterId && x.IsActive,
                    ct);

            if (requester == null)
                throw new UnauthorizedAccessException(
                    "Requester does not exist or is inactive.");

            if (requester.Role != UserRole.Requester)
                throw new UnauthorizedAccessException(
                    "Only Requesters can create access requests.");

            // Check existing idempotency key
            var existing = await LoadRequestAsync(
                requesterId,
                clientRequestId,
                ct);

            if (existing != null)
            {
                ValidateIdempotentReplay(existing, payloadHash);

                return new CreateRequestResult(
                    existing,
                    IsReplay: true);
            }

            // Validate Manager assignment
            if (requester.ManagerId == null)
                throw new InvalidOperationException(
                    "Requester does not have an assigned Manager.");

            var managerExists = await _db.Users.AnyAsync(
                x => x.Id == requester.ManagerId
                  && x.Role == UserRole.Manager
                  && x.IsActive,
                ct);

            if (!managerExists)
                throw new InvalidOperationException(
                    "Assigned Manager is invalid or inactive.");

            // Validate Application
            var application = await _db.Applications
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == command.ApplicationId
                      && x.IsActive,
                    ct);

            if (application == null)
                throw new InvalidOperationException(
                    "Application does not exist or is inactive.");

            // Validate System Owner
            var ownerExists = await _db.Users.AnyAsync(
                x => x.Id == application.SystemOwnerId
                  && x.Role == UserRole.SystemOwner
                  && x.IsActive,
                ct);

            if (!ownerExists)
                throw new InvalidOperationException(
                    "Application System Owner is invalid.");

            // Calculate risk
            var riskLevel = _workflow.CalculateRisk(
                command.Environment,
                command.AccessLevel);

            var now = DateTime.UtcNow;

            // Create request
            var request = new AccessRequest
            {
                Id = Guid.NewGuid(),

                ClientRequestId = clientRequestId,
                RequestPayloadHash = payloadHash,

                RequesterId = requesterId,
                ApplicationId = command.ApplicationId,

                Environment = command.Environment,
                AccessLevel = command.AccessLevel,
                RiskLevel = riskLevel,

                Justification = justification,

                Status = RequestStatus.PendingManagerApproval,

                CurrentApproverId = requester.ManagerId,

                PolicyVersion = CurrentPolicyVersion,

                Version = 1,

                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            // Initial audit
            var audit = new AuditLog
            {
                Id = Guid.NewGuid(),

                AccessRequestId = request.Id,

                ActorId = requesterId,
                ActorEmailSnapshot = requester.Email,

                Action = "RequestCreated",

                FromStatus = null,
                ToStatus = RequestStatus.PendingManagerApproval.ToString(),

                PreviousVersion = 0,
                NewVersion = 1,

                PolicyVersion = CurrentPolicyVersion,

                Reason = null,

                TimestampUtc = now
            };

            // Atomic database transaction
            try
            {
                await using var transaction =
                    await _db.Database.BeginTransactionAsync(ct);

                _db.AccessRequests.Add(request);
                _db.AuditLogs.Add(audit);

                await _db.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException ex)
                when (IsDuplicateKeyException(ex))
            {
                
                _db.ChangeTracker.Clear();

                var duplicate = await LoadRequestAsync(
                    requesterId,
                    clientRequestId,
                    ct);

                if (duplicate == null)
                    throw;

                ValidateIdempotentReplay(
                    duplicate,
                    payloadHash);

                return new CreateRequestResult(
                    duplicate,
                    IsReplay: true);
            }

            // Reload complete request
            var created = await LoadRequestAsync(
                requesterId,
                clientRequestId,
                ct);

            if (created == null)
                throw new InvalidOperationException(
                    "Created request could not be reloaded.");

            return new CreateRequestResult(
                created,
                IsReplay: false);
        }

        // GET MY REQUESTS

        public async Task<List<AccessRequest>> GetMyRequestsAsync(
            Guid requesterId,
            CancellationToken ct = default)
        {
            var requester = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == requesterId
                      && x.IsActive
                      && x.Role == UserRole.Requester,
                    ct);

            if (requester == null)
                throw new UnauthorizedAccessException(
                    "Requester is not authorized.");

            return await _db.AccessRequests
                .AsNoTracking()
                .Include(x => x.Requester)
                .Include(x => x.Application)
                .Include(x => x.CurrentApprover)
                .Where(x => x.RequesterId == requesterId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(ct);
        }

        // GET APPROVAL INBOX

        public async Task<List<AccessRequest>> GetInboxAsync(
            Guid actorId,
            CancellationToken ct = default)
        {
            var actor = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == actorId && x.IsActive,
                    ct);

            if (actor == null)
                throw new UnauthorizedAccessException(
                    "User does not exist or is inactive.");

            IQueryable<AccessRequest> query =
                _db.AccessRequests.AsNoTracking();

            switch (actor.Role)
            {
                case UserRole.Manager:

                    query = query.Where(x =>
                        x.Status == RequestStatus.PendingManagerApproval
                        && x.CurrentApproverId == actorId
                        && x.Requester!.ManagerId == actorId);

                    break;

                case UserRole.SystemOwner:

                    query = query.Where(x =>
                        x.Status == RequestStatus.PendingSystemOwnerApproval
                        && x.CurrentApproverId == actorId
                        && x.Application!.SystemOwnerId == actorId);

                    break;

                default:

                    return new List<AccessRequest>();
            }

            return await query
                .Include(x => x.Requester)
                .Include(x => x.Application)
                .Include(x => x.CurrentApprover)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(ct);
        }

        // GET REQUEST DETAIL

        public async Task<AccessRequest> GetRequestAsync(
            Guid actorId,
            Guid requestId,
            CancellationToken ct = default)
        {
            var request = await _db.AccessRequests
                .AsNoTracking()
                .Include(x => x.Requester)
                .Include(x => x.Application)
                .Include(x => x.CurrentApprover)
                .FirstOrDefaultAsync(
                    x => x.Id == requestId,
                    ct);

            if (request == null)
                throw new KeyNotFoundException(
                    "Request not found.");

            var actor = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == actorId && x.IsActive,
                    ct);

            if (actor == null)
                throw new UnauthorizedAccessException(
                    "User does not exist or is inactive.");

            var allowed = actor.Role switch
            {
                UserRole.AdminAuditor => true,

                UserRole.Requester =>
                    request.RequesterId == actorId,

                UserRole.Manager =>
                    request.Requester?.ManagerId == actorId,

                UserRole.SystemOwner =>
                    request.Application?.SystemOwnerId == actorId,

                _ => false
            };

            if (!allowed)
                throw new UnauthorizedAccessException(
                    "You are not allowed to view this request.");

            return request;
        }

        // APPROVE REQUEST

        public Task<AccessRequest> ApproveAsync(
            Guid actorId,
            Guid requestId,
            long expectedVersion,
            CancellationToken ct = default)
        {
            return ExecuteTransitionAsync(
                actorId,
                requestId,
                expectedVersion,
                reject: false,
                reason: null,
                ct);
        }

        // REJECT REQUEST

        public Task<AccessRequest> RejectAsync(
            Guid actorId,
            Guid requestId,
            long expectedVersion,
            string reason,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException(
                    "Rejection reason is required.");

            reason = reason.Trim();

            if (reason.Length > 1000)
                throw new ArgumentException(
                    "Rejection reason cannot exceed 1000 characters.");

            return ExecuteTransitionAsync(
                actorId,
                requestId,
                expectedVersion,
                reject: true,
                reason,
                ct);
        }

        // EXECUTE WORKFLOW TRANSITION

        private async Task<AccessRequest> ExecuteTransitionAsync(
            Guid actorId,
            Guid requestId,
            long expectedVersion,
            bool reject,
            string? reason,
            CancellationToken ct)
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(ct);

            // Load request
            var request = await _db.AccessRequests
                .AsNoTracking()
                .Include(x => x.Requester)
                .Include(x => x.Application)
                .FirstOrDefaultAsync(
                    x => x.Id == requestId,
                    ct);

            if (request == null)
                throw new KeyNotFoundException(
                    "Request not found.");

            // Load actor
            var actor = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == actorId && x.IsActive,
                    ct);

            if (actor == null)
                throw new UnauthorizedAccessException(
                    "Actor does not exist or is inactive.");

            // Prevent self approval / rejection
            if (request.RequesterId == actorId)
                throw new UnauthorizedAccessException(
                    "Requester cannot process their own request.");

            // Check version
            if (request.Version != expectedVersion)
                throw new InvalidOperationException(
                    "Request version is outdated. Please refresh.");

            // Validate authorization and current stage
            switch (request.Status)
            {
                case RequestStatus.PendingManagerApproval:

                    if (actor.Role != UserRole.Manager)
                        throw new UnauthorizedAccessException(
                            "Only the assigned Manager can process this request.");

                    if (request.Requester?.ManagerId != actorId ||
                        request.CurrentApproverId != actorId)
                        throw new UnauthorizedAccessException(
                            "Manager is not authorized for this request.");

                    break;

                case RequestStatus.PendingSystemOwnerApproval:

                    if (actor.Role != UserRole.SystemOwner)
                        throw new UnauthorizedAccessException(
                            "Only the assigned System Owner can process this request.");

                    if (request.Application?.SystemOwnerId != actorId ||
                        request.CurrentApproverId != actorId)
                        throw new UnauthorizedAccessException(
                            "System Owner is not authorized for this request.");

                    break;

                case RequestStatus.Approved:
                case RequestStatus.Rejected:

                    throw new InvalidOperationException(
                        "Request has already reached a terminal status.");

                default:

                    throw new InvalidOperationException(
                        "Invalid request status.");
            }

            // Determine next status
            var previousStatus = request.Status;

            var nextStatus = reject
                ? _workflow.GetAfterRejection(request)
                : previousStatus switch
                {
                    RequestStatus.PendingManagerApproval =>
                        _workflow.GetAfterManagerApproval(request),

                    RequestStatus.PendingSystemOwnerApproval =>
                        _workflow.GetAfterSystemOwnerApproval(request),

                    _ => throw new InvalidOperationException(
                        "Invalid approval transition.")
                };

            // Determine next approver
            Guid? nextApproverId = null;

            if (nextStatus ==
                RequestStatus.PendingSystemOwnerApproval)
            {
                nextApproverId = request.Application!.SystemOwnerId;
            }

            var now = DateTime.UtcNow;
            var newVersion = request.Version + 1;

            // Conditional update
            var affected = await _db.AccessRequests
                .Where(x =>
                    x.Id == requestId &&
                    x.Status == previousStatus &&
                    x.Version == expectedVersion)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, nextStatus)
                    .SetProperty(x => x.CurrentApproverId, nextApproverId)
                    .SetProperty(x => x.Version, newVersion)
                    .SetProperty(
                        x => x.RejectReason,
                        reject ? reason : null)
                    .SetProperty(x => x.UpdatedAtUtc, now),
                    ct);

            if (affected != 1)
                throw new InvalidOperationException(
                    "Request was modified by another action. Please refresh.");

            // Create audit event
            var audit = new AuditLog
            {
                Id = Guid.NewGuid(),

                AccessRequestId = requestId,

                ActorId = actorId,
                ActorEmailSnapshot = actor.Email,

                Action = reject
                    ? $"{actor.Role}Rejected"
                    : $"{actor.Role}Approved",

                FromStatus = previousStatus.ToString(),
                ToStatus = nextStatus.ToString(),

                PreviousVersion = request.Version,
                NewVersion = newVersion,

                PolicyVersion = request.PolicyVersion,

                Reason = reject ? reason : null,

                TimestampUtc = now
            };

            _db.AuditLogs.Add(audit);

            // Save audit and commit
            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            // Return updated state
            request.Status = nextStatus;
            request.CurrentApproverId = nextApproverId;
            request.Version = newVersion;
            request.RejectReason = reject ? reason : null;
            request.UpdatedAtUtc = now;

            return request;
        }

        // PRIVATE HELPERS

        private Task<AccessRequest?> LoadRequestAsync(
            Guid requesterId,
            string clientRequestId,
            CancellationToken ct)
        {
            return _db.AccessRequests
                .AsNoTracking()
                .Include(x => x.Requester)
                .Include(x => x.Application)
                .Include(x => x.CurrentApprover)
                .FirstOrDefaultAsync(
                    x => x.RequesterId == requesterId
                      && x.ClientRequestId == clientRequestId,
                    ct);
        }

        private static string CalculatePayloadHash(
            Guid applicationId,
            EnvironmentType environment,
            AccessLevel accessLevel,
            string justification)
        {
            var payload = JsonSerializer.Serialize(new
            {
                ApplicationId = applicationId,
                Environment = environment.ToString(),
                AccessLevel = accessLevel.ToString(),
                Justification = justification
            });

            var bytes = Encoding.UTF8.GetBytes(payload);

            return Convert.ToHexString(
                SHA256.HashData(bytes)).ToLowerInvariant();
        }

        private static void ValidateIdempotentReplay(
            AccessRequest existing,
            string payloadHash)
        {
            if (!string.Equals(
                existing.RequestPayloadHash,
                payloadHash,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "ClientRequestId already exists with a different payload.");
            }
        }

        private static bool IsDuplicateKeyException(
            DbUpdateException exception)
        {
            return exception.GetBaseException() is MySqlException mysql
                && mysql.Number == 1062;
        }
    }
}