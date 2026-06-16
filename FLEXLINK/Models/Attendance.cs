namespace FLEXLINK.Models
{
    public class Attendance
    {
        public int Id { get; set; }

        // Null for guest check-ins
        public string? UserId { get; set; }

        // Display name (member's full name or "Guest")
        public string Name { get; set; } = string.Empty;

        // "Member" or "Guest"
        public string Type { get; set; } = "Guest";

        public DateTime CheckedInAt { get; set; } = DateTime.Now;
    }
}
