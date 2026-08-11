using FLEXLINK.Data;
using FLEXLINK.Models;
using FLEXLINK.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FLEXLINK.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _db;

        private const int MaxCapacity = 50;

        public StaffController(UserManager<Users> userManager, AppDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        // ── Landing Page ──────────────────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            int currentCount = await GetTodayAttendanceCountAsync();
            ViewBag.CurrentCount = currentCount;
            ViewBag.MaxCapacity = MaxCapacity;
            ViewBag.IsFull = currentCount >= MaxCapacity;

            // Load pending registration requests for the notification panel
            var pendingRequests = _db.RegistrationRequest
                .Where(r => r.Status == "Pending")
                .OrderBy(r => r.RequestedAt)
                .ToList();
            ViewBag.PendingRequests = pendingRequests;
            ViewBag.PendingCount = pendingRequests.Count;

            return View(new LoginViewModel());
        }

        // ── Approve Registration ──────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUser(int requestId)
        {
            var request = _db.RegistrationRequest.FirstOrDefault(r => r.Id == requestId);
            if (request == null)
            {
                TempData["AttendanceError"] = "Registration request not found.";
                return RedirectToAction("Index");
            }

            request.Status = "Approved";
            request.ReviewedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["AttendanceSuccess"] = $"{request.FullName}'s account has been approved. They can now log in.";
            return RedirectToAction("Index");
        }

        // ── Reject Registration ───────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectUser(int requestId)
        {
            var request = _db.RegistrationRequest.FirstOrDefault(r => r.Id == requestId);
            if (request == null)
            {
                TempData["AttendanceError"] = "Registration request not found.";
                return RedirectToAction("Index");
            }

            request.Status = "Rejected";
            request.ReviewedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["AttendanceError"] = $"{request.FullName}'s registration has been rejected.";
            return RedirectToAction("Index");
        }

        // ── Member Check-In ───────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MemberLogin(LoginViewModel model)
        {
            int currentCount = await GetTodayAttendanceCountAsync();
            if (currentCount >= MaxCapacity)
            {
                TempData["AttendanceError"] = "Gym is at full capacity (50/50). Cannot check in any more members or guests right now.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
            {
                TempData["AttendanceError"] = "Please enter a valid email and password.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                TempData["AttendanceError"] = "No account found with that email.";
                return RedirectToAction("Index");
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordValid)
            {
                TempData["AttendanceError"] = "Incorrect password.";
                return RedirectToAction("Index");
            }

            string memberName = user.FullName ?? user.Email ?? "Member";

            // Check membership BEFORE allowing check-in
            var activeMembership = _db.UserMembership
                .Where(m => m.UserId == user.Id && m.ExpiryDate >= DateTime.Now)
                .OrderByDescending(m => m.ExpiryDate)
                .FirstOrDefault();

            if (activeMembership == null)
            {
                // No membership — block check-in, do NOT add to attendance
                TempData["AttendanceError"] = $"{memberName} cannot check in — no active membership plan.";
                TempData["MembershipWarning"] = $"⚠️ No active membership. {memberName} does not have a current membership plan.";
                return RedirectToAction("Index");
            }

            // Has active membership — allow check-in
            _db.Attendance.Add(new Attendance
            {
                UserId = user.Id,
                Name = memberName,
                Type = "Member",
                CheckedInAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            TempData["AttendanceSuccess"] = $"{memberName} checked in successfully.";
            TempData["MembershipStatus"] = $"✅ Active membership — valid until {activeMembership.ExpiryDate:MMMM dd, yyyy}.";
            return RedirectToAction("Index");
        }

        // ── Guest Check-In ────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuestLogin()
        {
            int currentCount = await GetTodayAttendanceCountAsync();
            if (currentCount >= MaxCapacity)
            {
                TempData["AttendanceError"] = "Gym is at full capacity (50/50). Cannot check in any more members or guests right now.";
                return RedirectToAction("Index");
            }

            _db.Attendance.Add(new Attendance
            {
                UserId = null,
                Name = "Guest",
                Type = "Guest",
                CheckedInAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            TempData["AttendanceSuccess"] = "Guest checked in successfully.";
            return RedirectToAction("Index");
        }

        private async Task<int> GetTodayAttendanceCountAsync()
        {
            var today = DateTime.Today;
            return await _db.Attendance
                .Where(a => a.CheckedInAt.Date == today)
                .CountAsync();
        }
    }
}
