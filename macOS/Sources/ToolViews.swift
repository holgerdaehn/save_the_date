import SwiftUI

private func bytes(_ n: Int64) -> String { ByteCountFormatter.string(fromByteCount: n, countStyle: .file) }

private struct ToolHeader: View {
    let title: String
    let text: String
    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title).font(.title2.bold())
            Text(text).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding()
    }
}

// MARK: Duplikate

struct DuplicatesView: View {
    @Environment(Store.self) private var store
    @Environment(\.dismiss) private var dismiss
    @State private var m = DupeModel()
    @State private var confirm = false
    @State private var result: String?

    var body: some View {
        VStack(spacing: 0) {
            ToolHeader(title: "Duplikate",
                       text: "Gleicher Inhalt (SHA-256) – egal, wie die Dateien heißen. Gelöscht wird nur in den Papierkorb.")
            Divider()
            if m.scanning {
                VStack(spacing: 10) {
                    ProgressView(value: m.progress).frame(width: 280)
                    Text("Vergleiche Dateien …").foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if m.groups.isEmpty {
                ContentUnavailableView("Keine Duplikate gefunden", systemImage: "checkmark.circle",
                                       description: Text("Verglichen wurden \(store.items.filter { !$0.isDir }.count) Dateien."))
            } else {
                List {
                    ForEach(Array(m.groups.enumerated()), id: \.offset) { i, g in
                        Section("Gruppe \(i + 1) · \(g.count) × \(bytes(g[0].size))") {
                            ForEach(g) { row($0, g) }
                        }
                    }
                }
                .listStyle(.inset(alternatesRowBackgrounds: true))
            }
            Divider()
            footer
        }
        .frame(minWidth: 860, minHeight: 580)
        .task { m.scan(store.items) }
        .onDisappear { m.cancel() }
        .confirmationDialog("\(m.marked.count) Dateien in den Papierkorb legen?", isPresented: $confirm) {
            Button("In den Papierkorb", role: .destructive) {
                let r = m.trash()
                store.rescan()
                result = "\(r.ok) Dateien in den Papierkorb gelegt."
                    + (r.errors.isEmpty ? "" : "\n\nFehler:\n" + r.errors.prefix(10).joined(separator: "\n"))
            }
        } message: { Text("Es werden \(bytes(m.freed)) frei. Pro Gruppe bleibt mindestens eine Datei erhalten.") }
        .alert("Fertig", isPresented: Binding(get: { result != nil }, set: { if !$0 { result = nil } })) {
            Button("OK") {}
        } message: { Text(result ?? "") }
    }

    private func row(_ it: Item, _ g: [Item]) -> some View {
        HStack {
            Toggle("", isOn: m.binding(it, in: g)).labelsHidden()
            VStack(alignment: .leading, spacing: 1) {
                Text(it.name).lineLimit(1)
                Text(it.folder).font(.caption).foregroundStyle(.secondary).lineLimit(1).truncationMode(.middle)
            }
            Spacer()
            Text(it.modified?.txt ?? "–").monospacedDigit().foregroundStyle(.secondary)
            Text(m.marked.contains(it.id) ? "Löschen" : "Behalten")
                .font(.caption.bold()).frame(width: 60)
                .foregroundStyle(m.marked.contains(it.id) ? .red : .green)
        }
    }

    private var footer: some View {
        @Bindable var model = m
        return HStack {
            Picker("", selection: $model.keep) {
                ForEach(Keep.allCases) { Text($0.rawValue).tag($0) }
            }
            .labelsHidden().fixedSize()
            .onChange(of: m.keep) { m.autoMark() }
            Text("\(m.groups.count) Gruppen · \(m.marked.count) zum Löschen · \(bytes(m.freed))").foregroundStyle(.secondary)
            Spacer()
            Button("Schließen") { dismiss() }.keyboardShortcut(.cancelAction)
            Button("In Papierkorb legen") { confirm = true }
                .buttonStyle(.borderedProminent).tint(.red).disabled(m.marked.isEmpty)
        }
        .padding()
    }
}

// MARK: Sortieren

struct OrganizeView: View {
    @Environment(Store.self) private var store
    @Environment(\.dismiss) private var dismiss
    struct Opts: Equatable {
        var root: URL?
        var pattern = Pattern.ym
        var source = DateSource.taken
        var onlyMedia = true
    }
    @State private var o = Opts()
    @State private var copy = false
    @State private var removeEmpty = true
    @State private var plan: [Organizer.Move] = []
    @State private var confirm = false

    private var todo: Int { plan.filter { $0.skip == nil }.count }

    var body: some View {
        VStack(spacing: 0) {
            ToolHeader(title: "Nach Datum sortieren",
                       text: "Legt Fotos und Videos in Unterordner wie JJJJ/MM ab. Wirkt auf \(store.selection.isEmpty ? "alle" : "die ausgewählten") Dateien der Liste.")
            Form {
                LabeledContent("Zielordner") {
                    HStack {
                        Text(o.root?.path ?? "Bitte wählen …").foregroundStyle(.secondary).lineLimit(1).truncationMode(.middle)
                        Button("Wählen …", action: choose)
                    }
                }
                Picker("Struktur", selection: $o.pattern) { ForEach(Pattern.allCases) { Text($0.rawValue).tag($0) } }
                Picker("Datum", selection: $o.source) { ForEach(DateSource.allCases) { Text($0.rawValue).tag($0) } }
                Toggle("Nur Fotos und Videos", isOn: $o.onlyMedia)
                Toggle("Kopieren statt verschieben", isOn: $copy)
                Toggle("Leere Quellordner entfernen", isOn: $removeEmpty).disabled(copy)
            }
            .formStyle(.grouped).scrollDisabled(true).frame(height: 290)
            Table(plan) {
                TableColumn("Datei") { Text($0.item.name).lineLimit(1) }
                TableColumn("Von") { Text($0.item.folder).foregroundStyle(.secondary).lineLimit(1).truncationMode(.middle) }
                TableColumn("Nach") { m in
                    Text(m.skip ?? rel(m.dst)).foregroundStyle(m.skip == nil ? Color.accentColor : .secondary).lineLimit(1)
                }
            }
            Divider()
            HStack {
                Text("\(todo) Dateien werden \(copy ? "kopiert" : "verschoben") · \(plan.count - todo) übersprungen").foregroundStyle(.secondary)
                Spacer()
                Button("Abbrechen") { dismiss() }.keyboardShortcut(.cancelAction)
                Button(copy ? "Kopieren" : "Ablegen") { confirm = true }
                    .buttonStyle(.borderedProminent).disabled(todo == 0)
            }
            .padding()
        }
        .frame(minWidth: 820, minHeight: 640)
        .onAppear {
            o.root = store.sources.first { (try? $0.resourceValues(forKeys: [.isDirectoryKey]))?.isDirectory == true }
            refresh()
        }
        .onChange(of: o) { refresh() }
        .onChange(of: store.progress) { if store.progress == nil { refresh() } }   // Aufnahmedaten werden noch eingelesen
        .confirmationDialog("\(todo) Dateien \(copy ? "kopieren" : "verschieben")?", isPresented: $confirm) {
            Button(copy ? "Kopieren" : "Verschieben") {
                if let r = o.root { store.organize(plan, copy: copy, removeEmpty: removeEmpty && !copy, root: r) }
                dismiss()
            }
        } message: { Text("Ziel: \(o.root?.path ?? ""). Die Aktion lässt sich über das Menü „Aktion“ rückgängig machen.") }
    }

    private func rel(_ u: URL) -> String { o.root.map { u.path.replacingOccurrences(of: $0.path + "/", with: "") } ?? u.path }

    private func refresh() {
        plan = o.root.map { Organizer.plan(store.targets, root: $0, pattern: o.pattern, source: o.source, onlyMedia: o.onlyMedia) } ?? []
    }

    private func choose() {
        let p = NSOpenPanel()
        p.canChooseFiles = false; p.canChooseDirectories = true; p.canCreateDirectories = true
        p.prompt = "Auswählen"; p.message = "Zielordner für die Sortierung"
        if p.runModal() == .OK { o.root = p.url?.standardizedFileURL }
    }
}

// MARK: Aufräumen

struct CleanupView: View {
    @Environment(Store.self) private var store
    @Environment(\.dismiss) private var dismiss
    @AppStorage("cleanPatterns") private var patterns = ""
    @AppStorage("cleanEmpty") private var removeEmpty = true
    @State private var m = CleanModel()
    @State private var confirm = false
    @State private var result: String?

    private var roots: [URL] {
        store.sources.filter { (try? $0.resourceValues(forKeys: [.isDirectoryKey]))?.isDirectory == true }
    }

    private var extra: [String] {
        patterns.split(whereSeparator: { ",;\n".contains($0) }).map { $0.trimmingCharacters(in: .whitespaces) }.filter { !$0.isEmpty }
    }

    var body: some View {
        VStack(spacing: 0) {
            ToolHeader(title: "Aufräumen",
                       text: "Findet überflüssige Dateien wie desktop.ini, Thumbs.db, .DS_Store und ._*-Reste sowie leere Ordner. Alles landet im Papierkorb.")
            Form {
                TextField("Eigene Muster", text: $patterns, prompt: Text("z. B. *.tmp, *.bak, Zone.Identifier"))
                    .onSubmit(rescan)
                Toggle("Leere Ordner entfernen", isOn: $removeEmpty)
            }
            .formStyle(.grouped).scrollDisabled(true).frame(height: 120)
            Divider()
            if roots.isEmpty {
                ContentUnavailableView("Kein Ordner in der Liste", systemImage: "folder.badge.questionmark",
                                       description: Text("Zum Aufräumen zuerst einen Ordner hinzufügen."))
            } else if m.scanning {
                ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if m.found.isEmpty {
                ContentUnavailableView("Nichts zu bereinigen", systemImage: "sparkles")
            } else {
                List(m.found) { j in
                    HStack {
                        Toggle("", isOn: Binding(get: { m.marked.contains(j.id) },
                                                 set: { if $0 { m.marked.insert(j.id) } else { m.marked.remove(j.id) } })).labelsHidden()
                        Image(systemName: j.isDir ? "folder" : "doc")
                        VStack(alignment: .leading, spacing: 1) {
                            Text(j.url.lastPathComponent).lineLimit(1)
                            Text(j.url.deletingLastPathComponent().path).font(.caption).foregroundStyle(.secondary)
                                .lineLimit(1).truncationMode(.middle)
                        }
                        Spacer()
                        Text(j.reason).font(.caption).foregroundStyle(.secondary)
                        Text(j.isDir ? "" : bytes(j.size)).monospacedDigit().foregroundStyle(.secondary).frame(width: 70, alignment: .trailing)
                    }
                }
                .listStyle(.inset(alternatesRowBackgrounds: true))
            }
            Divider()
            HStack {
                Text("\(m.marked.count) von \(m.found.count) ausgewählt · \(bytes(m.size))").foregroundStyle(.secondary)
                Button("Alle") { m.marked = Set(m.found.map(\.id)) }.disabled(m.found.isEmpty)
                Button("Keine") { m.marked = [] }.disabled(m.found.isEmpty)
                Spacer()
                Button("Schließen") { dismiss() }.keyboardShortcut(.cancelAction)
                Button("In Papierkorb legen") { confirm = true }
                    .buttonStyle(.borderedProminent).tint(.red).disabled(m.marked.isEmpty)
            }
            .padding()
        }
        .frame(minWidth: 780, minHeight: 600)
        .onAppear(perform: rescan)
        .onChange(of: removeEmpty) { rescan() }
        .confirmationDialog("\(m.marked.count) Elemente in den Papierkorb legen?", isPresented: $confirm) {
            Button("In den Papierkorb", role: .destructive) {
                let r = m.trash()
                store.rescan()
                result = "\(r.ok) Elemente in den Papierkorb gelegt."
                    + (r.errors.isEmpty ? "" : "\n\nFehler:\n" + r.errors.prefix(10).joined(separator: "\n"))
                rescan()
            }
        }
        .alert("Fertig", isPresented: Binding(get: { result != nil }, set: { if !$0 { result = nil } })) {
            Button("OK") {}
        } message: { Text(result ?? "") }
    }

    private func rescan() { m.scan(roots, extra: extra, removeEmpty: removeEmpty) }
}
