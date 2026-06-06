using Microsoft.AspNetCore.Identity;

namespace FLEXLINK.Models
{
    public class Users : IdentityUser
    {
        public string FullName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
