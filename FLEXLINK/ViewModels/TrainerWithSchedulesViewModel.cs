using FLEXLINK.Models;

namespace FLEXLINK.ViewModels
{
    /// <summary>
    /// Carries a trainer's profile together with their available (unbooked) schedule slots
    /// for display on the public Trainer landing page.
    /// </summary>
    public class TrainerWithSchedulesViewModel
    {
        public ProfileTrainer Trainer { get; set; } = null!;

        /// <summary>All future, unbooked slots belonging to this trainer.</summary>
        public List<TrainerSchedule> AvailableSchedules { get; set; } = new();
    }
}
