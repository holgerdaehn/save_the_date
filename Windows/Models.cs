using System.IO;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace SaveTheDate;

public enum Mode { Fixed, Shift, Copy }

/// <summary>Quelle für „Übernehmen“: woher das neue Datum kommt.</summary>
public enum Origin { Taken, Name, Created, Modified, Oldest }

public enum ItemFilter { All, Photos, Videos, NoTaken, NoGps, HasGps, Differs }

public readonly record struct Plan(DateTime? Created, DateTime? Modified, DateTime? Taken)
{
    public bool IsEmpty => Created == null && Modified == null && Taken == null;
}

public sealed record Settings
{
    public Mode Mode;
    public Origin Origin;
    public bool SetCreated, SetModified, SetTaken;
    public DateTime? Created, Modified, Taken;
    public double Interval;
    public bool Later = true;
    public int Days, Hours, Minutes, Seconds;
    public GeoKind Geo;
    public double? Lat, Lon;
    public bool Valid = true;
}

public sealed record ScanOptions(bool Recursive, bool Dirs, bool Hidden);

public sealed class Item : INotifyPropertyChanged
{
    public required string FullPath { get; init; }
    public bool IsDir { get; init; }
    public bool IsJpeg { get; init; }
    public bool IsPhoto { get; init; }
    public bool IsMovie { get; init; }
    public bool IsMedia => IsPhoto || IsMovie;
    /// <summary>MP4/MOV: Aufnahmedatum im Container les- und schreibbar.</summary>
    public bool IsMp4 => !IsDir && Mp4.Supports(FullPath);
    /// <summary>Aufnahmedatum vorhanden bzw. änderbar (JPEG-EXIF oder Video-Container).</summary>
    public bool CanTake => IsJpeg || IsMp4;
    public string Kind => IsDir ? "Ordner" : IsPhoto ? "Foto" : IsMovie ? "Video" : "Datei";
    public long SizeKey => IsDir ? -1 : Size;
    public string SizeText => IsDir ? "" : Fmt.Size(Size);
    public long Size { get; init; }
    public DateTime Created { get; init; }
    public DateTime Modified { get; init; }
    public DateTime? Taken { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }

    public string Name => System.IO.Path.GetFileName(FullPath.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : FullPath;
    public string Folder => System.IO.Path.GetDirectoryName(FullPath.TrimEnd('\\', '/')) ?? "";
    public string Icon => IsDir ? "" : IsJpeg ? "" : "";   // Segoe MDL2 Assets

    public string CreatedText => F(Created);
    public string ModifiedText => F(Modified);
    public string TakenText => Taken is { } t ? F(t) : "–";
    public string Location => Lat is { } la && Lon is { } lo
        ? string.Create(CultureInfo.InvariantCulture, $"{la:F5}, {lo:F5}") : "–";

    Plan plan;
    public string NewCreated => plan.Created is { } d ? F(d) : "";
    public string NewModified => plan.Modified is { } d ? F(d) : "";
    public string NewTaken => plan.Taken is { } d ? F(d) : "";

    public void SetPlan(Plan p)
    {
        if (p == plan) return;
        plan = p;
        PropertyChanged?.Invoke(this, new(nameof(NewCreated)));
        PropertyChanged?.Invoke(this, new(nameof(NewModified)));
        PropertyChanged?.Invoke(this, new(nameof(NewTaken)));
    }

    static string F(DateTime d) => d.ToString("G");

    public event PropertyChangedEventHandler? PropertyChanged;
}

public static class Planner
{
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    static extern int StrCmpLogicalW(string a, string b);

    public static DateTime? OriginDate(Item i, Origin o) => o switch
    {
        Origin.Taken => i.Taken,
        Origin.Name => NameDate.Parse(i.Name),
        Origin.Created => i.Created,
        Origin.Modified => i.Modified,
        _ => new[] { i.Created, i.Modified, i.Taken ?? DateTime.MaxValue }.Min(),
    };

    /// <summary>Ändert sich der Wert überhaupt? (Sekundengenau – sonst würde „Keine Änderung“ nie erscheinen.)</summary>
    static bool Differs(DateTime? cur, DateTime v) => cur is not { } x || Math.Abs((x - v).TotalSeconds) >= 1;

    public static Dictionary<Item, Plan> Make(IReadOnlyList<Item> targets, Settings s)
    {
        var r = new Dictionary<Item, Plan>(targets.Count);
        if (!s.Valid) return r;
        var order = targets;
        if (s is { Mode: Mode.Fixed, Interval: not 0 })
            order = targets.OrderBy(i => i.Name, Comparer<string>.Create(StrCmpLogicalW)).ToList();
        var off = TimeSpan.FromSeconds((((s.Days * 24L + s.Hours) * 60 + s.Minutes) * 60 + s.Seconds) * (s.Later ? 1 : -1));
        for (var i = 0; i < order.Count; i++)
        {
            var it = order[i];
            DateTime? c = null, m = null, t = null;
            switch (s.Mode)
            {
                case Mode.Fixed:
                    var d = TimeSpan.FromSeconds(i * s.Interval);
                    if (s.SetCreated) c = s.Created + d;
                    if (s.SetModified) m = s.Modified + d;
                    if (s.SetTaken && it.CanTake) t = s.Taken + d;
                    break;
                case Mode.Shift:
                    if (s.SetCreated) c = it.Created + off;
                    if (s.SetModified) m = it.Modified + off;
                    if (s.SetTaken && it.CanTake && it.Taken is { } tk) t = tk + off;
                    break;
                case Mode.Copy:
                    if (OriginDate(it, s.Origin) is not { } v) break;
                    if (s.SetCreated && Differs(it.Created, v)) c = v;
                    if (s.SetModified && Differs(it.Modified, v)) m = v;
                    if (s.SetTaken && it.CanTake && Differs(it.Taken, v)) t = v;
                    break;
            }
            r[it] = new Plan(c, m, t);
        }
        return r;
    }
}

public static class Scanner
{
    static readonly HashSet<string> PhotoExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".heic", ".heif", ".avif",
        ".dng", ".cr2", ".cr3", ".nef", ".arw", ".orf", ".rw2", ".raf",
    };

