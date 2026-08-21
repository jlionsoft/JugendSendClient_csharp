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

        var url = $"{baseUrl}/api/v3/core/groups/{groupId}/";

        var group = await _http.GetFromJsonAsync<AuthentikUserGroupDto>(url);

        var users = new List<AuthentikUser>();
        if (group?.users_obj != null)
        {
            foreach (var user in group.users_obj)
            {
                users.Add(new AuthentikUser
                {
                    Pk = user.pk,
                    Username = user.username ?? string.Empty,
                    Email = user.email ?? string.Empty,
                    Attributes = user.attributes
                });
            }
        }
        return users;
    }
}