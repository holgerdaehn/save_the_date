import Foundation

struct GpxPoint: Sendable {
    let t: Date
    let lat: Double
    let lon: Double
}

/// GPS-Tracklogs (GPX) lesen und Fotos anhand der Aufnahmezeit einen Standort zuordnen.
enum Gpx {
    static func parse(_ urls: [URL]) -> [GpxPoint] {
        urls.flatMap { u -> [GpxPoint] in
            guard let p = XMLParser(contentsOf: u) else { return [] }
            let d = Reader()
            p.delegate = d
            p.parse()
            return d.points
        }.sorted { $0.t < $1.t }
    }

    /// Standort zur Zeit `t`: zwischen zwei Trackpunkten interpoliert, sonst der nächste Punkt – jeweils
    /// nur, wenn er höchstens `maxGap` Sekunden entfernt ist.
    static func locate(_ p: [GpxPoint], at t: Date, maxGap: TimeInterval) -> (lat: Double, lon: Double)? {
        var lo = 0, hi = p.count
        while lo < hi { let m = (lo + hi) / 2; if p[m].t < t { lo = m + 1 } else { hi = m } }
        let next = lo < p.count ? p[lo] : nil, prev = lo > 0 ? p[lo - 1] : nil
        let dn = next.map { $0.t.timeIntervalSince(t) } ?? .infinity
        let dp = prev.map { t.timeIntervalSince($0.t) } ?? .infinity
        if let a = prev, let b = next, dp <= maxGap, dn <= maxGap {
            let span = b.t.timeIntervalSince(a.t)
            let w = span > 0 ? dp / span : 0
            return (a.lat + (b.lat - a.lat) * w, a.lon + (b.lon - a.lon) * w)
        }
        if let b = next, dn <= dp, dn <= maxGap { return (b.lat, b.lon) }
        if let a = prev, dp <= maxGap { return (a.lat, a.lon) }
        return nil
    }

    private static let fraction: ISO8601DateFormatter = {
        let f = ISO8601DateFormatter()
        f.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return f
    }()
    private static let plain = ISO8601DateFormatter()

    static func date(_ s: String) -> Date? {
        let s = s.trimmingCharacters(in: .whitespacesAndNewlines)
        return plain.date(from: s) ?? fraction.date(from: s)
    }

    private final class Reader: NSObject, XMLParserDelegate {
        var points: [GpxPoint] = []
        private var cur: (lat: Double, lon: Double)?
        private var text: String?

        func parser(_ p: XMLParser, didStartElement name: String, namespaceURI: String?, qualifiedName: String?,
                    attributes a: [String: String]) {
            switch name.lowercased() {
            case "trkpt", "rtept", "wpt":
                if let la = Double(a["lat"] ?? ""), let lo = Double(a["lon"] ?? ""), abs(la) <= 90, abs(lo) <= 180 { cur = (la, lo) } else { cur = nil }
            case "time": if cur != nil { text = "" }
            default: break
            }
        }

        func parser(_ p: XMLParser, foundCharacters s: String) { text?.append(s) }

        func parser(_ p: XMLParser, didEndElement name: String, namespaceURI: String?, qualifiedName: String?) {
            switch name.lowercased() {
            case "time":
                if let c = cur, let s = text, let d = Gpx.date(s) { points.append(GpxPoint(t: d, lat: c.lat, lon: c.lon)) }
                text = nil
            case "trkpt", "rtept", "wpt": cur = nil
            default: break
            }
        }
    }
}
