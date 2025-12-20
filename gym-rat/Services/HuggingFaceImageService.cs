using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace gym_rat.Services
{
    public interface IImageGenerationService
    {
        Task<byte[]?> GenerateImageAsync(string prompt, byte[]? sourceImage = null);
    }

    public class HuggingFaceImageService : IImageGenerationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiToken;
        private readonly ILogger<HuggingFaceImageService> _logger;

        public HuggingFaceImageService(IConfiguration configuration, ILogger<HuggingFaceImageService> logger)
        {
            _httpClient = new HttpClient();
            _apiToken = configuration["HuggingFace:ApiToken"] ?? throw new ArgumentNullException("HuggingFace API token not configured");
            _logger = logger;
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
            _httpClient.Timeout = TimeSpan.FromMinutes(3); // Model loading can be slow
        }

        public async Task<byte[]?> GenerateImageAsync(string prompt, byte[]? sourceImage = null)
        {
            try
            {
                // Always use text-to-image for now (img2img is more complex)
                return await GenerateTxt2ImgAsync(prompt);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Image generation timed out");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating image");
                return null;
            }
        }

        private async Task<byte[]?> GenerateTxt2ImgAsync(string prompt)
        {
            // Use SDXL 1.0 with the router endpoint
            var url = "https://router.huggingface.co/hf-inference/models/stabilityai/stable-diffusion-xl-base-1.0";
            
            var requestBody = new 
            { 
                inputs = prompt,
                options = new { wait_for_model = true } // Wait if model is loading
            };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation($"Generating image with prompt: {prompt}");
            
            // Retry logic for model loading
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var response = await _httpClient.PostAsync(url, content);
                
                // Check Content-Type to decide how to read the responses
                var contentType = response.Content.Headers.ContentType?.MediaType;
                
                if (response.IsSuccessStatusCode && contentType != null && contentType.StartsWith("image/"))
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    if (bytes.Length > 1000)
                    {
                        _logger.LogInformation($"Image generated successfully ({bytes.Length} bytes)");
                        return bytes;
                    }
                }

                // If not an image (error or loading message), read as string
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Check if model is loading
                if (responseContent.Contains("loading") || responseContent.Contains("estimated_time"))
                {
                    _logger.LogInformation($"Model is loading (attempt {attempt}/3), waiting...");
                    await Task.Delay(TimeSpan.FromSeconds(20));
                    continue;
                }
                
                // Log error
                _logger.LogError($"HuggingFace API error (attempt {attempt}): {response.StatusCode} - {responseContent}");
                
                if (attempt < 3)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
            
            _logger.LogError("All image generation attempts failed");
            return null;
        }
    }
}