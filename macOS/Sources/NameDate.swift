import Foundation

/// Datum aus Dateinamen wie `IMG-20190713-WA0001.jpg`, `PXL_20210101_120533123.jpg`,
/// `Bildschirmfoto 2020-02-18 um 14.22.05.png` oder `signal-1612345678901.jpg`.
enum NameDate {
    private static let date = try! NSRegularExpression(pattern:
        #"(?<!\d)((?:19|20)\d\d)[-_.]?(0[1-9]|1[0-2])[-_.]?(0[1-9]|[12]\d|3[01])(?:(?:[-_ T.]|\s(?:at|um)\s)?([01]\d|2[0-3])[-_.:]?([0-5]\d)[-_.:]?([0-5]\d)\d{0,3})?(?!\d)"#)
    private static let epoch = try! NSRegularExpression(pattern: #"(?<!\d)(1\d{12}|1\d{9})(?!\d)"#)

    /// Ohne Uhrzeit im Namen gilt 12:00:00 (so verrutscht das Datum nicht durch Zeitzonen).
    static func parse(_ name: String, calendar cal: Calendar = .current) -> Date? {
        let ns = name as NSString
        let all = NSRange(location: 0, length: ns.length)
        if let m = date.firstMatch(in: name, range: all) {
            func n(_ i: Int) -> Int? { m.range(at: i).location == NSNotFound ? nil : Int(ns.substring(with: m.range(at: i))) }
            let (y, mo, d) = (n(1)!, n(2)!, n(3)!)
            var c = DateComponents(year: y, month: mo, day: d, hour: n(4) ?? 12, minute: n(5) ?? 0, second: n(6) ?? 0)
            c.calendar = cal
            if y >= 1900, y <= cal.component(.year, from: Date()) + 1, let r = cal.date(from: c),
               cal.dateComponents([.year, .month, .day], from: r) == DateComponents(year: y, month: mo, day: d) { return r }
        }
        if let m = epoch.firstMatch(in: name, range: all) {
            let s = ns.substring(with: m.range(at: 1))
            if let v = Double(s) { return Date(timeIntervalSince1970: s.count == 13 ? v / 1000 : v) }
        }
        return nil
    }
}
