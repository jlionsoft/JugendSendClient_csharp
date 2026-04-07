using BotApi.Models;
using System.Net.Http.Json;

namespace BotApi.Services;

public class AuthentikService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public AuthentikService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<List<AuthentikUser>> GetYouthUsersAsync()
    {
        var baseUrl = _config["Authentik:BaseUrl"];
        var token = _config["Authentik:Token"];
        var groupId = _config["Authentik:YouthGroupId"];

        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var url = $"{baseUrl}/api/v3/core/groups/{groupId}/users/";

        var response = await _http.GetFromJsonAsync<AuthentikUserResponse>(url);

        return response?.Results?.Select(u => new AuthentikUser
        {
            Username = u.Username
        }).ToList() ?? new();
    }

    private class AuthentikUserResponse
    {
        public List<UserResult> Results { get; set; } = new();
    }

    private class UserResult
    {
        public string Username { get; set; } = "";
    }
}