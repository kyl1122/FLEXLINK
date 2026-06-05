namespace FLEXLINK.Models
{
    public class TrainerSchedule
    {
        public int Id { get; set; }

        // Which trainer owns this schedule slot
        public string UserId { get; set; }

        // Trainer's name (for display on users' Schedule page)
        public string TrainerName { get; set; }

        // The date of the session
        public DateTime ScheduleDate { get; set; }

        // Start and end time
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // Optional note e.g. "Strength Training Session"
        public string? Notes { get; set; }

        // When this record was created
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ── Booking fields ────────────────────────────────────────────────────
        // Whether a user has already booked this slot
        public bool IsBooked { get; set; } = false;

        // The Identity UserId of the user who booked (null = not booked yet)
        public string? BookedByUserId { get; set; }

        // Display name of the user who booked
        public string? BookedByName { get; set; }

        // When the booking was made
        public DateTime? BookedAt { get; set; }
    }
}
