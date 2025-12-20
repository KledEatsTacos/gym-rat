using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace gym_rat.Services
{
    public interface IAITextService
    {
        Task<string> GenerateFitnessPlanAsync(int weight, int height, int age, string gender, string goal, byte[]? imageData = null);
    }

    public class GroqService : IAITextService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GroqService> _logger;

        public GroqService(IConfiguration configuration, ILogger<GroqService> logger)
        {
            _httpClient = new HttpClient();
            _apiKey = configuration["Groq:ApiKey"] ?? throw new ArgumentNullException("Groq API key not configured");
            _logger = logger;
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<string> GenerateFitnessPlanAsync(int weight, int height, int age, string gender, string goal, byte[]? imageData = null)
        {
            var prompt = $@"You are a professional fitness trainer and nutritionist. 
Create a personalized fitness and diet plan for the following person:

- Weight: {weight} kg
- Height: {height} cm
- Age: {age} years
- Gender: {gender}
- Goal: {goal}

Please provide:
1. A brief assessment of their current BMI and health status
2. A weekly workout plan, for males optimize it to be a 3 day push pull legs workout routine for beginners.
3. A daily meal plan with specific foods
4. Tips for achieving their goal and not giving up mentally.
5. Expected timeline for results

Format your response in a clear, readable way with sections and bullet points.";

            // Note: Groq doesn't support image input, so we ignore imageData for now
            // The image is still used for HuggingFace image generation

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.7,
                max_tokens = 4096
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = "https://api.groq.com/openai/v1/chat/completions";

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Groq API error: {responseBody}");
                    return $"Error: AI service is currently unavailable. (Status: {response.StatusCode})";
                }

                // Parse the response
                using var doc = JsonDocument.Parse(responseBody);
                var text = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return text ?? "No result received";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Groq API");
                return $"Error: {ex.Message}";
            }
        }
    }
}
