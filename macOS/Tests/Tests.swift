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
    for n in ["desktop.ini", ".DS_Store", "._foto.jpg", "._weg.jpg", "x.tmp"] { try write(d.appendingPathComponent(n), [0]) }
    try write(d.appendingPathComponent("nurmuell/Thumbs.db"), [0])
    try FileManager.default.createDirectory(at: d.appendingPathComponent("leer/tief"), withIntermediateDirectories: true)
    let found = Cleanup.scan([d], extra: ["*.TMP"], removeEmpty: true).map { $0.url.lastPathComponent }
    for n in ["desktop.ini", ".DS_Store", "._weg.jpg", "x.tmp", "Thumbs.db", "nurmuell", "leer", "tief"] { #expect(found.contains(n), "\(n) fehlt in \(found)") }
    #expect(!found.contains("foto.jpg") && !found.contains("._foto.jpg"))
    #expect(found.firstIndex(of: "tief")! < found.firstIndex(of: "leer")!)
    #expect(!Cleanup.scan([d], extra: [], removeEmpty: false).contains { $0.reason == "Leerer Ordner" })
}

// MARK: Dateiname-Datum, Video, GPX, Umbenennen, Ähnliche Bilder, Filter

private func ymdhms(_ y: Int, _ mo: Int, _ d: Int, _ h: Int = 12, _ mi: Int = 0, _ s: Int = 0) -> Date {
    Calendar.current.date(from: DateComponents(year: y, month: mo, day: d, hour: h, minute: mi, second: s))!
}

@Test func nameDateParsing() {
    #expect(NameDate.parse("IMG-20190713-WA0001.jpg") == ymdhms(2019, 7, 13))
    #expect(NameDate.parse("PXL_20210101_120533123.jpg") == ymdhms(2021, 1, 1, 12, 5, 33))
    #expect(NameDate.parse("Bildschirmfoto 2020-02-18 um 14.22.05.png") == ymdhms(2020, 2, 18, 14, 22, 5))
    #expect(NameDate.parse("Screenshot_20200218-142205.png") == ymdhms(2020, 2, 18, 14, 22, 5))
    #expect(NameDate.parse("2019-07-13 14.22.05.jpg") == ymdhms(2019, 7, 13, 14, 22, 5))
    #expect(NameDate.parse("signal-1612345678901.jpg") == Date(timeIntervalSince1970: 1_612_345_678.901))
    #expect(NameDate.parse("19860701_120000000_iOS 1.jpg") == ymdhms(1986, 7, 1, 12, 0, 0))
    #expect(NameDate.parse("IMG_20190230_101010.jpg") == nil)         // 30. Februar
    #expect(NameDate.parse("IMG_4711.jpg") == nil && NameDate.parse("foto 120190713123.jpg") == nil)
}

/// Minimales MP4: ftyp, moov{mvhd, trak{tkhd(v1), mdia{mdhd}}}, mdat.
private func makeMP4(_ url: URL) throws {
    func be(_ v: UInt64, _ n: Int) -> [UInt8] { (0 ..< n).map { UInt8(truncatingIfNeeded: v >> (8 * (n - 1 - $0))) } }
    func box(_ t: String, _ body: [UInt8]) -> [UInt8] { be(UInt64(body.count + 8), 4) + Array(t.utf8) + body }
    let mvhd = box("mvhd", [0, 0, 0, 0] + be(0, 4) + be(0, 4) + be(1000, 4) + be(0, 4))          // Version 0, Datum 0
    let tkhd = box("tkhd", [1, 0, 0, 3] + be(0, 8) + be(0, 8) + be(1, 4))                       // Version 1
    let mdhd = box("mdhd", [0, 0, 0, 0] + be(0, 4) + be(0, 4) + be(600, 4))
    let moov = box("moov", mvhd + box("trak", tkhd + box("mdia", mdhd)))
    try Data(box("ftyp", Array("isom".utf8) + be(0, 4)) + moov + box("mdat", [1, 2, 3, 4, 5])).write(to: url)
}

