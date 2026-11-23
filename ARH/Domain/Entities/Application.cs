namespace ARH.Domain.Entities
{

    public class Application
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public Guid SystemOwnerId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; }

        // Navigation properties
        public User? SystemOwner { get; set; }

        public ICollection<AccessRequest> AccessRequests { get; set; }
            = new List<AccessRequest>();
    }
}