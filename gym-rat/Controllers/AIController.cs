using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using gym_rat.Services;

namespace gym_rat.Controllers
{
    [Authorize]
    public class AIController : Controller
    {
        private readonly IGeminiService _geminiService;

        public AIController(IGeminiService geminiService)
        {
            _geminiService = geminiService;
        }

        // GET: AI/Index - Show the form
        public IActionResult Index()
        {
            return View();
        }

        // POST: AI/Generate - Generate fitness plan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(int weight, int height, int age, string gender, string goal, IFormFile? photo)
        {
            if (weight <= 0 || height <= 0 || age <= 0)
            {
                TempData["Error"] = "Please enter valid values.";
                return RedirectToAction(nameof(Index));
            }

            byte[]? imageData = null;
            if (photo != null && photo.Length > 0)
            {
                using var memoryStream = new MemoryStream();
                await photo.CopyToAsync(memoryStream);
                imageData = memoryStream.ToArray();
            }

            var plan = await _geminiService.GenerateFitnessPlanAsync(weight, height, age, gender, goal, imageData);
            
            ViewBag.Plan = plan;
            ViewBag.Weight = weight;
            ViewBag.Height = height;
            ViewBag.Age = age;
            ViewBag.Gender = gender;
            ViewBag.Goal = goal;
            ViewBag.HasPhoto = imageData != null;

            return View("Result");
        }
    }
}
