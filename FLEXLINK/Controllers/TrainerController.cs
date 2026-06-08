using FLEXLINK.Data;
using FLEXLINK.Models;
using FLEXLINK.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        public async Task<IActionResult> Index()
        {
            // Load all equipment with their repair notes
            var equipment = await _db.Equipment
                .Include(e => e.RepairNotes)
                .OrderBy(e => e.Name)
                .ToListAsync();

            ViewBag.Equipment = equipment;
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

        // ─── SCHEDULE ────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> MySchedule()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            // Load only this trainer's schedule slots, newest first
            var schedules = _db.TrainerSchedule
                               .Where(s => s.UserId == currentUser.Id)
                               .OrderBy(s => s.ScheduleDate)
                               .ThenBy(s => s.StartTime)
                               .ToList();

            return View(schedules);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSchedule(DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime, int durationMinutes, string? notes)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            // Basic validation
            if (scheduleDate.Date < DateTime.Today)
            {
                TempData["ScheduleError"] = "Schedule date cannot be in the past.";
                return RedirectToAction("MySchedule");
            }

            // Enforce duration rules: minimum 60 min, multiples of 30 only
            var allowedDurations = new[] { 60, 90, 120, 150, 180, 210, 240 };
            if (!allowedDurations.Contains(durationMinutes))
            {
                TempData["ScheduleError"] = "Invalid duration. Please select a valid duration (1 hour minimum, in 30-minute increments).";
                return RedirectToAction("MySchedule");
            }

            // Calculate end time from start + duration
            endTime = startTime.Add(TimeSpan.FromMinutes(durationMinutes));

            // Duration must be between 1 hour and 1 hour 30 minutes
            var duration = endTime - startTime;
            if (duration < TimeSpan.FromHours(1))
            {
                TempData["ScheduleError"] = "Session duration must be at least 1 hour.";
                return RedirectToAction("MySchedule");
            }
            if (duration > TimeSpan.FromMinutes(90))
            {
                TempData["ScheduleError"] = "Session duration cannot exceed 1 hour and 30 minutes.";
                return RedirectToAction("MySchedule");
            }

            // Check if this trainer already has a slot that overlaps with the new one
            var conflict = _db.TrainerSchedule
                .FirstOrDefault(s => s.UserId == currentUser.Id
                                  && s.ScheduleDate.Date == scheduleDate.Date
                                  && s.StartTime < endTime
                                  && s.EndTime > startTime);

            if (conflict != null)
            {
                TempData["ScheduleError"] =
                    $"You already have a schedule on {scheduleDate:MMMM dd, yyyy} " +
                    $"from {DateTime.Today.Add(conflict.StartTime):hh:mm tt} to {DateTime.Today.Add(conflict.EndTime):hh:mm tt} " +
                    $"that conflicts with the new slot. Please choose a different time.";
                return RedirectToAction("MySchedule");
            }

            // Get the trainer's name for display on the user's schedule page
            var profile = _db.ProfileTrainer.FirstOrDefault(p => p.UserId == currentUser.Id);
            string trainerName = profile?.FullName ?? currentUser.Email ?? "Trainer";

            var schedule = new TrainerSchedule
            {
                UserId = currentUser.Id,
                TrainerName = trainerName,
                ScheduleDate = scheduleDate.Date,
                StartTime = startTime,
                EndTime = endTime,
                Notes = notes,
                CreatedAt = DateTime.Now
            };

            _db.TrainerSchedule.Add(schedule);
            await _db.SaveChangesAsync();

            TempData["ScheduleSuccess"] = "Schedule added successfully!";
            return RedirectToAction("MySchedule");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var schedule = _db.TrainerSchedule
                              .FirstOrDefault(s => s.Id == id && s.UserId == currentUser.Id);

            if (schedule != null)
            {
                _db.TrainerSchedule.Remove(schedule);
                await _db.SaveChangesAsync();
                TempData["ScheduleSuccess"] = "Schedule removed.";
            }

            return RedirectToAction("MySchedule");
        }

        // ─── EQUIPMENT REPAIR NOTES ───────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRepairNote(int equipmentId, string note)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(note))
            {
                TempData["EquipmentError"] = "Repair note cannot be empty.";
                return RedirectToAction("Index");
            }

            var equipment = await _db.Equipment.FindAsync(equipmentId);
            if (equipment == null)
            {
                TempData["EquipmentError"] = "Equipment not found.";
                return RedirectToAction("Index");
            }

            var profile = _db.ProfileTrainer.FirstOrDefault(p => p.UserId == currentUser.Id);
            string trainerName = profile?.FullName ?? currentUser.Email ?? "Trainer";

            _db.EquipmentRepairNote.Add(new EquipmentRepairNote
            {
                EquipmentId = equipmentId,
                TrainerId = currentUser.Id,
                TrainerName = trainerName,
                Note = note.Trim(),
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            TempData["EquipmentSuccess"] = $"Repair note added for '{equipment.Name}'.";
            return RedirectToAction("Index");
        }

        public IActionResult MyClients() => View();
        public IActionResult WorkoutPlans() => View();
    }
}


