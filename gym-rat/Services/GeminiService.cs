using System.Text;
using System.Text.Json;

namespace gym_rat.Services
{
    public interface IGeminiService
    {
        Task<string> GenerateFitnessPlanAsync(int weight, int height, int age, string gender, string goal, byte[]? imageData = null);
    }

    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GeminiService> _logger;

        public GeminiService(IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = new HttpClient();
            _apiKey = configuration["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini API key not configured");
            _logger = logger;
        }

        public async Task<string> GenerateFitnessPlanAsync(int weight, int height, int age, string gender, string goal, byte[]? imageData = null)
        {
            var basePrompt = $@"You are a professional fitness trainer and nutritionist. 
Create a personalized fitness and diet plan for the following person:

- Weight: {weight} kg
- Height: {height} cm
- Age: {age} years
- Gender: {gender}
- Goal: {goal}";

            string imageAnalysisPrompt = imageData != null 
                ? "\n\nThe user has also provided a photo of themselves. Please confirm whether it is an appropriate picture of a human body, and then analyze the current body composition and physique from the image and incorporate your visual assessment into the plan. Comment on what you observe (e.g., body fat percentage estimate, muscle development, posture) and tailor the recommendations accordingly."
                : "";

            var fullPrompt = basePrompt + imageAnalysisPrompt + @"

Please provide:
1. A brief assessment of their current BMI and health status" + (imageData != null ? " (including visual analysis from the photo)" : "") + @"
2. A weekly workout plan, for males optimize it to be a 3 day push pull legs workout routine for beginners.
3. A daily meal plan with specific foods
4. Tips for achieving their goal and not giving up mentally.
5. Expected timeline for results

Format your response in a clear, readable way with sections and bullet points.";

            object requestBody;

            if (imageData != null)
            {
                // Request with image
                var base64Image = Convert.ToBase64String(imageData);
                requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = fullPrompt },
                                new { 
                                    inline_data = new { 
                                        mime_type = "image/jpeg", 
                                        data = base64Image 
                                    } 
                                }
                            }
                        }
                    }
                };
            }
            else
            {
                // Text-only request
                requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = fullPrompt }
                            }
                        }
                    }
                };
            }

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Gemini API error: {responseBody}");
                    return $"Error: AI service is currently unavailable. (Status: {response.StatusCode})";
                }

                // Parse the response
                using var doc = JsonDocument.Parse(responseBody);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text ?? "No result received";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API");
                return $"Error: {ex.Message}";
            }
        }
    }
}
