using System.IO;
using System.Globalization;
using System.Text;

namespace SaveTheDate;

public readonly record struct Meta(DateTime? Taken, double? Lat, double? Lon);

public enum GeoKind { Keep, Set, Remove }

public readonly record struct GeoEdit(GeoKind Kind, double Lat = 0, double Lon = 0);

/// <summary>
/// Liest und schreibt EXIF-Aufnahmedatum und GPS-Standort in JPEG-Dateien.
/// Die Bilddaten werden nicht neu kodiert; bestehende EXIF-Daten bleiben unverändert
/// (neue Einträge werden hinten angehängt, damit MakerNote-Offsets gültig bleiben).
/// </summary>
public static class ExifJpeg
{
    static readonly byte[] Hdr = "Exif\0\0"u8.ToArray();

    public static Meta Read(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096);
            return FindExif(fs) is { } tiff ? new Tiff(tiff).ReadMeta() : default;
        }
        catch { return default; }
    }

    public static void Update(string path, DateTime? taken, GeoEdit geo)
    {
        var d = File.ReadAllBytes(path);
        if (d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8) throw new IOException("Kein JPEG");
        int pos = 2, insertAt = 2, segStart = -1, segEnd = -1;
        while (pos + 4 <= d.Length && d[pos] == 0xFF)
        {
            int m = d[pos + 1], len = d[pos + 2] << 8 | d[pos + 3];
            if (m == 0xDA || m == 0xD9 || len < 2) break;
            if (m == 0xE1 && len >= 8 && d.AsSpan(pos + 4, 6).SequenceEqual(Hdr)) { segStart = pos; segEnd = pos + 2 + len; break; }
            if (m == 0xE0) insertAt = pos + 2 + len;
            pos += 2 + len;
        }
        var t = new Tiff(segStart >= 0 ? d[(segStart + 10)..segEnd] : Tiff.Empty());
        t.Edit(taken, geo);
        var tb = t.Bytes();
        if (tb.Length + 8 > 0xFFFF) throw new IOException("EXIF-Block zu groß");

        var cut1 = segStart >= 0 ? segStart : insertAt;
        var cut2 = segStart >= 0 ? segEnd : insertAt;
        using var o = new MemoryStream(d.Length + 1024);
        o.Write(d, 0, cut1);
        o.Write([0xFF, 0xE1, (byte)((tb.Length + 8) >> 8), (byte)(tb.Length + 8)]);
        o.Write(Hdr);
        o.Write(tb);
        o.Write(d, cut2, d.Length - cut2);

        var tmp = path + ".tmp" + Guid.NewGuid().ToString("N")[..6];
        File.WriteAllBytes(tmp, o.ToArray());
        try { File.Replace(tmp, path, null, true); }
        catch { File.Delete(tmp); throw; }
    }

    static byte[]? FindExif(Stream s)
    {
        var h = new byte[4];
        if (s.Read(h, 0, 2) != 2 || h[0] != 0xFF || h[1] != 0xD8) return null;
        while (s.Read(h, 0, 4) == 4 && h[0] == 0xFF)
        {
            int m = h[1], len = h[2] << 8 | h[3];
            if (m == 0xDA || m == 0xD9 || len < 2) return null;
            if (m == 0xE1 && len >= 8)
            {
                var buf = new byte[len - 2];
                s.ReadExactly(buf);
                if (buf.AsSpan(0, 6).SequenceEqual(Hdr)) return buf[6..];
            }
            else s.Seek(len - 2, SeekOrigin.Current);
        }
        return null;
    }

    sealed class Tiff
    {
        readonly List<byte> b;
        readonly bool le;

        public static byte[] Empty() => [(byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        public Tiff(byte[] d)
        {
            if (d.Length < 14 || !(d[0] == 'I' && d[1] == 'I' || d[0] == 'M' && d[1] == 'M')) throw new IOException("Ungültige EXIF-Daten");
            b = new List<byte>(d);
            le = d[0] == 'I';
            if (G16(b, 2) != 42) throw new IOException("Ungültige EXIF-Daten");
        }

        public byte[] Bytes() => b.ToArray();

        // ---- Lesen ----
        public Meta ReadMeta()
        {
            var (ifd0, _) = Ifd(G32(b, 4));
            var ex = Sub(ifd0, 0x8769);
            var taken = (ex != null ? GetDate(ex, 0x9003) ?? GetDate(ex, 0x9004) : null) ?? GetDate(ifd0, 0x0132);
            double? lat = null, lon = null;
            if (Sub(ifd0, 0x8825) is { } g) { lat = Coord(g, 2, 1, 'S'); lon = Coord(g, 4, 3, 'W'); }
            return new Meta(taken, lat, lon);
        }

        DateTime? GetDate(List<byte[]> l, int tag)
        {
            if (Find(l, tag) is not { } e) return null;
            var s = Encoding.ASCII.GetString(Val(e)).TrimEnd('\0', ' ');
            return DateTime.TryParseExact(s, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        }

        double? Coord(List<byte[]> g, int tag, int refTag, char neg)
        {
            if (Find(g, tag) is not { } e) return null;
            var v = Val(e);
            if (v.Length < 24) return null;
            double R(int i) { var n = G32(v, i * 8); var d = G32(v, i * 8 + 4); return d == 0 ? 0 : (double)n / d; }
            var r = R(0) + R(1) / 60 + R(2) / 3600;
            return Find(g, refTag) is { } re && Val(re) is { Length: > 0 } rv && (char)rv[0] == neg ? -r : r;
        }

        // ---- Schreiben ----
        public void Edit(DateTime? taken, GeoEdit geo)
        {
            var (ifd0, next) = Ifd(G32(b, 4));
            if (taken is { } t)
            {
                var s = Ascii(t.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture));
                var news = new List<byte[]> { Entry(0x9003, 2, s), Entry(0x9004, 2, s) };
                if (Sub(ifd0, 0x8769) is not { } ex || Find(ex, 0x9000) == null) news.Add(Entry(0x9000, 7, "0231"u8.ToArray()));
                SetSub(ifd0, 0x8769, news, keep: true);
                Replace(ifd0, Entry(0x0132, 2, s));
            }
            if (geo.Kind == GeoKind.Remove) ifd0.RemoveAll(e => G16(e, 0) == 0x8825);
            else if (geo.Kind == GeoKind.Set)
                SetSub(ifd0, 0x8825, [
                    Entry(0, 1, [2, 2, 0, 0]),
                    Entry(1, 2, Ascii(geo.Lat >= 0 ? "N" : "S")), Entry(2, 5, Dms(geo.Lat)),
                    Entry(3, 2, Ascii(geo.Lon >= 0 ? "E" : "W")), Entry(4, 5, Dms(geo.Lon)),
                ], keep: false);
            // IFD0 wandert ans Ende (Platz für neue Einträge); die alten Bytes bleiben unberührt stehen.
            P32(b, 4, WriteIfd(ifd0, next));
        }

        void SetSub(List<byte[]> ifd0, int tag, List<byte[]> news, bool keep)
        {
            var l = keep && Find(ifd0, tag) is { } old ? Ifd(G32(old, 8)).E : [];
            foreach (var n in news) Replace(l, n);
            Replace(ifd0, Pointer(tag, WriteIfd(l, 0)));
        }

        byte[] Dms(double v)
        {
            var a = Math.Min(Math.Abs(v), 180);
            uint deg = (uint)a, min = (uint)((a - deg) * 60);
            var sec = (uint)Math.Round(((a - deg) * 60 - min) * 60 * 10000);
            if (sec >= 600000) { sec -= 600000; min++; }
            if (min >= 60) { min -= 60; deg++; }
            var r = new byte[24];
            (uint, uint)[] parts = [(deg, 1), (min, 1), (sec, 10000)];
            for (var i = 0; i < 3; i++) { P32(r, i * 8, parts[i].Item1); P32(r, i * 8 + 4, parts[i].Item2); }
            return r;
        }

        static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s + "\0");

        // ---- IFD-Hilfen ----
        (List<byte[]> E, uint Next) Ifd(uint off)
        {
            int o = (int)off, n = G16(b, o);
            var l = new List<byte[]>(n);
            for (var i = 0; i < n; i++) l.Add(b.GetRange(o + 2 + 12 * i, 12).ToArray());
            return (l, G32(b, o + 2 + 12 * n));
        }

        List<byte[]>? Sub(List<byte[]> ifd, int tag) => Find(ifd, tag) is { } e ? Ifd(G32(e, 8)).E : null;

        byte[]? Find(List<byte[]> l, int tag) => l.Find(x => G16(x, 0) == tag);

        void Replace(List<byte[]> l, byte[] e) { l.RemoveAll(x => G16(x, 0) == G16(e, 0)); l.Add(e); }

        byte[] Val(byte[] e)
        {
            int type = G16(e, 2);
            long len = G32(e, 4) * (type switch { 3 or 8 => 2, 4 or 9 or 11 => 4, 5 or 10 or 12 => 8, _ => 1 });
            return len <= 4 ? e[8..(8 + (int)len)] : b.GetRange((int)G32(e, 8), (int)len).ToArray();
        }

        byte[] Pointer(int tag, uint off)
        {
            var r = new byte[12];
            P16(r, 0, tag); P16(r, 2, 4); P32(r, 4, 1); P32(r, 8, off);
            return r;
        }

        byte[] Entry(int tag, int type, byte[] data)
        {
            var r = new byte[12];
            P16(r, 0, tag); P16(r, 2, type); P32(r, 4, (uint)(data.Length / (type == 5 ? 8 : 1)));
            if (data.Length <= 4) data.CopyTo(r, 8);
            else { Align(); P32(r, 8, (uint)b.Count); b.AddRange(data); }
            return r;
        }

        uint WriteIfd(List<byte[]> l, uint next)
        {
            l.Sort((x, y) => G16(x, 0).CompareTo(G16(y, 0)));
            Align();
            var off = (uint)b.Count;
            var h = new byte[2]; P16(h, 0, l.Count); b.AddRange(h);
            foreach (var e in l) b.AddRange(e);
            var n = new byte[4]; P32(n, 0, next); b.AddRange(n);
            return off;
        }

        void Align() { if (b.Count % 2 == 1) b.Add(0); }

        // ---- Byte-Order ----
        int G16(IList<byte> s, int p) => le ? s[p] | s[p + 1] << 8 : s[p] << 8 | s[p + 1];

        uint G32(IList<byte> s, int p) => unchecked(le
            ? (uint)(s[p] | s[p + 1] << 8 | s[p + 2] << 16 | s[p + 3] << 24)
            : (uint)(s[p] << 24 | s[p + 1] << 16 | s[p + 2] << 8 | s[p + 3]));

        void P16(IList<byte> s, int p, int v)
        {
            s[p + (le ? 0 : 1)] = (byte)v;
            s[p + (le ? 1 : 0)] = (byte)(v >> 8);
        }

        void P32(IList<byte> s, int p, uint v)
        {
            for (var i = 0; i < 4; i++) s[p + (le ? i : 3 - i)] = (byte)(v >> (8 * i));
        }
    }
}
