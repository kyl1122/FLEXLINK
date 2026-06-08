using FLEXLINK.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FLEXLINK.Data
{
    public class AppDbContext : IdentityDbContext<Users>
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }

        protected AppDbContext()
        {
        }

        public DbSet<ProfileTrainer> ProfileTrainer { get; set; }
        public DbSet<TrainerSchedule> TrainerSchedule { get; set; }
        public DbSet<Equipment> Equipment { get; set; }
        public DbSet<EquipmentRepairNote> EquipmentRepairNote { get; set; }
    }
}
