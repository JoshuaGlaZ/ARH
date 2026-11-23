using ARH.Domain.Enums;

namespace ARH.Domain.Entities{

    public class User
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public UserRole Role { get; set; }

        public Guid? ManagerId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; }

        // Navigation properties
        public User? Manager { get; set; }

        public ICollection<User> DirectReports { get; set; }
            = new List<User>();
    }
}