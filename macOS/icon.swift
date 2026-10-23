// Erzeugt AppIcon.icns: swift icon.swift
import AppKit

let size: CGFloat = 1024
let img = NSImage(size: NSSize(width: size, height: size), flipped: false) { r in
    let body = r.insetBy(dx: 100, dy: 100)
    let path = NSBezierPath(roundedRect: body, xRadius: 185, yRadius: 185)
    NSGraphicsContext.saveGraphicsState()
    let sh = NSShadow(); sh.shadowBlurRadius = 30; sh.shadowOffset = NSSize(width: 0, height: -14)
    sh.shadowColor = NSColor.black.withAlphaComponent(0.3); sh.set()
    NSColor.white.setFill(); path.fill()
    NSGraphicsContext.restoreGraphicsState()
    NSGradient(colors: [NSColor(red: 0.30, green: 0.69, blue: 1, alpha: 1), NSColor(red: 0.04, green: 0.35, blue: 0.95, alpha: 1)])!
        .draw(in: path, angle: -90)
    let cfg = NSImage.SymbolConfiguration(pointSize: 420, weight: .medium).applying(.init(paletteColors: [.white]))
    if let s = NSImage(systemSymbolName: "calendar.badge.clock", accessibilityDescription: nil)?.withSymbolConfiguration(cfg) {
        let w = s.size.width, h = s.size.height
        s.draw(in: NSRect(x: (size - w) / 2, y: (size - h) / 2, width: w, height: h))
    }
    return true
}

let set = "AppIcon.iconset"
try? FileManager.default.createDirectory(atPath: set, withIntermediateDirectories: true)
for base in [16, 32, 128, 256, 512] {
    for scale in [1, 2] {
        let px = base * scale
        let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: px, pixelsHigh: px, bitsPerSample: 8, samplesPerPixel: 4,
                                   hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
        img.draw(in: NSRect(x: 0, y: 0, width: px, height: px))
        NSGraphicsContext.restoreGraphicsState()
        try rep.representation(using: .png, properties: [:])!
            .write(to: URL(fileURLWithPath: "\(set)/icon_\(base)x\(base)\(scale == 2 ? "@2x" : "").png"))
    }
}
