namespace FLEXLINK.Models
{
    public class RegistrationRequest
    {
        public int Id { get; set; }

        // The user who just registered and is waiting for approval
        public string UserId { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        // "Pending", "Approved", "Rejected"
        public string Status { get; set; } = "Pending";

        public string? ProfilePicture { get; set; }

        public DateTime RequestedAt { get; set; } = DateTime.Now;

        public DateTime? ReviewedAt { get; set; }
    }
}
