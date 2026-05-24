using Microsoft.AspNetCore.Identity;

namespace FLEXLINK.Models
{
    public class Users : IdentityUser
    {
        public string FullName { get; set; }
    }
}
