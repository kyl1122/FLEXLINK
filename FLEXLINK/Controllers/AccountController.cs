using FLEXLINK.Data;
using FLEXLINK.Models;
using FLEXLINK.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FLEXLINK.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Users> signInManager;
        private readonly UserManager<Users> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly AppDbContext _db;

        public AccountController(
            SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext db)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.roleManager = roleManager;
            _db = db;
        }

        // ── LOGIN ─────────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Login()
        {
            var model = new LoginViewModel();

            if (Request.Cookies.TryGetValue("RememberedEmail", out var savedEmail) && !string.IsNullOrEmpty(savedEmail))
            {
                model.Email = savedEmail;
                model.RememberMe = true;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await userManager.FindByEmailAsync(model.Email); 

            if (user != null)
            {
                // Block if pending approval
                var pendingRequest = _db.RegistrationRequest
                    .FirstOrDefault(r => r.UserId == user.Id && r.Status == "Pending");

                if (pendingRequest != null)
                {
                    ModelState.AddModelError(string.Empty,
                        "Your account is pending staff approval. Please wait for verification before logging in.");
                    return View(model);
                }

                // Block if rejected
                var rejectedRequest = _db.RegistrationRequest
                    .FirstOrDefault(r => r.UserId == user.Id && r.Status == "Rejected");

                if (rejectedRequest != null)
                {
                    ModelState.AddModelError(string.Empty,
                        "Your registration has been rejected. Please contact the gym staff for more information.");
                    return View(model);
                }
            }

            var result = await signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (model.RememberMe)
                {
                    Response.Cookies.Append("RememberedEmail", model.Email, new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddDays(30),
                        HttpOnly = true,
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax
                    });
                }
                else
                {
                    Response.Cookies.Delete("RememberedEmail");
                }

                if (user != null)
                {
                    if (await userManager.IsInRoleAsync(user, "Admin"))
                        return RedirectToAction("Index", "Admin");

                    if (await userManager.IsInRoleAsync(user, "Trainer"))
                        return RedirectToAction("Index", "Trainer");

                    if (await userManager.IsInRoleAsync(user, "Staff"))
                        return RedirectToAction("Index", "Staff");
                }

                return RedirectToAction("Index", "Home");
            }



            if (result.Succeeded)
            {
                if (user != null)
                {
                    if (await userManager.IsInRoleAsync(user, "Admin"))
                        return RedirectToAction("Index", "Admin");

                    if (await userManager.IsInRoleAsync(user, "Trainer"))
                        return RedirectToAction("Index", "Trainer");

                    if (await userManager.IsInRoleAsync(user, "Staff"))
                        return RedirectToAction("Index", "Staff");
                }

                return RedirectToAction("Index", "Home");
            }


            if (result.IsLockedOut)
            {
                var lockoutEnd = await userManager.GetLockoutEndDateAsync(user!);
                int secondsLeft = lockoutEnd.HasValue
                    ? Math.Max(0, (int)(lockoutEnd.Value - DateTimeOffset.Now).TotalSeconds)
                    : 30;

                ModelState.AddModelError(string.Empty,
                $"Too many failed login attempts. Please try again in {secondsLeft} seconds.");
                ViewBag.LockoutSeconds = secondsLeft;
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Invalid Login Attempt.");
            return View(model);
        }

        // ── REGISTER ──────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // ── PROFILE PICTURE IS REQUIRED ────────────────────────────────────────
            if (model.ProfileImage == null || model.ProfileImage.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Profile picture is required.");
                return View(model);
            }

            // ── HANDLE PROFILE PICTURE (before creating the user) ─────────────────
            string? profilePicturePath = null;

            if (model.ProfileImage != null && model.ProfileImage.Length > 0)
            {
                if (model.ProfileImage.Length > 2 * 1024 * 1024)
                {
                    ModelState.AddModelError(string.Empty, "File size must not exceed 2MB.");
                    return View(model);
                }

                string[] allowedExtensions = { ".jpg", ".jpeg", ".png" };
                string extension = Path.GetExtension(model.ProfileImage.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(string.Empty, "Only JPG, JPEG, and PNG files are allowed.");
                    return View(model);
                }

                string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid().ToString() + extension;
                string filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await model.ProfileImage.CopyToAsync(stream);

                profilePicturePath = "/uploads/" + fileName;
            }

            var user = new Users
            {
                FullName = model.Name,
                UserName = model.Email,
                NormalizedUserName = model.Email.ToUpper(),
                Email = model.Email,
                NormalizedEmail = model.Email.ToUpper(),
                Age = model.Age,
                Address = model.Address,
                PhoneNumber = model.PhoneNumber,
                ProfilePicture = profilePicturePath
            };

            var result = await userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // Ensure the User role exists
                if (!await roleManager.RoleExistsAsync("User"))
                    await roleManager.CreateAsync(new IdentityRole("User"));

                await userManager.AddToRoleAsync(user, "User");

                // Create a pending registration request for staff to review
                _db.RegistrationRequest.Add(new RegistrationRequest
                {
                    UserId = user.Id,
                    FullName = user.FullName ?? user.Email ?? "Unknown",
                    Email = user.Email ?? "",
                    ProfilePicture = profilePicturePath,
                    Status = "Pending",
                    RequestedAt = DateTime.Now
                });
                await _db.SaveChangesAsync();

                // Do NOT sign in — send to pending page
                TempData["RegisterSuccess"] =
                    "Registration submitted! Please wait for a staff member to verify your account before logging in.";
                return RedirectToAction("PendingApproval", "Account");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // ── PENDING APPROVAL ──────────────────────────────────────────────────

        [HttpGet]
        public IActionResult PendingApproval()
        {
            return View();
        }

        // ── VERIFY EMAIL / CHANGE PASSWORD ────────────────────────────────────

        [HttpGet]
        public IActionResult VerifyEmail()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await userManager.FindByNameAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found!");
                return View(model);
            }

            return RedirectToAction("ChangePassword", "Account", new { username = user.UserName });
        }

        [HttpGet]
        public IActionResult ChangePassword(string username)
        {
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("VerifyEmail", "Account");

            return View(new ChangePasswordViewModel { Email = username });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Something went wrong");
                return View(model);
            }

            var user = await userManager.FindByNameAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found!");
                return View(model);
            }

            var result = await userManager.RemovePasswordAsync(user);
            if (result.Succeeded)
            {
                await userManager.AddPasswordAsync(user, model.NewPassword);
                return RedirectToAction("Login", "Account");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // ── LOGOUT ────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}
