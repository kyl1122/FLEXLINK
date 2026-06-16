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

        // Landing page — shows Login / Login as Guest + current space capacity
        public async Task<IActionResult> Index()
        {
            int currentCount = await GetTodayAttendanceCountAsync();
            ViewBag.CurrentCount = currentCount;
            ViewBag.MaxCapacity = MaxCapacity;
            ViewBag.IsFull = currentCount >= MaxCapacity;

            return View(new LoginViewModel());
        }

        // Member check-in — validates the account's credentials, then logs attendance
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

            _db.Attendance.Add(new Attendance
            {
                UserId = user.Id,
                Name = user.FullName ?? user.Email ?? "Member",
                Type = "Member",
                CheckedInAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            TempData["AttendanceSuccess"] = $"{(user.FullName ?? user.Email)} has been checked in successfully.";
            return RedirectToAction("Index");
        }

        // Guest check-in — no account required
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
