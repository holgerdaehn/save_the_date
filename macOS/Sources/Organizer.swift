import Foundation

enum Pattern: String, CaseIterable, Identifiable {
    case ym = "JJJJ/MM", ymd = "JJJJ/MM/TT", flat = "JJJJ-MM"
    var id: Self { self }

    func path(_ d: Date) -> String {
        let c = Calendar.current.dateComponents([.year, .month, .day], from: d)
        let (y, m, t) = (c.year ?? 0, c.month ?? 0, c.day ?? 0)
        switch self {
        case .ym: return String(format: "%04d/%02d", y, m)
        case .ymd: return String(format: "%04d/%02d/%02d", y, m, t)
        case .flat: return String(format: "%04d-%02d", y, m)
        }
    }
}

enum DateSource: String, CaseIterable, Identifiable {
    case taken = "Aufnahme, sonst Änderung", modified = "Änderungsdatum", created = "Erstelldatum"
    var id: Self { self }

    func date(_ i: Item) -> Date? {
        switch self {
        case .taken: i.taken ?? i.modified ?? i.created
        case .modified: i.modified ?? i.created
        case .created: i.created ?? i.modified
        }
    }
}

enum Organizer {
    struct Move: Identifiable {
        var id: URL { item.id }
        let item: Item
        let dst: URL
        var skip: String?
    }

    struct Op: Sendable {
        let src: URL
        let dst: URL
        let copied: Bool
    }

    /// Zielpfad pro Datei bestimmen. Namenskonflikte: identischer Inhalt → überspringen, sonst " (2)", " (3)" …
    static func plan(_ items: [Item], root: URL, pattern: Pattern, source: DateSource, onlyMedia: Bool) -> [Move] {
        let fm = FileManager.default
        var used = Set<String>()
        var out: [Move] = []
        for it in items.sorted(by: { $0.id.path < $1.id.path }) where !it.isDir && (!onlyMedia || it.isMedia) {
            let dir = root.appendingPathComponent(source.date(it).map(pattern.path) ?? "Ohne Datum", isDirectory: true)
            var dst = dir.appendingPathComponent(it.name)
            if dst.standardizedFileURL.path == it.id.path {
                used.insert(dst.path.lowercased())
                out.append(Move(item: it, dst: dst, skip: "Bereits am Ziel"))
                continue
            }
            let base = dst.deletingPathExtension().lastPathComponent, ext = dst.pathExtension
            var n = 1
            var skip: String?
            while true {
                let taken = used.contains(dst.path.lowercased())
                let exists = fm.fileExists(atPath: dst.path)
                if !taken && !exists { break }
                if !taken && exists && sameContent(it, dst) { skip = "Identische Datei existiert bereits"; break }
                n += 1
                dst = dir.appendingPathComponent("\(base) (\(n))" + (ext.isEmpty ? "" : ".\(ext)"))
            }
            used.insert(dst.path.lowercased())
            out.append(Move(item: it, dst: dst, skip: skip))
        }
        return out
    }

    private static func sameContent(_ it: Item, _ other: URL) -> Bool {
        let size = (try? FileManager.default.attributesOfItem(atPath: other.path)[.size] as? Int64) ?? -1
        return size == it.size && Dupes.hash(it.id, limit: nil) == Dupes.hash(other, limit: nil)
    }

    static func run(_ plan: [Move], copy: Bool, removeEmpty: Bool, roots: [URL],
                    progress: @Sendable (Double) -> Void) -> (ops: [Op], errors: [String], skipped: Int, removedDirs: Int) {
        let fm = FileManager.default
        var ops: [Op] = [], errors: [String] = [], skipped = 0
        var parents = Set<URL>()
        for (i, m) in plan.enumerated() {
            progress(Double(i) / Double(max(plan.count, 1)))
            if m.skip != nil { skipped += 1; continue }
            do {
                try fm.createDirectory(at: m.dst.deletingLastPathComponent(), withIntermediateDirectories: true)
                if copy {
                    try fm.copyItem(at: m.item.id, to: m.dst)
                    var a: [FileAttributeKey: Any] = [:]
                    a[.modificationDate] = m.item.modified
                    try? fm.setAttributes(a, ofItemAtPath: m.dst.path)
                    a = [.creationDate: m.item.created as Any]
                    if m.item.created != nil { try? fm.setAttributes(a, ofItemAtPath: m.dst.path) }
                } else {
                    try fm.moveItem(at: m.item.id, to: m.dst)
                    parents.insert(m.item.id.deletingLastPathComponent())
                }
                ops.append(Op(src: m.item.id, dst: m.dst, copied: copy))
            } catch { errors.append("\(m.item.name): \(error.localizedDescription)") }
        }
        return (ops, errors, skipped, removeEmpty ? pruneEmpty(parents, roots: roots) : 0)
    }

    static func undo(_ ops: [Op], root: URL) -> [String] {
        let fm = FileManager.default
        var errors: [String] = []
        var parents = Set<URL>()
        for op in ops.reversed() {
            do {
                if op.copied { try fm.trashItem(at: op.dst, resultingItemURL: nil) }
                else {
                    try fm.createDirectory(at: op.src.deletingLastPathComponent(), withIntermediateDirectories: true)
                    try fm.moveItem(at: op.dst, to: op.src)
                }
                parents.insert(op.dst.deletingLastPathComponent())
            } catch { errors.append("\(op.dst.lastPathComponent): \(error.localizedDescription)") }
        }
        _ = pruneEmpty(parents, roots: [root.standardizedFileURL])
        return errors
    }

    /// Entfernt leere Ordner (nur .DS_Store o. ä. darin) – ausschließlich unterhalb der Wurzeln, nie die Wurzel selbst.
    static func pruneEmpty(_ dirs: Set<URL>, roots: [URL]) -> Int {
        let fm = FileManager.default
        var removed = 0
        for d in dirs.sorted(by: { $0.path.count > $1.path.count }) {
            var cur = d.standardizedFileURL
            while roots.contains(where: { cur.path.hasPrefix($0.standardizedFileURL.path + "/") }) {
                guard let rest = try? fm.contentsOfDirectory(atPath: cur.path), rest.allSatisfy(Cleanup.isJunk) else { break }
                do { try fm.removeItem(at: cur); removed += 1 } catch { break }
                cur = cur.deletingLastPathComponent()
            }
        }
        return removed
    }
}
