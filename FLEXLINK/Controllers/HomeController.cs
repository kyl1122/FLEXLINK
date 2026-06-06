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

        public HomeController(AppDbContext db, UserManager<Users> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
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

        // ── Trainers page ─────────────────────────────────────────────────────
        // Shows every trainer who has filled in their profile, together with
        // their upcoming available (unbooked) schedule slots.
        public IActionResult Trainer()
        {
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

            // Combine into view-models
            var viewModel = trainers.Select(t => new TrainerWithSchedulesViewModel
            {
                Trainer = t,
                AvailableSchedules = availableSchedules
                                        .Where(s => s.UserId == t.UserId)
                                        .ToList()
            }).ToList();

            return View(viewModel);
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

        public IActionResult Membership()
        {
            return View();
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

            return View(schedules);
        }
    }
}
