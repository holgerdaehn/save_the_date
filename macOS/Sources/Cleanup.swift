import Foundation
import Observation

/// Überflüssige Systemdateien (desktop.ini, Thumbs.db, .DS_Store, …) und leere Ordner finden.
enum Cleanup {
    struct Junk: Identifiable, Sendable {
        var id: URL { url }
        let url: URL
        let isDir: Bool
        let size: Int64
        let reason: String
    }

    static let names: Set<String> = [
        ".ds_store", "thumbs.db", "ehthumbs.db", "ehthumbs_vista.db", "desktop.ini", ".picasa.ini", "picasa.ini",
        ".localized", ".directory", "__macosx", ".appledouble", "@eadir", ".spotlight-v100", ".fseventsd", ".temporaryitems",
    ]

    /// Vorgabeliste, "._*" (AppleDouble-Reste) oder eigene Muster (* und ?, ohne Groß-/Kleinschreibung).
    static func reason(_ name: String, extra: [String]) -> String? {
        let n = name.lowercased()
        if names.contains(n) || n.hasPrefix("._") { return "Systemdatei" }
        return extra.contains { fnmatch($0, name, FNM_CASEFOLD) == 0 } ? "Eigenes Muster" : nil
    }

    /// Verzeichnisinhalt per readdir: Foundation blendet "._*"-Dateien aus, die hier gerade gefunden werden sollen.
    static func contents(_ dir: URL) -> [URL] {
        guard let d = opendir(dir.path) else { return [] }
        defer { closedir(d) }
        var out: [URL] = []
        while let e = readdir(d) {
            let name = withUnsafePointer(to: &e.pointee.d_name) {
                $0.withMemoryRebound(to: CChar.self, capacity: Int(NAME_MAX) + 1) { String(cString: $0) }
            }
            if name != "." && name != ".." { out.append(dir.appendingPathComponent(name)) }
        }
        return out
    }

    static func isJunk(_ name: String) -> Bool { reason(name, extra: []) != nil }

    /// Durchsucht die Wurzelordner rekursiv (inkl. versteckter Dateien). Reihenfolge: tiefste Einträge zuerst,
    /// damit Dateien vor ihren (dann leeren) Ordnern in den Papierkorb wandern.
    static func scan(_ roots: [URL], extra: [String], removeEmpty: Bool) -> [Junk] {
        var out: [Junk] = []
        let keys: Set<URLResourceKey> = [.isDirectoryKey, .isSymbolicLinkKey, .isPackageKey, .fileSizeKey]

        func walk(_ dir: URL, root: Bool) -> Bool {
            var empty = true
            for u in contents(dir) {
                let v = try? u.resourceValues(forKeys: keys)
                let isDir = v?.isDirectory == true && v?.isSymbolicLink != true
                if let r = reason(u.lastPathComponent, extra: extra) {
                    out.append(Junk(url: u, isDir: isDir, size: Int64(v?.fileSize ?? 0), reason: r))
                } else if isDir && v?.isPackage != true {
                    if !walk(u, root: false) { empty = false }
                } else {
                    empty = false
                }
            }
            if empty && !root && removeEmpty { out.append(Junk(url: dir, isDir: true, size: 0, reason: "Leerer Ordner")) }
            return empty
        }
        for r in roots { _ = walk(r.standardizedFileURL, root: true) }
        return out
    }
}

@MainActor @Observable
final class CleanModel {
    var found: [Cleanup.Junk] = []
    var marked = Set<URL>()
    var scanning = false

    func scan(_ roots: [URL], extra: [String], removeEmpty: Bool) {
        scanning = true
        Task {
            let f = await Task.detached { Cleanup.scan(roots, extra: extra, removeEmpty: removeEmpty) }.value
            found = f
            marked = Set(f.map(\.id))
            scanning = false
        }
    }

    var size: Int64 { found.filter { marked.contains($0.id) }.reduce(0) { $0 + $1.size } }

    func trash() -> (ok: Int, errors: [String]) {
        var ok = 0, errs: [String] = []
        for j in found where marked.contains(j.id) {
            do { try FileManager.default.trashItem(at: j.url, resultingItemURL: nil); ok += 1 }
            catch { if FileManager.default.fileExists(atPath: j.url.path) { errs.append("\(j.url.lastPathComponent): \(error.localizedDescription)") } else { ok += 1 } }
        }
        return (ok, errs)
    }
}
