using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using gym_rat.Services;

namespace gym_rat.Controllers
{
    [Authorize]
    public class AIController : Controller
    {
        private readonly IAITextService _textService;
        private readonly IImageGenerationService _imageService;

        public AIController(IAITextService textService, IImageGenerationService imageService)
        {
            _textService = textService;
            _imageService = imageService;
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

            // Generate text plan with Groq (Llama 3.3)
            var plan = await _textService.GenerateFitnessPlanAsync(weight, height, age, gender, goal, imageData);
            
            // Generate motivational image with HuggingFace
            var imagePrompt = GenerateImagePrompt(gender, goal);
            var generatedImage = await _imageService.GenerateImageAsync(imagePrompt, imageData);
            
            ViewBag.Plan = plan;
            ViewBag.Weight = weight;
            ViewBag.Height = height;
            ViewBag.Age = age;
            ViewBag.Gender = gender;
            ViewBag.Goal = goal;
            ViewBag.HasPhoto = imageData != null;
            
            // Convert generated image to base64 for display
            if (generatedImage != null)
            {
                ViewBag.GeneratedImage = Convert.ToBase64String(generatedImage);
            }

            return View("Result");
        }

        private string GenerateImagePrompt(string gender, string goal)
        {
            var bodyType = gender.ToLower() == "male" ? "muscular athletic man" : "fit athletic woman";
            
            var goalDescription = goal.ToLower() switch
            {
                "lose weight" => "lean fit body, toned muscles, healthy physique",
                "build muscle" => "very muscular bodybuilder physique, defined muscles",
                "stay fit" => "athletic healthy body, active lifestyle",
                "increase endurance" => "lean athletic runner physique, cardio fitness",
                "improve flexibility" => "lean flexible yoga body, graceful pose",
                _ => "healthy fit athletic body"
            };

            return $"Professional fitness photo of a {bodyType}, {goalDescription}, gym background, motivational, high quality, realistic";
        }
    }
}
