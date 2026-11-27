// Setzt Erstell- und Änderungsdatum von Bildern auf den Zeitstempel im Dateinamen,
// z. B. "19860701_120000000_iOS 1.jpg" -> 01.07.1986 12:00:00.000 (lokale Zeit).
//
// Aufruf:  swift datum-aus-name.swift <Ordner>        ändern
//          swift datum-aus-name.swift -n <Ordner>     nur anzeigen (Probelauf)
import Foundation
import UniformTypeIdentifiers

var args = Array(CommandLine.arguments.dropFirst())
let dry = args.first == "-n"
if dry { args.removeFirst() }
guard let path = args.first else {
    print("Aufruf: swift datum-aus-name.swift [-n] <Ordner>"); exit(1)
}

let fm = FileManager.default
var isDir: ObjCBool = false
guard fm.fileExists(atPath: path, isDirectory: &isDir), isDir.boolValue else {
    print("Ordner nicht gefunden: \(path)\nNetzlaufwerke über /Volumes/… angeben, nicht als smb://-Adresse."); exit(1)
}
let re = try! NSRegularExpression(pattern: #"^(\d{8})_(\d{9})_iOS"#)
let fmt = DateFormatter()
fmt.locale = Locale(identifier: "en_US_POSIX")
fmt.timeZone = .current
fmt.dateFormat = "yyyyMMddHHmmssSSS"

let keys: Set<URLResourceKey> = [.contentTypeKey, .creationDateKey, .contentModificationDateKey, .isRegularFileKey]
guard let en = fm.enumerator(at: URL(fileURLWithPath: path), includingPropertiesForKeys: Array(keys), options: [.skipsHiddenFiles]) else {
    print("Ordner nicht lesbar: \(path)"); exit(1)
}

var ok = 0, fixed = 0, failed = 0
for case let u as URL in en {
    guard let v = try? u.resourceValues(forKeys: keys), v.isRegularFile == true,
          v.contentType?.conforms(to: .image) == true else { continue }
    let name = u.lastPathComponent
    guard let m = re.firstMatch(in: name, range: NSRange(name.startIndex..., in: name)) else { continue }
    // Ungültige Zeitstempel (z. B. 31. Juni oder 02:30 bei Beginn der Sommerzeit) melden statt still überspringen.
    guard let d = fmt.date(from: (name as NSString).substring(with: m.range(at: 1)) + (name as NSString).substring(with: m.range(at: 2))) else {
        failed += 1
        print("FEHLER: ungültiger Zeitstempel im Namen: \(u.path)")
        continue
    }

    let same = { (x: Date?) in x.map { abs($0.timeIntervalSince(d)) < 1 } ?? false }
    if same(v.creationDate) && same(v.contentModificationDate) { ok += 1; continue }

    print("\(dry ? "würde ändern" : "ändere"): \(u.path)")
    if dry { fixed += 1; continue }
    do {
        // Erst "geändert", dann "erstellt": macOS zieht sonst das Erstelldatum herunter.
        try fm.setAttributes([.modificationDate: d], ofItemAtPath: u.path)
        try fm.setAttributes([.creationDate: d], ofItemAtPath: u.path)
        let a = try fm.attributesOfItem(atPath: u.path)
        guard same(a[.creationDate] as? Date), same(a[.modificationDate] as? Date) else {
            throw NSError(domain: "", code: 0, userInfo: [NSLocalizedDescriptionKey: "Datum wurde vom Speicherort nicht übernommen"])
        }
        fixed += 1
    } catch {
        failed += 1
        print("  FEHLER: \(error.localizedDescription)")
    }
}
print("\nPassend: \(ok)  \(dry ? "Zu ändern" : "Geändert"): \(fixed)  Fehler: \(failed)")
