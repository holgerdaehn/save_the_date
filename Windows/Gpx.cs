using System.IO;
using System.Globalization;
using System.Xml.Linq;

namespace SaveTheDate;

public readonly record struct GpxPoint(DateTimeOffset T, double Lat, double Lon);

/// <summary>GPS-Tracklogs (GPX) lesen und Fotos anhand der Aufnahmezeit einen Standort zuordnen.</summary>
public static class Gpx
{
    /// <summary>Alle Punkte mit Zeitstempel (trkpt, rtept, wpt), nach Zeit sortiert. Unlesbare Dateien werden ausgelassen.</summary>
    public static List<GpxPoint> Parse(IEnumerable<string> paths)
    {
        var pts = new List<GpxPoint>();
        foreach (var p in paths)
        {
            try
            {
                foreach (var e in XDocument.Load(p).Descendants().Where(e => e.Name.LocalName is "trkpt" or "rtept" or "wpt"))
                {
                    if (e.Elements().FirstOrDefault(x => x.Name.LocalName == "time")?.Value is not { } ts
                        || !DateTimeOffset.TryParse(ts.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t)
                        || !double.TryParse((string?)e.Attribute("lat"), NumberStyles.Float, CultureInfo.InvariantCulture, out var la)
                        || !double.TryParse((string?)e.Attribute("lon"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lo)
                        || Math.Abs(la) > 90 || Math.Abs(lo) > 180) continue;
                    pts.Add(new GpxPoint(t, la, lo));
                }
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException) { }
        }
        return pts.OrderBy(p => p.T).ToList();
    }

    /// <summary>
    /// Standort zur Zeit <paramref name="t"/>: zwischen zwei Trackpunkten interpoliert, sonst der nächste Punkt –
    /// jeweils nur, wenn er höchstens <paramref name="maxGap"/> entfernt ist.
    /// </summary>
    public static (double Lat, double Lon)? Locate(IReadOnlyList<GpxPoint> p, DateTimeOffset t, TimeSpan maxGap)
    {
        int lo = 0, hi = p.Count;
        while (lo < hi) { var m = (lo + hi) / 2; if (p[m].T < t) lo = m + 1; else hi = m; }
        GpxPoint? next = lo < p.Count ? p[lo] : null, prev = lo > 0 ? p[lo - 1] : null;
        var dn = next is { } n ? n.T - t : TimeSpan.MaxValue;
        var dp = prev is { } q ? t - q.T : TimeSpan.MaxValue;
        if (prev is { } a && next is { } b && dp <= maxGap && dn <= maxGap)
        {
            var span = (b.T - a.T).TotalSeconds;
            var w = span > 0 ? dp.TotalSeconds / span : 0;
            return (a.Lat + (b.Lat - a.Lat) * w, a.Lon + (b.Lon - a.Lon) * w);
        }
        if (next is { } nb && dn <= dp && dn <= maxGap) return (nb.Lat, nb.Lon);
        if (prev is { } pa && dp <= maxGap) return (pa.Lat, pa.Lon);
        return null;
    }

    /// <summary>Aufnahmezeit (Uhrzeit der Kamera) als Zeitpunkt; <paramref name="cameraUtcHours"/> = null: Zeitzone dieses PCs.</summary>
    public static DateTimeOffset Instant(DateTime taken, double? cameraUtcHours) =>
        cameraUtcHours is { } h ? new DateTimeOffset(DateTime.SpecifyKind(taken, DateTimeKind.Unspecified), TimeSpan.FromHours(h)) : new DateTimeOffset(taken);
}
