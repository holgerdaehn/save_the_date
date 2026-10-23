using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace SaveTheDate;

public sealed record Junk(string Path, bool IsDir, long Size, string Reason);

/// <summary>Löschen in den Papierkorb (nie endgültig).</summary>
public static class Recycle
{
    public static void Delete(string path, bool isDir)
    {
        if (isDir) FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        else FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
    }
}

/// <summary>Überflüssige Systemdateien (desktop.ini, Thumbs.db, .DS_Store, …) und leere Ordner finden.</summary>
public static class Cleanup
{
    static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ds_store", "thumbs.db", "ehthumbs.db", "ehthumbs_vista.db", "desktop.ini", ".picasa.ini", "picasa.ini",
        ".localized", ".directory", "__macosx", ".appledouble", "@eadir", ".spotlight-v100", ".fseventsd", ".temporaryitems",
    };

    /// <summary>Eigene Muster mit * und ? (ohne Groß-/Kleinschreibung), getrennt durch Komma, Semikolon oder Zeilenumbruch.</summary>
    public static List<Regex> Patterns(string text) =>
        text.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => new Regex("^" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase)).ToList();

    public static string? Reason(string name, IReadOnlyList<Regex> extra)
    {
        if (Names.Contains(name) || name.StartsWith("._")) return "Systemdatei";
        return extra.Any(r => r.IsMatch(name)) ? "Eigenes Muster" : null;
    }

    public static bool IsJunk(string name) => Reason(name, []) != null;

    /// <summary>
    /// Durchsucht die Wurzelordner rekursiv (inkl. versteckter/System-Dateien). Reihenfolge: tiefste Einträge zuerst,
    /// damit Dateien vor ihren (dann leeren) Ordnern entfernt werden.
    /// </summary>
    public static List<Junk> Scan(IEnumerable<string> roots, IReadOnlyList<Regex> extra, bool removeEmpty, CancellationToken ct)
    {
        var found = new List<Junk>();
        var eo = new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = true };

        bool Walk(string dir, bool root)
        {
            ct.ThrowIfCancellationRequested();
            var empty = true;
            foreach (var e in new DirectoryInfo(dir).EnumerateFileSystemInfos("*", eo))
            {
                var isDir = e is DirectoryInfo && (e.Attributes & FileAttributes.ReparsePoint) == 0;
                if (Reason(e.Name, extra) is { } r) found.Add(new Junk(e.FullName, e is DirectoryInfo, e is FileInfo f ? f.Length : 0, r));
                else if (isDir) { if (!Walk(e.FullName, false)) empty = false; }
                else empty = false;
            }
            if (empty && !root && removeEmpty) found.Add(new Junk(dir, true, 0, "Leerer Ordner"));
            return empty;
        }

        foreach (var r in roots) Walk(Path.GetFullPath(r), true);
        return found;
    }
}
