using System.IO;
using System.Text;

namespace SaveTheDate;

public sealed class RenameMove
{
    public required Item Item { get; init; }
    public required string Dst { get; init; }
    public string? Skip { get; init; }
    public string Display => Skip ?? Path.GetFileName(Dst);
}

/// <summary>
/// Dateien anhand ihres Datums umbenennen, z. B. <c>{JJJJ}-{MM}-{TT}_{hh}-{mm}-{ss}</c> → <c>2024-07-13_14-22-05.jpg</c>.
/// Platzhalter: JJJJ MM TT hh mm ss (Datum), Name (alter Name ohne Endung), Nr (Zähler, dreistellig).
/// </summary>
public static class Renamer
{
    public const string DefaultTemplate = "{JJJJ}-{MM}-{TT}_{hh}-{mm}-{ss}";

    public static readonly (string Label, string Template)[] Presets =
    [
        ("2024-07-13_14-22-05", DefaultTemplate),
        ("20240713_142205", "{JJJJ}{MM}{TT}_{hhmmss}"),
        ("2024-07-13 Name", "{JJJJ}-{MM}-{TT} {Name}"),
        ("Name 2024-07", "{Name} {JJJJ}-{MM}"),
        ("Foto 001, 002 …", "Foto {Nr}"),
    ];

    static readonly char[] Invalid = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    public static string Render(string template, DateTime? date, string name, int nr, int count)
    {
        var s = template.Replace("{hhmmss}", "{hh}{mm}{ss}");
        if (date is { } d)
            s = s.Replace("{JJJJ}", $"{d.Year:0000}").Replace("{MM}", $"{d.Month:00}").Replace("{TT}", $"{d.Day:00}")
                 .Replace("{hh}", $"{d.Hour:00}").Replace("{mm}", $"{d.Minute:00}").Replace("{ss}", $"{d.Second:00}");
        s = s.Replace("{Name}", name).Replace("{Nr}", nr.ToString("D" + Math.Max(3, count.ToString().Length)));
        // In allen Dateisystemen ungültige Zeichen entfernen.
        return new string(s.Where(c => c >= ' ' && Array.IndexOf(Invalid, c) < 0).ToArray()).Trim().TrimEnd('.');
    }

    static bool UsesDate(string t) => new[] { "{JJJJ}", "{MM}", "{TT}", "{hh}", "{mm}", "{ss}", "{hhmmss}" }.Any(t.Contains);

    /// <summary>Chronologisch sortiert (für den Zähler); Namenskonflikte werden zu „name (2)“.</summary>
    public static List<RenameMove> Plan(IEnumerable<Item> items, string template, DateSource source, bool lowerExt, bool onlyMedia)
    {
        var needDate = UsesDate(template);
        var files = items.Where(i => !i.IsDir && (!onlyMedia || i.IsMedia))
            .OrderBy(i => Organizer.Date(i, source)).ThenBy(i => i.FullPath, StringComparer.OrdinalIgnoreCase).ToList();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<RenameMove>();
        for (var i = 0; i < files.Count; i++)
        {
            var it = files[i];
            RenameMove Skip(string why) { used.Add(it.FullPath); return new() { Item = it, Dst = it.FullPath, Skip = why }; }
            var date = Organizer.Date(it, source);
            if (needDate && date == null) { list.Add(Skip("Kein Datum")); continue; }
            var baseName = Render(template, date, Path.GetFileNameWithoutExtension(it.Name), i + 1, files.Count);
            if (baseName.Length == 0) { list.Add(Skip("Leerer Name")); continue; }
            var ext = Path.GetExtension(it.Name);
            if (lowerExt) ext = ext.ToLowerInvariant();
            var dir = Path.GetDirectoryName(it.FullPath)!;
            string Url(int n) => Path.Combine(dir, (n == 1 ? baseName : $"{baseName} ({n})") + ext);
            var dst = Url(1);
            if (string.Equals(dst, it.FullPath, StringComparison.Ordinal)) { list.Add(Skip("Name bleibt gleich")); continue; }
            for (var n = 1; ; dst = Url(++n))
            {
                if (string.Equals(dst, it.FullPath, StringComparison.OrdinalIgnoreCase)) break;     // nur Groß-/Kleinschreibung
                if (!used.Contains(dst) && !File.Exists(dst) && !Directory.Exists(dst)) break;
            }
            used.Add(dst);
            list.Add(new RenameMove { Item = it, Dst = dst });
        }
        return list;
    }

    public static (List<Op> Ops, List<string> Errors) Run(IEnumerable<RenameMove> plan, Action<double>? progress)
    {
        var all = plan.ToList();
        List<Op> ops = []; List<string> errors = [];
        for (var i = 0; i < all.Count; i++)
        {
            progress?.Invoke((double)i / Math.Max(all.Count, 1));
            if (all[i].Skip != null) continue;
            try { File.Move(all[i].Item.FullPath, all[i].Dst); ops.Add(new Op(all[i].Item.FullPath, all[i].Dst, false)); }
            catch (Exception ex) { errors.Add($"{all[i].Item.Name}: {ex.Message}"); }
        }
        return (ops, errors);
    }

    public static List<string> Undo(IEnumerable<Op> ops)
    {
        List<string> errors = [];
        foreach (var op in ops.Reverse())
        {
            try { File.Move(op.Dst, op.Src); }
            catch (Exception ex) { errors.Add($"{Path.GetFileName(op.Dst)}: {ex.Message}"); }
        }
        return errors;
    }
}
