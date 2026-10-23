import AppKit
import CryptoKit
import Observation
import SwiftUI

enum Keep: String, CaseIterable, Identifiable {
    case oldest = "Ältestes behalten", newest = "Neuestes behalten", shortest = "Kürzesten Pfad behalten", first = "Erstes behalten"
    var id: Self { self }
}

enum Dupes {
    private static let partial = 64 * 1024

    /// Gruppen byte-identischer Dateien: gleiche Größe, gleicher Anfangs-Hash, gleicher Gesamt-Hash (SHA-256).
    /// Dateinamen spielen keine Rolle. Nur Dateien gleicher Größe werden überhaupt gelesen.
    static func find(_ items: [Item], progress: @escaping @Sendable (Double) -> Void) async -> [[Item]] {
        let files = items.filter { !$0.isDir && $0.size > 0 }
        let sized = Dictionary(grouping: files, by: \.size).values.filter { $0.count > 1 }
        let heads = await regroup(Array(sized), limit: partial) { progress($0 * 0.3) }
        // Dateien bis 64 KB sind mit dem Anfangs-Hash bereits vollständig verglichen.
        let done = heads.filter { $0[0].size <= partial }
        let rest = heads.filter { $0[0].size > partial }
        let full = await regroup(rest, limit: nil) { progress(0.3 + $0 * 0.7) }
        return (done + full)
            .map { $0.sorted { $0.id.path < $1.id.path } }
            .sorted { $0[0].size * Int64($0.count) > $1[0].size * Int64($1.count) }
    }

    private static func regroup(_ groups: [[Item]], limit: Int?, progress: @escaping @Sendable (Double) -> Void) async -> [[Item]] {
        let all = groups.flatMap { $0 }
        var hashes: [URL: String] = [:]
        var count = 0
        await withTaskGroup(of: (URL, String?).self) { g in
            var it = all.makeIterator()
            let add = { (g: inout TaskGroup<(URL, String?)>) in
                if let x = it.next() { g.addTask { (x.id, hash(x.id, limit: limit)) } }
            }
            for _ in 0 ..< ProcessInfo.processInfo.activeProcessorCount { add(&g) }
            for await (u, h) in g {
                if let h { hashes[u] = h }
                count += 1
                progress(Double(count) / Double(all.count))
                if Task.isCancelled { g.cancelAll(); return }
                add(&g)
            }
        }
        return groups.flatMap { grp in
            Dictionary(grouping: grp) { hashes[$0.id] ?? $0.id.path }.values.filter { $0.count > 1 }
        }
    }

    /// SHA-256 der ersten `limit` Bytes (nil = ganze Datei).
    static func hash(_ url: URL, limit: Int?) -> String? {
        guard let fh = try? FileHandle(forReadingFrom: url) else { return nil }
        defer { try? fh.close() }
        var h = SHA256()
        var left = limit ?? Int.max
        while left > 0, !Task.isCancelled {
            guard let d = try? fh.read(upToCount: min(1 << 20, left)), !d.isEmpty else { break }
            h.update(data: d)
            left -= d.count
        }
        return h.finalize().map { String(format: "%02x", $0) }.joined()
    }

    static func keeper(_ g: [Item], _ rule: Keep) -> Item {
        func age(_ i: Item) -> Date { min(i.created ?? .distantFuture, i.modified ?? .distantFuture) }
        switch rule {
        case .oldest: return g.min { (age($0), $0.id.path.count, $0.id.path) < (age($1), $1.id.path.count, $1.id.path) }!
        case .newest: return g.max { (age($0), $1.id.path.count, $1.id.path) < (age($1), $0.id.path.count, $0.id.path) }!
        case .shortest: return g.min { ($0.id.path.count, $0.id.path) < ($1.id.path.count, $1.id.path) }!
        case .first: return g[0]
        }
    }
}

@MainActor @Observable
final class DupeModel {
    var groups: [[Item]] = []
    var marked = Set<URL>()
    var keep = Keep.oldest
    var progress = 0.0
    var scanning = true
    @ObservationIgnored private var task: Task<Void, Never>?

    func scan(_ items: [Item]) {
        task = Task {
            let g = await Dupes.find(items) { p in Task { @MainActor in self.progress = p } }
            guard !Task.isCancelled else { return }
            groups = g
            autoMark()
            scanning = false
        }
    }

    func cancel() { task?.cancel() }

    func autoMark() {
        marked = Set(groups.flatMap { g in
            let k = Dupes.keeper(g, keep)
            return g.filter { $0.id != k.id }.map(\.id)
        })
    }

    /// Pro Gruppe muss mindestens eine Datei übrig bleiben.
    func binding(_ it: Item, in g: [Item]) -> Binding<Bool> {
        Binding(get: { self.marked.contains(it.id) }, set: { on in
            if !on { self.marked.remove(it.id) }
            else if g.filter({ !self.marked.contains($0.id) }).count > 1 { self.marked.insert(it.id) }
            else { NSSound.beep() }
        })
    }

    var freed: Int64 { groups.joined().filter { marked.contains($0.id) }.reduce(0) { $0 + $1.size } }

    /// Legt markierte Dateien in den Papierkorb (nie endgültig löschen). Gibt Fehlermeldungen zurück.
    func trash() -> (ok: Int, errors: [String]) {
        var ok = 0, errs: [String] = []
        for g in groups where g.contains(where: { !marked.contains($0.id) }) {
            for it in g where marked.contains(it.id) {
                do { try FileManager.default.trashItem(at: it.id, resultingItemURL: nil); ok += 1 }
                catch { errs.append("\(it.name): \(error.localizedDescription)") }
            }
        }
        groups = groups.map { $0.filter { FileManager.default.fileExists(atPath: $0.id.path) } }.filter { $0.count > 1 }
        marked.formIntersection(Set(groups.joined().map(\.id)))
        return (ok, errs)
    }
}
