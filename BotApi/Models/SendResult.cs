namespace BotApi.Models;

public class SendResult
{
    public bool NextcloudSuccess { get; set; }
    public int MatrixSent { get; set; }
    public int SmsSent { get; set; }
}