@Test func mp4DateRoundtrip() throws {
    let f = try tempDir().appendingPathComponent("v.mp4")
    try makeMP4(f)
    let before = try Data(contentsOf: f)
    #expect(Mp4.read(f) == nil)
    let t = Date(timeIntervalSince1970: 1_600_000_000)
    try Mp4.write(f, t)
    #expect(Mp4.read(f) == t)
    let after = try Data(contentsOf: f)
    #expect(after.count == before.count && after.suffix(13) == before.suffix(13))              // mdat unberührt
    #expect(Exif.read(f).taken == t)
    #expect(throws: (any Error).self) { try Mp4.write(try tempDir().appendingPathComponent("x.mp4").also { try Data([1, 2, 3, 4, 5, 6, 7, 8]).write(to: $0) }, t) }
}

private extension URL { func also(_ f: (URL) throws -> Void) rethrows -> URL { try f(self); return self } }

@Test func gpxParseAndLocate() throws {
    let gpx = """
    <?xml version="1.0"?><gpx><trk><trkseg>
    <trkpt lat="52.0" lon="13.0"><time>2024-07-13T10:00:00Z</time></trkpt>
    <trkpt lat="52.2" lon="13.2"><time>2024-07-13T10:10:00.500Z</time></trkpt>
    <trkpt lat="53.0" lon="14.0"><time>2024-07-13T15:00:00+02:00</time></trkpt>
    </trkseg></trk><wpt lat="1" lon="2"></wpt></gpx>
    """
    let f = try tempDir().appendingPathComponent("t.gpx")
    try gpx.write(to: f, atomically: true, encoding: .utf8)
    let pts = Gpx.parse([f])
    #expect(pts.count == 3)
    let t0 = Gpx.date("2024-07-13T10:00:00Z")!
    let mid = Gpx.locate(pts, at: t0.addingTimeInterval(300), maxGap: 900)!
    #expect(abs(mid.lat - 52.1) < 0.001 && abs(mid.lon - 13.1) < 0.001)                         // interpoliert
    #expect(Gpx.locate(pts, at: t0.addingTimeInterval(-120), maxGap: 300)?.lat == 52.0)          // nächster Punkt
    #expect(Gpx.locate(pts, at: t0.addingTimeInterval(-3600), maxGap: 300) == nil)
    #expect(Gpx.locate(pts, at: t0.addingTimeInterval(7200), maxGap: 300) == nil)                // Lücke im Track
    #expect(Gpx.locate([], at: t0, maxGap: 300) == nil)
}

@Test func renamePlanRunUndo() throws {
    let d = try tempDir()
    let t1 = ymdhms(2024, 7, 13, 14, 22, 5)
    try write(d.appendingPathComponent("IMG_1.JPG"), [1], date: t1)
    try write(d.appendingPathComponent("IMG_2.jpg"), [2], date: t1)                              // gleiche Sekunde
    try write(d.appendingPathComponent("2024-07-13_14-22-05.jpg"), [3], date: ymdhms(2024, 7, 14))        // belegt den Wunschnamen
    try write(d.appendingPathComponent("notiz.txt"), [4], date: t1)
    let items = Scan.list([d], all)
    let plan = Renamer.plan(items, template: Renamer.defaultTemplate, source: .modified, lowercaseExt: true, onlyMedia: true)
    let names = plan.filter { $0.skip == nil }.map { $0.dst.lastPathComponent }.sorted()
    #expect(names == ["2024-07-13_14-22-05 (2).jpg", "2024-07-13_14-22-05 (3).jpg", "2024-07-14_12-00-00.jpg"])
    #expect(!plan.contains { $0.item.name == "notiz.txt" })
    let r = Renamer.run(plan) { _ in }
    #expect(r.ops.count == 3 && r.errors.isEmpty)
    #expect(FileManager.default.fileExists(atPath: d.appendingPathComponent("2024-07-13_14-22-05 (2).jpg").path))
    #expect(!FileManager.default.fileExists(atPath: d.appendingPathComponent("IMG_1.JPG").path))
    #expect(Renamer.undo(r.ops).isEmpty)
    #expect(FileManager.default.fileExists(atPath: d.appendingPathComponent("IMG_1.JPG").path))

    // Nur Groß-/Kleinschreibung ändern (APFS ist standardmäßig nicht zwischen Groß- und Kleinschreibung unterscheidend)
    let again = Renamer.plan(Scan.list([d.appendingPathComponent("IMG_1.JPG")], all), template: "{Name}", source: .modified, lowercaseExt: true, onlyMedia: true)
    #expect(again.first?.skip == nil && again.first?.dst.lastPathComponent == "IMG_1.jpg")
    #expect(Renamer.run(again) { _ in }.errors.isEmpty)
    #expect(try FileManager.default.contentsOfDirectory(atPath: d.path).contains("IMG_1.jpg"))

    #expect(Renamer.render("{Nr} {Name}: a/b", date: nil, name: "x", nr: 7, count: 12) == "007 x ab")
}

