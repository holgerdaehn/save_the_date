import AppKit
import MapKit
import SwiftUI

struct DateTimeField: NSViewRepresentable {
    @Binding var date: Date

    func makeNSView(context: Context) -> NSDatePicker {
        let p = NSDatePicker()
        p.datePickerStyle = .textFieldAndStepper
        p.datePickerElements = [.yearMonthDay, .hourMinuteSecond]
        p.target = context.coordinator
        p.action = #selector(Coordinator.changed(_:))
        return p
    }

    func updateNSView(_ p: NSDatePicker, context: Context) {
        context.coordinator.parent = self
        p.isEnabled = context.environment.isEnabled
        if abs(p.dateValue.timeIntervalSince(date)) > 0.5 { p.dateValue = date }
    }

    func makeCoordinator() -> Coordinator { Coordinator(self) }

    final class Coordinator: NSObject {
        var parent: DateTimeField
        init(_ p: DateTimeField) { parent = p }
        @objc func changed(_ s: NSDatePicker) { parent.date = s.dateValue }
    }
}

struct Card<Content: View>: View {
    let title: String
    let icon: String
    @ViewBuilder var content: Content
    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label(title, systemImage: icon).font(.headline)
            content
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(.quaternary.opacity(0.5), in: .rect(cornerRadius: 12))
    }
}

struct DateRow: View {
    let title: String
    @Binding var on: Bool
    @Binding var date: Date
    var showPicker = true
    @State private var pop = false

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Toggle(title, isOn: $on)
            if showPicker {
                HStack {
                    DateTimeField(date: $date).disabled(!on)
                    Button { pop = true } label: { Image(systemName: "calendar") }
                        .buttonStyle(.borderless).disabled(!on).help("Kalender")
                        .popover(isPresented: $pop) {
                            DatePicker("", selection: $date, displayedComponents: .date)
                                .datePickerStyle(.graphical).labelsHidden().padding()
                        }
                    Button("Jetzt") { date = .nowToSecond }.controlSize(.small).disabled(!on)
                }
            }
        }
    }
}

struct InspectorView: View {
    @Environment(Store.self) private var store

    var body: some View {
        @Bindable var st = store
        let pl = store.plans()
        let n = store.work(pl).count
        ScrollView {
            VStack(spacing: 14) {
                Card(title: "Datum", icon: "calendar") {
                    Picker("", selection: $st.s.mode) {
                        ForEach(Mode.allCases) { Text($0.rawValue).tag($0) }
                    }
                    .pickerStyle(.segmented).labelsHidden()
                    switch st.s.mode {
                    case .fixed:
                        DateRow(title: "Erstellt", on: $st.s.setCreated, date: store.date(\.created))
                        DateRow(title: "Geändert", on: $st.s.setModified, date: store.date(\.modified))
                        DateRow(title: "Aufnahme (Foto-EXIF)", on: $st.s.setTaken, date: store.date(\.taken))
                        Toggle("Alle Werte verknüpfen", isOn: $st.s.linked)
                        HStack {
                            Text("Abstand pro Datei")
                            TextField("", value: $st.s.interval, format: .number).frame(width: 60).multilineTextAlignment(.trailing)
                            Text("Sek.").foregroundStyle(.secondary)
                        }
                        .help("0 = alle gleich; sonst Abstand in Sekunden zwischen Dateien (nach Name sortiert)")
                    case .shift:
                        Picker("", selection: $st.s.later) {
                            Text("Später").tag(true)
                            Text("Früher").tag(false)
                        }
                        .pickerStyle(.segmented).labelsHidden()
                        HStack(spacing: 8) {
                            ShiftField(label: "Tage", value: $st.s.days)
                            ShiftField(label: "Std.", value: $st.s.hours)
                            ShiftField(label: "Min.", value: $st.s.minutes)
                            ShiftField(label: "Sek.", value: $st.s.seconds)
                        }
                        Toggle("Erstellt", isOn: $st.s.setCreated)
                        Toggle("Geändert", isOn: $st.s.setModified)
                        Toggle("Aufnahme (Foto-EXIF)", isOn: $st.s.setTaken)
                    case .exif:
                        Text("Erstellt- und Änderungsdatum werden auf das EXIF-Aufnahmedatum der Fotos gesetzt.")
                            .font(.callout).foregroundStyle(.secondary)
                        Toggle("Erstellt", isOn: $st.s.setCreated)
                        Toggle("Geändert", isOn: $st.s.setModified)
                    }
                }
                Card(title: "Standort", icon: "mappin.and.ellipse") {
                    Picker("", selection: $st.s.geo) {
                        ForEach(GeoMode.allCases) { Text($0.rawValue).tag($0) }
                    }
                    .pickerStyle(.segmented).labelsHidden()
                    if st.s.geo == .set { LocationPicker() }
                    Text("Nur für Fotos (JPEG, HEIC, PNG, TIFF).").font(.caption).foregroundStyle(.secondary)
                }
            }
            .padding(14)
            .frame(maxHeight: .infinity, alignment: .top)
        }
        .ignoresSafeArea(.container, edges: .top)
        .safeAreaInset(edge: .bottom, spacing: 0) {
            VStack(spacing: 8) {
                Text(store.selection.isEmpty ? "Wirkt auf alle \(store.items.count) Objekte" : "Wirkt auf \(store.selection.count) ausgewählte Objekte")
                    .font(.callout).foregroundStyle(.secondary)
                Button { store.requestApply() } label: {
                    Text(n == 0 ? "Keine Änderung" : "\(n) Objekte ändern").frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent).controlSize(.large).disabled(n == 0 || store.progress != nil)
                Button("Datei-Daten zurücksetzen") { store.undo() }.controlSize(.small).disabled(!store.canUndo)
            }
            .padding(14)
            .background(.bar)
        }
    }
}