    static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mov", ".mp4", ".m4v", ".avi", ".mkv", ".3gp", ".3g2", ".mts", ".m2ts", ".wmv",
    };

    public static List<Item> Run(IEnumerable<string> sources, ScanOptions o, CancellationToken ct)
    {
        var found = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        void Add(FileSystemInfo fi)
        {
            var dir = fi is DirectoryInfo;
            if (dir && !o.Dirs) return;
            var p = fi.FullName;
            found[p] = new Item
            {
                FullPath = p, IsDir = dir,
                IsPhoto = !dir && PhotoExt.Contains(Path.GetExtension(p)), IsMovie = !dir && VideoExt.Contains(Path.GetExtension(p)),
                Size = fi is FileInfo f ? f.Length : 0,
                IsJpeg = !dir && Path.GetExtension(p).ToLowerInvariant() is ".jpg" or ".jpeg" or ".jpe",
                Created = fi.CreationTime, Modified = fi.LastWriteTime,
            };
        }

        var eo = new EnumerationOptions
        {
            RecurseSubdirectories = o.Recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = o.Hidden ? 0 : FileAttributes.Hidden | FileAttributes.System,
        };
        foreach (var s in sources)
        {
            ct.ThrowIfCancellationRequested();
            if (Directory.Exists(s))
            {
                var root = new DirectoryInfo(s);
                Add(root);
                foreach (var fi in root.EnumerateFileSystemInfos("*", eo)) { ct.ThrowIfCancellationRequested(); Add(fi); }
            }
            else if (File.Exists(s)) Add(new FileInfo(s));
        }

        var list = found.Values.ToList();
        Parallel.ForEach(list.Where(i => i.CanTake), new ParallelOptions { CancellationToken = ct }, it =>
        {
            if (it.IsJpeg) { var m = ExifJpeg.Read(it.FullPath); it.Taken = m.Taken; it.Lat = m.Lat; it.Lon = m.Lon; }
            else it.Taken = Mp4.Read(it.FullPath);
        });
        return list;
    }
}

public static class Writer
{
    public static void Apply(Item it, Plan p, GeoEdit geo)
    {
        var meta = false;
        if (it.IsJpeg && (p.Taken != null || geo.Kind != GeoKind.Keep)) { ExifJpeg.Update(it.FullPath, p.Taken, geo); meta = true; }
        else if (it.IsMp4 && p.Taken is { } tk) { Mp4.Write(it.FullPath, tk); meta = true; }
        if (!meta && p.Created == null && p.Modified == null) return;
        // Beim Schreiben der Metadaten ändert sich das Änderungsdatum: gewünschte (oder ursprüngliche) Werte setzen.
        var c = p.Created ?? it.Created;
        var m = p.Modified ?? it.Modified;
        if (it.IsDir) { Directory.SetCreationTime(it.FullPath, c); Directory.SetLastWriteTime(it.FullPath, m); }
        else { File.SetCreationTime(it.FullPath, c); File.SetLastWriteTime(it.FullPath, m); }
    }
}

/// <summary>Zustand vor einer Änderung (Datei-Daten, Aufnahmedatum, Standort) für „Rückgängig“.</summary>
public sealed record UndoRec(DateTime Created, DateTime Modified, DateTime? Taken, double? Lat, double? Lon, bool TouchedTaken, bool TouchedGeo)
{
    /// <summary>Was geschrieben werden muss, um den Zustand wiederherzustellen.</summary>
    public (Plan Plan, GeoEdit Geo) Restore() =>
        (new Plan(Created, Modified, TouchedTaken ? Taken : null),
         !TouchedGeo ? new GeoEdit(GeoKind.Keep) : Lat is { } la ? new GeoEdit(GeoKind.Set, la, Lon ?? 0) : new GeoEdit(GeoKind.Remove));
}

public static class ItemFilterExt
{
    public static bool Matches(this ItemFilter f, Item i) => f switch
    {
        ItemFilter.Photos => i.IsPhoto,
        ItemFilter.Videos => i.IsMovie,
        ItemFilter.NoTaken => i.CanTake && i.Taken == null,
        ItemFilter.NoGps => i.IsJpeg && i.Lat == null,
        ItemFilter.HasGps => i.Lat != null,
        ItemFilter.Differs => i.Taken is { } t && (Math.Abs((i.Created - t).TotalSeconds) > 2 || Math.Abs((i.Modified - t).TotalSeconds) > 2),
        _ => true,
    };
}
