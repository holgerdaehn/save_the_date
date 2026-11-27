using System.Globalization;
using System.Text.RegularExpressions;

namespace SaveTheDate;

/// <summary>
/// Datum aus Dateinamen wie <c>IMG-20190713-WA0001.jpg</c>, <c>PXL_20210101_120533123.jpg</c>,
/// <c>Bildschirmfoto 2020-02-18 um 14.22.05.png</c> oder <c>signal-1612345678901.jpg</c>.
/// Ohne Uhrzeit im Namen gilt 12:00:00 (so verrutscht das Datum nicht durch Zeitzonen).
/// </summary>
public static partial class NameDate
{
    [GeneratedRegex(@"(?<!\d)((?:19|20)\d\d)[-_.]?(0[1-9]|1[0-2])[-_.]?(0[1-9]|[12]\d|3[01])(?:(?:[-_ T.]|\s(?:at|um)\s)?([01]\d|2[0-3])[-_.:]?([0-5]\d)[-_.:]?([0-5]\d)\d{0,3})?(?!\d)")]
    private static partial Regex DateRx();

    [GeneratedRegex(@"(?<!\d)(1\d{12}|1\d{9})(?!\d)")]
    private static partial Regex EpochRx();

    public static DateTime? Parse(string name)
    {
        if (DateRx().Match(name) is { Success: true } m)
        {
            int N(int i, int def) => m.Groups[i].Success ? int.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture) : def;
            var y = N(1, 0);
            try
            {
                if (y >= 1900 && y <= DateTime.Now.Year + 1) return new DateTime(y, N(2, 1), N(3, 1), N(4, 12), N(5, 0), N(6, 0));
            }
            catch (ArgumentOutOfRangeException) { }       // z. B. 30. Februar
        }
        if (EpochRx().Match(name) is { Success: true } e)
        {
            var v = long.Parse(e.Value, CultureInfo.InvariantCulture);
            return (e.Value.Length == 13 ? DateTimeOffset.FromUnixTimeMilliseconds(v) : DateTimeOffset.FromUnixTimeSeconds(v)).LocalDateTime;
        }
        return null;
    }
}
