using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;

namespace SaveTheDate;

public enum Keep { Oldest, Newest, Shortest, First }

public static class Dupes
{
    const int Partial = 64 * 1024;

    /// <summary>
    /// Gruppen byte-identischer Dateien: gleiche Größe, gleicher Anfangs-Hash, gleicher Gesamt-Hash (SHA-256).
    /// Dateinamen spielen keine Rolle. Nur Dateien gleicher Größe werden überhaupt gelesen.
    /// </summary>
    public static List<List<Item>> Find(IEnumerable<Item> items, Action<double>? progress, CancellationToken ct)
    {
        var sized = items.Where(i => !i.IsDir && i.Size > 0).GroupBy(i => i.Size)
            .Where(g => g.Count() > 1).Select(g => g.ToList()).ToList();
        var heads = Regroup(sized, Partial, p => progress?.Invoke(p * 0.3), ct);
        // Dateien bis 64 KB sind mit dem Anfangs-Hash bereits vollständig verglichen.
        var done = heads.Where(g => g[0].Size <= Partial);
        var full = Regroup(heads.Where(g => g[0].Size > Partial).ToList(), null, p => progress?.Invoke(0.3 + p * 0.7), ct);
        return done.Concat(full)
            .Select(g => g.OrderBy(i => i.FullPath, StringComparer.OrdinalIgnoreCase).ToList())
            .OrderByDescending(g => g[0].Size * g.Count).ToList();
    }

    static List<List<Item>> Regroup(List<List<Item>> groups, int? limit, Action<double> progress, CancellationToken ct)
    {
        var all = groups.SelectMany(g => g).ToList();
        var hashes = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var n = 0;
        Parallel.ForEach(all, new ParallelOptions { CancellationToken = ct }, it =>
        {
            if (Hash(it.FullPath, limit) is { } h) hashes[it.FullPath] = h;
            progress((double)Interlocked.Increment(ref n) / all.Count);
        });
        return groups.SelectMany(g => g.GroupBy(i => hashes.GetValueOrDefault(i.FullPath, i.FullPath), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1).Select(x => x.ToList())).ToList();
    }

    /// <summary>SHA-256 der ersten <paramref name="limit"/> Bytes (null = ganze Datei).</summary>
    public static string? Hash(string path, int? limit)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16, FileOptions.SequentialScan);
            using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buf = new byte[1 << 20];
            var left = limit ?? int.MaxValue;
            int r;
            while (left > 0 && (r = fs.Read(buf, 0, Math.Min(buf.Length, left))) > 0) { h.AppendData(buf, 0, r); left -= r; }
            return Convert.ToHexString(h.GetHashAndReset());
        }
        catch { return null; }
    }

    public static Item Keeper(List<Item> g, Keep rule)
    {
        static DateTime Age(Item i) => i.Created < i.Modified ? i.Created : i.Modified;
        var cmp = StringComparer.OrdinalIgnoreCase;
        return rule switch
        {
            Keep.Oldest => g.OrderBy(Age).ThenBy(i => i.FullPath.Length).ThenBy(i => i.FullPath, cmp).First(),
            Keep.Newest => g.OrderByDescending(Age).ThenBy(i => i.FullPath.Length).ThenBy(i => i.FullPath, cmp).First(),
            Keep.Shortest => g.OrderBy(i => i.FullPath.Length).ThenBy(i => i.FullPath, cmp).First(),
            _ => g[0],
        };
    }
}
