using FLEXLINK.Models;
using FLEXLINK.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace FLEXLINK.Controllers
{
    [Authorize(Roles = "Trainer")] // Only users with the Trainer role can enter here
    public class TrainerController : Controller
    {
        // The Trainer's Landing Page (Dashboard)
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult MySchedule()
        {
            return View();
        }

        // Example: A page where trainers can see their assigned clients/users
        public IActionResult MyClients()
        {
            // Logic to fetch clients specific to this trainer would go here
            return View();
        }

        // Example: A page for trainers to upload workout plans
        public IActionResult WorkoutPlans()
        {
            return View();
        }
    }
}