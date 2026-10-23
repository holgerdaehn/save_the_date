import Foundation
import ImageIO
import Testing
import UniformTypeIdentifiers
@testable import SaveTheDate

private func tempDir() throws -> URL {
    let d = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    try FileManager.default.createDirectory(at: d, withIntermediateDirectories: true)
    return d
}

private func makeJPEG(_ url: URL) {
    let ctx = CGContext(data: nil, width: 64, height: 64, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
    for i in 0 ..< 8 {
        ctx.setFillColor(red: CGFloat(i) / 8, green: 0.4, blue: 1 - CGFloat(i) / 8, alpha: 1)
        ctx.fill(CGRect(x: i * 8, y: 0, width: 8, height: 64))
    }
    let dest = CGImageDestinationCreateWithURL(url as CFURL, UTType.jpeg.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(dest, ctx.makeImage()!, nil)
    CGImageDestinationFinalize(dest)
}

private func pixels(_ url: URL) -> Data? {
    guard let s = CGImageSourceCreateWithURL(url as CFURL, nil), let i = CGImageSourceCreateImageAtIndex(s, 0, nil) else { return nil }
    return i.dataProvider?.data as Data?
}

@Test func exifRoundtripIsLossless() throws {
    let f = try tempDir().appendingPathComponent("a.jpg")
    makeJPEG(f)
    let before = pixels(f)
    let t = Date(timeIntervalSince1970: 1_600_000_000)
    try Exif.write(f, taken: t, geo: .set(52.52, -13.405))
    let m = Exif.read(f)
    #expect(m.taken == t)
    #expect(abs(m.lat! - 52.52) < 1e-5 && abs(m.lon! + 13.405) < 1e-5)
    try Exif.write(f, taken: nil, geo: .remove)
    #expect(Exif.read(f).lat == nil && Exif.read(f).taken == t)
    #expect(pixels(f) == before)
}

@Test func fileTimesBothOrders() throws {
    let f = try tempDir().appendingPathComponent("b.txt")
    try "x".write(to: f, atomically: true, encoding: .utf8)
    let a = Date(timeIntervalSince1970: 1_000_000_000), b = Date(timeIntervalSince1970: 1_100_000_000)
    for (c, m) in [(a, b), (b, a)] {
        let it = Item(id: f, isDir: false, isImage: false, created: nil, modified: nil)
        try Apply.one(it, Plan(created: c, modified: m), .keep)
        let at = try FileManager.default.attributesOfItem(atPath: f.path)
        #expect(at[.creationDate] as? Date == c && at[.modificationDate] as? Date == m)
    }
}

@Test func scanRecursiveAndDedupe() throws {
    let d = try tempDir()
    try FileManager.default.createDirectory(at: d.appendingPathComponent("sub"), withIntermediateDirectories: true)
    for p in ["x.txt", "sub/y.txt"] { try "x".write(to: d.appendingPathComponent(p), atomically: true, encoding: .utf8) }
    let all = Scan.list([d, d.appendingPathComponent("x.txt")], ScanOptions(recursive: true, dirs: false, hidden: false))
    #expect(all.count == 2)
    #expect(Scan.list([d], ScanOptions(recursive: false, dirs: false, hidden: false)).count == 1)
}

// MARK: Duplikate, Sortieren, Aufräumen

private func write(_ url: URL, _ bytes: [UInt8], date: Date? = nil) throws {
    try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
    try Data(bytes).write(to: url)
    if let date { try FileManager.default.setAttributes([.modificationDate: date], ofItemAtPath: url.path) }
}

private let all = ScanOptions(recursive: true, dirs: false, hidden: true)

@Test func findsDuplicatesWithDifferentNames() async throws {
    let d = try tempDir()
    let big = (0 ..< 200_000).map { UInt8($0 % 251) }
    var other = big
    other[199_999] ^= 1                       // gleiche Größe, Unterschied erst hinter den ersten 64 KB
    try write(d.appendingPathComponent("a.jpg"), big)
    try write(d.appendingPathComponent("x/ganz-anders.jpg"), big)
    try write(d.appendingPathComponent("c.jpg"), other)
    try write(d.appendingPathComponent("d.txt"), [1, 2, 3])
    try write(d.appendingPathComponent("e.txt"), [1, 2, 3])
    try write(d.appendingPathComponent("leer1"), [])
    try write(d.appendingPathComponent("leer2"), [])
    let groups = await Dupes.find(Scan.list([d], all)) { _ in }
    let names = groups.map { $0.map(\.name).sorted() }
    #expect(names.count == 2)
    #expect(names.contains(["a.jpg", "ganz-anders.jpg"]) && names.contains(["d.txt", "e.txt"]))
    #expect(Dupes.keeper(groups[0], .shortest).name == "a.jpg")
}

@Test func organizesByMonthAndUndoes() throws {
    let d = try tempDir()
    let jul = Date(timeIntervalSince1970: 1_563_000_000)   // 2019-07-13
    let feb = Date(timeIntervalSince1970: 1_582_000_000)   // 2020-02-18
    try write(d.appendingPathComponent("alt/IMG_1.jpg"), [1, 1], date: jul)
    try write(d.appendingPathComponent("neu/IMG_1.jpg"), [2, 2], date: jul)       // gleicher Name, anderer Inhalt
    try write(d.appendingPathComponent("neu/IMG_2.jpg"), [3, 3], date: feb)
    try write(d.appendingPathComponent("neu/notiz.txt"), [4], date: feb)           // kein Medium
    let out = d.appendingPathComponent("Sortiert")
    let items = Scan.list([d], all)
    let plan = Organizer.plan(items, root: out, pattern: .ym, source: .modified, onlyMedia: true)
    let rel = plan.map { $0.dst.path.replacingOccurrences(of: out.path + "/", with: "") }.sorted()
    #expect(rel == ["2019/07/IMG_1 (2).jpg", "2019/07/IMG_1.jpg", "2020/02/IMG_2.jpg"])

    let r = Organizer.run(plan, copy: false, removeEmpty: true, roots: [d.standardizedFileURL]) { _ in }
    #expect(r.ops.count == 3 && r.errors.isEmpty)
    #expect(FileManager.default.fileExists(atPath: out.appendingPathComponent("2020/02/IMG_2.jpg").path))
    #expect(!FileManager.default.fileExists(atPath: d.appendingPathComponent("alt").path))      // leer → entfernt
    #expect(FileManager.default.fileExists(atPath: d.appendingPathComponent("neu/notiz.txt").path))

    #expect(Organizer.undo(r.ops, root: out).isEmpty)
    #expect(FileManager.default.fileExists(atPath: d.appendingPathComponent("alt/IMG_1.jpg").path))
    #expect(!FileManager.default.fileExists(atPath: out.appendingPathComponent("2019").path))

    // Identische Datei am Ziel wird übersprungen
    try write(out.appendingPathComponent("2020/02/IMG_2.jpg"), [3, 3], date: feb)
    let again = Organizer.plan(Scan.list([d.appendingPathComponent("neu")], all), root: out, pattern: .ym, source: .modified, onlyMedia: true)
    #expect(again.first { $0.item.name == "IMG_2.jpg" }?.skip != nil)
}

@Test func cleanupFindsJunkAndEmptyFolders() throws {
    let d = try tempDir()
    try write(d.appendingPathComponent("foto.jpg"), [1])
    for n in ["desktop.ini", ".DS_Store", "._foto.jpg", "x.tmp"] { try write(d.appendingPathComponent(n), [0]) }
    try write(d.appendingPathComponent("nurmuell/Thumbs.db"), [0])
    try FileManager.default.createDirectory(at: d.appendingPathComponent("leer/tief"), withIntermediateDirectories: true)
    let found = Cleanup.scan([d], extra: ["*.TMP"], removeEmpty: true).map { $0.url.lastPathComponent }
    for n in ["desktop.ini", ".DS_Store", "._foto.jpg", "x.tmp", "Thumbs.db", "nurmuell", "leer", "tief"] { #expect(found.contains(n), "\(n) fehlt in \(found)") }
    #expect(!found.contains("foto.jpg"))
    #expect(found.firstIndex(of: "tief")! < found.firstIndex(of: "leer")!)
    #expect(!Cleanup.scan([d], extra: [], removeEmpty: false).contains { $0.reason == "Leerer Ordner" })
}
