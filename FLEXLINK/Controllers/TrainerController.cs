using FLEXLINK.Data;
using FLEXLINK.Models;
using FLEXLINK.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FLEXLINK.Controllers
{
    [Authorize(Roles = "Trainer")] // Only users with the Trainer role can enter here
    public class TrainerController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<Users> _userManager;

        // CONSTRUCTOR
        public TrainerController(AppDbContext db, UserManager<Users> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // The Trainer's Landing Page (Dashboard)
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            // Get the currently logged-in trainer's Identity user ID
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Find this trainer's profile row, scoped to their UserId
            var profile = _db.ProfileTrainer
                             .FirstOrDefault(p => p.UserId == currentUser.Id);

            ProfileViewModel vm;

            if (profile == null)
            {
                // Auto-create a profile row for this trainer if none exists yet
                vm = new ProfileViewModel
                {
                    FullName = currentUser.FullName ?? string.Empty,
                    Email = currentUser.Email ?? string.Empty,
                    Address = string.Empty,
                    ExistingProfilePicture = "/uploads/DefaultProfile.png"
                };
            }
            else
            {
                vm = new ProfileViewModel
                {
                    FullName = profile.FullName,
                    Email = profile.Email,
                    PhoneNumber = profile.PhoneNumber,
                    Address = profile.Address,
                    Expertise = profile.Expertise,
                    ExistingProfilePicture = profile.ProfilePicture ?? "/uploads/DefaultProfile.png"
                };
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(ProfileViewModel vm)
        {
            // Get the currently logged-in trainer
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // FILE VALIDATION (runs before ModelState check so errors show up properly)
            if (vm.ProfileImage != null)
            {
                // FILE SIZE VALIDATION (2MB max)
                if (vm.ProfileImage.Length > 2 * 1024 * 1024)
                {
                    ModelState.AddModelError("ProfileImage", "File size must not exceed 2MB.");
                }

                // FILE TYPE VALIDATION
                string[] allowedExtensions = { ".jpg", ".jpeg", ".png" };
                string extension = Path.GetExtension(vm.ProfileImage.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("ProfileImage", "Only JPG, JPEG, and PNG files are allowed.");
                }
            }

            if (!ModelState.IsValid)
            {
                // Re-populate the existing picture so it still renders on the form
                var existingProfile = _db.ProfileTrainer
                                         .FirstOrDefault(p => p.UserId == currentUser.Id);
                vm.ExistingProfilePicture = existingProfile?.ProfilePicture
                                            ?? "/uploads/DefaultProfile.png";
                return View(vm);
            }

            // Find or create the profile row for this trainer
            var profile = _db.ProfileTrainer
                             .FirstOrDefault(p => p.UserId == currentUser.Id);

            if (profile == null)
            {
                profile = new ProfileTrainer
                {
                    UserId = currentUser.Id,
                    Email = currentUser.Email ?? string.Empty,
                    ProfilePicture = "/uploads/DefaultProfile.png"
                };
                _db.ProfileTrainer.Add(profile);
            }

            // HANDLE IMAGE UPLOAD
            if (vm.ProfileImage != null)
            {
                // CREATE FOLDER if it doesn't exist
                string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // GENERATE UNIQUE FILE NAME
                string fileName = Guid.NewGuid().ToString()
                                  + Path.GetExtension(vm.ProfileImage.FileName);
                string filePath = Path.Combine(folder, fileName);

                // SAVE IMAGE TO DISK
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await vm.ProfileImage.CopyToAsync(stream);
                }

                // SAVE PATH TO DATABASE
                profile.ProfilePicture = "/uploads/" + fileName;
            }

            // UPDATE OTHER INFORMATION
            profile.FullName = vm.FullName;
            profile.PhoneNumber = vm.PhoneNumber;
            profile.Address = vm.Address;
            profile.Expertise = vm.Expertise;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Profile updated successfully!";

            // FIX: Redirect to the correct action name
            return RedirectToAction("EditProfile");
        }

        public IActionResult MySchedule()
        {
            return View();
        }

        public IActionResult MyClients()
        {
            return View();
        }

        public IActionResult WorkoutPlans()
        {
            return View();
        }
    }
}
