using BotApi.Models;
using System.Text;
using System.Linq;

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
            return new SendResult
            {
                NextcloudSuccess = false,
                MatrixSent = 0,
                SmsSent = 0,
                MatrixTotal = 0,
                SmsTotal = 0,
                MatrixFailed = Array.Empty<string>(),
                SmsFailed = Array.Empty<string>()
            };
        }

        var nextcloudMessage = await _ncTalk.SendMessageAsync(message);

        var users = await _authentik.GetYouthUsersAsync();

        var matrixCount = 0;
        var smsCount = 0;

        var matrixFailed = new List<string>();
        var smsFailed = new List<string>();

        // Matrix total: attempt for every user
        var matrixTotal = users.Count;

        // SMS total: only users with SMS enabled and phone number
        var smsTotal = users.Count(u => u.SmsEnabled && !string.IsNullOrEmpty(u.PhoneNumber));

        // First: send Matrix messages for all users
        foreach (var user in users)
        {
            var matrixId = $"@{user.Username}:{_matrix.ServerName}";
            try
            {
                var matrixOk = await _matrix.SendPrivateMessageAsync(matrixId, message);
                if (matrixOk) matrixCount++;
                else matrixFailed.Add(matrixId);
            }
            catch
            {
                matrixFailed.Add(matrixId);
            }
        }

        // Prüfen, ob SMS-Gateway erreichbar ist, bevor wir mit SMS versenden
        var smsGatewayAvailable = await _sms.IsGatewayAvailableAsync();

        if (!smsGatewayAvailable)
        {
            // Gateway nicht erreichbar: Markiere alle vorgesehenen SMS als fehlgeschlagen und überspringe das Versenden
            smsCount = 0;
            smsFailed.AddRange(users
                .Where(u => u.SmsEnabled && !string.IsNullOrEmpty(u.PhoneNumber))
                .Select(u => u.PhoneNumber!)
            );

            return new SendResult
            {
                NextcloudSuccess = nextcloudMessage,
                MatrixSent = matrixCount,
                SmsSent = smsCount,
                MatrixTotal = matrixTotal,
                SmsTotal = smsTotal,
                MatrixFailed = matrixFailed.ToArray(),
                SmsFailed = smsFailed.ToArray(),
                SmsGatewayAvailable = false
            };
        }

        // Wenn Gateway erreichbar ist: SMS versenden
        foreach (var user in users)
        {
            if (user.SmsEnabled && !string.IsNullOrEmpty(user.PhoneNumber))
            {
                try
                {
                    var smsOk = await _sms.SendSmsAsync(user.PhoneNumber, message);
                    if (smsOk) smsCount++;
                    else smsFailed.Add(user.PhoneNumber!);
                }
                catch
                {
                    smsFailed.Add(user.PhoneNumber!);
                }
            }
        }

        return new SendResult
        {
            NextcloudSuccess = nextcloudMessage,
            MatrixSent = matrixCount,
            SmsSent = smsCount,
            MatrixTotal = matrixTotal,
            SmsTotal = smsTotal,
            MatrixFailed = matrixFailed.ToArray(),
            SmsFailed = smsFailed.ToArray(),
            SmsGatewayAvailable = true
        };
    }
}