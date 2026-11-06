import SwiftUI

struct ContentView: View {
    @Environment(Store.self) private var store
    @AppStorage("recursive") private var recursive = true
    @AppStorage("dirs") private var dirs = false
    @AppStorage("hidden") private var hidden = false
    @State private var cols = TableColumnCustomization<Item>()

    var body: some View {
        @Bindable var st = store
        let pl = store.plans()
        let rows = store.visible
        Table(rows, selection: $st.selection, sortOrder: $st.sortOrder, columnCustomization: $cols) {
            TableColumn("Name", value: \.name) { it in
                Label(it.name, systemImage: it.isDir ? "folder.fill" : it.isImage ? "photo" : "doc")
                    .lineLimit(1)
            }
            .width(min: 140, ideal: 220).customizationID("name")
            TableColumn("Größe", value: \.sizeKey) { Text($0.isDir ? "" : bytes($0.size)).monospacedDigit().foregroundStyle(.secondary) }
                .width(min: 70, ideal: 80).customizationID("size")
            TableColumn("Ordner", value: \.folder) { Text($0.folder).foregroundStyle(.secondary).lineLimit(1) }
                .width(min: 100, ideal: 200).customizationID("folder").defaultVisibility(.hidden)
            TableColumn("Erstellt", value: \.createdKey) { Text($0.created?.txt ?? "–").monospacedDigit() }
                .width(min: 130, ideal: 150).customizationID("created")
            TableColumn("Geändert", value: \.modifiedKey) { Text($0.modified?.txt ?? "–").monospacedDigit() }
                .width(min: 130, ideal: 150).customizationID("modified")
            TableColumn("Aufnahme", value: \.takenKey) { Text($0.taken?.txt ?? "–").monospacedDigit() }
                .width(min: 130, ideal: 150).customizationID("taken")
            TableColumn("Standort", value: \.gpsKey) { it in
                Text(it.lat.map { String(format: "%.5f, %.5f", $0, it.lon ?? 0) } ?? "–").monospacedDigit()
            }
            .width(min: 110, ideal: 150).customizationID("gps")
            TableColumn("→ Erstellt") { Text(pl[$0.id]?.created?.txt ?? "").monospacedDigit().foregroundStyle(.tint) }
                .width(min: 130, ideal: 150).customizationID("newCreated")
            TableColumn("→ Geändert") { Text(pl[$0.id]?.modified?.txt ?? "").monospacedDigit().foregroundStyle(.tint) }
                .width(min: 130, ideal: 150).customizationID("newModified")
            TableColumn("→ Aufnahme") { Text(pl[$0.id]?.taken?.txt ?? "").monospacedDigit().foregroundStyle(.tint) }
                .width(min: 130, ideal: 150).customizationID("newTaken")
        }
        .contextMenu(forSelectionType: URL.self) { ids in
            Button("Im Finder zeigen") { NSWorkspace.shared.activateFileViewerSelecting(Array(ids)) }
            Button("Aus Liste entfernen") { store.remove(ids) }
        }
        .onDeleteCommand { store.remove(store.selection) }
        .onChange(of: st.sortOrder) { store.items.sort(using: store.sortOrder) }
        .onChange(of: recursive) { store.rescan() }
        .onChange(of: dirs) { store.rescan() }
        .onChange(of: hidden) { store.rescan() }
        .dropDestination(for: URL.self) { urls, _ in store.add(urls); return true }
        .overlay { if store.sources.isEmpty { EmptyState() } }
        .safeAreaInset(edge: .bottom, spacing: 0) { StatusBar() }
        .inspector(isPresented: $st.showInspector) {
            InspectorView().inspectorColumnWidth(min: 320, ideal: 360, max: 460)
        }
        .sheet(isPresented: $st.showDupes) { DuplicatesView() }
        .sheet(isPresented: $st.showOrganize) { OrganizeView() }
        .sheet(isPresented: $st.showCleanup) { CleanupView() }
        .sheet(isPresented: $st.showRename) { RenameView() }
        .sheet(isPresented: $st.showGeotag) { GeotagView() }
        .searchable(text: $st.search, prompt: "Filtern")
        .toolbar {
            ToolbarItemGroup(placement: .navigation) {
                Menu {
                    Button("Dateien …") { store.openPanel(files: true) }
                    Button("Ordner …") { store.openPanel(files: false) }
                } label: { Label("Hinzufügen", systemImage: "plus") }
                    .help("Dateien oder Ordner hinzufügen (auch per Drag & Drop)")
                Button { store.rescan() } label: { Label("Neu einlesen", systemImage: "arrow.clockwise") }
                    .disabled(store.sources.isEmpty)
                Button { store.clear() } label: { Label("Liste leeren", systemImage: "trash") }
                    .disabled(store.sources.isEmpty)
            }
            ToolbarItemGroup {
                Button { store.showDupes = true } label: { Label("Duplikate", systemImage: "square.on.square") }
                    .help("Doppelte Dateien finden (auch mit anderem Namen)").disabled(store.items.isEmpty)
                Button { store.showOrganize = true } label: { Label("Sortieren", systemImage: "calendar.badge.plus") }
                    .help("Nach Datum in JJJJ/MM-Ordner ablegen").disabled(store.items.isEmpty)
                Button { store.showRename = true } label: { Label("Umbenennen", systemImage: "textformat.abc") }
                    .help("Dateien nach Datum umbenennen, z. B. 2024-07-13_14-22-05").disabled(store.items.isEmpty)
                Button { store.showGeotag = true } label: { Label("Geotagging", systemImage: "location.viewfinder") }
                    .help("Standort aus einem GPX-Track (GPS-Logger, Wander-App) zuordnen").disabled(store.items.isEmpty)
                Button { store.showCleanup = true } label: { Label("Aufräumen", systemImage: "wand.and.sparkles") }
                    .help("desktop.ini, Thumbs.db, .DS_Store & leere Ordner entfernen").disabled(store.sources.isEmpty)
            }
            ToolbarItem {
                Menu {
                    Picker("Anzeigen", selection: $st.filter) { ForEach(ItemFilter.allCases) { Text($0.rawValue).tag($0) } }
                        .pickerStyle(.inline)
                } label: {
                    Label(st.filter.rawValue, systemImage: st.filter == .all ? "line.3.horizontal.decrease.circle" : "line.3.horizontal.decrease.circle.fill")
                }
                .help("Nur bestimmte Dateien anzeigen – Änderungen wirken dann nur auf die angezeigten Zeilen")
            }
            ToolbarItemGroup {
                Toggle(isOn: $recursive) { Label("Unterordner", systemImage: "list.bullet.indent") }
                    .help("Unterordner einbeziehen")
                Toggle(isOn: $dirs) { Label("Ordner mitbearbeiten", systemImage: "folder") }
                    .help("Ordner selbst ebenfalls ändern")
                Toggle(isOn: $hidden) { Label("Versteckte Dateien", systemImage: "eye") }
                    .help("Versteckte Dateien anzeigen")
                Button { store.showInspector.toggle() } label: { Label("Informationen", systemImage: "sidebar.right") }
            }
        }
        .navigationTitle("Save the Date")
        .frame(minWidth: 900, minHeight: 560)
        .confirmationDialog("Änderungen anwenden?", isPresented: $st.confirmApply) {
            Button("Anwenden") { store.apply() }
        } message: {
            Text("\(store.work(pl).count) Objekte werden geändert.")
        }
        .alert("Fertig", isPresented: Binding(get: { store.report != nil }, set: { if !$0 { store.report = nil } })) {
            Button("OK") {}
        } message: { Text(store.report ?? "") }
    }
}

struct EmptyState: View {
    @Environment(Store.self) private var store
    var body: some View {
        ContentUnavailableView {
            Label("Dateien oder Ordner hierher ziehen", systemImage: "calendar.badge.clock")
        } description: {
            Text("Fotos, Dokumente, Ordner – inklusive Unterordner. Danach Datum und Standort rechts einstellen.")
        } actions: {
            Button("Dateien …") { store.openPanel(files: true) }
            Button("Ordner …") { store.openPanel(files: false) }
        }
    }
}

struct StatusBar: View {
    @Environment(Store.self) private var store
    var body: some View {
        HStack(spacing: 12) {
            let gps = store.items.filter { $0.lat != nil }.count
            Text("\(store.items.count) Objekte · \(store.selection.count) ausgewählt · \(gps) mit Standort" + (store.isFiltered ? " · \(store.visible.count) angezeigt" : ""))
                .foregroundStyle(.secondary)
            Spacer()
            if let p = store.progress {
                if p < 0 { ProgressView().controlSize(.small) }
                else { ProgressView(value: p).frame(width: 140) }
            }
        }
        .font(.callout)
        .padding(.horizontal, 12).padding(.vertical, 6)
        .background(.bar)
    }
}
