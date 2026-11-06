import AppKit
import ImageIO
import SwiftUI

/// Vorschaubild (Foto, Video oder Dateisymbol) – wird erst beim Sichtbarwerden geladen.
struct Thumb: View {
    let url: URL
    @State private var img: NSImage?

    var body: some View {
        Image(nsImage: img ?? NSWorkspace.shared.icon(forFile: url.path))
            .resizable().scaledToFill()
            .frame(width: 44, height: 44).clipShape(.rect(cornerRadius: 6))
            .task(id: url) {
                img = await Task.detached(priority: .utility) { () -> NSImage? in
                    let o = [kCGImageSourceCreateThumbnailFromImageAlways: true, kCGImageSourceCreateThumbnailWithTransform: true,
                             kCGImageSourceThumbnailMaxPixelSize: 96] as CFDictionary
                    guard let s = CGImageSourceCreateWithURL(url as CFURL, nil),
                          let c = CGImageSourceCreateThumbnailAtIndex(s, 0, o) else { return nil }
                    return NSImage(cgImage: c, size: .zero)
                }.value
            }
    }
}

// MARK: Umbenennen

struct RenameView: View {
    @Environment(Store.self) private var store
    @Environment(\.dismiss) private var dismiss
    @AppStorage("renameTemplate") private var template = Renamer.defaultTemplate
    @AppStorage("renameLower") private var lower = true
    @AppStorage("renameMedia") private var onlyMedia = true
    @State private var source = DateSource.smart
    @State private var plan: [Renamer.Move] = []
    @State private var confirm = false

    private var todo: Int { plan.filter { $0.skip == nil }.count }

    var body: some View {
        VStack(spacing: 0) {
            ToolHeader(title: "Nach Datum umbenennen",
                       text: "Benennt Dateien nach ihrem Datum um, z. B. 2024-07-13_14-22-05.jpg. Wirkt auf \(store.selection.isEmpty ? (store.isFiltered ? "die angezeigten" : "alle") : "die ausgewählten") Dateien der Liste.")
            Form {
                HStack {
                    TextField("Muster", text: $template)
                    Menu("Vorlagen") {
                        ForEach(Renamer.presets, id: \.1) { p in Button(p.0) { template = p.1 } }
                    }
                    .fixedSize()
                }
                Text("Platzhalter: {JJJJ} {MM} {TT} {hh} {mm} {ss} · {Name} = alter Name · {Nr} = Zähler")
                    .font(.caption).foregroundStyle(.secondary)
                Picker("Datum", selection: $source) { ForEach(DateSource.allCases) { Text($0.rawValue).tag($0) } }
                Toggle("Endung klein schreiben (.JPG → .jpg)", isOn: $lower)
                Toggle("Nur Fotos und Videos", isOn: $onlyMedia)
            }
            .formStyle(.grouped).scrollDisabled(true).frame(height: 230)
            Table(plan) {
                TableColumn("Bisher") { Text($0.item.name).lineLimit(1) }
                TableColumn("Neu") { m in
                    Text(m.skip ?? m.dst.lastPathComponent).foregroundStyle(m.skip == nil ? Color.accentColor : .secondary).lineLimit(1)
                }
                TableColumn("Ordner") { Text($0.item.folder).foregroundStyle(.secondary).lineLimit(1).truncationMode(.middle) }
            }
            Divider()
            HStack {
                Text("\(todo) Dateien werden umbenannt · \(plan.count - todo) bleiben unverändert").foregroundStyle(.secondary)
                Spacer()
                Button("Abbrechen") { dismiss() }.keyboardShortcut(.cancelAction)
                Button("Umbenennen") { confirm = true }.buttonStyle(.borderedProminent).disabled(todo == 0)
            }
            .padding()
        }
        .frame(minWidth: 820, minHeight: 620)
        .onAppear(perform: refresh)
        .onChange(of: template) { refresh() }
        .onChange(of: source) { refresh() }
        .onChange(of: lower) { refresh() }
        .onChange(of: onlyMedia) { refresh() }
        .onChange(of: store.progress) { if store.progress == nil { refresh() } }
        .confirmationDialog("\(todo) Dateien umbenennen?", isPresented: $confirm) {
            Button("Umbenennen") { store.rename(plan); dismiss() }
        } message: { Text("Die Aktion lässt sich über das Menü „Werkzeuge“ rückgängig machen.") }
    }

    private func refresh() {
        plan = Renamer.plan(store.targets, template: template, source: source, lowercaseExt: lower, onlyMedia: onlyMedia)
    }
}

// MARK: Geotagging (GPX)