struct ShiftField: View {
    let label: String
    @Binding var value: Int
    var body: some View {
        VStack(spacing: 2) {
            TextField("", value: $value, format: .number).multilineTextAlignment(.trailing)
            Text(label).font(.caption2).foregroundStyle(.secondary)
        }
    }
}

struct LocationPicker: View {
    @Environment(Store.self) private var store
    @State private var query = ""
    @State private var hits: [MKMapItem] = []
    @State private var camera = MapCameraPosition.automatic
    @State private var text = ""

    var body: some View {
        VStack(spacing: 8) {
            TextField("Ort oder Adresse suchen", text: $query).textFieldStyle(.roundedBorder).onSubmit(search)
            ForEach(hits.prefix(5), id: \.self) { h in
                Button { pick(h.placemark.coordinate) } label: {
                    VStack(alignment: .leading) {
                        Text(h.name ?? "").lineLimit(1)
                        Text(h.placemark.title ?? "").font(.caption).foregroundStyle(.secondary).lineLimit(1)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                .buttonStyle(.plain)
            }
            MapReader { proxy in
                Map(position: $camera) {
                    if let c = store.coordinate { Marker("", coordinate: c) }
                }
                .onTapGesture { pt in
                    if let c = proxy.convert(pt, from: .local) { set(c) }
                }
            }
            .frame(height: 240).clipShape(.rect(cornerRadius: 10))
            TextField("Breite, Länge  (z. B. 52.5200, 13.4050)", text: $text)
                .textFieldStyle(.roundedBorder).onSubmit(parse)
            HStack {
                Button("Von Auswahl übernehmen") {
                    if let it = store.targets.first(where: { $0.lat != nil }) {
                        pick(CLLocationCoordinate2D(latitude: it.lat!, longitude: it.lon ?? 0))
                    }
                }
                .disabled(!store.targets.contains { $0.lat != nil })
                Spacer()
                if store.coordinate != nil {
                    Text(store.coordinate == nil ? "" : "Koordinate gesetzt").font(.caption).foregroundStyle(.secondary)
                } else {
                    Text("Auf Karte tippen oder Ort suchen").font(.caption).foregroundStyle(.secondary)
                }
            }
            .controlSize(.small)
        }
        .onAppear { sync() }
    }

    private func sync() {
        text = store.coordinate.map { String(format: "%.6f, %.6f", $0.latitude, $0.longitude) } ?? ""
    }

    private func set(_ c: CLLocationCoordinate2D) {
        store.s.lat = c.latitude
        store.s.lon = c.longitude
        sync()
    }

    private func pick(_ c: CLLocationCoordinate2D) {
        set(c)
        hits = []
        camera = .region(MKCoordinateRegion(center: c, latitudinalMeters: 3000, longitudinalMeters: 3000))
    }

    private func search() {
        let r = MKLocalSearch.Request()
        r.naturalLanguageQuery = query
        Task { hits = (try? await MKLocalSearch(request: r).start().mapItems) ?? [] }
    }

    /// "52.52, 13.40" oder (deutsche Schreibweise) "52,52; 13,40".
    private func parse() {
        let norm = text.contains(";") ? text.replacingOccurrences(of: ",", with: ".").replacingOccurrences(of: ";", with: ",") : text
        let p = norm.split(whereSeparator: { ",  ".contains($0) }).compactMap { Double($0) }
        if p.count == 2, abs(p[0]) <= 90, abs(p[1]) <= 180 { pick(CLLocationCoordinate2D(latitude: p[0], longitude: p[1])) } else { NSSound.beep(); sync() }
    }
}
