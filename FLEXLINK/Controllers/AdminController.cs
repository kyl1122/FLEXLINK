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

        // Landing page — shows dashboard overview (equipment, capacity, repair notes)
        public async Task<IActionResult> Index()
        {
            // CHANGED: Removed individual user/trainer lists from Index to simplify the main dashboard overview
            // (They are now isolated in their own dedicated pages below).

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
            var today = DateTime.Today;
            int currentCount = await _db.Attendance
                .Where(a => a.CheckedInAt.Date == today)
                .CountAsync();
            ViewBag.CurrentCount = currentCount;
            ViewBag.MaxCapacity = 50;

            return View();
        }

        // =========================================================================
        // ADDED: Dedicated Page Action for Trainers Navigation
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEquipment(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["AdminError"] = "Equipment name is required.";
                return RedirectToAction("Index");
            }

            _db.Equipment.Add(new Equipment
            {
                Name = name.Trim(),
                Description = description?.Trim(),
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            TempData["AdminSuccess"] = $"Equipment '{name.Trim()}' added successfully.";
            return RedirectToAction("Index");
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
            return RedirectToAction("Index");
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