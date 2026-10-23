import AppKit
import CoreLocation
import Observation
import SwiftUI
import UniformTypeIdentifiers

struct Item: Identifiable, Sendable {
    let id: URL
    let isDir: Bool
    let isImage: Bool
    var isMedia = false
    var size: Int64 = 0
    var created: Date?
    var modified: Date?
    var taken: Date?
    var lat: Double?
    var lon: Double?

    var name: String { id.lastPathComponent }
    var folder: String { (id.deletingLastPathComponent().path as NSString).abbreviatingWithTildeInPath }
    var createdKey: Date { created ?? .distantPast }
    var modifiedKey: Date { modified ?? .distantPast }
    var takenKey: Date { taken ?? .distantPast }
    var gpsKey: Double { lat ?? -999 }
}

struct Plan: Sendable {
    var created: Date?
    var modified: Date?
    var taken: Date?
    var isEmpty: Bool { created == nil && modified == nil && taken == nil }
}

enum Mode: String, CaseIterable, Identifiable {
    case fixed = "Festes Datum", shift = "Verschieben", exif = "Aus Aufnahme"
    var id: Self { self }
}

enum GeoMode: String, CaseIterable, Identifiable {
    case keep = "Unverändert", set = "Setzen", remove = "Entfernen"
    var id: Self { self }
}

struct Settings {
    var mode = Mode.fixed
    var setCreated = true, setModified = true, setTaken = false
    var created = Date.nowToSecond, modified = Date.nowToSecond, taken = Date.nowToSecond
    var linked = true
    var interval = 0.0
    var later = true
    var days = 0, hours = 0, minutes = 0, seconds = 0
    var geo = GeoMode.keep
    var lat: Double?
    var lon: Double?
}

struct ScanOptions: Sendable {
    var recursive: Bool
    var dirs: Bool
    var hidden: Bool
}

extension Date {
    static var nowToSecond: Date { Date(timeIntervalSince1970: Date().timeIntervalSince1970.rounded(.down)) }
    var txt: String { formatted(.dateTime.day(.twoDigits).month(.twoDigits).year().hour().minute().second()) }
}

enum Scan {
    static func list(_ sources: [URL], _ o: ScanOptions) -> [Item] {
        let keys: [URLResourceKey] = [.isDirectoryKey, .creationDateKey, .contentModificationDateKey, .contentTypeKey, .fileSizeKey]
        var out: [URL: Item] = [:]
        func make(_ u: URL) -> Item? {
            guard let v = try? u.resourceValues(forKeys: Set(keys)) else { return nil }
            let dir = v.isDirectory == true
            if dir && !o.dirs { return nil }
            let image = !dir && v.contentType?.conforms(to: .image) == true
            let movie = !dir && v.contentType?.conforms(to: .movie) == true
            return Item(id: u, isDir: dir, isImage: image, isMedia: image || movie, size: Int64(v.fileSize ?? 0),
                        created: v.creationDate, modified: v.contentModificationDate)
        }
        for s in sources.map(\.standardizedFileURL) {
            guard let root = try? s.resourceValues(forKeys: [.isDirectoryKey]) else { continue }
            if root.isDirectory != true {
                out[s] = make(s) ?? out[s]
                continue
            }
            if o.dirs { out[s] = make(s) }
            var opts: FileManager.DirectoryEnumerationOptions = [.skipsPackageDescendants]
            if !o.recursive { opts.insert(.skipsSubdirectoryDescendants) }
            if !o.hidden { opts.insert(.skipsHiddenFiles) }
            guard let en = FileManager.default.enumerator(at: s, includingPropertiesForKeys: keys, options: opts) else { continue }
            for case let u as URL in en {
                let u = u.standardizedFileURL
                if let it = make(u) { out[u] = it }
            }
        }
        return Array(out.values)
    }
}

enum Apply {
    /// Reihenfolge ist wichtig: macOS zieht das Erstelldatum auf den Änderungszeitpunkt herunter,
    /// wenn dieser früher liegt. Daher zuerst "geändert", danach "erstellt" setzen.
    static func one(_ it: Item, _ p: Plan, _ geo: GeoEdit) throws {
        var meta = false
        if it.isImage, p.taken != nil || geo != .keep {
            try Exif.write(it.id, taken: p.taken, geo: geo)
            meta = true
        }
        guard meta || p.modified != nil || p.created != nil else { return }
        let fm = FileManager.default
        if let m = p.modified ?? it.modified { try fm.setAttributes([.modificationDate: m], ofItemAtPath: it.id.path) }
        if let c = p.created ?? it.created { try fm.setAttributes([.creationDate: c], ofItemAtPath: it.id.path) }
    }
}

