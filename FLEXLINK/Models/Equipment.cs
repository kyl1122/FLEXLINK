namespace FLEXLINK.Models
{
    public class Equipment
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<EquipmentRepairNote> RepairNotes { get; set; } = new List<EquipmentRepairNote>();
    }
}
