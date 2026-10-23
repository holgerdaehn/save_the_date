namespace SaveTheDate;

/// <summary>Inhalt der Anleitung. Auszeichnung: **fett**, `Code`, Zeilen mit • als Aufzählung.</summary>
public static class HelpContent
{
    public static readonly (string Id, string Title, string Body)[] Topics =
    [
        ("start", "Erste Schritte", """
Save the Date ändert **Erstell-, Änderungs- und Aufnahmedatum** (EXIF) sowie den **Standort** (GPS) von Dateien – einzeln oder für ganze Ordner.

1. **Hinzufügen:** Dateien oder Ordner ins Fenster ziehen oder über „Dateien …“ / „Ordner …“ in der Symbolleiste auswählen (Strg+O / Strg+Umschalt+O).
2. **Prüfen:** Die Tabelle zeigt für jede Datei Erstellt, Geändert, Aufnahme und Standort.
3. **Einstellen:** Rechts unter „Datum“ und „Standort“ die gewünschten Werte wählen. Die blauen →-Spalten zeigen sofort, was sich ändern würde.
4. **Anwenden:** Unten rechts auf „… Objekte ändern“ klicken (Strg+Enter) und bestätigen.

Ohne Auswahl gelten die Einstellungen für **alle** Einträge der Liste, mit Auswahl nur für die markierten Zeilen. Unten rechts steht immer, auf wie viele Objekte die Änderung wirkt.
"""),
        ("add", "Dateien hinzufügen", """
• Dateien, Ordner oder beides per Drag & Drop ins Fenster ziehen.
• **Unterordner:** liest Ordner samt allen Unterordnern ein (Standard: an).
• **Ordner mitbearbeiten:** nimmt auch die Ordner selbst in die Liste auf, damit deren Datum ebenfalls geändert werden kann.
• **Versteckte:** zeigt auch versteckte und Systemdateien an.
• Die Schalter stehen in der Symbolleiste und im Menü „Optionen“. Nach einer Änderung wird die Liste automatisch neu eingelesen (F5 liest jederzeit neu ein).
• Das Filterfeld grenzt die Liste nach Dateinamen ein, ein Klick auf eine Spaltenüberschrift sortiert. Weitere Spalten (z. B. „Ordner“) blenden Sie im Menü „Ansicht“ ein.
• **Entf** entfernt markierte Zeilen aus der Liste – die Dateien selbst bleiben unverändert.
"""),
        ("date", "Datum ändern", """
Im Bereich „Datum“ gibt es drei Arten:

**Festes Datum** – setzt die gewählten Werte auf ein bestimmtes Datum samt Uhrzeit (hh:mm:ss).
• Erstellt, Geändert und Aufnahme (Foto-EXIF) lassen sich einzeln ein- und ausschalten. „Jetzt“ übernimmt die aktuelle Zeit.
• „Alle Werte verknüpfen“ hält die drei Zeitpunkte gleich; ausschalten, wenn sie sich unterscheiden sollen.
• „Abstand pro Datei“: Bei einem Wert über 0 erhält jede weitere Datei (nach Name sortiert) diesen Abstand in Sekunden – so bleibt die Reihenfolge einer Fotoserie erhalten. 0 bedeutet: alle gleich.

**Verschieben** – verschiebt die vorhandenen Werte jeder Datei um Tage, Stunden, Minuten und Sekunden nach „Später“ oder „Früher“. Praktisch bei falsch eingestellter Kamerauhr oder Zeitzone.

**Aus Aufnahme** – setzt Erstellt und Geändert auf das EXIF-Aufnahmedatum des Fotos. Dateien ohne Aufnahmedatum bleiben unverändert.

Gut zu wissen: Das Aufnahmedatum kann nur bei **JPEG-Fotos** geändert werden. Die Bilddaten werden dabei nicht neu komprimiert. Sind „Erstellt“ und „Geändert“ ausgeschaltet, bleiben diese Werte auch beim Schreiben der Fotodaten erhalten.
"""),
        ("geo", "Standort setzen", """
Im Bereich „Standort“ stehen **Unverändert**, **Setzen** und **Entfernen** zur Wahl.

Bei „Setzen“ legen Sie den Aufnahmeort fest durch:
• **Suche:** Ortsname oder Adresse eingeben, Enter drücken und einen Treffer anklicken.
• **Karte:** direkt auf die Karte klicken. Ziehen verschiebt, das Mausrad oder die Tasten ＋ / − zoomen.
• **Koordinaten:** z. B. „52.5200, 13.4050“ oder „52,52; 13,40“ eintippen und Enter drücken.
• **Von Auswahl übernehmen:** übernimmt den Standort eines markierten Fotos.

Der Standort wird als GPS-Angabe in die EXIF-Daten von **JPEG-Fotos** geschrieben. Andere Dateien werden ausgelassen. **Entfernen** löscht vorhandene GPS-Angaben – etwa bevor Fotos weitergegeben werden. Für Karte und Ortssuche ist eine Internetverbindung nötig (Kartendaten von OpenStreetMap).
"""),
        ("dupes", "Duplikate finden", """
Menü „Werkzeuge“ › **Duplikate suchen** (Strg+Umschalt+D). Verglichen werden alle Dateien der Liste.

• Erkannt werden Dateien mit **identischem Inhalt** – auch wenn sie anders heißen oder in verschiedenen Ordnern liegen. Zuerst wird die Größe verglichen, dann ein Hash der ersten 64 KB und schließlich SHA-256 der ganzen Datei. Das ist auch bei vielen Fotos schnell.
• Pro Gruppe wird **eine Datei behalten**. Die Regel wählen Sie unten: ältestes, neuestes, kürzester Pfad oder erstes. Alle übrigen sind zum Löschen vorgewählt; jede Datei lässt sich einzeln umschalten. Pro Gruppe bleibt immer mindestens eine Datei übrig.
• „In Papierkorb legen“ verschiebt die markierten Dateien nach Bestätigung in den Papierkorb – es wird nichts endgültig gelöscht.
• Nicht erkannt werden neu komprimierte, skalierte oder bearbeitete Kopien eines Fotos, weil sich deren Inhalt unterscheidet.
"""),
        ("sort", "Nach Datum sortieren", """
Menü „Werkzeuge“ › **Nach Datum sortieren** (Strg+Umschalt+S) legt Fotos und Videos in Unterordner wie `2024\07` ab.

1. **Zielordner** wählen (Vorgabe: der erste Ordner der Liste).
2. **Struktur:** JJJJ/MM, JJJJ/MM/TT oder JJJJ-MM.
3. **Datum:** Aufnahmedatum (sonst Änderungsdatum), nur Änderungsdatum oder nur Erstelldatum.
4. Optionen: nur Fotos und Videos, Kopieren statt Verschieben, leere Quellordner entfernen.
5. Die Vorschau zeigt für jede Datei den neuen Pfad. „Ablegen“ startet die Aktion.

Gleiche Dateinamen mit unterschiedlichem Inhalt werden zu „name (2).jpg“. Liegt am Ziel schon eine identische Datei, wird übersprungen. Ohne Auswahl wird die ganze Liste sortiert, sonst nur die markierte.

Tipp: Erst Duplikate entfernen, dann sortieren. Mit **Letzte Sortierung rückgängig** (Menü „Werkzeuge“) lässt sich die Aktion zurücknehmen. Das Aufnahmedatum wird nur bei JPEG-Fotos gelesen; bei allen anderen Dateien dient das Änderungsdatum.
"""),
        ("clean", "Aufräumen", """
Menü „Werkzeuge“ › **Aufräumen** (Strg+Umschalt+K) sucht in den hinzugefügten Ordnern – inklusive Unterordnern und versteckter Dateien – nach Überflüssigem:

• Systemdateien wie `desktop.ini`, `Thumbs.db`, `.DS_Store`, `._*`-Reste, `.picasa.ini`, `@eaDir`, `__MACOSX` u. ä.
• **Eigene Muster**, z. B. `*.tmp, *.bak` (mit * und ? als Platzhalter, Enter übernimmt).
• **Leere Ordner** – auch solche, die nur aus Systemdateien bestehen.

Alle Fundstellen werden aufgelistet und sind vorgewählt. Einzelne lassen sich abwählen. „In Papierkorb legen“ räumt auf; endgültig gelöscht wird nichts.
"""),
        ("undo", "Rückgängig machen", """
• **Datei-Daten zurücksetzen** (Schaltfläche unter „Ändern“ oder Menü „Aktion“) stellt Erstell- und Änderungsdatum der letzten Aktion wieder her. Fotodaten (Aufnahmedatum, Standort) werden dadurch **nicht** zurückgesetzt – bei wichtigen Fotos vorher eine Kopie anlegen.
• **Letzte Sortierung rückgängig** (Menü „Werkzeuge“) legt verschobene Dateien zurück bzw. entfernt Kopien.
• Gelöschte Duplikate und aufgeräumte Dateien liegen im Papierkorb und lassen sich dort wiederherstellen.

Die Rückgängig-Information gilt nur bis zum Beenden der App.
"""),
        ("keys", "Tastenkürzel", """
`Strg+O`  Dateien öffnen
`Strg+Umschalt+O`  Ordner öffnen
`F5`  Neu einlesen
`Strg+A`  Alles auswählen
`Entf`  Aus Liste entfernen
`Strg+Enter`  Änderungen anwenden
`Strg+Umschalt+D`  Duplikate suchen
`Strg+Umschalt+S`  Nach Datum sortieren
`Strg+Umschalt+K`  Aufräumen
`F1`  Anleitung
"""),
        ("faq", "Hinweise & Fehlerbehebung", """
• **Zugriff verweigert / Fehler beim Ändern:** Schreibgeschützte Dateien (Eigenschaften › Schreibgeschützt) lassen sich nicht umschreiben; das gilt für Aufnahmedatum und Standort. In Systemordnern sind Administratorrechte nötig.
• **Aufnahmedatum und Standort:** Nur JPEG-Fotos. Bei HEIC, PNG, RAW und Videos lassen sich nur die Datei-Daten (Erstellt, Geändert) ändern.
• **OneDrive & Co.:** Dateien, die nur online verfügbar sind, werden beim Duplikatvergleich heruntergeladen. Vorher „Immer auf diesem Gerät behalten“ wählen oder diese Ordner auslassen.
• **Karte bleibt leer:** Für Karte und Ortssuche ist eine Internetverbindung nötig.
• **Datensicherung:** Vor größeren Aktionen wie Sortieren oder Löschen empfiehlt sich ein Backup.
• **Datenschutz:** Alle Dateien werden nur lokal verarbeitet; es werden keine Daten an den Entwickler übertragen. Nur Karte und Ortssuche senden Kartenausschnitte und Suchbegriffe an OpenStreetMap.
"""),
    ];
}
