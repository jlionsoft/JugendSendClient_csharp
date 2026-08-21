namespace BotApi.Services;

using BotApi.Models;
using System.Net.Http.Headers;

// ========================================================
// AUTHENTIK USER SERVICE
// ========================================================

public class AuthentikUserService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public AuthentikUserService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    private void ApplyAuthHeader()
    {
        var token = _config["Authentik:Token"];
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<AuthentikUser?> GetUserAsync(string username)
    {
        var baseUrl = _config["Authentik:BaseUrl"];
        ApplyAuthHeader();

        var url = $"{baseUrl}/api/v3/core/users/?username={Uri.EscapeDataString(username)}";

        var resp = await _http.GetFromJsonAsync<AuthentikUserListResponse>(url);
        return resp?.Results.FirstOrDefault();
    }

    public async Task<AuthentikUser> CreateUserAsync(string username, string? displayName, string? email)
    {
        var baseUrl = _config["Authentik:BaseUrl"];
        ApplyAuthHeader();

        var url = $"{baseUrl}/api/v3/core/users/";

        var payload = new
        {
            username,
            name = displayName,   // Anzeigename
            email         // optional
        };

        var response = await _http.PostAsJsonAsync(url, payload);
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<AuthentikUser>();
        return user!;
    }
    public async Task<bool> IsUserInGroupAsync(int userPk, string groupId)
    {
        var baseUrl = _config["Authentik:BaseUrl"];
        ApplyAuthHeader();

        var url = $"{baseUrl}/api/v3/core/groups/{groupId}/";

        var group = await _http.GetFromJsonAsync<AuthentikGroupResponse>(url);
        return group?.Users.Contains(userPk) == true;
    }

    public async Task AddUserToGroupAsync(int userPk, string groupId)
    {
        var baseUrl = _config["Authentik:BaseUrl"];
        ApplyAuthHeader();

        var url = $"{baseUrl}/api/v3/core/groups/{groupId}/add_user/";

        var payload = new { pk = userPk };

        var response = await _http.PostAsJsonAsync(url, payload);
        response.EnsureSuccessStatusCode();
    }
}

// ========================================================
// MATRIX ADMIN SERVICE (Synapse Admin API)
// ========================================================

public class MatrixAdminService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public MatrixAdminService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    private void ApplyAdminAuthHeader()
    {
        var token = _config["Matrix:AdminToken"]; // eigener Admin-Token
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public string ServerName => _config["Matrix:ServerName"]!;

    public async Task<bool> UserExistsAsync(string matrixUserId)
    {
        var homeserver = _config["Matrix:Homeserver"];
        ApplyAdminAuthHeader();

        var url = $"{homeserver}/_synapse/admin/v2/users/{Uri.EscapeDataString(matrixUserId)}";

        var response = await _http.GetAsync(url);
        return response.IsSuccessStatusCode;
    }

    public async Task CreateUserAsync(string matrixUserId, string password, string displayName)
    {
        var homeserver = _config["Matrix:Homeserver"];
        ApplyAdminAuthHeader();
        _logger.LogInformation("Matrix: Creating user {UserId} with password {Password}", matrixUserId, password);

        var url = $"{homeserver}/_synapse/admin/v2/users/{Uri.EscapeDataString(matrixUserId)}";

        var payload = new
        {
            password,
            displayname = displayName,
            // optional: admin = false
        };

        var response = await _http.PutAsJsonAsync(url, payload);
        response.EnsureSuccessStatusCode();
    }
    private readonly ILogger<MatrixAdminService> _logger;

    public MatrixAdminService(HttpClient http, IConfiguration config, ILogger<MatrixAdminService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }
}

// ========================================================
// USER PROVISIONING SERVICE (Orchestrierung)
// ========================================================

public class UserProvisioningService
{
    private readonly AuthentikUserService _authentik;
    private readonly MatrixAdminService _matrix;
    private readonly IConfiguration _config;

    public UserProvisioningService(
        AuthentikUserService authentik,
        MatrixAdminService matrix,
        IConfiguration config)
    {
        _authentik = authentik;
        _matrix = matrix;
        _config = config;
    }

    public async Task EnsureUserProvisionedAsync(string username, string? displayName, string? email)
    {
        var groupId = _config["Authentik:YouthGroupId"];

        // 1. Authentik: User existiert?
        var user = await _authentik.GetUserAsync(username);

        if (user == null)
        {
            user = await _authentik.CreateUserAsync(
                username,
                displayName,
                email
            );
        }

        // 2. Authentik: User in Gruppe?
        if (!await _authentik.IsUserInGroupAsync(user.Pk, groupId!))
        {
            await _authentik.AddUserToGroupAsync(user.Pk, groupId!);
        }

        // 3. Matrix: User existiert?
        var matrixUserId = $"@{username}:{_matrix.ServerName}";

        if (!await _matrix.UserExistsAsync(matrixUserId))
        {
            await _matrix.CreateUserAsync(
                matrixUserId,
                password: GenerateSecurePassword(), // Matrix braucht zwingend ein Passwort
                displayName: displayName ?? username
            );
        }
    }
    private string GenerateSecurePassword()
    {
        return Guid.NewGuid().ToString("N");
    }
}
