using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace BotApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NextcloudController : ControllerBase
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<NextcloudController> _logger;
        private readonly string _botSecret;

        public NextcloudController(IHttpClientFactory httpFactory, ILogger<NextcloudController> logger, IConfiguration config)
        {
            _httpFactory = httpFactory;
            _logger = logger;
            _botSecret = config["NextcloudTalk:BotSecret"]
                ?? Environment.GetEnvironmentVariable("BOT_SECRET")
                ?? throw new InvalidOperationException("BOT_SECRET ist nicht konfiguriert.");
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            // Body MUSS roh gelesen werden, bevor irgendein Model-Binding passiert -
            // die Signatur wird über die exakten rohen Bytes gebildet.
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var rawBody = await reader.ReadToEndAsync();

            var random = Request.Headers["X-Nextcloud-Talk-Random"].ToString();
            var signature = Request.Headers["X-Nextcloud-Talk-Signature"].ToString();

            if (string.IsNullOrEmpty(random) || string.IsNullOrEmpty(signature) ||
                !IsValidSignature(random, rawBody, signature, _botSecret))
            {
                _logger.LogWarning("Webhook mit ungültiger oder fehlender Signatur verworfen.");
                return Unauthorized();
            }

            TalkEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<TalkEvent>(rawBody);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Konnte Webhook-Body nicht als JSON parsen.");
                return BadRequest();
            }

            if (evt is null)
            {
                return BadRequest();
            }

            switch (evt.Type)
            {
                case "Join" or "Leave":
                    _logger.LogInformation("Bot-Event {Type} für Konversation {Token}", evt.Type, evt.Object?.Id);
                    break;

                case "Create" when evt.Object?.Name == "message":
                    if (evt.Actor?.Id?.StartsWith("bots/", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        break; // eigene Nachrichten ignorieren, sonst Endlosschleife
                    }

                    var roomToken = evt.Target?.Id;
                    var messageText = ExtractMessageText(evt.Object.Content)?.Trim();

                    _logger.LogInformation("Nachricht in {Token} von {Actor}: {Text}",
                        roomToken, evt.Actor?.Name, messageText);

                    if (roomToken is not null && !string.IsNullOrEmpty(messageText))
                    {
                        await HandleIncomingMessageAsync(roomToken, messageText);
                    }
                    break;

                case "Like" or "Undo":
                    _logger.LogInformation("Reaktions-Event {Type} in {Token}", evt.Type, evt.Target?.Id);
                    break;

                default:
                    _logger.LogInformation("Unbehandelter Event-Typ: {Type}", evt.Type);
                    break;
            }

            return Ok();
        }

        private static bool IsValidSignature(string random, string body, string signature, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(random + body));
            var computedHex = Convert.ToHexStringLower(computed);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex),
                Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
        }

        private static string? ExtractMessageText(string? content)
        {
            if (string.IsNullOrEmpty(content)) return null;
            try
            {
                var doc = JsonSerializer.Deserialize<JsonElement>(content);
                if (doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("message", out var msg))
                {
                    return msg.GetString();
                }
            }
            catch (JsonException) { }
            return content;
        }

        private async Task HandleIncomingMessageAsync(string roomToken, string text)
        {
            if (text.Equals("!ping", StringComparison.OrdinalIgnoreCase))
            {
                var client = _httpFactory.CreateClient("nextcloud");
                await SendTalkMessageAsync(client, _botSecret, roomToken, "🏓 pong");
            }
        }

        private static async Task SendTalkMessageAsync(HttpClient client, string secret, string roomToken, string message)
        {
            var jsonBody = JsonSerializer.Serialize(new Dictionary<string, object?> { ["message"] = message });

            var randomBytes = new byte[32];
            RandomNumberGenerator.Fill(randomBytes);
            var random = Convert.ToHexStringLower(randomBytes);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var signature = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(random + jsonBody)));

            using var request = new HttpRequestMessage(HttpMethod.Post, $"ocs/v2.php/apps/spreed/api/v1/bot/{roomToken}/message");
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            request.Headers.Add("OCS-APIRequest", "true");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("X-Nextcloud-Talk-Bot-Random", random);
            request.Headers.Add("X-Nextcloud-Talk-Bot-Signature", signature);

            await client.SendAsync(request);
        }
    }

    record TalkEvent(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("actor")] TalkActor? Actor,
        [property: JsonPropertyName("object")] TalkObject? Object,
        [property: JsonPropertyName("target")] TalkTarget? Target
    );

    record TalkActor(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name
    );

    record TalkObject(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("mediaType")] string? MediaType
    );

    record TalkTarget(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name
    );
}