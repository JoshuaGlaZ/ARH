
using ARH.Domain.Entities;

namespace ARH.DTOs
{
    public class AuditLogDto
    {
        public Guid Id { get; set; }

        public Guid AccessRequestId { get; set; }

        public string ActorName { get; set; } = string.Empty;

        public string ActorEmail { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string? FromStatus { get; set; }

        public string ToStatus { get; set; } = string.Empty;

        public long PreviousVersion { get; set; }

        public long NewVersion { get; set; }

        public string PolicyVersion { get; set; } = string.Empty;

        public string? Reason { get; set; }

        public DateTime TimestampUtc { get; set; }

        public static AuditLogDto FromEntity(AuditLog audit)
        {
            return new AuditLogDto
            {
                Id = audit.Id,
                AccessRequestId = audit.AccessRequestId,

                ActorName = audit.Actor?.FullName
                    ?? audit.ActorEmailSnapshot,

                ActorEmail = audit.ActorEmailSnapshot,

                Action = audit.Action,

                FromStatus = audit.FromStatus,
                ToStatus = audit.ToStatus,

                PreviousVersion = audit.PreviousVersion,
                NewVersion = audit.NewVersion,

                PolicyVersion = audit.PolicyVersion,

                Reason = audit.Reason,

                TimestampUtc = audit.TimestampUtc
            };
        }
    }
}