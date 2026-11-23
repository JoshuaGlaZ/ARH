using ARH.Domain.Enums;

namespace ARH.Domain.Entities
{

    public class AccessRequest
    {
        public Guid Id { get; set; }

        // Idempotency
        public string ClientRequestId { get; set; } = string.Empty;

        public string RequestPayloadHash { get; set; } = string.Empty;

        // Request information
        public Guid RequesterId { get; set; }

        public Guid ApplicationId { get; set; }

        public EnvironmentType Environment { get; set; }

        public AccessLevel AccessLevel { get; set; }

        public RiskLevel RiskLevel { get; set; }

        public string Justification { get; set; } = string.Empty;

        // Workflow
        public RequestStatus Status { get; set; }

        public Guid? CurrentApproverId { get; set; }

        public string? RejectReason { get; set; }

        // Policy snapshot
        public string PolicyVersion { get; set; } = "v1";

        // Optimistic concurrency
        public long Version { get; set; } = 1;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        // Navigation properties
        public User? Requester { get; set; }

        public Application? Application { get; set; }

        public User? CurrentApprover { get; set; }

        public ICollection<AuditLog> AuditLogs { get; set; }
            = new List<AuditLog>();
    }
}