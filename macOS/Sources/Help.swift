import AppKit
import SwiftUI

enum AppInfo {
    static let name = "Save the Date"
    static let developer = "Kundida (Holger Dähn)"

    /// Standard-Infofenster von macOS (Symbol, Name, Version und Copyright kommen aus der Info.plist).
    @MainActor static func showAbout() {
        let body = NSFont.systemFont(ofSize: 11)
        let head = NSFont.boldSystemFont(ofSize: 11)
        let para = NSMutableParagraphStyle()
        para.alignment = .center
        para.paragraphSpacing = 4
        let text = NSMutableAttributedString()
        func add(_ title: String?, _ s: String) {
            if let title { text.append(NSAttributedString(string: title + "\n", attributes: [.font: head, .paragraphStyle: para, .foregroundColor: NSColor.labelColor])) }
            text.append(NSAttributedString(string: s + "\n\n", attributes: [.font: body, .paragraphStyle: para, .foregroundColor: NSColor.secondaryLabelColor]))
        }
        add(nil, "Datum, Aufnahmezeit und Standort von Fotos, Videos und Dateien ändern – dazu Duplikate und ähnliche Bilder finden, Dateien nach Datum sortieren und umbenennen, Fotos per GPX-Track geotaggen und überflüssige Dateien aufräumen.")
        add("Entwickler", developer)
        add("Datenschutz", "Alle Dateien werden ausschließlich lokal auf diesem Mac verarbeitet; es werden keine Daten an den Entwickler übertragen. Für Karte und Ortssuche verwendet die App Apple Karten (MapKit) – dabei werden Kartenausschnitte und Suchbegriffe an Apple übermittelt.")
        add("Hinweis", "Die Software wird ohne Gewähr bereitgestellt. Vor größeren Änderungen wird eine Datensicherung empfohlen. Gelöschte Dateien werden in den Papierkorb gelegt.")
        add("Lizenz", "MIT-Lizenz – freie Software, Quellcode auf GitHub.")
        add("Systemvoraussetzung", "macOS 15 oder neuer")
        let link = NSMutableAttributedString(string: "github.com/holgerdaehn/save_the_date", attributes: [
            .font: body, .paragraphStyle: para, .link: URL(string: "https://github.com/holgerdaehn/save_the_date")!,
        ])
        text.append(NSAttributedString(string: "Projektseite\n", attributes: [.font: head, .paragraphStyle: para, .foregroundColor: NSColor.labelColor]))
        text.append(link)
        NSApp.orderFrontStandardAboutPanel(options: [.credits: text, .applicationName: name])
        NSApp.activate(ignoringOtherApps: true)
    }
}

struct HelpTopic: Identifiable {
    let id: String
    let title: String
    let symbol: String
    let body: String