@Test @MainActor func copyModeFilterAndUndoRecord() {
    let s = Store()
    let a = Item(id: URL(fileURLWithPath: "/t/IMG-20190713-WA0001.jpg"), isDir: false, isImage: true,
                 created: ymdhms(2024, 1, 1), modified: ymdhms(2024, 1, 1), taken: ymdhms(2019, 7, 13, 10))
    let b = Item(id: URL(fileURLWithPath: "/t/notiz.txt"), isDir: false, isImage: false,
                 created: ymdhms(2024, 1, 2), modified: ymdhms(2023, 1, 2))
    s.items = [a, b]
    s.s.mode = .copy; s.s.setCreated = true; s.s.setModified = false
    s.s.origin = .name
    #expect(s.plans()[a.id]?.created == ymdhms(2019, 7, 13) && s.plans()[b.id]?.isEmpty == true)
    s.s.origin = .oldest
    #expect(s.plans()[a.id]?.created == ymdhms(2019, 7, 13, 10) && s.plans()[b.id]?.created == ymdhms(2023, 1, 2))
    s.s.origin = .taken; s.s.setTaken = true
    #expect(s.plans()[a.id]?.taken == nil)                                                       // schon gleich → keine Änderung

    // Ohne Auswahl wirkt nur, was der Filter zeigt
    s.filter = .noTaken
    #expect(s.targets.isEmpty)                                                                   // Fotos haben ein Aufnahmedatum, .txt kann keins haben
    s.filter = .all; s.search = "notiz"
    #expect(s.targets.map(\.name) == ["notiz.txt"])
    s.selection = [a.id]
    #expect(s.targets.map(\.name) == ["IMG-20190713-WA0001.jpg"])

    let r = UndoRec(created: a.created, modified: a.modified, taken: a.taken, lat: nil, lon: nil, touchedTaken: true, touchedGeo: true)
    #expect(r.restore.geo == .remove && r.restore.plan.taken == a.taken)
    #expect(UndoRec(created: nil, modified: nil, taken: nil, lat: 1, lon: 2, touchedTaken: false, touchedGeo: true).restore.geo == .set(1, 2))
}

