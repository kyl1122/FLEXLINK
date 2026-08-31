using Azure;
using FLEXLINK.Data;
using FLEXLINK.Models;
using FLEXLINK.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace FLEXLINK.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<Users> _userManager;
        private readonly SignInManager<Users> _signInManager;

        public HomeController(AppDbContext db, UserManager<Users> userManager, SignInManager<Users> signInManager)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    var roles = await _userManager.GetRolesAsync(currentUser);

                    if (roles.Contains("Admin"))
                        return RedirectToAction("Index", "Admin");
                    if (roles.Contains("Trainer"))
                        return RedirectToAction("Index", "Trainer"); // change to your actual Trainer controller/action
                    if (roles.Contains("Staff"))
                        return RedirectToAction("Index", "Staff");
                    // otherwise fall through to regular User landing page

                    // ── Subscription Expiry Alert (fires within 3 days of expiry) ──────
                    var activeMembership = _db.UserMembership
                        .Where(m => m.UserId == currentUser.Id
                                 && m.Status == "Approved"
                                 && m.ExpiryDate >= DateTime.Now)
                        .OrderBy(m => m.ExpiryDate)
                        .FirstOrDefault();

                    if (activeMembership != null)
                    {
                        double daysRemaining = (activeMembership.ExpiryDate - DateTime.Now).TotalDays;
                        if (daysRemaining <= 3)
                        {
                            ViewBag.ShowExpiryAlert = true;
                            ViewBag.ExpiryDate = activeMembership.ExpiryDate;
                            ViewBag.DaysRemaining = Math.Max(0, (int)Math.Ceiling(daysRemaining));
                        }
                    }
                }
            }

            return View();
        }



        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Result()
        {
            return View();
        }

        // ── User Profile ──────────────────────────────────────────────────────
        // Shows the logged-in user's profile with an option to upload a picture.
        public async Task<IActionResult> UserProfile()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            return View(currentUser);
        }

        // ── CHANGE PASSWORD ───────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            return View(new UserChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(UserChangePasswordViewModel vm)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var result = await _userManager.ChangePasswordAsync(currentUser, vm.CurrentPassword, vm.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                return View(vm);
            }

            await _signInManager.RefreshSignInAsync(currentUser);

            TempData["ProfileSuccess"] = "Password changed successfully!";
            return RedirectToAction("UserProfile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserProfile(
            IFormFile? ProfileImage,
            string? FullName,
            string? PhoneNumber,
            string? Address,
            int? Age)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            // ── VALIDATION — all fields required ─────────────────────────────────
            if (string.IsNullOrWhiteSpace(FullName))
            {
                TempData["ProfileError"] = "Full Name is required.";
                return RedirectToAction("UserProfile");
            }

            if (string.IsNullOrWhiteSpace(PhoneNumber))
            {
                TempData["ProfileError"] = "Phone Number is required.";
                return RedirectToAction("UserProfile");
            }

            if (string.IsNullOrWhiteSpace(Address))
            {
                TempData["ProfileError"] = "Address is required.";
                return RedirectToAction("UserProfile");
            }

            // ── AGE VALIDATION ─────────────────────────────────────────────────
            if (Age == null)
            {
                TempData["ProfileError"] = "Age is required.";
                return RedirectToAction("UserProfile");
            }

            if (Age < 18 || Age > 80)
            {
                TempData["ProfileError"] = "Invalid age.";
                return RedirectToAction("UserProfile");
            }

            // ── PHONE NUMBER VALIDATION ───────────────────────────────────────────
            if (!PhoneNumber.Trim().All(char.IsDigit))
            {
                TempData["ProfileError"] = "Phone Number must contain numbers only.";
                return RedirectToAction("UserProfile");
            }

            if (PhoneNumber.Trim().Length != 11)
            {
                TempData["ProfileError"] = "Phone Number must be exactly 11 digits.";
                return RedirectToAction("UserProfile");
            }

            // ── UPDATE TEXT FIELDS ────────────────────────────────────────────────
            currentUser.FullName = FullName.Trim();
            currentUser.PhoneNumber = PhoneNumber.Trim();
            currentUser.Address = Address.Trim();
            currentUser.Age = Age.Value;

            // ── HANDLE PROFILE PICTURE ────────────────────────────────────────────
            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                if (ProfileImage.Length > 2 * 1024 * 1024)
                {
                    TempData["ProfileError"] = "File size must not exceed 2MB.";
                    return RedirectToAction("UserProfile");
                }

                string[] allowedExtensions = { ".jpg", ".jpeg", ".png" };
                string extension = Path.GetExtension(ProfileImage.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    TempData["ProfileError"] = "Only JPG, JPEG, and PNG files are allowed.";
                    return RedirectToAction("UserProfile");
                }

                string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid().ToString() + extension;
                string filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await ProfileImage.CopyToAsync(stream);

                currentUser.ProfilePicture = "/uploads/" + fileName;
            }

            await _userManager.UpdateAsync(currentUser);
            TempData["ProfileSuccess"] = "Profile updated successfully!";
            return RedirectToAction("UserProfile");
        }

        // ── Trainers page ─────────────────────────────────────────────────────
        // Shows every trainer who has filled in their profile, together with
        // their upcoming available (unbooked) schedule slots.
        public async Task<IActionResult> Trainer()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // check if this user has an active, approved membership
            bool hasActiveMembership = false;
            if (currentUser != null)
            {
                hasActiveMembership = _db.UserMembership
                    .Any(m => m.UserId == currentUser.Id
                           && m.ExpiryDate >= DateTime.Now
                           && m.Status == "Approved");
            }
            ViewBag.HasActiveMembership = hasActiveMembership;

            // Only fetch trainers that have filled in at least their name
            var trainers = _db.ProfileTrainer
                              .Where(p => p.FullName != null && p.FullName != "")
                              .ToList();

            // Pull all future, unbooked schedules in one DB call
            var availableSchedules = _db.TrainerSchedule
                                        .Where(s => s.ScheduleDate.Date >= DateTime.Today
                                                    && !s.IsBooked)
                                        .OrderBy(s => s.ScheduleDate)
                                        .ThenBy(s => s.StartTime)
                                        .ToList();

            // Load all ratings once
            var allRatings = _db.TrainerRating.ToList();

            // Load current user's bookings (to know which trainers they can rate)
            List<TrainerSchedule> userBookings = new();
            List<TrainerRating> userRatings = new();
            if (currentUser != null)
            {
                userBookings = _db.TrainerSchedule
                    .Where(s => s.IsBooked && s.BookedByUserId == currentUser.Id)
                    .ToList();
                userRatings = allRatings
                    .Where(r => r.UserId == currentUser.Id)
                    .ToList();
            }

            // Combine into view-models
            var viewModel = trainers.Select(t =>
            {
                var trainerRatings = allRatings.Where(r => r.TrainerId == t.Id).ToList();
                var myRating = currentUser != null
                    ? userRatings.FirstOrDefault(r => r.TrainerId == t.Id)
                    : null;
                bool hasBooked = currentUser != null &&
                    userBookings.Any(s => s.TrainerName == t.FullName || s.UserId == t.UserId);

                return new TrainerWithSchedulesViewModel
                {
                    Trainer = t,
                    AvailableSchedules = availableSchedules
                                            .Where(s => s.UserId == t.UserId)
                                            .ToList(),
                    AverageRating = trainerRatings.Any()
                        ? Math.Round(trainerRatings.Average(r => r.Stars), 1)
                        : 0,
                    RatingCount = trainerRatings.Count,
                    CurrentUserRating = myRating?.Stars,
                    CurrentUserHasBooked = hasBooked
                };
            })
                .Where(vm => vm.AvailableSchedules.Any())
                .ToList();

            return View(viewModel);
        }

        // ── Rate a Trainer ────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateTrainer(int trainerId, int stars, string? source)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["BookingError"] = "You must be logged in to rate a trainer.";
                return source == "Schedule" ? RedirectToAction("Schedule") : RedirectToAction("Trainer");
            }

            if (stars < 1 || stars > 5)
            {
                TempData["BookingError"] = "Invalid rating.";
                return source == "Schedule" ? RedirectToAction("Schedule") : RedirectToAction("Trainer");
            }

            var existing = _db.TrainerRating
                .FirstOrDefault(r => r.TrainerId == trainerId && r.UserId == currentUser.Id);

            if (existing != null)
            {
                existing.Stars = stars;
                existing.RatedAt = DateTime.Now;
            }
            else
            {
                _db.TrainerRating.Add(new TrainerRating
                {
                    TrainerId = trainerId,
                    UserId = currentUser.Id,
                    Stars = stars,
                    RatedAt = DateTime.Now
                });
            }

            await _db.SaveChangesAsync();

            TempData["BookingSuccess"] = "Your rating has been submitted!";
            TempData["RatingSuccess"] = "Your rating has been submitted!";

            return source == "Schedule" ? RedirectToAction("Schedule") : RedirectToAction("Trainer");
        }

        // ── Book Schedules ────────────────────────────────────────────────────
        // Receives a comma-separated list of schedule IDs the user wants to book.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookSchedules(string scheduleIds)
        {
            // Must be logged in
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["BookingError"] = "You must be logged in to book a session.";
                return RedirectToAction("Trainer");
            }

            // must have an active, approved membership
            bool hasActiveMembership = _db.UserMembership
                .Any(m => m.UserId == currentUser.Id
                       && m.ExpiryDate >= DateTime.Now
                       && m.Status == "Approved");

            if (!hasActiveMembership)
            {
                TempData["BookingError"] = "You need an active membership to book a session. Please subscribe first.";
                return RedirectToAction("Trainer");
            }

            if (string.IsNullOrWhiteSpace(scheduleIds))
            {
                TempData["BookingError"] = "Please select at least one schedule slot.";
                return RedirectToAction("Trainer");
            }

            if (string.IsNullOrWhiteSpace(scheduleIds))
            {
                TempData["BookingError"] = "Please select at least one schedule slot.";
                return RedirectToAction("Trainer");
            }

            // Parse the comma-separated IDs
            var ids = scheduleIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => int.TryParse(x.Trim(), out var n) ? n : 0)
                        .Where(n => n > 0)
                        .Distinct()
                        .ToList();

            if (!ids.Any())
            {
                TempData["BookingError"] = "No valid schedule slots were selected.";
                return RedirectToAction("Trainer");
            }

            // Load the user's already-booked slots for conflict checking
            var userBookedSlots = _db.TrainerSchedule
                .Where(s => s.IsBooked && s.BookedByUserId == currentUser.Id)
                .ToList();

            int bookedCount = 0;
            int alreadyTakenCount = 0;
            var conflictMessages = new List<string>();

            foreach (var id in ids)
            {
                var slot = _db.TrainerSchedule.FirstOrDefault(s => s.Id == id);
                if (slot == null) continue;

                // Already booked by someone else
                if (slot.IsBooked)
                {
                    alreadyTakenCount++;
                    continue;
                }

                // Check if this user already has a booking that overlaps with this slot
                var conflict = userBookedSlots.FirstOrDefault(existing =>
                    existing.ScheduleDate.Date == slot.ScheduleDate.Date &&
                    existing.StartTime < slot.EndTime &&
                    existing.EndTime > slot.StartTime);

                if (conflict != null)
                {
                    conflictMessages.Add(
                        $"You already have a booked session on {slot.ScheduleDate:MMMM dd, yyyy} " +
                        $"from {DateTime.Today.Add(conflict.StartTime):hh:mm tt} to {DateTime.Today.Add(conflict.EndTime):hh:mm tt} " +
                        $"with {conflict.TrainerName}. The slot {slot.ScheduleDate:MMM dd} " +
                        $"{DateTime.Today.Add(slot.StartTime):hh:mm tt}–{DateTime.Today.Add(slot.EndTime):hh:mm tt} conflicts with it.");
                    continue;
                }

                slot.IsBooked = true;
                slot.BookedByUserId = currentUser.Id;
                slot.BookedByName = currentUser.FullName ?? currentUser.Email ?? "User";
                slot.BookedAt = DateTime.Now;

                // Add to local list so subsequent slots in same request are also checked
                userBookedSlots.Add(slot);
                bookedCount++;
            }

            await _db.SaveChangesAsync();

            if (conflictMessages.Any())
                TempData["BookingConflict"] = string.Join("|", conflictMessages);

            if (bookedCount > 0 && alreadyTakenCount == 0 && !conflictMessages.Any())
                TempData["BookingSuccess"] = $"Successfully booked {bookedCount} session{(bookedCount > 1 ? "s" : "")}!";
            else if (bookedCount > 0)
                TempData["BookingSuccess"] = $"Booked {bookedCount} session{(bookedCount > 1 ? "s" : "")}.";
            else if (!conflictMessages.Any())
                TempData["BookingError"] = "All selected slots were already booked by someone else.";

            return RedirectToAction("Trainer");
        }

        public async Task<IActionResult> Membership()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            UserMembership? activeMembership = null;
            if (currentUser != null)
            {
                activeMembership = _db.UserMembership
                    .Where(m => m.UserId == currentUser.Id && m.ExpiryDate >= DateTime.Now && m.Status == "Approved")
                    .OrderByDescending(m => m.ExpiryDate)
                    .FirstOrDefault();
            }

            ViewBag.ActiveMembership = activeMembership;
            ViewBag.PendingMembership = _db.UserMembership
            .Where(m => m.UserId == currentUser.Id && m.Status == "Pending")
            .OrderByDescending(m => m.StartDate)
            .FirstOrDefault();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(int months)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var validMonths = new[] { 1, 2, 3 };
            if (!validMonths.Contains(months))
            {
                TempData["MembershipError"] = "Invalid plan selected.";
                return RedirectToAction("Membership");
            }

            // Extend from existing expiry if user already has an active plan
            var existing = _db.UserMembership
                .Where(m => m.UserId == currentUser.Id && m.ExpiryDate >= DateTime.Now)
                .OrderByDescending(m => m.ExpiryDate)
                .FirstOrDefault();

            var startDate = existing != null ? existing.ExpiryDate : DateTime.Now;
            var expiryDate = startDate.AddMonths(months);
            _db.UserMembership.Add(new UserMembership
            {
                UserId = currentUser.Id,
                Months = months,
                StartDate = startDate,
                ExpiryDate = expiryDate,
                Status = "Pending"
            });

            await _db.SaveChangesAsync();

            TempData["MembershipSuccess"] = $"Your {months}-month plan request has been submitted and is awaiting staff approval.";
            return RedirectToAction("Membership");


        }

        public IActionResult Contact()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Users' Schedule page — shows ONLY the sessions booked by the logged-in user
        public async Task<IActionResult> Schedule()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var schedules = _db.TrainerSchedule
                               .Where(s => s.IsBooked
                                        && s.BookedByUserId == currentUser.Id
                                        && s.ScheduleDate.Date >= DateTime.Today)
                               .OrderBy(s => s.ScheduleDate)
                               .ThenBy(s => s.StartTime)
                               .ToList();

            // Pass trainer IDs (by name) so the Schedule view can show rating prompts
            // Map trainer name → ProfileTrainer.Id
            var trainerNames = schedules.Select(s => s.TrainerName).Distinct().ToList();
            var trainerProfiles = _db.ProfileTrainer
                .Where(p => trainerNames.Contains(p.FullName))
                .ToList();

            var userRatings = _db.TrainerRating
                .Where(r => r.UserId == currentUser.Id)
                .ToList();

            var allRatings = _db.TrainerRating.ToList();

            ViewBag.TrainerProfiles = trainerProfiles;
            ViewBag.UserRatings = userRatings;
            ViewBag.AllRatings = allRatings;

            return View(schedules);
        }
    }
}