    static let all: [HelpTopic] = [
        HelpTopic(id: "start", title: "Erste Schritte", symbol: "play.circle", body: """
        Save the Date ändert **Erstell-, Änderungs- und Aufnahmedatum** (EXIF) sowie den **Standort** (GPS) von Dateien – einzeln oder für ganze Ordner.

        1. **Hinzufügen:** Dateien oder Ordner ins Fenster ziehen oder über das Plus in der Symbolleiste auswählen (⌘O / ⇧⌘O).
        2. **Prüfen:** Die Tabelle zeigt für jede Datei Erstellt, Geändert, Aufnahme und Standort.
        3. **Einstellen:** Rechts unter „Datum“ und „Standort“ die gewünschten Werte wählen. Die blauen →-Spalten zeigen sofort, was sich ändern würde.
        4. **Anwenden:** „… Objekte ändern“ klicken (⌘↩) und bestätigen.

        Ohne Auswahl gelten die Einstellungen für **alle** Einträge der Liste, mit Auswahl nur für die markierten Zeilen. Unten rechts steht immer, auf wie viele Objekte die Änderung wirkt.
        """),
        HelpTopic(id: "add", title: "Dateien hinzufügen", symbol: "plus.circle", body: """
        • Dateien, Ordner oder beides per Drag & Drop ins Fenster ziehen – auch auf das Programmsymbol im Dock.
        • **Unterordner:** liest Ordner samt allen Unterordnern ein (Standard: an).
        • **Ordner mitbearbeiten:** nimmt auch die Ordner selbst in die Liste auf, damit deren Datum ebenfalls geändert werden kann.
        • **Versteckte Dateien:** zeigt auch unsichtbare Dateien an.
        • Die Schalter stehen in der Symbolleiste und im Menü „Darstellung“. Nach einer Änderung wird die Liste automatisch neu eingelesen.
        • Das Filterfeld grenzt die Liste nach Dateinamen ein, ein Klick auf eine Spaltenüberschrift sortiert. Weitere Spalten (z. B. „Ordner“) blenden Sie per Rechtsklick auf die Kopfzeile ein.
        • Der **Anzeigen**-Filter in der Symbolleiste zeigt nur Fotos, nur Videos, Dateien **ohne Aufnahmedatum**, **ohne Standort**, **mit Standort** oder solche, bei denen das **Datum abweicht** (Erstellt/Geändert ≠ Aufnahme).
        • **Wichtig:** Ist ein Filter oder Suchtext aktiv und nichts markiert, wirken Änderungen und Werkzeuge nur auf die angezeigten Zeilen – unten rechts steht immer, auf wie viele.
        • **Entf** entfernt markierte Zeilen aus der Liste – die Dateien selbst bleiben unverändert.
        • Menü „Ablage“ › **Liste als CSV exportieren** (⇧⌘E) speichert die angezeigten Zeilen als Tabelle für Excel oder Numbers.
        """),
        HelpTopic(id: "date", title: "Datum ändern", symbol: "calendar", body: """
        Im Bereich „Datum“ gibt es drei Arten:

        **Festes Datum** – setzt die gewählten Werte auf ein bestimmtes Datum samt Uhrzeit.
        • Erstellt, Geändert und Aufnahme (Foto-EXIF) lassen sich einzeln ein- und ausschalten. „Jetzt“ übernimmt die aktuelle Zeit, das Kalendersymbol öffnet einen Kalender.
        • „Alle Werte verknüpfen“ hält die drei Zeitpunkte gleich; ausschalten, wenn sie sich unterscheiden sollen.
        • „Abstand pro Datei“: Bei einem Wert über 0 erhält jede weitere Datei (nach Name sortiert) diesen Abstand in Sekunden – so bleibt die Reihenfolge einer Fotoserie erhalten. 0 bedeutet: alle gleich.

        **Verschieben** – verschiebt die vorhandenen Werte jeder Datei um Tage, Stunden, Minuten und Sekunden nach „Später“ oder „Früher“. Praktisch bei falsch eingestellter Kamerauhr oder Zeitzone.

        **Übernehmen** – setzt die gewählten Werte (Erstellt, Geändert, Aufnahme) auf ein Datum, das schon vorhanden ist. Die Quelle wählen Sie darüber:
        • **Aufnahmedatum** – das EXIF-Datum des Fotos bzw. Videos (der bisherige Modus „Aus Aufnahme“).
        • **Dateiname** – liest das Datum aus dem Namen, etwa `IMG-20190713-WA0001.jpg` (WhatsApp), `PXL_20210101_120533.jpg`, `Bildschirmfoto 2020-02-18 um 14.22.05.png` oder `signal-1612345678901.jpg`. Ohne Uhrzeit im Namen gilt 12:00 Uhr. Ideal für Fotos ohne EXIF-Daten.
        • **Erstelldatum**, **Änderungsdatum** – kopiert das eine Datei-Datum auf das andere.
        • **Ältestes Datum** – nimmt das früheste aller vorhandenen Daten. Das repariert Dateien, die beim Kopieren ein neues Erstelldatum bekommen haben.
        Dateien ohne das Quelldatum und Werte, die schon stimmen, bleiben unverändert (die Tabelle zeigt es in den →-Spalten).

        Gut zu wissen: Das Aufnahmedatum kann bei Fotos (JPEG, HEIC, PNG, TIFF) und bei MP4-/MOV-Videos geändert werden. Die Bilddaten werden dabei nicht neu komprimiert, bei Videos wird nur der Zeitstempel im Container ersetzt (UTC). Sind „Erstellt“ und „Geändert“ ausgeschaltet, bleiben diese Werte auch beim Schreiben der Fotodaten erhalten.
        """),
        HelpTopic(id: "geo", title: "Standort setzen", symbol: "mappin.and.ellipse", body: """
        Im Bereich „Standort“ stehen **Unverändert**, **Setzen** und **Entfernen** zur Wahl.

        Bei „Setzen“ legen Sie den Aufnahmeort fest durch:
        • **Suche:** Ortsname oder Adresse eingeben, Enter drücken und einen Treffer anklicken.
        • **Karte:** direkt auf die Karte klicken.
        • **Koordinaten:** z. B. „52.5200, 13.4050“ oder „52,52; 13,40“ eintippen und Enter drücken.
        • **Von Auswahl übernehmen:** übernimmt den Standort eines markierten Fotos.

        Der Standort wird als GPS-Angabe in die EXIF-Daten der Fotos geschrieben (JPEG, HEIC, PNG, TIFF). Andere Dateien werden ausgelassen. **Entfernen** löscht vorhandene GPS-Angaben – etwa bevor Fotos weitergegeben werden. Für Karte und Ortssuche ist eine Internetverbindung nötig.
        """),
        HelpTopic(id: "dupes", title: "Duplikate finden", symbol: "square.on.square", body: """
        Menü „Werkzeuge“ › **Duplikate suchen** (⇧⌘D). Verglichen werden alle Dateien der Liste.

        • Erkannt werden Dateien mit **identischem Inhalt** – auch wenn sie anders heißen oder in verschiedenen Ordnern liegen. Zuerst wird die Größe verglichen, dann ein Hash der ersten 64 KB und schließlich SHA-256 der ganzen Datei. Das ist auch bei vielen Fotos schnell.
        • Pro Gruppe wird **eine Datei behalten**. Die Regel wählen Sie unten: ältestes, neuestes, größte Datei, kürzester Pfad oder erstes. Alle übrigen sind zum Löschen vorgewählt; jede Datei lässt sich einzeln umschalten. Pro Gruppe bleibt immer mindestens eine Datei übrig. Vorschaubilder helfen bei der Entscheidung.
        • „In Papierkorb legen“ verschiebt die markierten Dateien nach Bestätigung in den Papierkorb – es wird nichts endgültig gelöscht.
        • **Ähnliche Bilder:** Oben von „Identischer Inhalt“ auf „Ähnliche Bilder“ umschalten. Dann werden auch skalierte, neu komprimierte oder leicht bearbeitete Kopien eines Fotos gefunden. Die Empfindlichkeit („Streng“, „Normal“, „Locker“) bestimmt, wie verschieden Bilder sein dürfen. Vorgabe ist „Größte Datei behalten“ – prüfen Sie die Gruppen trotzdem, bevor Sie löschen.
        """),
        HelpTopic(id: "sort", title: "Nach Datum sortieren", symbol: "calendar.badge.plus", body: """
        Menü „Werkzeuge“ › **Nach Datum sortieren** (⇧⌘S) legt Fotos und Videos in Unterordner wie `2024/07` ab.

        1. **Zielordner** wählen (Vorgabe: der erste Ordner der Liste).
        2. **Struktur:** JJJJ/MM, JJJJ/MM/TT, JJJJ-MM, JJJJ, JJJJ-MM-TT oder JJJJ/JJJJ-MM-TT.
        3. **Datum:** Vorgabe ist die beste Schätzung – Aufnahmedatum, sonst das Datum im Dateinamen (z. B. WhatsApp-Fotos), sonst Änderungsdatum. Alternativ nur Aufnahme/Änderung/Erstellung.
        4. Optionen: nur Fotos und Videos, Kopieren statt Verschieben, leere Quellordner entfernen.
        5. Die Vorschau zeigt für jede Datei den neuen Pfad. „Ablegen“ startet die Aktion.

        Gleiche Dateinamen mit unterschiedlichem Inhalt werden zu „name (2).jpg“. Liegt am Ziel schon eine identische Datei, wird übersprungen. Ohne Auswahl wird die ganze Liste sortiert, sonst nur die markierte.

        Tipp: Erst Duplikate entfernen, dann sortieren. Mit **Letzte Sortierung rückgängig** (Menü „Werkzeuge“) lässt sich die Aktion zurücknehmen.
        """),
        HelpTopic(id: "rename", title: "Nach Datum umbenennen", symbol: "textformat.abc", body: """
        Menü „Werkzeuge“ › **Nach Datum umbenennen** (⇧⌘R) benennt Dateien nach ihrem Datum um – etwa `IMG_4711.JPG` in `2024-07-13_14-22-05.jpg`. So sortieren sich Fotos von mehreren Kameras und Handys im Finder automatisch in der richtigen Reihenfolge.

        1. **Muster** eingeben oder unter „Vorlagen“ wählen. Platzhalter: `{JJJJ}` `{MM}` `{TT}` `{hh}` `{mm}` `{ss}` für das Datum, `{Name}` für den alten Namen ohne Endung und `{Nr}` für einen Zähler (001, 002 …, in zeitlicher Reihenfolge).
        2. **Datum:** wie beim Sortieren – Vorgabe ist die beste Schätzung aus Aufnahme, Dateiname und Änderung.
        3. Optionen: Endung klein schreiben, nur Fotos und Videos.
        4. Die Vorschau zeigt alt und neu. Gibt es einen Namen schon (auch in derselben Sekunde aufgenommene Fotos), wird „(2)“, „(3)“ … angehängt. Dateien ohne Datum werden übersprungen.

        Die Endung bleibt erhalten. Mit **Letzte Umbenennung rückgängig** (Menü „Werkzeuge“) lässt sich die Aktion zurücknehmen.
        """),
        HelpTopic(id: "geotag", title: "Geotagging per GPX-Track", symbol: "location.viewfinder", body: """
        Menü „Werkzeuge“ › **Geotagging per GPX-Track** (⇧⌘G) ordnet Fotos anhand der Aufnahmezeit einen Ort zu. Die Ortsdaten stammen aus einem Tracklog im GPX-Format, wie ihn GPS-Logger, Wander-, Lauf- und Fahrrad-Apps (Komoot, Strava, Outdooractive, Garmin …) und die Google-Karten-Zeitleiste liefern.

        1. **GPX-Dateien wählen** – auch mehrere.
        2. Liegt die Kamerauhr in einer anderen Zeitzone als dieser Mac (z. B. im Urlaub nicht umgestellt), „Kamerauhr in anderer Zeitzone“ einschalten und deren UTC-Versatz eintragen (Berlin im Sommer: 2).
        3. **Höchstens … Minuten:** So weit darf ein Trackpunkt von der Aufnahmezeit entfernt sein. Zwischen zwei Punkten wird der Ort berechnet, sonst gilt der nächste.
        4. Die Vorschau zeigt für jedes Foto den neuen Ort oder den Grund, warum keiner zugeordnet wird. Fotos mit Standort bleiben unberührt, außer „Vorhandene Standorte überschreiben“ ist an.

        Der Standort wird verlustfrei in die EXIF-Daten geschrieben (JPEG, HEIC, PNG, TIFF). Zurücknehmen: „Letzte Änderung rückgängig“.
        """),
        HelpTopic(id: "clean", title: "Aufräumen", symbol: "wand.and.sparkles", body: """
        Menü „Werkzeuge“ › **Aufräumen** (⇧⌘K) sucht in den hinzugefügten Ordnern – inklusive Unterordnern und versteckter Dateien – nach Überflüssigem:

        • Systemdateien wie `desktop.ini`, `Thumbs.db`, `.DS_Store`, `._*`-Reste, `.picasa.ini`, `@eaDir`, `__MACOSX` u. ä.
        • **Eigene Muster**, z. B. `*.tmp, *.bak` (mit * und ? als Platzhalter, Enter übernimmt).
        • **Leere Ordner** – auch solche, die nur aus Systemdateien bestehen.

        Alle Fundstellen werden aufgelistet und sind vorgewählt. Einzelne lassen sich abwählen. „In Papierkorb legen“ räumt auf; endgültig gelöscht wird nichts.
        """),
        HelpTopic(id: "undo", title: "Rückgängig machen", symbol: "arrow.uturn.backward", body: """
        • **Letzte Änderung rückgängig** (Schaltfläche unter „Ändern“ oder Menü „Aktion“, ⌥⌘Z) stellt Erstell- und Änderungsdatum, ein geändertes Aufnahmedatum und einen geänderten Standort der letzten Aktion wieder her. Eine Ausnahme: Hatte ein Foto vorher gar kein Aufnahmedatum, bleibt das neue bestehen. Bei wichtigen Fotos vorher trotzdem eine Kopie anlegen.
        • **Letzte Sortierung rückgängig** und **Letzte Umbenennung rückgängig** (Menü „Werkzeuge“) legen Dateien zurück bzw. entfernen Kopien.
        • Gelöschte Duplikate und aufgeräumte Dateien liegen im Papierkorb und lassen sich dort zurücklegen.

        Die Rückgängig-Information gilt nur bis zum Beenden der App.
        """),
        HelpTopic(id: "keys", title: "Tastenkürzel", symbol: "command", body: """
        `⌘O`  Dateien öffnen
        `⇧⌘O`  Ordner öffnen
        `⌘R`  Neu einlesen
        `⇧⌘⌫`  Liste leeren
        `⌘A`  Alles auswählen
        `Entf`  Aus Liste entfernen
        `⌘↩`  Änderungen anwenden
        `⌥⌘Z`  Letzte Änderung rückgängig
        `⇧⌘D`  Duplikate suchen
        `⇧⌘S`  Nach Datum sortieren
        `⇧⌘R`  Nach Datum umbenennen
        `⇧⌘G`  Geotagging per GPX-Track
        `⇧⌘E`  Liste als CSV exportieren
        `⇧⌘K`  Aufräumen
        `⌥⌘I`  Informationen ein-/ausblenden
        `⌘?`  Anleitung
        """),
        HelpTopic(id: "faq", title: "Hinweise & Fehlerbehebung", symbol: "questionmark.circle", body: """
        • **Zugriff verweigert:** macOS schützt z. B. Schreibtisch, Dokumente, Downloads und externe Laufwerke. Erlauben Sie den Zugriff bei der Nachfrage oder in den Systemeinstellungen unter „Datenschutz & Sicherheit“ › „Dateien und Ordner“ bzw. „Voller Festplattenzugriff“.
        • **Erstelldatum:** Manche Dateisysteme und Netzlaufwerke (z. B. FAT, SMB) speichern das Erstelldatum nicht oder ungenau.
        • **Karte bleibt leer:** Für Karte und Ortssuche ist eine Internetverbindung nötig.
        • **Die Fotos-App zeigt das alte Datum:** Mediatheken der Fotos-App werden nicht verändert. Exportieren Sie die Fotos und importieren Sie sie neu.
        • **Datensicherung:** Vor größeren Aktionen wie Sortieren oder Löschen empfiehlt sich ein Backup.
        • **Datenschutz:** Alle Dateien werden nur lokal verarbeitet; es werden keine Daten an den Entwickler übertragen.
        """),
    ]
}

