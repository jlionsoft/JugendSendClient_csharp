using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BotApi.NextcloudTalk;

internal static class NextcloudTalkBot
{
    public static void MapNextcloudTalkBot(this WebApplication app)
    {
        var logger = app.Logger;

        var botSecret = Environment.GetEnvironmentVariable("BOT_SECRET")
            ?? throw new InvalidOperationException("Umgebungsvariable BOT_SECRET ist nicht gesetzt.");
        var relayApiKey = Environment.GetEnvironmentVariable("RELAY_API_KEY");
        var defaultRoomToken = Environment.GetEnvironmentVariable("DEFAULT_ROOM_TOKEN");

        app.MapGet("/nextcloud/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/nextcloud/webhook", async (HttpContext ctx, IHttpClientFactory httpFactory) =>
        {
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var rawBody = await reader.ReadToEndAsync();

            var random = ctx.Request.Headers["X-Nextcloud-Talk-Random"].ToString();
            var signature = ctx.Request.Headers["X-Nextcloud-Talk-Signature"].ToString();

            if (string.IsNullOrEmpty(random) || string.IsNullOrEmpty(signature) ||
                !IsValidSignature(random, rawBody, signature, botSecret))
            {
                logger.LogWarning("Webhook mit ungültiger oder fehlender Signatur verworfen.");
                return Results.Unauthorized();
            }

            TalkEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<TalkEvent>(rawBody);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Konnte Webhook-Body nicht als JSON parsen.");
                return Results.BadRequest();
            }

            if (evt is null)
            {
                return Results.BadRequest();
            }

            switch (evt.Type)
            {
                case "Join" or "Leave":
                    logger.LogInformation("Bot-Event {Type} für Konversation {Token} ({Name})",
                        evt.Type, evt.Object?.Id, evt.Object?.Name);
                    break;

                case "Create" when evt.Object?.Name == "message":
                    if (evt.Actor?.Id?.StartsWith("bots/", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        break;
                    }

                    var roomToken = evt.Target?.Id;
                    var messageText = ExtractMessageText(evt.Object.Content)?.Trim();

                    logger.LogInformation("Nachricht in Konversation {Token} von {Actor}: {Text}",
                        roomToken, evt.Actor?.Name, messageText);

                    if (roomToken is not null && !string.IsNullOrEmpty(messageText))
                    {
                        await HandleIncomingMessageAsync(httpFactory, botSecret, roomToken, messageText, evt.Actor?.Name);
                    }

                    break;

                case "Like" or "Undo":
                    logger.LogInformation("Reaktions-Event {Type} in Konversation {Token}", evt.Type, evt.Target?.Id);
                    break;

                default:
                    logger.LogInformation("Unbehandelter Event-Typ: {Type}", evt.Type);
                    break;
            }

            return Results.Ok();
        });

        app.MapPost("/nextcloud/api/send", async (HttpContext ctx, IHttpClientFactory httpFactory) =>
        {
            if (!string.IsNullOrEmpty(relayApiKey))
            {
                var providedKey = ctx.Request.Headers["X-Api-Key"].ToString();
                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(providedKey),
                        Encoding.UTF8.GetBytes(relayApiKey)))
                {
                    return Results.Unauthorized();
                }
            }

            SendRequest? req;
            try
            {
                req = await JsonSerializer.DeserializeAsync<SendRequest>(ctx.Request.Body);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Ungültiges JSON im Request-Body." });
            }

            var roomToken = req?.RoomToken ?? defaultRoomToken;
            if (string.IsNullOrWhiteSpace(roomToken))
            {
                return Results.BadRequest(new { error = "roomToken fehlt und DEFAULT_ROOM_TOKEN ist nicht gesetzt." });
            }

            if (string.IsNullOrWhiteSpace(req?.Message))
            {
                return Results.BadRequest(new { error = "message fehlt." });
            }

            var client = httpFactory.CreateClient("nextcloud");
            var (status, body) = await SendTalkMessageAsync(client, botSecret, roomToken, req.Message, req.ReplyTo, req.ReferenceId);

            logger.LogInformation("Nachricht an Konversation {Token} gesendet, Status {Status}", roomToken, (int)status);

            return Results.Text(body, "application/json", statusCode: (int)status);
        });
    }

    // Helpers
    private static bool IsValidSignature(string random, string body, string signature, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(random + body));
        var computedHex = ToHexLower(computed);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }

    private static string? ExtractMessageText(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        try
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(content);
            if (doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("message", out var msg))
            {
                return msg.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return content;
    }

    private static async Task HandleIncomingMessageAsync(
        IHttpClientFactory httpFactory, string botSecret, string roomToken, string text, string? senderName)
    {
        if (text.Equals("!ping", StringComparison.OrdinalIgnoreCase))
        {
            var client = httpFactory.CreateClient("nextcloud");
            await SendTalkMessageAsync(client, botSecret, roomToken, "🏓 pong", null, null);
        }
        else if (text.Equals("!hilfe", StringComparison.OrdinalIgnoreCase))
        {
            var client = httpFactory.CreateClient("nextcloud");
            await SendTalkMessageAsync(client, botSecret, roomToken,
                "Verfügbare Kommandos: `!ping`", null, null);
        }
    }

    private static async Task<(HttpStatusCode Status, string Body)> SendTalkMessageAsync(
        HttpClient client, string secret, string roomToken, string message, int? replyTo, string? referenceId)
    {
        var payload = new Dictionary<string, object?> { ["message"] = message };
        if (replyTo is not null) payload["replyTo"] = replyTo;
        if (referenceId is not null) payload["referenceId"] = referenceId;

        var jsonBody = JsonSerializer.Serialize(payload);

        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        var random = ToHexLower(randomBytes);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = ToHexLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(random + jsonBody)));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"ocs/v2.php/apps/spreed/api/v1/bot/{roomToken}/message");
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        request.Headers.Add("OCS-APIRequest", "true");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("X-Nextcloud-Talk-Bot-Random", random);
        request.Headers.Add("X-Nextcloud-Talk-Bot-Signature", signature);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, body);
    }

    private static string ToHexLower(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    // DTOs
    private record TalkEvent(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("actor")] TalkActor? Actor,
        [property: JsonPropertyName("object")] TalkObject? Object,
        [property: JsonPropertyName("target")] TalkTarget? Target
    );

    private record TalkActor(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name
    );

    private record TalkObject(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("mediaType")] string? MediaType
    );

    private record TalkTarget(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name
    );

    private record SendRequest(
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("roomToken")] string? RoomToken,
        [property: JsonPropertyName("replyTo")] int? ReplyTo,
        [property: JsonPropertyName("referenceId")] string? ReferenceId
    );
}
