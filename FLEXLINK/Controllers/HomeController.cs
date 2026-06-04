using FLEXLINK.Data;
using FLEXLINK.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace FLEXLINK.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db)
        {
            _db = db;
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

        // "View Trainers" page — shows all trainers who have set up their profile
        public IActionResult Trainer()
        {
            // Only fetch trainers that have filled in at least their name
            var trainers = _db.ProfileTrainer
                              .Where(p => p.FullName != null && p.FullName != "")
                              .ToList();

            return View(trainers);
        }

        public IActionResult Membership()
        {
            return View();
        }

        public IActionResult Schedule()
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
    }
}