@MainActor @Observable
final class Store {
    static let shared = Store()
    var items: [Item] = []
    var sources: [URL] = []
    var selection = Set<URL>()
    var sortOrder = [KeyPathComparator(\Item.name)]
    var s = Settings()
    var progress: Double?
    var report: String?
    var confirmApply = false
    var showInspector = true
    var canUndo = false
    var showDupes = false
    var showOrganize = false
    var showCleanup = false
    var helpTopic: String?
    var lastOps: [Organizer.Op] = []
    var lastRoot: URL?
    @ObservationIgnored private var scanTask: Task<Void, Never>?
    @ObservationIgnored private var undoData: [URL: Plan] = [:]

    init() {
        UserDefaults.standard.register(defaults: ["recursive": true, "dirs": false, "hidden": false, "confirm": true])
    }

    var options: ScanOptions {
        let d = UserDefaults.standard
        return ScanOptions(recursive: d.bool(forKey: "recursive"), dirs: d.bool(forKey: "dirs"), hidden: d.bool(forKey: "hidden"))
    }

    var targets: [Item] { selection.isEmpty ? items : items.filter { selection.contains($0.id) } }

    var coordinate: CLLocationCoordinate2D? {
        guard let la = s.lat, let lo = s.lon else { return nil }
        return CLLocationCoordinate2D(latitude: la, longitude: lo)
    }

    var geoEdit: GeoEdit {
        switch s.geo {
        case .keep: .keep
        case .remove: .remove
        case .set: coordinate.map { .set($0.latitude, $0.longitude) } ?? .keep
        }
    }

    /// Bindung für Datumsfelder; bei "verknüpft" ziehen alle drei Werte mit.
    func date(_ kp: WritableKeyPath<Settings, Date>) -> Binding<Date> {
        Binding(get: { self.s[keyPath: kp] }, set: { v in
            self.s[keyPath: kp] = v
            if self.s.linked { self.s.created = v; self.s.modified = v; self.s.taken = v }
        })
    }

    // MARK: Plan

    func plans() -> [URL: Plan] {
        var order = targets
        if s.mode == .fixed, s.interval != 0 {
            order.sort { $0.name.localizedStandardCompare($1.name) == .orderedAscending }
        }
        let off = TimeInterval(((s.days * 24 + s.hours) * 60 + s.minutes) * 60 + s.seconds) * (s.later ? 1 : -1)
        var r: [URL: Plan] = [:]
        for (i, it) in order.enumerated() {
            var p = Plan()
            switch s.mode {
            case .fixed:
                let d = TimeInterval(i) * s.interval
                if s.setCreated { p.created = s.created + d }
                if s.setModified { p.modified = s.modified + d }
                if s.setTaken, it.isImage { p.taken = s.taken + d }
            case .shift:
                if s.setCreated { p.created = it.created.map { $0 + off } }
                if s.setModified { p.modified = it.modified.map { $0 + off } }
                if s.setTaken, it.isImage { p.taken = it.taken.map { $0 + off } }
            case .exif:
                if let t = it.taken {
                    if s.setCreated { p.created = t }
                    if s.setModified { p.modified = t }
                }
            }
            r[it.id] = p
        }
        return r
    }

    func work(_ pl: [URL: Plan]) -> [Item] {
        let geo = geoEdit != .keep
        return targets.filter { pl[$0.id]?.isEmpty == false || (geo && $0.isImage) }
    }

    // MARK: Quellen & Scan

    func add(_ urls: [URL]) {
        for u in urls where !sources.contains(u) { sources.append(u) }
        rescan()
    }

    func clear() {
        scanTask?.cancel()
        sources = []; items = []; selection = []; progress = nil
    }

    func remove(_ ids: Set<URL>) {
        items.removeAll { ids.contains($0.id) }
        selection.subtract(ids)
    }

    func rescan() {
        scanTask?.cancel()
        let src = sources, opt = options
        guard !src.isEmpty else { return clear() }
        scanTask = Task {
            progress = -1
            let list = await Task.detached(priority: .userInitiated) { Scan.list(src, opt) }.value
            guard !Task.isCancelled else { return }
            items = list.sorted(using: sortOrder)
            selection.formIntersection(Set(items.map(\.id)))
            let imgs = items.filter(\.isImage).map(\.id)
            let chunks = stride(from: 0, to: imgs.count, by: 128).map { Array(imgs[$0 ..< min($0 + 128, imgs.count)]) }
            var done = 0
            await withTaskGroup(of: [(URL, Meta)].self) { g in
                var it = chunks.makeIterator()
                let add = { (g: inout TaskGroup<[(URL, Meta)]>) in
                    if let c = it.next() { g.addTask { c.map { ($0, Exif.read($0)) } } }
                }
                for _ in 0 ..< ProcessInfo.processInfo.activeProcessorCount { add(&g) }
                for await res in g {
                    guard !Task.isCancelled else { g.cancelAll(); return }
                    var a = items
                    let idx = Dictionary(uniqueKeysWithValues: a.enumerated().map { ($1.id, $0) })
                    for (u, m) in res { if let i = idx[u] { a[i].taken = m.taken; a[i].lat = m.lat; a[i].lon = m.lon } }
                    items = a
                    done += res.count
                    progress = Double(done) / Double(imgs.count)
                    add(&g)
                }
            }
            guard !Task.isCancelled else { return }
            items.sort(using: sortOrder)
            progress = nil
        }
    }

