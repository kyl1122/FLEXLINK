namespace FLEXLINK.Models
{
    public class ProfileTrainer
    {
        public int Id { get; set; }

        // Foreign key linking to the ASP.NET Identity user
        public string UserId { get; set; }

        public string FullName { get; set; }

        public string Email { get; set; }

        public string? ProfilePicture { get; set; }

        public string PhoneNumber { get; set; }

        public string? Address { get; set; }

        public string? Expertise { get; set; }


    }
}
