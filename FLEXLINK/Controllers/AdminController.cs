using FLEXLINK.Models;
using FLEXLINK.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization; 

namespace FLEXLINK.Controllers
{
    [Authorize(Roles = "Admin")] 
    public class AdminController : Controller
    {
        // This is your "Landing Page"
        public IActionResult Index()
        {
            return View();
        }

        // Example: A page to see all registered users
        public IActionResult ManageUsers()
        {
            // Logic to get users would go here
            return View();
        }
    }
}