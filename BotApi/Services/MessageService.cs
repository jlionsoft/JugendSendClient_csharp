using BotApi.Models;

namespace BotApi.Services;

public class MessageService
{
    private readonly AuthentikService _authentik;
    private readonly MatrixService _matrix;

    public MessageService(AuthentikService authentik, MatrixService matrix)
    {
        _authentik = authentik;
        _matrix = matrix;
    }

    public async Task SendToYouthAsync(string message)
    {
        var users = await _authentik.GetYouthUsersAsync();

        foreach (var user in users)
        {
            var matrixId = $"@{user.Username}:{_matrix.ServerName}";
            await _matrix.SendPrivateMessageAsync(matrixId, message);
        }
    }
}