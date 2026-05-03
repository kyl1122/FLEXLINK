using Microsoft.AspNetCore.Mvc;

namespace FLEXLINK.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
