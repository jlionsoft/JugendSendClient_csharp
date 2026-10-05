namespace BotApi.Services
{
    public class SmsService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public SmsService(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

        private string Gateway =>
            _config["SMS:Gateway"]
            ?? throw new InvalidOperationException("SMS gateway URL is not configured");

        private string Username =>
            _config["SMS:Username"]
            ?? throw new InvalidOperationException("SMS username is not configured");

        private string Password =>
            _config["SMS:Password"]
            ?? throw new InvalidOperationException("SMS password is not configured");

        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                var credentials = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes($"{Username}:{Password}")
                );

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{Gateway.TrimEnd('/')}/rest/tool/sms/send"
                );

                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Basic",
                        credentials
                    );

                var data = new Dictionary<string, string>
                {
                    ["port"] = "lte1",
                    ["phone-number"] = phoneNumber,
                    ["message"] = message
                };

                request.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(data),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                using var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _ = responseBody; // keep for debugging if needed
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
