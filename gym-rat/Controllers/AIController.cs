using Microsoft.AspNetCore.Mvc;

namespace gym_rat.Controllers
{
    public class AIController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult GeneratePlan(double weight, double height, string goal)
        {
            // This is where we will integrate the AI API later.
            // For now, we'll just return a mock result view.
            ViewBag.Weight = weight;
            ViewBag.Height = height;
            ViewBag.Goal = goal;
            return View("Result");
        }
    }
}
