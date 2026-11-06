import Foundation
import ImageIO

/// Ähnliche Bilder (auch skaliert, neu komprimiert oder leicht bearbeitet) über einen 64-Bit-Differenz-Hash.
enum Similar {
    static func hash(_ url: URL) -> UInt64? {
        let o = [kCGImageSourceCreateThumbnailFromImageAlways: true, kCGImageSourceCreateThumbnailWithTransform: true,
                 kCGImageSourceThumbnailMaxPixelSize: 128] as CFDictionary
        guard let src = CGImageSourceCreateWithURL(url as CFURL, nil),
              let img = CGImageSourceCreateThumbnailAtIndex(src, 0, o) else { return nil }
        var px = [UInt8](repeating: 0, count: 9 * 8)
        let ok = px.withUnsafeMutableBytes { b -> Bool in
            guard let ctx = CGContext(data: b.baseAddress, width: 9, height: 8, bitsPerComponent: 8, bytesPerRow: 9,
                                      space: CGColorSpaceCreateDeviceGray(), bitmapInfo: CGImageAlphaInfo.none.rawValue) else { return false }
            ctx.interpolationQuality = .high
            ctx.draw(img, in: CGRect(x: 0, y: 0, width: 9, height: 8))
            return true
        }
        guard ok else { return nil }
        var h: UInt64 = 0
        for y in 0 ..< 8 { for x in 0 ..< 8 { h = h << 1 | (px[y * 9 + x] > px[y * 9 + x + 1] ? 1 : 0) } }
        return h
    }

    /// Gruppen von Bildern, deren Hashes sich in höchstens `maxDistance` von 64 Bit unterscheiden.
    static func groups(_ items: [Item], maxDistance: Int, progress: @escaping @Sendable (Double) -> Void) async -> [[Item]] {
        let imgs = items.filter { $0.isImage && !$0.isDir }
        var hashes = [UInt64?](repeating: nil, count: imgs.count)
        var done = 0
        await withTaskGroup(of: (Int, UInt64?).self) { g in
            var next = 0
            let add = { (g: inout TaskGroup<(Int, UInt64?)>) in
                guard next < imgs.count else { return }
                let i = next; next += 1
                g.addTask { (i, hash(imgs[i].id)) }
            }
            for _ in 0 ..< ProcessInfo.processInfo.activeProcessorCount { add(&g) }
            for await (i, h) in g {
                hashes[i] = h
                done += 1
                progress(Double(done) / Double(imgs.count))
                if Task.isCancelled { g.cancelAll(); return }
                add(&g)
            }
        }
        return cluster(hashes, maxDistance).map { $0.map { imgs[$0] } }
            .map { $0.sorted { $0.id.path < $1.id.path } }
            .sorted { $0[0].size * Int64($0.count) > $1[0].size * Int64($1.count) }
    }

    /// Union-Find über alle Paare (O(n²) auf 64-Bit-Vergleichen – auch bei zehntausenden Bildern in Sekunden).
    static func cluster(_ h: [UInt64?], _ maxDistance: Int) -> [[Int]] {
        var parent = Array(0 ..< h.count)
        func find(_ x: Int) -> Int { var x = x; while parent[x] != x { parent[x] = parent[parent[x]]; x = parent[x] }; return x }
        let idx = h.indices.filter { h[$0] != nil }
        for (a, i) in idx.enumerated() {
            for j in idx[(a + 1)...] where (h[i]! ^ h[j]!).nonzeroBitCount <= maxDistance {
                let (ri, rj) = (find(i), find(j))
                if ri != rj { parent[ri] = rj }
            }
        }
        return Dictionary(grouping: idx, by: find).values.filter { $0.count > 1 }.map { $0.sorted() }
    }
}
