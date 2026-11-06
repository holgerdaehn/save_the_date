using System.Collections.Concurrent;
using System.Numerics;

namespace SaveTheDate;

public enum Sensitivity { Strict = 3, Normal = 6, Loose = 10 }      // maximaler Hash-Abstand von 64 Bit

/// <summary>Ähnliche Bilder (auch skaliert, neu komprimiert oder leicht bearbeitet) über einen 64-Bit-Differenz-Hash.</summary>
public static class Similar
{
    /// <summary>Gruppen von Bildern, deren Hashes sich in höchstens <paramref name="maxDistance"/> von 64 Bit unterscheiden.</summary>
    public static List<List<Item>> Groups(IEnumerable<Item> items, Func<string, ulong?> hasher, int maxDistance,
        Action<double>? progress, CancellationToken ct)
    {
        var imgs = items.Where(i => i.IsPhoto && !i.IsDir).ToList();
        var hashes = new ulong?[imgs.Count];
        var n = 0;
        Parallel.For(0, imgs.Count, new ParallelOptions { CancellationToken = ct }, i =>
        {
            hashes[i] = hasher(imgs[i].FullPath);
            progress?.Invoke((double)Interlocked.Increment(ref n) / imgs.Count);
        });
        return Cluster(hashes, maxDistance)
            .Select(g => g.Select(i => imgs[i]).OrderBy(x => x.FullPath, StringComparer.OrdinalIgnoreCase).ToList())
            .OrderByDescending(g => g[0].Size * g.Count).ToList();
    }

    /// <summary>Union-Find über alle Paare (O(n²) auf 64-Bit-Vergleichen – auch bei zehntausenden Bildern in Sekunden).</summary>
    public static List<List<int>> Cluster(IReadOnlyList<ulong?> h, int maxDistance)
    {
        var parent = Enumerable.Range(0, h.Count).ToArray();
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        var idx = Enumerable.Range(0, h.Count).Where(i => h[i] != null).ToArray();
        for (var a = 0; a < idx.Length; a++)
            for (var b = a + 1; b < idx.Length; b++)
                if (BitOperations.PopCount(h[idx[a]]!.Value ^ h[idx[b]]!.Value) <= maxDistance)
                {
                    int ra = Find(idx[a]), rb = Find(idx[b]);
                    if (ra != rb) parent[ra] = rb;
                }
        return idx.GroupBy(Find).Where(g => g.Count() > 1).Select(g => g.OrderBy(i => i).ToList()).ToList();
    }
}