    // MARK: Anwenden

    func requestApply() {
        if UserDefaults.standard.bool(forKey: "confirm") { confirmApply = true } else { apply() }
    }

    func apply() {
        let pl = plans(), geo = geoEdit
        let w = work(pl)
        guard !w.isEmpty else { report = "Keine Änderungen ausgewählt."; return }
        undoData = Dictionary(uniqueKeysWithValues: w.map { ($0.id, Plan(created: $0.created, modified: $0.modified))})
        canUndo = true
        run(w, "geändert") { try Apply.one($0, pl[$0.id] ?? Plan(), geo) }
    }

    /// Setzt nur Datei-Daten zurück; EXIF-Änderungen werden nicht rückgängig gemacht.
    func undo() {
        let d = undoData
        canUndo = false
        run(items.filter { d[$0.id] != nil }, "zurückgesetzt") { try Apply.one($0, d[$0.id]!, .keep) }
    }

    private func run(_ w: [Item], _ verb: String, _ job: @escaping @Sendable (Item) throws -> Void) {
        Task {
            progress = 0
            var ok = 0, failed: [String] = [], done = 0
            await withTaskGroup(of: (String, String?).self) { g in
                var it = w.makeIterator()
                let add = { (g: inout TaskGroup<(String, String?)>) in
                    if let x = it.next() {
                        g.addTask { do { try job(x); return (x.name, nil) } catch { return (x.name, error.localizedDescription) } }
                    }
                }
                for _ in 0 ..< 8 { add(&g) }
                for await (name, err) in g {
                    done += 1
                    if let err { failed.append("\(name): \(err)") } else { ok += 1 }
                    if done % 8 == 0 || done == w.count { progress = Double(done) / Double(w.count) }
                    add(&g)
                }
            }
            progress = nil
            report = "\(ok) von \(w.count) Objekten \(verb)."
                + (failed.isEmpty ? "" : "\n\nFehler:\n" + failed.prefix(10).joined(separator: "\n")
                    + (failed.count > 10 ? "\n… und \(failed.count - 10) weitere" : ""))
            rescan()
        }
    }

    // MARK: Sortieren

    func organize(_ plan: [Organizer.Move], copy: Bool, removeEmpty: Bool, root: URL) {
        let roots = sources.map(\.standardizedFileURL).filter { (try? $0.resourceValues(forKeys: [.isDirectoryKey]))?.isDirectory == true }
        progress = 0
        Task {
            let r = await Task.detached {
                Organizer.run(plan, copy: copy, removeEmpty: removeEmpty, roots: roots) { p in Task { @MainActor in self.progress = p } }
            }.value
            lastOps = r.ops
            lastRoot = root
            if !sources.contains(where: { $0.path == root.path || root.path.hasPrefix($0.path + "/") }) { sources.append(root) }
            progress = nil
            report = "\(r.ops.count) Dateien \(copy ? "kopiert" : "abgelegt"), \(r.skipped) übersprungen"
                + (r.removedDirs > 0 ? ", \(r.removedDirs) leere Ordner entfernt" : "") + "."
                + (r.errors.isEmpty ? "" : "\n\nFehler:\n" + r.errors.prefix(10).joined(separator: "\n"))
            rescan()
        }
    }

    func undoOrganize() {
        guard let root = lastRoot else { return }
        let ops = lastOps
        lastOps = []
        let errs = Organizer.undo(ops, root: root)
        report = "\(ops.count - errs.count) Dateien zurückgelegt." + (errs.isEmpty ? "" : "\n\nFehler:\n" + errs.prefix(10).joined(separator: "\n"))
        rescan()
    }

    // MARK: Dialoge

    func openPanel(files: Bool) {
        let p = NSOpenPanel()
        p.canChooseFiles = files
        p.canChooseDirectories = !files
        p.allowsMultipleSelection = true
        p.prompt = "Auswählen"
        p.message = files ? "Dateien auswählen" : "Ordner auswählen"
        if p.runModal() == .OK { add(p.urls) }
    }
}
