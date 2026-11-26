namespace Synesthesia_Audio.Core;

/// <summary>
/// The "work number" a DLsite response carries: sometimes "RJ123456", sometimes "123456", sometimes a bare
/// product id.
///
/// The comparison appeared three times inside the JSON parser, each time as an inline <c>Regex.Replace(…, "\D", "")</c>
/// (DUP-08). One of the three could be edited without the others, and a work number that matched in one path but
/// not another made the same request succeed or fail depending on which shape the response had.
/// </summary>
public static class DlsiteWorkNumber
{
    /// <summary>
    /// The digits of <paramref name="value"/>, which is what actually identifies the work. Returns empty when
    /// there are none.
    /// </summary>
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

    /// <summary>
    /// True when <paramref name="value"/> refers to the same work as <paramref name="expected"/>. Both are reduced
    /// to digits first, so "RJ123456" and "123456" compare equal.
    /// </summary>
    public static bool RefersToSameWork(string? value, string? expected)
    {
        var left = DigitsOf(value);
        if (left.Length == 0) return false;

        return string.Equals(left, DigitsOf(expected), StringComparison.Ordinal);
    }
}
