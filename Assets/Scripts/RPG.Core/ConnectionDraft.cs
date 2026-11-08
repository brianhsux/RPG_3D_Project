/// <summary>
/// Connection fields stay as typed until Host/Client is pressed.
/// Parsing on every GUI frame was resetting the port while the player edited it.
/// </summary>
public static class ConnectionDraft
{
    public const string DefaultAddress = "127.0.0.1";
    public const int DefaultPort = 7777;

    public static string NormalizeAddress(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DefaultAddress;
        return raw.Trim();
    }

    public static bool TryParsePort(string raw, out int port)
    {
        port = DefaultPort;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (!int.TryParse(raw.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value))
            return false;
        if (value < 1 || value > 65535) return false;
        port = value;
        return true;
    }
}
