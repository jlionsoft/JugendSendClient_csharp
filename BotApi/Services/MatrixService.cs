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

    private string Homeserver => _config["Matrix:Homeserver"] ?? string.Empty;
    private string BotToken => _config["Matrix:BotToken"] ?? string.Empty;
    private string BotUserId => _config["Matrix:BotUserId"] ?? string.Empty;
    public string ServerName => _config["Matrix:ServerName"] ?? string.Empty;

    private void ApplyAuth()
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", BotToken);
    }

    // ------------------------------------------------------------
    // PUBLIC API
    // ------------------------------------------------------------

    public async Task<bool> SendPrivateMessageAsync(string matrixUserId, string message)
    {
        if (!await UserExistsAsync(matrixUserId))
            return false;

        var roomId = await EnsureDmRoomAsync(matrixUserId);
        return await SendMessageToRoomAsync(roomId, message);
    }

    public async Task<bool> SendMessageToRoomAsync(string roomId, string message)
    {
        ApplyAuth();

        var txnId = Guid.NewGuid().ToString();
        var url = $"{Homeserver}/_matrix/client/v3/rooms/{roomId}/send/m.room.message/{txnId}";

        var payload = new
        {
            msgtype = "m.text",
            body = message
        };

        try
        {
            var response = await _http.PutAsJsonAsync(url, payload);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
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

        if (!direct.TryGetValue(matrixUserId, out var rooms))
            return null;

        ApplyAuth();
        var joinedUrl = $"{Homeserver}/_matrix/client/v3/joined_rooms";
        var joined = await _http.GetFromJsonAsync<JoinedRoomsResponse>(joinedUrl);
        var joinedSet = joined?.JoinedRooms?.ToHashSet() ?? new HashSet<string>();

        return rooms.FirstOrDefault(r => joinedSet.Contains(r));
    }

    public class JoinedRoomsResponse
    {
        [JsonPropertyName("joined_rooms")]
        public List<string>? JoinedRooms { get; set; }
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

        // Das Root-Objekt IST direkt die Map – kein "m.direct"-Wrapper
        return await response.Content
            .ReadFromJsonAsync<Dictionary<string, List<string>>>()
            ?? new Dictionary<string, List<string>>();
    }
    
    public async Task SetDirectMappingAsync(string matrixUserId, string roomId)
    {
        ApplyAuth();

        // Erst bestehende Map laden
        var existing = await GetDirectMappingAsync();

        // Eintrag hinzufügen oder aktualisieren
        if (!existing.TryGetValue(matrixUserId, out var rooms))
        {
            rooms = new List<string>();
            existing[matrixUserId] = rooms;
        }

        if (!rooms.Contains(roomId))
            rooms.Add(roomId);

        var url = $"{Homeserver}/_matrix/client/v3/user/{BotUserId}/account_data/m.direct";
        await _http.PutAsJsonAsync(url, existing);
    }

    public class DirectData
    {
        [JsonPropertyName("m.direct")]
        public Dictionary<string, List<string>>? Direct { get; set; }
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
        return result?.RoomId ?? string.Empty;
    }

    public class CreateRoomResponse
    {
        [JsonPropertyName("room_id")]
        public string? RoomId { get; set; }
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
