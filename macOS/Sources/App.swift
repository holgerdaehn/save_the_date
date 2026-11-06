import AppKit
import SwiftUI

final class AppDelegate: NSObject, NSApplicationDelegate {
    func application(_ app: NSApplication, open urls: [URL]) { Store.shared.add(urls) }
}

@main
struct SaveTheDateApp: App {
    @NSApplicationDelegateAdaptor private var delegate: AppDelegate
    private let store = Store.shared
    @AppStorage("recursive") private var recursive = true
    @AppStorage("dirs") private var dirs = false
    @AppStorage("hidden") private var hidden = false
    @AppStorage("confirm") private var confirm = true

    var body: some Scene {
        Window("Save the Date", id: "main") {
            ContentView().environment(store)
        }
        .defaultSize(width: 1320, height: 800)
        Window("\(AppInfo.name) Anleitung", id: "help") {
            HelpView()
        }
        .defaultSize(width: 920, height: 640)
        .commands {
            HelpCommands()
            CommandGroup(replacing: .newItem) {
                Button("Dateien öffnen …") { store.openPanel(files: true) }.keyboardShortcut("o")
                Button("Ordner öffnen …") { store.openPanel(files: false) }.keyboardShortcut("o", modifiers: [.command, .shift])
                Divider()
                Button("Neu einlesen") { store.rescan() }.keyboardShortcut("r").disabled(store.sources.isEmpty)
                Button("Liste leeren") { store.clear() }.keyboardShortcut(.delete, modifiers: [.command, .shift]).disabled(store.sources.isEmpty)
                Divider()
                Button("Liste als CSV exportieren …") { store.exportCSV() }
                    .keyboardShortcut("e", modifiers: [.command, .shift]).disabled(store.items.isEmpty)
            }
            CommandMenu("Aktion") {
                Button("Änderungen anwenden …") { store.requestApply() }
                    .keyboardShortcut(.return).disabled(store.items.isEmpty)
                Button("Letzte Änderung rückgängig") { store.undo() }
                    .keyboardShortcut("z", modifiers: [.command, .option]).disabled(!store.canUndo)
            }
            CommandMenu("Werkzeuge") {
                Button("Duplikate suchen …") { store.showDupes = true }
                    .keyboardShortcut("d", modifiers: [.command, .shift]).disabled(store.items.isEmpty)
                Button("Nach Datum sortieren …") { store.showOrganize = true }
                    .keyboardShortcut("s", modifiers: [.command, .shift]).disabled(store.items.isEmpty)
                Button("Letzte Sortierung rückgängig") { store.undoOrganize() }.disabled(store.lastOps.isEmpty)
                Button("Nach Datum umbenennen …") { store.showRename = true }
                    .keyboardShortcut("r", modifiers: [.command, .shift]).disabled(store.items.isEmpty)
                Button("Letzte Umbenennung rückgängig") { store.undoRename() }.disabled(store.lastRenames.isEmpty)
                Divider()
                Button("Geotagging per GPX-Track …") { store.showGeotag = true }
                    .keyboardShortcut("g", modifiers: [.command, .shift]).disabled(store.items.isEmpty)
                Divider()
                Button("Aufräumen …") { store.showCleanup = true }
                    .keyboardShortcut("k", modifiers: [.command, .shift]).disabled(store.sources.isEmpty)
            }
            CommandGroup(after: .toolbar) {
                Toggle("Informationen", isOn: Binding(get: { store.showInspector }, set: { store.showInspector = $0 }))
                    .keyboardShortcut("i", modifiers: [.command, .option])
                Picker("Anzeigen", selection: Binding(get: { store.filter }, set: { store.filter = $0 })) {
                    ForEach(ItemFilter.allCases) { Text($0.rawValue).tag($0) }
                }
                Divider()
                Toggle("Unterordner einbeziehen", isOn: $recursive)
                Toggle("Ordner mitbearbeiten", isOn: $dirs)
                Toggle("Versteckte Dateien", isOn: $hidden)
                Toggle("Vor dem Anwenden nachfragen", isOn: $confirm)
            }
        }
    }
}
