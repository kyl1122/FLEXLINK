using Microsoft.AspNetCore.Identity;

namespace FLEXLINK.Models
{
    public class Users : IdentityUser
    {
        public string? ProfilePictureUrl { get; set; }
        public string FullName { get; set; }
    }
}
