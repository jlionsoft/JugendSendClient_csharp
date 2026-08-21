using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BotApi.Models;


// -------------------- AUTHENTIK DTOs --------------------

public class AuthentikUser
{
    [JsonPropertyName("pk")]
    public int Pk { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    // Optional custom attributes from Authentik
    [JsonPropertyName("attributes")]
    public Attributes? Attributes { get; set; }

    // Convenience accessors
    [JsonIgnore]
    public string? PhoneNumber => Attributes?.PhoneNumber;

    [JsonIgnore]
    public bool SmsEnabled => Attributes?.Sms ?? false;
}

public class AuthentikUserListResponse
{
    [JsonPropertyName("results")]
    public List<AuthentikUser> Results { get; set; } = new();
}

public class AuthentikGroupResponse
{
    [JsonPropertyName("pk")]
    public string Pk { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("users")]
    public List<int> Users { get; set; } = new();
}

// -------------------- MATRIX DTOs --------------------

public class MatrixAdminUser
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}