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
            _httpClient.Timeout = TimeSpan.FromMinutes(2); // Image generation can be slow
        }

        public async Task<byte[]?> GenerateImageAsync(string prompt, byte[]? sourceImage = null)
        {
            try
            {
                if (sourceImage != null)
                {
                    // Image-to-image transformation
                    return await GenerateImg2ImgAsync(prompt, sourceImage);
                }
                else
                {
                    // Text-to-image generation
                    return await GenerateTxt2ImgAsync(prompt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating image");
                return null;
            }
        }

        private async Task<byte[]?> GenerateTxt2ImgAsync(string prompt)
        {
            var url = "https://api-inference.huggingface.co/models/stabilityai/stable-diffusion-xl-base-1.0";
            
            var requestBody = new { inputs = prompt };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation($"Generating image with prompt: {prompt}");
            
            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"HuggingFace API error: {error}");
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task<byte[]?> GenerateImg2ImgAsync(string prompt, byte[] sourceImage)
        {
            // Use SDXL refiner or img2img endpoint
            var url = "https://api-inference.huggingface.co/models/stabilityai/stable-diffusion-xl-refiner-1.0";
            
            using var formData = new MultipartFormDataContent();
            
            // Add the source image
            var imageContent = new ByteArrayContent(sourceImage);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            formData.Add(imageContent, "image", "source.jpg");
            
            // Add the prompt as form field
            formData.Add(new StringContent(prompt), "prompt");
            formData.Add(new StringContent("0.35"), "strength"); // How much to change (0.35 = moderate transformation)

            _logger.LogInformation($"Generating img2img with prompt: {prompt}");
            
            var response = await _httpClient.PostAsync(url, formData);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"HuggingFace img2img error: {error}");
                
                // Fallback to text-to-image if img2img fails
                _logger.LogInformation("Falling back to text-to-image");
                return await GenerateTxt2ImgAsync(prompt);
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
    }
}
