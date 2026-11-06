using System.Globalization;
using System.Text;

namespace SaveTheDate;

public static class Fmt
{
    public static string Size(long n) => n switch
    {
        >= 1L << 30 => $"{n / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{n / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{n / (double)(1L << 10):0} KB",
        _ => $"{n} Byte",
    };
}

/// <summary>Liste als CSV (Trennzeichen „;“, UTF-8 mit BOM – öffnet sich in deutschem Excel direkt richtig).</summary>
public static class Csv
{
    public static string Make(IEnumerable<Item> items)
    {
        static string Q(string s)
        {
            // Zellen, die wie Formeln aussehen, entschärfen (CSV-Injection).
            if (s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s;
            return s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
        static string D(DateTime? d) => d?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";
        static string N(double? v) => v?.ToString("F6", CultureInfo.InvariantCulture).Replace('.', ',') ?? "";
        var sb = new StringBuilder("﻿Pfad;Name;Art;Größe (Byte);Erstellt;Geändert;Aufnahme;Breite;Länge\r\n");
        foreach (var i in items)
            sb.Append(string.Join(';', Q(i.FullPath), Q(i.Name), i.Kind, i.IsDir ? "" : i.Size.ToString(CultureInfo.InvariantCulture),
                D(i.Created), D(i.Modified), D(i.Taken), N(i.Lat), N(i.Lon))).Append("\r\n");
        return sb.ToString();
    }
}