struct HelpView: View {
    @State private var sel: String? = Store.shared.helpTopic ?? HelpTopic.all[0].id

    var body: some View {
        NavigationSplitView {
            List(HelpTopic.all, selection: $sel) { t in
                Label(t.title, systemImage: t.symbol).tag(t.id)
            }
            .navigationSplitViewColumnWidth(min: 200, ideal: 220, max: 280)
        } detail: {
            if let t = HelpTopic.all.first(where: { $0.id == sel }) {
                ScrollView {
                    VStack(alignment: .leading, spacing: 16) {
                        Text(t.title).font(.largeTitle.bold())
                        Text(LocalizedStringKey(t.body)).lineSpacing(5).textSelection(.enabled)
                    }
                    .padding(28)
                    .frame(maxWidth: 720, alignment: .leading)
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
        }
        .frame(minWidth: 840, minHeight: 560)
        .onChange(of: Store.shared.helpTopic) { _, v in if let v { sel = v } }
    }
}

struct HelpCommands: Commands {
    @Environment(\.openWindow) private var openWindow

    var body: some Commands {
        CommandGroup(replacing: .appInfo) {
            Button("Über \(AppInfo.name)") { AppInfo.showAbout() }
        }
        CommandGroup(replacing: .help) {
            Button("\(AppInfo.name) Anleitung") { Store.shared.helpTopic = nil; openWindow(id: "help") }
                .keyboardShortcut("?", modifiers: .command)
            Button("Tastenkürzel") { Store.shared.helpTopic = "keys"; openWindow(id: "help") }
        }
    }
}
