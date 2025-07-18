using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SecurityMonitor.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction(nameof(Admin));
            if (User.IsInRole("Analyst"))
                return RedirectToAction(nameof(Analyst));
            if (User.IsInRole("User"))
                return RedirectToAction(nameof(UserDashboard));

            // Nếu không thuộc role nào → chuyển về Alerts hoặc AccessDenied
            return RedirectToAction("Index", "Alerts");
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Admin()
        {
            ViewData["Title"] = "Dashboard quản trị";
            return View(); // Views/Dashboard/Admin.cshtml
        }

        [Authorize(Roles = "Analyst")]
        public IActionResult Analyst()
        {
            ViewData["Title"] = "Dashboard phân tích";
            return View(); // Views/Dashboard/Analyst.cshtml
        }

        [Authorize(Roles = "User")]
        public IActionResult UserDashboard()
        {
            ViewData["Title"] = "Trang của người dùng";
            return View("User"); // Views/Dashboard/User.cshtml
        }
    }
}
