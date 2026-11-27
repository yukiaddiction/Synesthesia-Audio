namespace Synesthesia_Audio.Core;

/// <summary>
/// DLsite responses carry the work number as "RJ123456", "123456" or a bare product id; comparing digits only
/// makes every shape match the same way.
/// </summary>
public static class DlsiteWorkNumber
{
    public static string DigitsOf(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var digits = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsAsciiDigit(c)) digits.Append(c);
        }

        return digits.ToString();
    }

    public static bool RefersToSameWork(string? value, string? expected)
    {
        var left = DigitsOf(value);
        if (left.Length == 0) return false;

        return string.Equals(left, DigitsOf(expected), StringComparison.Ordinal);
    }
}
