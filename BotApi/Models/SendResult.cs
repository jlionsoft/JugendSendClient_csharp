namespace BotApi.Models;

public class SendResult
{
    public bool NextcloudSuccess { get; set; }
    public int MatrixSent { get; set; }
    public int SmsSent { get; set; }
    // Neue Felder für Teilergebnis-Reporting
    public int MatrixTotal { get; set; }
    public int SmsTotal { get; set; }

    // Optional: Listen der fehlgeschlagenen Empfänger (z.B. Matrix-IDs / Telefonnummern)
    public string[]? MatrixFailed { get; set; }
    public string[]? SmsFailed { get; set; }
}
