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
            // 1. Attendance & Space Capacity calculations
            int currentCount = await GetTodayAttendanceCountAsync();
            ViewBag.CurrentCount = currentCount;
            ViewBag.MaxCapacity = MaxCapacity;
            ViewBag.IsFull = currentCount >= MaxCapacity;

            // 2. Fetch pending registration requests sorted by RequestedAt
            var pendingRequests = await _db.RegistrationRequest
                .Where(r => r.Status == "Pending")
                .OrderBy(r => r.RequestedAt)
                .ToListAsync();

            var pendingPayments = await _db.UserMembership
            .Where(p => p.Status == "Pending")
            .OrderBy(p => p.StartDate)
            .ToListAsync();

            var userIds = pendingPayments.Select(p => p.UserId).Distinct().ToList();
            var users = await _db.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u);

            // 3. Keep existing ViewBag items
            ViewBag.PendingRequests = pendingRequests;
            ViewBag.PendingCount = pendingRequests.Count;
            ViewBag.PendingPaymentsCount = pendingPayments.Count;
            ViewBag.PendingPayments = pendingPayments;
            ViewBag.PaymentUsers = users;

            // 4. Build the ViewModel with both PendingRegistrations and PendingPayments
            var viewModel = new StaffApprovalViewModel
            {
                PendingRegistrations = pendingRequests,
                PendingPayments = pendingPayments
            };

            return View(viewModel);
        }


        // ── Landing Page (Staffers Dashboard) ─────────────────────────────────
        public async Task<IActionResult> Index(string? checkedInUserId)
        {
            int currentCount = await GetTodayAttendanceCountAsync();
            ViewBag.CurrentCount = currentCount;
            ViewBag.MaxCapacity = MaxCapacity;
            ViewBag.IsFull = currentCount >= MaxCapacity;

            // Only retrieve the count for the dashboard indicator
            int pendingCount = await _db.RegistrationRequest
                .CountAsync(r => r.Status == "Pending");

            int pendingPaymentsCount = await _db.UserMembership
            .Where(p => p.Status == "Pending") 
            .CountAsync();
            ViewBag.PendingCount = pendingCount;
            ViewBag.PendingPaymentsCount = pendingPaymentsCount;
            ViewBag.CheckedInUserId = checkedInUserId;

            return View(new LoginViewModel());


        }

        // — Approve Registration —
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

        // — Approve Membership Payment —
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePayment(int paymentId)
        {
            var payment = await _db.UserMembership.FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null)
            {
                TempData["AttendanceError"] = "Membership payment not found.";
                return RedirectToAction("SpaceCapacity");
            }
            payment.Status = "Approved";
            payment.ReviewedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["AttendanceSuccess"] = "Membership payment approved.";
            return RedirectToAction("SpaceCapacity");
        }

        // — Reject / Delete Membership Payment —
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayment(int paymentId)
        {
            var payment = await _db.UserMembership.FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null)
            {
                TempData["AttendanceError"] = "Membership payment not found.";
                return RedirectToAction("SpaceCapacity");
            }

            _db.UserMembership.Remove(payment); 
            await _db.SaveChangesAsync();

            TempData["AttendanceError"] = "Membership payment rejected and removed.";
            return RedirectToAction("SpaceCapacity");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectUser(int requestId)
        {
            var request = await _db.RegistrationRequest.FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null)
            {
                TempData["AttendanceError"] = "Registration request not found or already processed.";
                return RedirectToAction("SpaceCapacity");
            }

            var user = await _userManager.FindByIdAsync(request.UserId);

            try
            {
                _db.RegistrationRequest.Remove(request);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Row was already deleted (e.g., double submit) — safe to ignore
                TempData["AttendanceError"] = "This registration was already processed.";
                return RedirectToAction("SpaceCapacity");
            }

            if (user != null)
            {
                if (!string.IsNullOrEmpty(user.ProfilePicture))
                {
                    var picPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfilePicture.TrimStart('/'));
                    if (System.IO.File.Exists(picPath))
                        System.IO.File.Delete(picPath);
                }

                await _userManager.DeleteAsync(user);
            }

            TempData["AttendanceError"] = $"{request.FullName}'s registration has been rejected and removed.";
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

            // ── Block Staff/Admin accounts from Member Check-In ──
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Staff") || roles.Contains("Admin"))
            {
                TempData["AttendanceError"] = "Staff and Admin accounts cannot be checked in as members.";
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
                .Where(m => m.UserId == user.Id && m.ExpiryDate >= DateTime.Now && m.Status == "Approved")
                .OrderByDescending(m => m.ExpiryDate)
                .FirstOrDefaultAsync();

            if (activeMembership == null)
            {
                TempData["MemberCheckInStatus"] = "Inactive";
                TempData["AttendanceError"] = $"{memberName} cannot check in — no active membership plan.";
                return RedirectToAction("Index", new { checkedInUserId = user.Id });
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

            return RedirectToAction("Index", new { checkedInUserId = user.Id });
        }

        // ── Quick Add Membership (Staff-Initiated) ─────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMembershipQuick(string userId, int months)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["AttendanceError"] = $"User not found. (Received userId: '{userId}')";
                return RedirectToAction("Index");
            }

            var validMonths = new[] { 1, 2, 3 };
            if (!validMonths.Contains(months))
            {
                TempData["AttendanceError"] = "Invalid plan selected.";
                return RedirectToAction("Index");
            }

            var start = DateTime.Now;
            var membership = new UserMembership
            {
                UserId = userId,
                Months = months,
                StartDate = start,
                ExpiryDate = start.AddMonths(months),
                Status = "Approved",
                ReviewedAt = DateTime.Now
            };

            _db.UserMembership.Add(membership);
            await _db.SaveChangesAsync();

            TempData["AttendanceSuccess"] = $"{user.FullName ?? user.Email} has been given a {months}-month membership.";
            return RedirectToAction("Index", new { checkedInUserId = user.Id });
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