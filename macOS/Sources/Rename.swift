import Foundation

/// Dateien anhand ihres Datums umbenennen, z. B. `{JJJJ}-{MM}-{TT}_{hh}-{mm}-{ss}` → `2024-07-13_14-22-05.jpg`.
/// Platzhalter: JJJJ MM TT hh mm ss (Datum), Name (alter Name ohne Endung), Nr (Zähler, dreistellig).
enum Renamer {
    static let presets: [(String, String)] = [
        ("2024-07-13_14-22-05", "{JJJJ}-{MM}-{TT}_{hh}-{mm}-{ss}"),
        ("20240713_142205", "{JJJJ}{MM}{TT}_{hhmmss}"),
        ("2024-07-13 Name", "{JJJJ}-{MM}-{TT} {Name}"),
        ("Name 2024-07", "{Name} {JJJJ}-{MM}"),
        ("Foto 001, 002 …", "Foto {Nr}"),
    ]
    static let defaultTemplate = presets[0].1

    struct Move: Identifiable {
        var id: URL { item.id }
        let item: Item
        let dst: URL
        var skip: String?
    }

    static func render(_ template: String, date: Date?, name: String, nr: Int, count: Int) -> String {
        var s = template.replacingOccurrences(of: "{hhmmss}", with: "{hh}{mm}{ss}")
        if let d = date {
            let c = Calendar.current.dateComponents([.year, .month, .day, .hour, .minute, .second], from: d)
            let f = { (v: Int?, w: Int) in String(format: "%0\(w)d", v ?? 0) }
            for (k, v) in [("JJJJ", f(c.year, 4)), ("MM", f(c.month, 2)), ("TT", f(c.day, 2)),
                           ("hh", f(c.hour, 2)), ("mm", f(c.minute, 2)), ("ss", f(c.second, 2))] {
                s = s.replacingOccurrences(of: "{\(k)}", with: v)
            }
        }
        s = s.replacingOccurrences(of: "{Name}", with: name)
        s = s.replacingOccurrences(of: "{Nr}", with: String(format: "%0\(max(3, String(count).count))d", nr))
        // In allen Dateisystemen ungültige Zeichen entfernen.
        return String(s.unicodeScalars.filter { !"\\/:*?\"<>|".unicodeScalars.contains($0) && $0.value >= 32 })
            .trimmingCharacters(in: .whitespaces)
    }

    private static func usesDate(_ t: String) -> Bool {
        ["{JJJJ}", "{MM}", "{TT}", "{hh}", "{mm}", "{ss}", "{hhmmss}"].contains { t.contains($0) }
    }

    /// Chronologisch sortiert (für den Zähler); Namenskonflikte werden zu „name (2)“.
    static func plan(_ items: [Item], template: String, source: DateSource, lowercaseExt: Bool, onlyMedia: Bool) -> [Move] {
        let fm = FileManager.default
        let needDate = usesDate(template)
        let files = items.filter { !$0.isDir && (!onlyMedia || $0.isMedia) }
            .sorted { (source.date($0) ?? .distantPast, $0.id.path) < (source.date($1) ?? .distantPast, $1.id.path) }
        var used = Set<String>()
        var out: [Move] = []
        for (i, it) in files.enumerated() {
            let date = source.date(it)
            func skip(_ why: String) { out.append(Move(item: it, dst: it.id, skip: why)); used.insert(it.id.path.lowercased()) }
            if needDate && date == nil { skip("Kein Datum"); continue }
            let base = render(template, date: date, name: it.id.deletingPathExtension().lastPathComponent, nr: i + 1, count: files.count)
            if base.isEmpty { skip("Leerer Name"); continue }
            var ext = it.id.pathExtension
            if lowercaseExt { ext = ext.lowercased() }
            let dir = it.id.deletingLastPathComponent()
            func url(_ n: Int) -> URL {
                dir.appendingPathComponent((n == 1 ? base : "\(base) (\(n))") + (ext.isEmpty ? "" : ".\(ext)"))
            }
            var n = 1, dst = url(1)
            if dst.path == it.id.path { skip("Name bleibt gleich"); continue }
            while true {
                let key = dst.path.lowercased()
                if key == it.id.path.lowercased() { break }                   // nur Groß-/Kleinschreibung
                if !used.contains(key) && !fm.fileExists(atPath: dst.path) { break }
                n += 1; dst = url(n)
            }
            used.insert(dst.path.lowercased())
            out.append(Move(item: it, dst: dst, skip: nil))
        }
        return out
    }

    static func run(_ plan: [Move], progress: @Sendable (Double) -> Void) -> (ops: [Organizer.Op], errors: [String]) {
        var ops: [Organizer.Op] = [], errors: [String] = []
        for (i, m) in plan.enumerated() where m.skip == nil {
            progress(Double(i) / Double(max(plan.count, 1)))
            do { try move(m.item.id, m.dst); ops.append(Organizer.Op(src: m.item.id, dst: m.dst, copied: false)) }
            catch { errors.append("\(m.item.name): \(error.localizedDescription)") }
        }
        return (ops, errors)
    }

    static func undo(_ ops: [Organizer.Op]) -> [String] {
        var errors: [String] = []
        for op in ops.reversed() {
            do { try move(op.dst, op.src) } catch { errors.append("\(op.dst.lastPathComponent): \(error.localizedDescription)") }
        }
        return errors
    }

    /// Reine Groß-/Kleinschreibungs-Änderung braucht auf macOS einen Zwischenschritt.
    private static func move(_ a: URL, _ b: URL) throws {
        let fm = FileManager.default
        if a.path.lowercased() == b.path.lowercased() {
            let tmp = a.deletingLastPathComponent().appendingPathComponent(".\(UUID().uuidString).tmp")
            try fm.moveItem(at: a, to: tmp)
            do { try fm.moveItem(at: tmp, to: b) } catch { try? fm.moveItem(at: tmp, to: a); throw error }
        } else { try fm.moveItem(at: a, to: b) }
    }
}
