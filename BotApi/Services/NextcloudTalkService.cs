using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BotApi.Services
{
    public interface INextcloudTalkService
    {
        /// <summary>
        /// Sendet eine Nachricht als Bot in die in der Konfiguration hinterlegte Gruppe.
        /// </summary>
        Task<bool> SendMessageAsync(string message);

        /// <summary>
        /// Sendet eine Nachricht in eine explizit angegebene Konversation
        /// (z.B. wenn später mehrere Gruppen unterstützt werden sollen).
        /// </summary>
        Task<bool> SendMessageAsync(string message, string roomToken, int? replyTo = null, string? referenceId = null);
    }

    public class NextcloudTalkService : INextcloudTalkService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<NextcloudTalkService> _logger;
        private readonly string _botSecret;
        private readonly string _defaultRoomToken;

        public NextcloudTalkService(
            IHttpClientFactory httpFactory,
            ILogger<NextcloudTalkService> logger,
            IConfiguration config)
        {
            _httpFactory = httpFactory;
            _logger = logger;

            _botSecret = config["NextcloudTalk:BotSecret"]
                ?? Environment.GetEnvironmentVariable("BOT_SECRET")
                ?? throw new InvalidOperationException("NextcloudTalk:BotSecret ist nicht konfiguriert.");

            _defaultRoomToken = config["NextcloudTalk:DefaultRoomToken"]
                ?? Environment.GetEnvironmentVariable("DEFAULT_ROOM_TOKEN")
                ?? throw new InvalidOperationException("NextcloudTalk:DefaultRoomToken ist nicht konfiguriert.");
        }

        public Task<bool> SendMessageAsync(string message)
            => SendMessageAsync(message, _defaultRoomToken);

        public async Task<bool> SendMessageAsync(string message, string roomToken, int? replyTo = null, string? referenceId = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("SendMessageAsync mit leerer Nachricht aufgerufen - abgebrochen.");
                return false;
            }

            var payload = new Dictionary<string, object?> { ["message"] = message };
            if (replyTo is not null) payload["replyTo"] = replyTo;
            if (referenceId is not null) payload["referenceId"] = referenceId;

            var jsonBody = JsonSerializer.Serialize(payload);

            var randomBytes = new byte[32];
            RandomNumberGenerator.Fill(randomBytes);
            var random = Convert.ToHexStringLower(randomBytes);

            // Wichtig: Signiert wird NUR der Nachrichteninhalt (message), nicht der vollständige JSON-Body.
            // Nextcloud extrahiert den "message"-Wert serverseitig vor dem Signaturvergleich (BotController.php).
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_botSecret));
            var signature = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(random + message)));

            using var request = new HttpRequestMessage(HttpMethod.Post, $"ocs/v2.php/apps/spreed/api/v1/bot/{roomToken}/message?format=json");
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            request.Headers.Add("OCS-APIRequest", "true");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("X-Nextcloud-Talk-Bot-Random", random);
            request.Headers.Add("X-Nextcloud-Talk-Bot-Signature", signature);

            var client = _httpFactory.CreateClient("nextcloud");
            var requestUri = request.RequestUri;
            if (requestUri is not null && !requestUri.IsAbsoluteUri && client.BaseAddress is not null)
            {
                requestUri = new Uri(client.BaseAddress, requestUri);
            }

            var requestBody = await request.Content.ReadAsStringAsync();
            var requestLogBuilder = new StringBuilder();
            requestLogBuilder.AppendLine("Ausgehende Nextcloud Talk Anfrage:");
            requestLogBuilder.AppendLine($"{request.Method} {requestUri}");
            requestLogBuilder.AppendLine("Headers:");

            foreach (var header in request.Headers)
            {
                requestLogBuilder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            foreach (var header in request.Content.Headers)
            {
                requestLogBuilder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            requestLogBuilder.AppendLine("Body:");
            requestLogBuilder.AppendLine(requestBody);
            _logger.LogInformation("{RequestDetails}", requestLogBuilder.ToString());

            try
            {
                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "Senden an Konversation {Token} fehlgeschlagen: {Status} - {Body}",
                        roomToken, (int)response.StatusCode, body);
                    return false;
                }

                _logger.LogInformation("Nachricht an Konversation {Token} gesendet.", roomToken);
                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Fehler beim Senden an Konversation {Token}.", roomToken);
                return false;
            }
        }
    }
}