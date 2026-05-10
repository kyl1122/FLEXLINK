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

        private readonly UserManager<Users> userManager;

        public AdminController(UserManager<Users> userManager)
        {
            this.userManager = userManager;
        }


        // The "Landing Page"
        public IActionResult Index()
        {
            return View();
        }

        //A page to see all registered users
        public IActionResult ManageUsers()
        {
            // Logic to get users would go here
            return View();
        }

        // This shows the form to the Admin
        [HttpGet]
        public IActionResult CreateTrainer()
        {
            return View();
        }

        // This processes the form
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTrainer(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // We set both UserName and Email to model.Email
                var user = new Users
                {
                    FullName = model.Name,
                    UserName = model.Email,
                    NormalizedUserName = model.Email.ToUpper(),
                    Email = model.Email,
                    NormalizedEmail = model.Email.ToUpper()
                };

                // Create the user with the password provided in the form
                var result = await userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Give them the Trainer role
                    await userManager.AddToRoleAsync(user, "Trainer");

                    // Send the admin back to the dashboard with a success message
                    return RedirectToAction("Index", "Admin");
                }

                // show the errors
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View(model);
        }
    }
}