struct GeotagView: View {
    @Environment(Store.self) private var store
    @Environment(\.dismiss) private var dismiss
    @AppStorage("gpxGap") private var gapMinutes = 15.0
    @State private var files: [URL] = []
    @State private var points: [GpxPoint] = []
    @State private var otherZone = false
    @State private var zone = 0.0
    @State private var overwrite = false
    @State private var confirm = false

    struct Row: Identifiable {
        var id: URL { item.id }
        let item: Item
        let pos: (lat: Double, lon: Double)?
        let note: String
    }

    private var rows: [Row] {
        store.targets.filter { $0.isImage }.map { it in
            guard let t = it.taken else { return Row(item: it, pos: nil, note: "Kein Aufnahmedatum") }
            if it.lat != nil && !overwrite { return Row(item: it, pos: nil, note: "Hat schon einen Standort") }
            var when = t
            if otherZone {   // Kamerauhr zeigt die Zeit in einer anderen Zeitzone als dieser Mac
                when = t.addingTimeInterval(TimeInterval(TimeZone.current.secondsFromGMT(for: t)) - zone * 3600)
            }
            guard let p = Gpx.locate(points, at: when, maxGap: gapMinutes * 60) else {
                return Row(item: it, pos: nil, note: points.isEmpty ? "Kein Track geladen" : "Kein Trackpunkt in der Nähe")
            }
            return Row(item: it, pos: p, note: "")
        }
    }

    var body: some View {
        let rows = rows
        let hits = rows.filter { $0.pos != nil }
        VStack(spacing: 0) {
            ToolHeader(title: "Geotagging per GPX-Track",
                       text: "Ordnet Fotos anhand der Aufnahmezeit einen Ort aus dem Tracklog eines GPS-Loggers oder einer Wander-/Lauf-App zu (GPX).")
            Form {
                LabeledContent("GPX-Dateien") {
                    HStack {
                        Text(files.isEmpty ? "Bitte wählen …" : "\(files.count) Datei(en) · \(points.count) Punkte")
                            .foregroundStyle(.secondary).lineLimit(1)
                        Button("Wählen …", action: choose)
                    }
                }
                Toggle("Kamerauhr in anderer Zeitzone als dieser Mac", isOn: $otherZone)
                if otherZone {
                    HStack {
                        Text("Zeitzone der Kamera (UTC±)")
                        TextField("", value: $zone, format: .number).frame(width: 60).multilineTextAlignment(.trailing)
                        Text("Std.").foregroundStyle(.secondary)
                    }
                }
                HStack {
                    Text("Höchstens")
                    TextField("", value: $gapMinutes, format: .number).frame(width: 60).multilineTextAlignment(.trailing)
                    Text("Minuten vom nächsten Trackpunkt").foregroundStyle(.secondary)
                }
                Toggle("Vorhandene Standorte überschreiben", isOn: $overwrite)
            }
            .formStyle(.grouped).scrollDisabled(true).frame(height: otherZone ? 260 : 220)
            Table(rows) {
                TableColumn("Datei") { Text($0.item.name).lineLimit(1) }
                TableColumn("Aufnahme") { Text($0.item.taken?.txt ?? "–").monospacedDigit() }
                TableColumn("Standort") { r in
                    Text(r.pos.map { String(format: "%.5f, %.5f", $0.lat, $0.lon) } ?? r.note)
                        .monospacedDigit().foregroundStyle(r.pos == nil ? Color.secondary : Color.accentColor)
                }
            }
            Divider()
            HStack {
                Text("\(hits.count) von \(rows.count) Fotos bekommen einen Standort").foregroundStyle(.secondary)
                Spacer()
                Button("Abbrechen") { dismiss() }.keyboardShortcut(.cancelAction)
                Button("Standort schreiben") { confirm = true }.buttonStyle(.borderedProminent).disabled(hits.isEmpty)
            }
            .padding()
        }
        .frame(minWidth: 820, minHeight: 620)
        .confirmationDialog("\(hits.count) Fotos mit Standort versehen?", isPresented: $confirm) {
            Button("Schreiben") {
                store.applyGeo(Dictionary(uniqueKeysWithValues: hits.compactMap { r in r.pos.map { (r.item.id, $0) } }))
                dismiss()
            }
        } message: { Text("Der Standort wird verlustfrei in die EXIF-Daten geschrieben und lässt sich über „Letzte Änderung rückgängig“ zurücknehmen.") }
    }

    private func choose() {
        let p = NSOpenPanel()
        p.canChooseFiles = true; p.canChooseDirectories = false; p.allowsMultipleSelection = true
        p.allowedContentTypes = [.init(filenameExtension: "gpx") ?? .xml]
        p.prompt = "Auswählen"; p.message = "GPX-Tracklog(s) auswählen"
        guard p.runModal() == .OK else { return }
        files = p.urls
        points = Gpx.parse(p.urls)
    }
}
