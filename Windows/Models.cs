using System.IO;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace SaveTheDate;

public enum Mode { Fixed, Shift, Exif }

public readonly record struct Plan(DateTime? Created, DateTime? Modified, DateTime? Taken)
{
    public bool IsEmpty => Created == null && Modified == null && Taken == null;
}

public sealed record Settings
{
    public Mode Mode;
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
    public bool IsMedia { get; init; }
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
                    if (s.SetTaken && it.IsJpeg) t = s.Taken + d;
                    break;
                case Mode.Shift:
                    if (s.SetCreated) c = it.Created + off;
                    if (s.SetModified) m = it.Modified + off;
                    if (s.SetTaken && it.IsJpeg && it.Taken is { } tk) t = tk + off;
                    break;
                case Mode.Exif:
                    if (it.Taken is { } x) { if (s.SetCreated) c = x; if (s.SetModified) m = x; }
                    break;
            }
            r[it] = new Plan(c, m, t);
        }
        return r;
    }
}

public static class Scanner
{
    static readonly HashSet<string> MediaExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".heic", ".heif", ".avif",
        ".dng", ".cr2", ".cr3", ".nef", ".arw", ".orf", ".rw2", ".raf",
        ".mov", ".mp4", ".m4v", ".avi", ".mkv", ".3gp", ".mts", ".m2ts", ".wmv",
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
                FullPath = p, IsDir = dir, IsMedia = !dir && MediaExt.Contains(Path.GetExtension(p)),
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
        Parallel.ForEach(list.Where(i => i.IsJpeg), new ParallelOptions { CancellationToken = ct }, it =>
        {
            var m = ExifJpeg.Read(it.FullPath);
            it.Taken = m.Taken; it.Lat = m.Lat; it.Lon = m.Lon;
        });
        return list;
    }
}

public static class Writer
{
    public static void Apply(Item it, Plan p, GeoEdit geo)
    {
        var meta = it.IsJpeg && (p.Taken != null || geo.Kind != GeoKind.Keep);
        if (meta) ExifJpeg.Update(it.FullPath, p.Taken, geo);
        if (!meta && p.Created == null && p.Modified == null) return;
        // Beim Schreiben der Metadaten ändert sich das Änderungsdatum: gewünschte (oder ursprüngliche) Werte setzen.
        var c = p.Created ?? it.Created;
        var m = p.Modified ?? it.Modified;
        if (it.IsDir) { Directory.SetCreationTime(it.FullPath, c); Directory.SetLastWriteTime(it.FullPath, m); }
        else { File.SetCreationTime(it.FullPath, c); File.SetLastWriteTime(it.FullPath, m); }
    }
}
