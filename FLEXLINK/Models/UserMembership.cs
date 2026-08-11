namespace FLEXLINK.Models
{
    public class UserMembership
    {
        public int Id { get; set; }

        public string UserId { get; set; }

        // 1, 2, or 3 months
        public int Months { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Now;

        // Calculated: StartDate + Months
        public DateTime ExpiryDate { get; set; }

        // Whether this plan is still active
        public bool IsActive => DateTime.Now <= ExpiryDate;
    }
}
