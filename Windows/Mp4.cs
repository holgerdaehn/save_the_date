using System.IO;
using System.Text;

namespace SaveTheDate;

/// <summary>
/// Aufnahmedatum in MP4/MOV-Containern (mvhd/tkhd/mdhd, Sekunden seit 1904, UTC).
/// Es werden nur die Zeitstempel-Felder an Ort und Stelle überschrieben – Bild und Ton bleiben unberührt.
/// </summary>
public static class Mp4
{
    public static readonly HashSet<string> Exts = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".m4v", ".mov", ".3gp", ".3g2" };
    const long Epoch = 2_082_844_800;      // 1904-01-01 → 1970-01-01

    record struct Field(string Kind, long Off, bool Wide);

    public static bool Supports(string path) => Exts.Contains(Path.GetExtension(path));

    /// <summary>Aufnahmedatum (mvhd) in Ortszeit; null, wenn nicht vorhanden oder nicht lesbar.</summary>
    public static DateTime? Read(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (Fields(fs).FirstOrDefault(f => f.Kind == "mvhd") is not { Kind: "mvhd" } f) return null;
            fs.Position = f.Off;
            var b = new byte[f.Wide ? 8 : 4];
            fs.ReadExactly(b);
            var v = b.Aggregate(0UL, (a, x) => a << 8 | x);
            return v == 0 ? null : DateTimeOffset.FromUnixTimeSeconds((long)v - Epoch).LocalDateTime;
        }
        catch { return null; }
    }

    public static void Write(string path, DateTime date)
    {
        var secs = new DateTimeOffset(date).ToUnixTimeSeconds() + Epoch;
        if (secs <= 0) throw new IOException("Datum liegt vor 1904");
        using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var list = Fields(fs);
        if (!list.Any(f => f.Kind == "mvhd")) throw new IOException("Kein MP4/MOV-Video");
        if (!list.All(f => f.Wide) && secs > uint.MaxValue) throw new IOException("Datum nach 2040 nicht darstellbar");
        foreach (var f in list)
        {
            var n = f.Wide ? 8 : 4;
            var b = new byte[n];
            for (var i = 0; i < n; i++) b[i] = (byte)(secs >> (8 * (n - 1 - i)));
            fs.Position = f.Off; fs.Write(b);          // Erstellt
            fs.Position = f.Off + n; fs.Write(b);      // Geändert
        }
    }

    static List<Field> Fields(FileStream fs)
    {
        var o = new List<Field>();
        Scan(fs, 0, fs.Length, 0, o);
        return o;
    }

    static void Scan(FileStream fs, long start, long end, int depth, List<Field> o)
    {
        var h = new byte[16];
        for (var pos = start; pos + 8 <= end;)
        {
            fs.Position = pos;
            var n = fs.Read(h, 0, 16);
            if (n < 8) return;
            var size = BE(h, 0, 4);
            long hdr = 8;
            var kind = Encoding.ASCII.GetString(h, 4, 4);
            if (size == 1) { if (n < 16) return; size = BE(h, 8, 8); hdr = 16; }
            else if (size == 0) size = (ulong)(end - pos);
            if (size < (ulong)hdr || size > (ulong)(end - pos)) return;
            switch (kind)
            {
                case "moov" or "trak" or "mdia":
                    if (depth < 4) Scan(fs, pos + hdr, pos + (long)size, depth + 1, o);
                    break;
                case "mvhd" or "tkhd" or "mdhd":
                    if (size >= (ulong)hdr + 4)
                    {
                        fs.Position = pos + hdr;
                        var v = fs.ReadByte();
                        if (v >= 0) o.Add(new Field(kind, pos + hdr + 4, v == 1));
                    }
                    break;
            }
            pos += (long)size;
        }
    }

    static ulong BE(byte[] b, int at, int n)
    {
        ulong v = 0;
        for (var i = 0; i < n; i++) v = v << 8 | b[at + i];
        return v;
    }
}
