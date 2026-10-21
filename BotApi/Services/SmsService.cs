namespace BotApi.Services
{
    public class SmsService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly ILogger<SmsService> _logger;

        public SmsService(IConfiguration config, HttpClient httpClient, ILogger<SmsService> logger)
        {
            _config = config;
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Prüft kurz, ob das SMS-Gateway erreichbar ist. Liefert true wenn erreichbar.
        /// Diese Methode sollte schnell fehlschlagen (kurzes Timeout).
        /// </summary>
        public async Task<bool> IsGatewayAvailableAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, Gateway);
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var response = await _httpClient.SendAsync(request, cts.Token);

                // Wenn der Server erreichbar ist, akzeptieren wir 2xx sowie 3xx/4xx als "erreichbar" — Fehler 5xx deutet auf Serverproblem hin
                if ((int)response.StatusCode >= 500)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private string Gateway =>
            _config["SMS:Gateway"]
            ?? throw new InvalidOperationException("SMS gateway URL is not configured");

        private string Username =>
            _config["SMS:Username"]
            ?? throw new InvalidOperationException("SMS username is not configured");

        private string Password =>
            _config["SMS:Password"]
            ?? throw new InvalidOperationException("SMS password is not configured");


        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("SMS-Nachricht ist leer.");
                return false;
            }

            List<string> parts;

            try
            {
                parts = SplitSmsMessage(message);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    "SMS an {PhoneNumber} nicht versendet: {Reason}",
                    phoneNumber,
                    ex.Message);

                return false;
            }

            _logger.LogInformation(
                "SMS an {PhoneNumber}: Versand in {PartCount} Teil(en).",
                phoneNumber,
                parts.Count);

            for (int i = 0; i < parts.Count; i++)
            {
                string part = $"[{i + 1}] {parts[i]}";

                bool success = await SendSingleSmsAsync(
                    phoneNumber,
                    part);

                if (!success)
                {
                    _logger.LogError(
                        "SMS an {PhoneNumber}: Teil {PartNumber}/{PartCount} fehlgeschlagen. Versand abgebrochen.",
                        phoneNumber,
                        i + 1,
                        parts.Count);

                    return false;
                }

                _logger.LogInformation(
                    "SMS an {PhoneNumber}: Teil {PartNumber}/{PartCount} vom Gateway angenommen.",
                    phoneNumber,
                    i + 1,
                    parts.Count);

                // Modem zwischen zwei SMS kurz entlasten.
                if (i < parts.Count - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }

            return true;
        }

        private async Task<bool> SendSingleSmsAsync(
            string phoneNumber,
            string message)
        {
            try
            {
                var credentials = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(
                        $"{Username}:{Password}"));

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{Gateway.TrimEnd('/')}/rest/tool/sms/send");

                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Basic",
                        credentials);

                var data = new Dictionary<string, string>
                {
                    ["port"] = "lte1",
                    ["phone-number"] = phoneNumber,
                    ["message"] = message
                };

                request.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(data),
                    System.Text.Encoding.UTF8,
                    "application/json");

                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();

                    _logger.LogWarning(
                        "SMS-Gateway: HTTP {StatusCode}. Antwort: {Body}",
                        (int)response.StatusCode,
                        body);

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim SMS-Gateway-Aufruf.");
                return false;
            }
        }

        private static List<string> SplitSmsMessage(string message)
        {
            const int maxSlots = 150;

            const string basic =
                "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./" +
                "0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿" +
                "abcdefghijklmnopqrstuvwxyzäöñüà";

            const string extended = "^{}\\[~]|€";

            foreach (char c in message)
            {
                if (basic.Contains(c) || extended.Contains(c))
                    continue;

                throw new ArgumentException(
                    $"Nicht unterstütztes GSM-7-Zeichen: U+{(int)c:X4} ('{c}').");
            }

            int Slots(string value)
            {
                int count = 0;

                foreach (char c in value)
                    count += extended.Contains(c) ? 2 : 1;

                return count;
            }

            var result = new List<string>();
            int start = 0;

            while (start < message.Length)
            {
                int end = start;
                int lastWhitespace = -1;
                int slots = 0;

                while (end < message.Length)
                {
                    int charSlots = Slots(message[end].ToString());

                    if (slots + charSlots > maxSlots)
                        break;

                    slots += charSlots;

                    if (char.IsWhiteSpace(message[end]))
                        lastWhitespace = end;

                    end++;
                }

                if (end == message.Length)
                {
                    result.Add(message[start..end].Trim());
                    break;
                }

                if (end == start)
                {
                    throw new ArgumentException(
                        "Ein Zeichen überschreitet die zulässige Nachrichtenlänge.");
                }

                // Wenn möglich an einer Wortgrenze trennen.
                int split = lastWhitespace > start
                    ? lastWhitespace
                    : end;

                string part = message[start..split].Trim();

                if (part.Length == 0)
                {
                    start = split + 1;
                    continue;
                }

                result.Add(part);

                start = split;
                while (start < message.Length &&
                       char.IsWhiteSpace(message[start]))
                {
                    start++;
                }
            }

            return result;
        }
    }
}
