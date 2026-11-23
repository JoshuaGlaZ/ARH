namespace ARH.Domain.Entities
{

    public class AuditLog
    {
        public Guid Id { get; set; }

        public Guid AccessRequestId { get; set; }

        public Guid ActorId { get; set; }

        public string ActorEmailSnapshot { get; set; }
            = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string? FromStatus { get; set; }

        public string ToStatus { get; set; } = string.Empty;

        public long PreviousVersion { get; set; }

        public long NewVersion { get; set; }

        public string PolicyVersion { get; set; } = "v1";

        public string? Reason { get; set; }

        public DateTime TimestampUtc { get; set; }

        // Navigation properties
        public AccessRequest? AccessRequest { get; set; }

        public User? Actor { get; set; }
    }
}