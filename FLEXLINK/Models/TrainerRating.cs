namespace FLEXLINK.Models
{
    public class TrainerRating
    {
        public int Id { get; set; }

        // The trainer being rated (ProfileTrainer.Id)
        public int TrainerId { get; set; }

        // The user who gave the rating
        public string UserId { get; set; }

        // Rating value 1–5
        public int Stars { get; set; }

        public DateTime RatedAt { get; set; } = DateTime.Now;
    }
}
