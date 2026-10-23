using System.IO;

namespace SaveTheDate;

public enum Pattern { YearMonth, YearMonthDay, Flat }       // JJJJ/MM, JJJJ/MM/TT, JJJJ-MM

public enum DateSource { Taken, Modified, Created }         // Aufnahme (sonst Änderung), Änderung, Erstellung

public sealed class Move
{
    public required Item Item { get; init; }
    public required string Dst { get; init; }
    public string Rel { get; init; } = "";
    public string? Skip { get; init; }
    public string Display => Skip ?? Rel;
}

public sealed record Op(string Src, string Dst, bool Copied);

public sealed record RunResult(List<Op> Ops, List<string> Errors, int Skipped, int RemovedDirs);

public static class Organizer
{
    public static string Format(Pattern p, DateTime d) => p switch
    {
        Pattern.YearMonthDay => Path.Combine($"{d.Year:0000}", $"{d.Month:00}", $"{d.Day:00}"),
        Pattern.Flat => $"{d.Year:0000}-{d.Month:00}",
        _ => Path.Combine($"{d.Year:0000}", $"{d.Month:00}"),
    };

    static DateTime Date(Item i, DateSource s) => s switch
    {
        DateSource.Created => i.Created,
        DateSource.Modified => i.Modified,
        _ => i.Taken ?? i.Modified,
    };

    /// <summary>Zielpfad pro Datei bestimmen. Namenskonflikte: identischer Inhalt → überspringen, sonst " (2)", " (3)" …</summary>
    public static List<Move> Plan(IEnumerable<Item> items, string root, Pattern pattern, DateSource source, bool onlyMedia)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<Move>();
        foreach (var it in items.Where(i => !i.IsDir && (!onlyMedia || i.IsMedia)).OrderBy(i => i.FullPath, StringComparer.OrdinalIgnoreCase))
        {
            var dir = Path.Combine(root, Format(pattern, Date(it, source)));
            var dst = Path.Combine(dir, it.Name);
            Move Make(string? skip = null) => new() { Item = it, Dst = dst, Rel = Path.GetRelativePath(root, dst), Skip = skip };
            if (string.Equals(Path.GetFullPath(dst), it.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                used.Add(dst);
                list.Add(Make("Bereits am Ziel"));
                continue;
            }
            var name = Path.GetFileNameWithoutExtension(it.Name);
            var ext = Path.GetExtension(it.Name);
            string? skipReason = null;
            for (var n = 1; ; )
            {
                var taken = used.Contains(dst);
                var exists = File.Exists(dst);
                if (!taken && !exists) break;
                if (!taken && exists && SameContent(it, dst)) { skipReason = "Identische Datei existiert bereits"; break; }
                dst = Path.Combine(dir, $"{name} ({++n}){ext}");
            }
            used.Add(dst);
            list.Add(Make(skipReason));
        }
        return list;
    }

    static bool SameContent(Item it, string other) =>
        new FileInfo(other).Length == it.Size && Dupes.Hash(it.FullPath, null) is { } a && a == Dupes.Hash(other, null);

    public static RunResult Run(IEnumerable<Move> plan, bool copy, bool removeEmpty, IEnumerable<string> roots, Action<double>? progress)
    {
        var all = plan.ToList();
        List<Op> ops = []; List<string> errors = [];
        var skipped = 0;
        var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < all.Count; i++)
        {
            progress?.Invoke((double)i / Math.Max(all.Count, 1));
            var m = all[i];
            if (m.Skip != null) { skipped++; continue; }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(m.Dst)!);
                if (copy)
                {
                    File.Copy(m.Item.FullPath, m.Dst);
                    File.SetCreationTime(m.Dst, m.Item.Created);
                    File.SetLastWriteTime(m.Dst, m.Item.Modified);
                }
                else
                {
                    File.Move(m.Item.FullPath, m.Dst);
                    parents.Add(Path.GetDirectoryName(m.Item.FullPath)!);
                }
                ops.Add(new Op(m.Item.FullPath, m.Dst, copy));
            }
            catch (Exception ex) { errors.Add($"{m.Item.Name}: {ex.Message}"); }
        }
        return new RunResult(ops, errors, skipped, removeEmpty ? PruneEmpty(parents, roots) : 0);
    }

    public static List<string> Undo(IEnumerable<Op> ops, string root)
    {
        List<string> errors = [];
        var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var op in ops.Reverse())
        {
            try
            {
                if (op.Copied) Recycle.Delete(op.Dst, false);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(op.Src)!);
                    File.Move(op.Dst, op.Src);
                }
                parents.Add(Path.GetDirectoryName(op.Dst)!);
            }
            catch (Exception ex) { errors.Add($"{Path.GetFileName(op.Dst)}: {ex.Message}"); }
        }
        PruneEmpty(parents, [root]);
        return errors;
    }

    /// <summary>Entfernt leere Ordner (nur desktop.ini o. ä. darin) – ausschließlich unterhalb der Wurzeln, nie die Wurzel selbst.</summary>
    public static int PruneEmpty(IEnumerable<string> dirs, IEnumerable<string> roots)
    {
        var rs = roots.Select(r => Path.TrimEndingDirectorySeparator(Path.GetFullPath(r))).ToList();
        var removed = 0;
        foreach (var d in dirs.OrderByDescending(x => x.Length))
        {
            var cur = Path.TrimEndingDirectorySeparator(Path.GetFullPath(d));
            while (rs.Any(r => cur.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                if (Directory.Exists(cur))
                {
                    if (!Directory.EnumerateFileSystemEntries(cur).All(e => Cleanup.IsJunk(Path.GetFileName(e)))) break;
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(cur, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
                        Directory.Delete(cur, true);
                        removed++;
                    }
                    catch { break; }
                }
                cur = Path.GetDirectoryName(cur)!;
            }
        }
        return removed;
    }
}
