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

    // ------------------------------------------------------------
    // PUBLIC API
    // ------------------------------------------------------------

    public async Task SendPrivateMessageAsync(string matrixUserId, string message)
    {
        var roomId = await EnsureDmRoomAsync(matrixUserId);
        await SendMessageToRoomAsync(roomId, message);
    }

    // ------------------------------------------------------------
    // SEND MESSAGE
    // ------------------------------------------------------------

    public async Task SendMessageToRoomAsync(string roomId, string message)
    {
        var homeserver = _config["Matrix:Homeserver"];
        var token = _config["Matrix:BotToken"];

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var txnId = Guid.NewGuid().ToString();
        var url = $"{homeserver}/_matrix/client/v3/rooms/{roomId}/send/m.room.message/{txnId}";

        var payload = new
        {
            msgtype = "m.text",
            body = message
        };

        await _http.PutAsJsonAsync(url, payload);
    }

    // ------------------------------------------------------------
    // ENSURE DM ROOM
    // ------------------------------------------------------------

    public async Task<string> EnsureDmRoomAsync(string matrixUserId)
    {
        var existing = await FindExistingDmRoomAsync(matrixUserId);
        if (existing != null)
            return existing;

        return await CreateDmRoomAsync(matrixUserId);
    }

    // ------------------------------------------------------------
    // FIND EXISTING DM ROOM
    // ------------------------------------------------------------

    public async Task<string?> FindExistingDmRoomAsync(string matrixUserId)
    {
        var rooms = await GetBotJoinedRoomsAsync();

        foreach (var roomId in rooms)
        {
            if (await IsDirectRoomAsync(roomId, matrixUserId))
                return roomId;
        }

        return null;
    }

    // Bot's joined rooms
    public async Task<List<string>> GetBotJoinedRoomsAsync()
    {
        var homeserver = _config["Matrix:Homeserver"];
        var token = _config["Matrix:BotToken"];

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var url = $"{homeserver}/_matrix/client/v3/joined_rooms";

        var response = await _http.GetFromJsonAsync<JoinedRoomsResponse>(url);
        return response?.JoinedRooms ?? new List<string>();
    }

    public class JoinedRoomsResponse
    {
        [JsonPropertyName("joined_rooms")]
        public List<string> JoinedRooms { get; set; }
    }

    // ------------------------------------------------------------
    // CHECK IF ROOM IS A REAL ACTIVE DM ROOM
    // ------------------------------------------------------------

    public async Task<bool> IsDirectRoomAsync(string roomId, string matrixUserId)
    {
        var botUserId = _config["Matrix:BotUserId"];

        // 1. Membership des Users prüfen
        var membership = await GetUserMembershipAsync(roomId, matrixUserId);

        if (membership != "join")
            return false;

        // 2. Mitgliederliste abrufen
        var members = await GetRoomMembersAsync(roomId);

        // 3. DM-Raum hat genau 2 Mitglieder
        if (members.Count != 2)
            return false;


        bool r = members.Contains(matrixUserId) && members.Contains(botUserId);
        return r;
    }

    // ------------------------------------------------------------
    // GET ROOM MEMBERS
    // ------------------------------------------------------------

    public async Task<List<string>> GetRoomMembersAsync(string roomId)
    {
        var homeserver = _config["Matrix:Homeserver"];
        var token = _config["Matrix:BotToken"];

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var url = $"{homeserver}/_matrix/client/v3/rooms/{roomId}/joined_members";

        var response = await _http.GetFromJsonAsync<JoinedMembersResponse>(url);
        return response?.Joined.Keys.ToList() ?? new List<string>();
    }

    public class JoinedMembersResponse
    {
        [JsonPropertyName("joined")]
        public Dictionary<string, object> Joined { get; set; }
    }

    // ------------------------------------------------------------
    // GET USER MEMBERSHIP (join / leave / ban)
    // ------------------------------------------------------------

    public async Task<string?> GetUserMembershipAsync(string roomId, string matrixUserId)
    {
        var homeserver = _config["Matrix:Homeserver"];
        var token = _config["Matrix:BotToken"];

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var url = $"{homeserver}/_matrix/client/v3/rooms/{roomId}/state/m.room.member/{matrixUserId}";

        var response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return null;

        var data = await response.Content.ReadFromJsonAsync<MemberState>();
        return data?.Membership;
    }

    public class MemberState
    {
        [JsonPropertyName("membership")]
        public string Membership { get; set; }
    }

    // ------------------------------------------------------------
    // CREATE DM ROOM
    // ------------------------------------------------------------

    public async Task<string> CreateDmRoomAsync(string matrixUserId)
    {
        var homeserver = _config["Matrix:Homeserver"];
        var token = _config["Matrix:BotToken"];

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var url = $"{homeserver}/_matrix/client/v3/createRoom";

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
}