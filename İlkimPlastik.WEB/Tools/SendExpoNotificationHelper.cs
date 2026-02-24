using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

    public class SendExpoNotificationHelper
    {
        private readonly HttpClient _httpClient;

        public SendExpoNotificationHelper()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new System.Uri("https://exp.host")
            };

            // Expo API header'ları
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
            );
        }

        public async Task<bool> SendAsync(SendMobileNotificationDTO sendMobileNotificationDTO)
        {
            var message = new
            {
                to = sendMobileNotificationDTO.To,
                title = sendMobileNotificationDTO.Title,
                body = sendMobileNotificationDTO.Body,
                sound = "default",
                priority = "high"
            };

            var json = JsonSerializer.Serialize(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/--/api/v2/push/send", content);

            var resStr = await response.Content.ReadAsStringAsync();
            
            return response.IsSuccessStatusCode;
        }
    }