@Test func exifUndoRestoresTakenAndGeo() throws {
    let f = try tempDir().appendingPathComponent("u.jpg")
    makeJPEG(f)
    let old = Date(timeIntervalSince1970: 1_500_000_000)
    try Exif.write(f, taken: old, geo: .set(10, 20))
    let item = Scan.list([f], all)[0]
    var it = item; let m = Exif.read(f); it.taken = m.taken; it.lat = m.lat; it.lon = m.lon
    try Apply.one(it, Plan(taken: Date(timeIntervalSince1970: 1_600_000_000)), .remove)
    #expect(Exif.read(f).lat == nil && Exif.read(f).taken == Date(timeIntervalSince1970: 1_600_000_000))
    let rec = UndoRec(created: it.created, modified: it.modified, taken: it.taken, lat: it.lat, lon: it.lon, touchedTaken: true, touchedGeo: true).restore
    try Apply.one(Scan.list([f], all)[0], rec.plan, rec.geo)
    let back = Exif.read(f)
    #expect(back.taken == old && abs(back.lat! - 10) < 1e-5 && abs(back.lon! - 20) < 1e-5)
    let mt = try FileManager.default.attributesOfItem(atPath: f.path)[.modificationDate] as? Date
    #expect(abs(mt!.timeIntervalSince(it.modified!)) < 1)
}

@Test func similarImagesAndOrganizerDefaults() throws {
    func pic(_ url: URL, size: Int, invert: Bool) {
        let ctx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0,
                            space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
        for i in 0 ..< 8 {
            let v = CGFloat(invert ? 7 - i : i) / 7
            ctx.setFillColor(red: v, green: v * v, blue: 1 - v, alpha: 1)
            ctx.fill(CGRect(x: i * size / 8, y: 0, width: size / 8 + 1, height: size))
        }
        let dest = CGImageDestinationCreateWithURL(url as CFURL, UTType.jpeg.identifier as CFString, 1, nil)!
        CGImageDestinationAddImage(dest, ctx.makeImage()!, [kCGImageDestinationLossyCompressionQuality: 0.5] as CFDictionary)
        CGImageDestinationFinalize(dest)
    }
    let d = try tempDir()
    pic(d.appendingPathComponent("a.jpg"), size: 256, invert: false)
    pic(d.appendingPathComponent("a-klein.jpg"), size: 96, invert: false)
    pic(d.appendingPathComponent("b.jpg"), size: 256, invert: true)
    let h = ["a.jpg", "a-klein.jpg", "b.jpg"].map { Similar.hash(d.appendingPathComponent($0))! }
    #expect((h[0] ^ h[1]).nonzeroBitCount <= 6 && (h[0] ^ h[2]).nonzeroBitCount > 10)
    #expect(Similar.cluster([1, 3, 0xFFFF_FFFF_FFFF_FFFF, nil, 0xFFFF_FFFF_FFFF_FFFE], 2).map(\.count).sorted() == [2, 2])

    let it = Item(id: URL(fileURLWithPath: "/x/IMG-20190713-WA0001.jpg"), isDir: false, isImage: true, created: nil, modified: ymdhms(2024, 1, 1))
    #expect(DateSource.smart.date(it) == ymdhms(2019, 7, 13) && DateSource.taken.date(it) == ymdhms(2024, 1, 1))
    #expect(Pattern.yearDay.path(ymdhms(2024, 7, 13)) == "2024/2024-07-13" && Pattern.year.path(ymdhms(2024, 7, 13)) == "2024")
}

@Test func csvQuotingAndInjection() {
    let it = Item(id: URL(fileURLWithPath: "/x/=SUM(A1);\"q\".txt"), isDir: false, isImage: false, size: 5, created: nil, modified: nil)
    let csv = Csv.make([it])
    #expect(csv.hasPrefix("\u{FEFF}Pfad;Name;Art"))
    #expect(csv.contains("\"'=SUM(A1);\"\"q\"\".txt\"") && csv.contains(";Datei;5;"))
}

@Test func pruneKeepsJunkFolders() throws {
    let d = try tempDir()
    try write(d.appendingPathComponent("a/__MACOSX/x"), [1])
    try write(d.appendingPathComponent("b/.DS_Store"), [0])
    #expect(Organizer.pruneEmpty([d.appendingPathComponent("a"), d.appendingPathComponent("b")], roots: [d]) == 1)
    #expect(FileManager.default.fileExists(atPath: d.appendingPathComponent("a/__MACOSX/x").path))
}
