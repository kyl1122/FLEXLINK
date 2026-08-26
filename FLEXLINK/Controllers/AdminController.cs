using FLEXLINK.Data;
using FLEXLINK.Models;
using FLEXLINK.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // Maps a membership's Months to its price. Keep in sync with Subscribe()'s plans.
        private static decimal GetMembershipPrice(int months) => months switch
        {
            0 => 50m,
            1 => 300m,
            2 => 550m,
            3 => 800m,
            _ => 0m
        };

        // Landing page — shows dashboard overview (equipment, capacity, repair notes)
        public async Task<IActionResult> Index()
        {

            // Load all repair notes with equipment info
            var repairNotes = await _db.EquipmentRepairNote
                .Include(r => r.Equipment)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            ViewBag.RepairNotes = repairNotes;

            // Load all equipment with their repair notes
            var equipmentList = await _db.Equipment
                .Include(e => e.RepairNotes)
                .OrderBy(e => e.Name)
                .ToListAsync();
            ViewBag.EquipmentList = equipmentList;

            // Space capacity — counts today's check-ins (members + guests)
            // Only count people still checked in (excludes anyone who has signed out)
            var today = DateTime.Today;
            int currentCount = await _db.Attendance
                .Where(a => a.CheckedInAt.Date == today && a.CheckedOutAt == null)
                .CountAsync();

            ViewBag.CurrentCount = currentCount;
            ViewBag.MaxCapacity = 50;

            // just counts, not full lists — the dashboard only shows totals
            var allUsers = await _userManager.Users.ToListAsync();
            int trainerCount = 0;
            int userCount = 0;
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Trainer")) trainerCount++;
                else if (!roles.Contains("Admin")) userCount++;
            }
            ViewBag.TrainerCount = trainerCount;
            ViewBag.UserCount = userCount;

            // equipment counts only
            ViewBag.EquipmentCount = await _db.Equipment.CountAsync();
            ViewBag.PendingRepairCount = await _db.EquipmentRepairNote.CountAsync();

            // dictionary for resolving UserId -> display name
            var userLookup = allUsers.ToDictionary(u => u.Id, u => u.FullName ?? u.Email ?? "Unknown");

            // income summary (based on approved/paid memberships)
            var approvedMemberships = await _db.UserMembership
                .Where(m => m.Status == "Approved")
                .ToListAsync();

            var now = DateTime.Now;
            ViewBag.CurrentMonthIncome = approvedMemberships
                .Where(m => m.StartDate.Year == now.Year && m.StartDate.Month == now.Month)
                .Sum(m => GetMembershipPrice(m.Months));
            ViewBag.TotalIncome = approvedMemberships.Sum(m => GetMembershipPrice(m.Months));

            // monthly breakdown, shown directly on the dashboard
            ViewBag.MonthlyIncome = approvedMemberships
                .GroupBy(m => new { m.StartDate.Year, m.StartDate.Month })
                .Select(g => new MonthlyIncomeViewModel
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MembershipCount = g.Count(),
                    Total = g.Sum(m => GetMembershipPrice(m.Months)),
                    OneMonthCount = g.Count(m => m.Months == 1),
                    TwoMonthCount = g.Count(m => m.Months == 2),
                    ThreeMonthCount = g.Count(m => m.Months == 3),
                    GuestCount = g.Count(m => m.Months == 0),
                    Details = g.Select(m => new MembershipSaleDetail
                    {
                        UserName = userLookup.TryGetValue(m.UserId, out var name) ? name : "Unknown",
                        Months = m.Months,
                        Price = GetMembershipPrice(m.Months),
                        StartDate = m.StartDate,
                        ExpiryDate = m.ExpiryDate
                    })
                    .OrderBy(d => d.UserName)
                    .ToList()



                })
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToList();

            return View();
        }

        // =========================================================================
        // Page Action for Income Reports
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Income()
        {
            var allUsers = await _userManager.Users.ToListAsync();
            var userLookup = allUsers.ToDictionary(u => u.Id, u => u.FullName ?? u.Email ?? "Unknown");

            var approvedMemberships = await _db.UserMembership
                .Where(m => m.Status == "Approved")
                .ToListAsync();

            var monthlyIncome = approvedMemberships
                .GroupBy(m => new { m.StartDate.Year, m.StartDate.Month })
                .Select(g => new MonthlyIncomeViewModel
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MembershipCount = g.Count(),
                    Total = g.Sum(m => GetMembershipPrice(m.Months)),
                    OneMonthCount = g.Count(m => m.Months == 1),
                    TwoMonthCount = g.Count(m => m.Months == 2),
                    ThreeMonthCount = g.Count(m => m.Months == 3),
                    Details = g.Select(m => new MembershipSaleDetail
                    {
                        UserName = userLookup.TryGetValue(m.UserId, out var name) ? name : "Unknown",
                        Months = m.Months,
                        Price = GetMembershipPrice(m.Months),
                        StartDate = m.StartDate,
                        ExpiryDate = m.ExpiryDate
                    })
                    .OrderBy(d => d.UserName)
                    .ToList()
                })
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToList();

            ViewBag.TotalIncome = monthlyIncome.Sum(x => x.Total);

            return View(monthlyIncome);
        }

        // =========================================================================
        // Page Action for Trainers Navigation
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Trainers()
        {
            var allUsers = await _userManager.Users.ToListAsync();
            var trainers = new List<Users>();

            foreach (var user in allUsers)
            {
                // Filter specifically for users assigned to the Trainer role
                if (await _userManager.IsInRoleAsync(user, "Trainer"))
                {
                    trainers.Add(user);
                }
            }

            // Pull each trainer's actual uploaded picture from ProfileTrainer
            var trainerIds = trainers.Select(t => t.Id).ToList();
            var profilePics = await _db.ProfileTrainer
                .Where(p => trainerIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId, p => p.ProfilePicture);

            foreach (var trainer in trainers)
            {
                if (profilePics.TryGetValue(trainer.Id, out var pic) && !string.IsNullOrEmpty(pic))
                {
                    trainer.ProfilePicture = pic; // override with the real uploaded picture
                }
            }

            // Sort newest first
            trainers = trainers.OrderByDescending(t => t.CreatedAt).ToList();

            // Passes the List<Users> directly as a Strongly-Typed Model to Views/Admin/Trainers.cshtml
            return View(trainers);
        }

        // =========================================================================
        // ADDED: Dedicated Page Action for Users Navigation
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var allUsers = await _userManager.Users.ToListAsync();
            var regularUsers = new List<Users>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                // Exclude Admin and Trainer roles to keep only regular members
                if (!roles.Contains("Admin") && !roles.Contains("Trainer"))
                {
                    regularUsers.Add(user);
                }
            }

            // Sort newest first
            regularUsers = regularUsers.OrderByDescending(u => u.CreatedAt).ToList();

            // Passes the List<Users> directly as a Strongly-Typed Model to Views/Admin/Users.cshtml
            return View(regularUsers);
        }

        // DELETE a user or trainer account (Unchanged - existing logic works for both new pages)
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

            // CHANGED: Redirect back to referring page or default to Index
            string? returnUrl = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }

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
                    Age = model.Age,
                    Address = model.Address,
                    PhoneNumber = model.PhoneNumber,
                    NormalizedUserName = model.Email.ToUpper(),
                    Email = model.Email,
                    NormalizedEmail = model.Email.ToUpper()
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Trainer");
                    TempData["AdminSuccess"] = $"Trainer account '{model.Email}' created successfully.";

                    // CHANGED: Redirects directly to the new Trainers page after creation
                    return RedirectToAction("Trainers");
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        // ─── EQUIPMENT ────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Equipment()
        {
            var equipmentList = await _db.Equipment
                .Include(e => e.RepairNotes)
                .OrderBy(e => e.Name)   // ─── BY NAME ARRANGEMENT ────────────────────────────────────────────────────────────
                .ToListAsync();

            var repairNotes = await _db.EquipmentRepairNote
                .Include(r => r.Equipment)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            ViewBag.RepairNotes = repairNotes;

            return View(equipmentList);
        }

        // ─── ADD EQUIPMENT ────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEquipment(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["AdminError"] = "Equipment name is required.";
                return RedirectToAction("Equipment");
            }

            _db.Equipment.Add(new Equipment
            {
                Name = name.Trim(),
                Description = description?.Trim(),
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            TempData["AdminSuccess"] = $"Equipment '{name.Trim()}' added successfully.";
            return RedirectToAction("Equipment");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEquipment(int equipmentId)
        {
            var equipment = await _db.Equipment.FindAsync(equipmentId);
            if (equipment != null)
            {
                _db.Equipment.Remove(equipment);
                await _db.SaveChangesAsync();
                TempData["AdminSuccess"] = $"Equipment '{equipment.Name}' removed.";
            }
            return RedirectToAction("Equipment");
        }

        // Mark a repair note as resolved (repaired) — deletes the note
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRepaired(int noteId)
        {
            var note = await _db.EquipmentRepairNote
                .Include(r => r.Equipment)
                .FirstOrDefaultAsync(r => r.Id == noteId);

            if (note != null)
            {
                string equipmentName = note.Equipment?.Name ?? "Equipment";
                _db.EquipmentRepairNote.Remove(note);
                await _db.SaveChangesAsync();
                TempData["AdminSuccess"] = $"'{equipmentName}' marked as repaired.";
            }
            return RedirectToAction("Index");
        }
    }
}