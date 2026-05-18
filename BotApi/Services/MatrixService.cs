using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

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

    private string Homeserver => _config["Matrix:Homeserver"];
    private string BotToken => _config["Matrix:BotToken"];
    private string BotUserId => _config["Matrix:BotUserId"];

    private void ApplyAuth()
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", BotToken);
    }

    // ------------------------------------------------------------
    // PUBLIC API
    // ------------------------------------------------------------

    public async Task SendPrivateMessageAsync(string matrixUserId, string message)
    {
        if (!await UserExistsAsync(matrixUserId))
            return;

        var roomId = await EnsureDmRoomAsync(matrixUserId);
        await SendMessageToRoomAsync(roomId, message);
    }

    public async Task SendMessageToRoomAsync(string roomId, string message)
    {
        ApplyAuth();

        var txnId = Guid.NewGuid().ToString();
        var url = $"{Homeserver}/_matrix/client/v3/rooms/{roomId}/send/m.room.message/{txnId}";

        var payload = new
        {
            msgtype = "m.text",
            body = message
        };

        await _http.PutAsJsonAsync(url, payload);
    }

    // ------------------------------------------------------------
    // DM ROOM HANDLING
    // ------------------------------------------------------------

    public async Task<string> EnsureDmRoomAsync(string matrixUserId)
    {
        var existing = await FindExistingDmRoomAsync(matrixUserId);
        if (existing != null)
            return existing;

        var roomId = await CreateDmRoomAsync(matrixUserId);
        await SetDirectMappingAsync(matrixUserId, roomId);
        return roomId;
    }

    public async Task<string?> FindExistingDmRoomAsync(string matrixUserId)
    {
        var direct = await GetDirectMappingAsync();

        if (direct.TryGetValue(matrixUserId, out var rooms) && rooms.Count > 0)
            return rooms.First();

        return null;
    }

    // ------------------------------------------------------------
    // m.direct ACCOUNT DATA
    // ------------------------------------------------------------

    public async Task<Dictionary<string, List<string>>> GetDirectMappingAsync()
    {
        ApplyAuth();

        var url = $"{Homeserver}/_matrix/client/v3/user/{BotUserId}/account_data/m.direct";
        var response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return new Dictionary<string, List<string>>();

        var data = await response.Content.ReadFromJsonAsync<DirectData>();
        return data?.Direct ?? new Dictionary<string, List<string>>();
    }

    public async Task SetDirectMappingAsync(string matrixUserId, string roomId)
    {
        ApplyAuth();

        var url = $"{Homeserver}/_matrix/client/v3/user/{BotUserId}/account_data/m.direct";

        var payload = new Dictionary<string, List<string>>
        {
            { matrixUserId, new List<string> { roomId } }
        };

        await _http.PutAsJsonAsync(url, payload);
    }

    public class DirectData
    {
        [JsonPropertyName("m.direct")]
        public Dictionary<string, List<string>> Direct { get; set; }
    }

    // ------------------------------------------------------------
    // ROOM CREATION
    // ------------------------------------------------------------

    public async Task<string> CreateDmRoomAsync(string matrixUserId)
    {
        ApplyAuth();

        var url = $"{Homeserver}/_matrix/client/v3/createRoom";

        var payload = new
        {
            preset = "trusted_private_chat",
            invite = new[] { matrixUserId },
            is_direct = true
        };

        var response = await _http.PostAsJsonAsync(url, payload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateRoomResponse>();
        return result.RoomId;
    }

    public class CreateRoomResponse
    {
        [JsonPropertyName("room_id")]
        public string RoomId { get; set; }
    }

    // ------------------------------------------------------------
    // USER CHECK
    // ------------------------------------------------------------

    public async Task<bool> UserExistsAsync(string matrixUserId)
    {
        ApplyAuth();

        var url = $"{Homeserver}/_matrix/client/v3/profile/{matrixUserId}";
        var response = await _http.GetAsync(url);

        return response.IsSuccessStatusCode;
    }
}
