namespace FLEXLINK.Models
{
    public class EquipmentRepairNote
    {
        public int Id { get; set; }
        public int EquipmentId { get; set; }
        public string TrainerId { get; set; } = string.Empty;
        public string TrainerName { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public Equipment? Equipment { get; set; }
    }
}
