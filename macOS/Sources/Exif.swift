import Foundation
import ImageIO

struct Meta: Sendable {
    var taken: Date?
    var lat: Double?
    var lon: Double?
}

enum GeoEdit: Equatable, Sendable {
    case keep
    case set(Double, Double)
    case remove
}

struct EditError: LocalizedError {
    let errorDescription: String?
    init(_ m: String) { errorDescription = m }
}

enum Exif {
    private static let df: DateFormatter = {
        let f = DateFormatter()
        f.locale = Locale(identifier: "en_US_POSIX")
        f.dateFormat = "yyyy:MM:dd HH:mm:ss"
        return f
    }()

    static func read(_ url: URL) -> Meta {
        let opts = [kCGImageSourceShouldCache: false] as CFDictionary
        guard let src = CGImageSourceCreateWithURL(url as CFURL, opts),
              let p = CGImageSourceCopyPropertiesAtIndex(src, 0, nil) as? [CFString: Any] else { return Meta() }
        let ex = p[kCGImagePropertyExifDictionary] as? [CFString: Any]
        let raw = (ex?[kCGImagePropertyExifDateTimeOriginal] ?? ex?[kCGImagePropertyExifDateTimeDigitized]) as? String
        let g = p[kCGImagePropertyGPSDictionary] as? [CFString: Any]
        func coord(_ k: CFString, _ ref: CFString, _ neg: String) -> Double? {
            guard let v = g?[k] as? Double else { return nil }
            return (g?[ref] as? String) == neg ? -v : v
        }
        return Meta(taken: raw.flatMap(df.date(from:)),
                    lat: coord(kCGImagePropertyGPSLatitude, kCGImagePropertyGPSLatitudeRef, "S"),
                    lon: coord(kCGImagePropertyGPSLongitude, kCGImagePropertyGPSLongitudeRef, "W"))
    }

    /// Schreibt EXIF-Aufnahmedatum / GPS verlustfrei (ohne Neukodierung der Bilddaten).
    static func write(_ url: URL, taken: Date?, geo: GeoEdit) throws {
        if geo != .keep {
            let md = CGImageMetadataCreateMutable()
            func set(_ path: String, _ v: CFTypeRef) { CGImageMetadataSetValueWithPath(md, nil, path as CFString, v) }
            switch geo {
            case .set(let la, let lo):
                set("exif:GPSLatitude", dms(la, "N", "S") as CFString)
                set("exif:GPSLongitude", dms(lo, "E", "W") as CFString)
            default:
                for k in ["Latitude", "Longitude", "Altitude", "AltitudeRef", "VersionID", "TimeStamp", "DateStamp"] { set("exif:GPS" + k, kCFNull) }
            }
            try copy(url, [kCGImageDestinationMetadata: md, kCGImageDestinationMergeMetadata: true])
        }
        // ImageIO erlaubt Metadaten und Datum nicht im selben Durchgang.
        if let taken { try copy(url, [kCGImageDestinationDateTime: taken]) }
    }

    private static func copy(_ url: URL, _ opts: [CFString: Any]) throws {
        guard let src = CGImageSourceCreateWithURL(url as CFURL, nil), let type = CGImageSourceGetType(src) else {
            throw EditError("Kein unterstütztes Bildformat")
        }
        let tmp = url.deletingLastPathComponent().appendingPathComponent(".\(UUID().uuidString).tmp")
        guard let dest = CGImageDestinationCreateWithURL(tmp as CFURL, type, 1, nil) else {
            throw EditError("Format kann nicht geschrieben werden")
        }
        var err: Unmanaged<CFError>?
        guard CGImageDestinationCopyImageSource(dest, src, opts as CFDictionary, &err) else {
            try? FileManager.default.removeItem(at: tmp)
            throw EditError(err?.takeRetainedValue().localizedDescription ?? "Metadaten konnten nicht geschrieben werden")
        }
        _ = try FileManager.default.replaceItemAt(url, withItemAt: tmp)
    }

    private static func dms(_ v: Double, _ pos: String, _ neg: String) -> String {
        let a = abs(v), d = Int(a)
        return String(format: "%d,%.6f%@", d, (a - Double(d)) * 60, v >= 0 ? pos : neg)
    }
}
