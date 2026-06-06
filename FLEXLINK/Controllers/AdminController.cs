using FLEXLINK.Data;
using FLEXLINK.Models;
using FLEXLINK.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FLEXLINK.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _db;

        public AdminController(UserManager<Users> userManager, AppDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        // Landing page — shows all users and trainers grouped
        public async Task<IActionResult> Index()
        {
            var allUsers = _userManager.Users.ToList();

            var users = new List<Users>();
            var trainers = new List<Users>();

            foreach (var u in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                if (roles.Contains("Admin")) continue; // Don't show admins
                if (roles.Contains("Trainer"))
                    trainers.Add(u);
                else
                    users.Add(u);
            }

            ViewBag.Users = users;
            ViewBag.Trainers = trainers;

            return View();
        }

        // DELETE a user or trainer account
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["AdminError"] = "User not found.";
                return RedirectToAction("Index");
            }

            // Also remove their ProfileTrainer row and schedules if they are a trainer
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Trainer"))
            {
                var profile = _db.ProfileTrainer.FirstOrDefault(p => p.UserId == userId);
                if (profile != null) _db.ProfileTrainer.Remove(profile);

                var schedules = _db.TrainerSchedule.Where(s => s.UserId == userId).ToList();
                _db.TrainerSchedule.RemoveRange(schedules);

                await _db.SaveChangesAsync();
            }

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
                TempData["AdminSuccess"] = $"Account '{user.Email}' has been deleted.";
            else
                TempData["AdminError"] = "Failed to delete account: " +
                    string.Join(", ", result.Errors.Select(e => e.Description));

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult CreateTrainer() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTrainer(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new Users
                {
                    FullName = model.Name,
                    UserName = model.Email,
                    NormalizedUserName = model.Email.ToUpper(),
                    Email = model.Email,
                    NormalizedEmail = model.Email.ToUpper()
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Trainer");
                    TempData["AdminSuccess"] = $"Trainer account '{model.Email}' created successfully.";
                    return RedirectToAction("Index");
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }
    }
}
