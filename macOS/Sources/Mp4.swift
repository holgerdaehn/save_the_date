import Foundation

/// Erstellungsdatum in MP4/MOV-Containern (mvhd/tkhd/mdhd, Sekunden seit 1904, UTC).
/// Es werden nur die Zeitstempel-Felder an Ort und Stelle überschrieben – Bild und Ton bleiben unberührt.
enum Mp4 {
    static let exts: Set<String> = ["mp4", "m4v", "mov", "3gp", "3g2"]
    private static let epoch: Int64 = 2_082_844_800      // 1904-01-01 → 1970-01-01

    static func supports(_ u: URL) -> Bool { exts.contains(u.pathExtension.lowercased()) }

    /// Aufnahmedatum (mvhd); nil, wenn nicht vorhanden oder nicht lesbar.
    static func read(_ url: URL) -> Date? {
        guard let fh = try? FileHandle(forReadingFrom: url) else { return nil }
        defer { try? fh.close() }
        guard let f = try? fields(fh).first(where: { $0.kind == "mvhd" }) else { return nil }
        try? fh.seek(toOffset: f.off)
        guard let d = try? fh.read(upToCount: f.wide ? 8 : 4), d.count == (f.wide ? 8 : 4) else { return nil }
        let v = d.reduce(UInt64(0)) { $0 << 8 | UInt64($1) }
        return v == 0 ? nil : Date(timeIntervalSince1970: Double(Int64(clamping: v) - epoch))
    }

    static func write(_ url: URL, _ date: Date) throws {
        let secs = Int64(date.timeIntervalSince1970.rounded(.down)) + epoch
        guard secs > 0 else { throw EditError("Datum liegt vor 1904") }
        let fh = try FileHandle(forUpdating: url)
        defer { try? fh.close() }
        let list = try fields(fh)
        guard list.contains(where: { $0.kind == "mvhd" }) else { throw EditError("Kein MP4/MOV-Video") }
        if !list.allSatisfy(\.wide), secs > Int64(UInt32.max) { throw EditError("Datum nach 2040 nicht darstellbar") }
        for f in list {
            let n = f.wide ? 8 : 4
            let bytes = Data((0 ..< n).map { UInt8(truncatingIfNeeded: secs >> (8 * (n - 1 - $0))) })
            try fh.seek(toOffset: f.off); try fh.write(contentsOf: bytes)        // Erstellt
            try fh.seek(toOffset: f.off + UInt64(n)); try fh.write(contentsOf: bytes)   // Geändert
        }
    }

    private struct Field { let kind: String; let off: UInt64; let wide: Bool }

    private static func fields(_ fh: FileHandle) throws -> [Field] {
        var out: [Field] = []
        try scan(fh, 0, try fh.seekToEnd(), 0, &out)
        return out
    }

    private static func scan(_ fh: FileHandle, _ start: UInt64, _ end: UInt64, _ depth: Int, _ out: inout [Field]) throws {
        var pos = start
        while pos + 8 <= end {
            try fh.seek(toOffset: pos)
            guard let h = try fh.read(upToCount: 17).map({ [UInt8]($0) }), h.count >= 8 else { return }
            func be(_ at: Int, _ n: Int) -> UInt64 { h[at ..< at + n].reduce(0) { $0 << 8 | UInt64($1) } }
            var size = be(0, 4), hdr: UInt64 = 8
            let kind = String(decoding: h[4 ..< 8], as: UTF8.self)
            if size == 1 { guard h.count >= 16 else { return }; size = be(8, 8); hdr = 16 } else if size == 0 { size = end - pos }
            guard size >= hdr, pos + size <= end else { return }
            switch kind {
            case "moov", "trak", "mdia":
                if depth < 4 { try scan(fh, pos + hdr, pos + size, depth + 1, &out) }
            case "mvhd", "tkhd", "mdhd":
                if size >= hdr + 4 {
                    try fh.seek(toOffset: pos + hdr)
                    if let v = try fh.read(upToCount: 1)?.first { out.append(Field(kind: kind, off: pos + hdr + 4, wide: v == 1)) }
                }
            default: break
            }
            pos += size
        }
    }
}
