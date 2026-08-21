using BotApi.Models;

namespace BotApi.Services;

public class MessageService
{
    private readonly AuthentikService _authentik;
    private readonly MatrixService _matrix;
    private readonly SmsService _sms;

    public MessageService(AuthentikService authentik, MatrixService matrix, SmsService sms)
    {
        _authentik = authentik;
        _matrix = matrix;
        _sms = sms;
    }

    public async Task SendToYouthAsync(string message)
    {
        var users = await _authentik.GetYouthUsersAsync();

        foreach (var user in users)
        {
            var matrixId = $"@{user.Username}:{_matrix.ServerName}";
            await _matrix.SendPrivateMessageAsync(matrixId, message);

            if (user.SmsEnabled && !string.IsNullOrEmpty(user.PhoneNumber))
            {
                await _sms.SendSmsAsync(user.PhoneNumber, message);
            }
        }
    }
}