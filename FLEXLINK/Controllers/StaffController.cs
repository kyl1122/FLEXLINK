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

        // ── Space Capacity Page ───────────────────────────────────────────────
        public async Task<IActionResult> SpaceCapacity()
        {
            int currentCount = await GetTodayAttendanceCountAsync();
            ViewBag.CurrentCount = currentCount;
            ViewBag.MaxCapacity = MaxCapacity;
            ViewBag.IsFull = currentCount >= MaxCapacity;

            // Full list of pending requests loaded here
            var pendingRequests = await _db.RegistrationRequest
                .Where(r => r.Status == "Pending")
                .OrderBy(r => r.RequestedAt)
                .ToListAsync();

            ViewBag.PendingRequests = pendingRequests;
            ViewBag.PendingCount = pendingRequests.Count;

            return View();
        }

        // ── Landing Page (Staffers Dashboard) ─────────────────────────────────
        public async Task<IActionResult> Index()
        {
            int currentCount = await GetTodayAttendanceCountAsync();
            ViewBag.CurrentCount = currentCount;
            ViewBag.MaxCapacity = MaxCapacity;
            ViewBag.IsFull = currentCount >= MaxCapacity;

            // Only retrieve the count for the dashboard indicator
            int pendingCount = await _db.RegistrationRequest
                .CountAsync(r => r.Status == "Pending");
            ViewBag.PendingCount = pendingCount;

            return View(new LoginViewModel());
        }

        // ── Approve Registration ──────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUser(int requestId)
        {
            var request = await _db.RegistrationRequest.FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null)
            {
                TempData["AttendanceError"] = "Registration request not found.";
                return RedirectToAction("SpaceCapacity");
            }

            request.Status = "Approved";
            request.ReviewedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["AttendanceSuccess"] = $"{request.FullName}'s account has been approved. They can now log in.";
            return RedirectToAction("SpaceCapacity");
        }

        // ── Reject Registration ───────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectUser(int requestId)
        {
            var request = await _db.RegistrationRequest.FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null)
            {
                TempData["AttendanceError"] = "Registration request not found.";
                return RedirectToAction("SpaceCapacity");
            }

            request.Status = "Rejected";
            request.ReviewedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["AttendanceError"] = $"{request.FullName}'s registration has been rejected.";
            return RedirectToAction("SpaceCapacity");
        }

        // ── Member Check-In (Email Only) ──────────────────────────────────────
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

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                TempData["AttendanceError"] = "Please enter a valid email address.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                TempData["AttendanceError"] = "No account found with that email.";
                return RedirectToAction("Index");
            }

            string memberName = user.FullName ?? user.Email ?? "Member";
            string profilePic = !string.IsNullOrEmpty(user.ProfilePicture) ? user.ProfilePicture : "/uploads/default-avatar.png";

            // Set common member information for the status card
            TempData["MemberCheckInName"] = memberName;
            TempData["MemberCheckInEmail"] = user.Email;
            TempData["MemberCheckInPic"] = profilePic;

            // Check active membership BEFORE allowing check-in
            var activeMembership = await _db.UserMembership
                .Where(m => m.UserId == user.Id && m.ExpiryDate >= DateTime.Now)
                .OrderByDescending(m => m.ExpiryDate)
                .FirstOrDefaultAsync();

            if (activeMembership == null)
            {
                TempData["MemberCheckInStatus"] = "Inactive";
                TempData["AttendanceError"] = $"{memberName} cannot check in — no active membership plan.";
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

            TempData["MemberCheckInStatus"] = "Active";
            TempData["MemberExpiryDate"] = activeMembership.ExpiryDate.ToString("MMMM dd, yyyy");
            TempData["AttendanceSuccess"] = $"{memberName} checked in successfully.";

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



        // ── Member Check-Out ──────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOutMember(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["AttendanceError"] = "Please enter an email to check out.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                TempData["AttendanceError"] = "No account found with that email.";
                return RedirectToAction("Index");
            }

            var today = DateTime.Today;
            var activeAttendance = await _db.Attendance
                .Where(a => a.UserId == user.Id && a.CheckedInAt.Date == today && a.CheckedOutAt == null)
                .OrderByDescending(a => a.CheckedInAt)
                .FirstOrDefaultAsync();

            if (activeAttendance == null)
            {
                TempData["AttendanceError"] = $"{user.FullName ?? user.Email} is not currently checked in.";
                return RedirectToAction("Index");
            }

            activeAttendance.CheckedOutAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["AttendanceSuccess"] = $"{user.FullName ?? user.Email} signed out successfully.";
            return RedirectToAction("Index");
        }

        // ── Guest Check-Out ───────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOutGuest()
        {
            var today = DateTime.Today;
            var activeGuest = await _db.Attendance
                .Where(a => a.Type == "Guest" && a.CheckedInAt.Date == today && a.CheckedOutAt == null)
                .OrderBy(a => a.CheckedInAt)
                .FirstOrDefaultAsync();

            if (activeGuest == null)
            {
                TempData["AttendanceError"] = "No active guests currently checked in.";
                return RedirectToAction("Index");
            }

            activeGuest.CheckedOutAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["AttendanceSuccess"] = "Guest signed out successfully.";
            return RedirectToAction("Index");
        }

        // ── Updated Capacity Helper ──────────────────────────────────────────
        private async Task<int> GetTodayAttendanceCountAsync()
        {
            var today = DateTime.Today;
            return await _db.Attendance
                .Where(a => a.CheckedInAt.Date == today && a.CheckedOutAt == null)
                .CountAsync();
        }


    }
}