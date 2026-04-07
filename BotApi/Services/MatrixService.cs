using System.Net.Http.Json;

namespace BotApi.Services;

public class MatrixService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public MatrixService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task SendPrivateMessageAsync(string matrixUserId, string message)
    {
        var homeserver = _config["Matrix:Homeserver"];
        var token = _config["Matrix:BotToken"];

        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var txnId = Guid.NewGuid().ToString();
        var url = $"{homeserver}/_matrix/client/v3/sendToDevice/m.room.message/{txnId}";

        var payload = new
        {
            messages = new Dictionary<string, object>
            {
                [matrixUserId] = new Dictionary<string, object>
                {
                    ["*"] = new
                    {
                        msgtype = "m.text",
                        body = message
                    }
                }
            }
        };

        await _http.PostAsJsonAsync(url, payload);
    }
}