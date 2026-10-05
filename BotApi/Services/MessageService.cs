using BotApi.Models;
using System.Text;

namespace BotApi.Services;

public class MessageService
{
    private readonly AuthentikService _authentik;
    private readonly MatrixService _matrix;
    private readonly SmsService _sms;

    private readonly INextcloudTalkService _ncTalk;

    public MessageService(AuthentikService authentik, MatrixService matrix, SmsService sms, INextcloudTalkService ncTalk)
    {
        _authentik = authentik;
        _matrix = matrix;
        _sms = sms;
        _ncTalk = ncTalk;
    }

    public async Task<SendResult> SendToYouthAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new SendResult { NextcloudSuccess = false, MatrixSent = 0, SmsSent = 0 };
        }

        var nextcloudMessage = await _ncTalk.SendMessageAsync(message);

        var users = await _authentik.GetYouthUsersAsync();

        var matrixCount = 0;
        var smsCount = 0;

        foreach (var user in users)
        {
            var matrixId = $"@{user.Username}:{_matrix.ServerName}";
            try
            {
                var matrixOk = await _matrix.SendPrivateMessageAsync(matrixId, message);
                if (matrixOk) matrixCount++;
            }
            catch
            {
                // ignore individual failures
            }

            if (user.SmsEnabled && !string.IsNullOrEmpty(user.PhoneNumber))
            {
                try
                {
                    var smsOk = await _sms.SendSmsAsync(user.PhoneNumber, message);
                    if (smsOk) smsCount++;
                }
                catch
                {
                    // ignore
                }
            }
        }

        return new SendResult
        {
            NextcloudSuccess = nextcloudMessage,
            MatrixSent = matrixCount,
            SmsSent = smsCount
        };
    }
}