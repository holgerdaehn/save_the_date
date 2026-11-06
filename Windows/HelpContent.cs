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
• Der **Anzeigen**-Filter neben dem Suchfeld zeigt nur Fotos, nur Videos, Dateien **ohne Aufnahmedatum**, **ohne Standort**, **mit Standort** oder solche, bei denen das **Datum abweicht** (Erstellt/Geändert ≠ Aufnahme).
• **Wichtig:** Ist ein Filter oder Suchtext aktiv und nichts markiert, wirken Änderungen und Werkzeuge nur auf die angezeigten Zeilen – unten rechts steht immer, auf wie viele.
• **Entf** entfernt markierte Zeilen aus der Liste – die Dateien selbst bleiben unverändert.
• Menü „Datei“ › **Liste als CSV exportieren** (Strg+Umschalt+E) speichert die angezeigten Zeilen als Tabelle für Excel.
"""),
        ("date", "Datum ändern", """
Im Bereich „Datum“ gibt es drei Arten:

**Festes Datum** – setzt die gewählten Werte auf ein bestimmtes Datum samt Uhrzeit (hh:mm:ss).
• Erstellt, Geändert und Aufnahme (Foto-EXIF) lassen sich einzeln ein- und ausschalten. „Jetzt“ übernimmt die aktuelle Zeit.
• „Alle Werte verknüpfen“ hält die drei Zeitpunkte gleich; ausschalten, wenn sie sich unterscheiden sollen.
• „Abstand pro Datei“: Bei einem Wert über 0 erhält jede weitere Datei (nach Name sortiert) diesen Abstand in Sekunden – so bleibt die Reihenfolge einer Fotoserie erhalten. 0 bedeutet: alle gleich.

**Verschieben** – verschiebt die vorhandenen Werte jeder Datei um Tage, Stunden, Minuten und Sekunden nach „Später“ oder „Früher“. Praktisch bei falsch eingestellter Kamerauhr oder Zeitzone.

**Übernehmen** – setzt die gewählten Werte (Erstellt, Geändert, Aufnahme) auf ein Datum, das schon vorhanden ist. Die Quelle wählen Sie darunter:
• **Aufnahmedatum** – das EXIF-Datum des Fotos bzw. Videos (der bisherige Modus „Aus Aufnahme“).
• **Dateiname** – liest das Datum aus dem Namen, etwa `IMG-20190713-WA0001.jpg` (WhatsApp), `PXL_20210101_120533.jpg`, `Bildschirmfoto 2020-02-18 um 14.22.05.png` oder `signal-1612345678901.jpg`. Ohne Uhrzeit im Namen gilt 12:00 Uhr. Ideal für Fotos ohne EXIF-Daten.
• **Erstelldatum**, **Änderungsdatum** – kopiert das eine Datei-Datum auf das andere.
• **Ältestes Datum** – nimmt das früheste aller vorhandenen Daten. Das repariert Dateien, die beim Kopieren ein neues Erstelldatum bekommen haben.
Dateien ohne das Quelldatum und Werte, die schon stimmen, bleiben unverändert (die →-Spalten zeigen es).

Gut zu wissen: Das Aufnahmedatum kann bei **JPEG-Fotos** und **MP4-/MOV-Videos** geändert werden. Die Bilddaten werden dabei nicht neu komprimiert, bei Videos wird nur der Zeitstempel im Container ersetzt (UTC). Sind „Erstellt“ und „Geändert“ ausgeschaltet, bleiben diese Werte auch beim Schreiben der Fotodaten erhalten.
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
• Pro Gruppe wird **eine Datei behalten**. Die Regel wählen Sie unten: ältestes, neuestes, größte Datei, kürzester Pfad oder erstes. Alle übrigen sind zum Löschen vorgewählt; jede Datei lässt sich einzeln umschalten. Pro Gruppe bleibt immer mindestens eine Datei übrig. Vorschaubilder helfen bei der Entscheidung.
• „In Papierkorb legen“ verschiebt die markierten Dateien nach Bestätigung in den Papierkorb – es wird nichts endgültig gelöscht.
• **Ähnliche Bilder:** Oben von „Identischer Inhalt“ auf „Ähnliche Bilder“ umschalten. Dann werden auch skalierte, neu komprimierte oder leicht bearbeitete Kopien eines Fotos gefunden. Die Empfindlichkeit („Streng“, „Normal“, „Locker“) bestimmt, wie verschieden Bilder sein dürfen. Vorgabe ist „Größte Datei behalten“ – prüfen Sie die Gruppen trotzdem, bevor Sie löschen. Erkannt werden Formate, die Windows darstellen kann (JPEG, PNG, BMP, GIF, TIFF …).
"""),
        ("sort", "Nach Datum sortieren", """
Menü „Werkzeuge“ › **Nach Datum sortieren** (Strg+Umschalt+S) legt Fotos und Videos in Unterordner wie `2024\07` ab.

1. **Zielordner** wählen (Vorgabe: der erste Ordner der Liste).
2. **Struktur:** JJJJ\MM, JJJJ\MM\TT, JJJJ-MM, JJJJ, JJJJ-MM-TT oder JJJJ\JJJJ-MM-TT.
3. **Datum:** Vorgabe ist die beste Schätzung – Aufnahmedatum, sonst das Datum im Dateinamen (z. B. WhatsApp-Fotos), sonst Änderungsdatum. Alternativ nur Aufnahme/Änderung/Erstellung.
4. Optionen: nur Fotos und Videos, Kopieren statt Verschieben, leere Quellordner entfernen.
5. Die Vorschau zeigt für jede Datei den neuen Pfad. „Ablegen“ startet die Aktion.

Gleiche Dateinamen mit unterschiedlichem Inhalt werden zu „name (2).jpg“. Liegt am Ziel schon eine identische Datei, wird übersprungen. Ohne Auswahl wird die ganze Liste sortiert, sonst nur die markierte.

Tipp: Erst Duplikate entfernen, dann sortieren. Mit **Letzte Sortierung rückgängig** (Menü „Werkzeuge“) lässt sich die Aktion zurücknehmen. Das Aufnahmedatum wird bei JPEG-Fotos und MP4-/MOV-Videos gelesen; bei allen anderen Dateien zählt das Datum im Namen, sonst das Änderungsdatum.
"""),
        ("rename", "Nach Datum umbenennen", """
Menü „Werkzeuge“ › **Nach Datum umbenennen** (Strg+Umschalt+R) benennt Dateien nach ihrem Datum um – etwa `IMG_4711.JPG` in `2024-07-13_14-22-05.jpg`. So sortieren sich Fotos von mehreren Kameras und Handys im Explorer automatisch in der richtigen Reihenfolge.

1. **Muster** eingeben oder eine Vorlage wählen. Platzhalter: `{JJJJ}` `{MM}` `{TT}` `{hh}` `{mm}` `{ss}` für das Datum, `{Name}` für den alten Namen ohne Endung und `{Nr}` für einen Zähler (001, 002 …, in zeitlicher Reihenfolge).
2. **Datum:** wie beim Sortieren – Vorgabe ist die beste Schätzung aus Aufnahme, Dateiname und Änderung.
3. Optionen: Endung klein schreiben, nur Fotos und Videos.
4. Die Vorschau zeigt alt und neu. Gibt es einen Namen schon (auch in derselben Sekunde aufgenommene Fotos), wird „(2)“, „(3)“ … angehängt. Dateien ohne Datum werden übersprungen.

Die Endung bleibt erhalten. Mit **Letzte Umbenennung rückgängig** (Menü „Werkzeuge“) lässt sich die Aktion zurücknehmen.
"""),
        ("geotag", "Geotagging per GPX-Track", """
Menü „Werkzeuge“ › **Geotagging per GPX-Track** (Strg+Umschalt+G) ordnet JPEG-Fotos anhand der Aufnahmezeit einen Ort zu. Die Ortsdaten stammen aus einem Tracklog im GPX-Format, wie ihn GPS-Logger, Wander-, Lauf- und Fahrrad-Apps (Komoot, Strava, Outdooractive, Garmin …) und die Google-Karten-Zeitleiste liefern.

1. **GPX-Dateien wählen** – auch mehrere.
2. Liegt die Kamerauhr in einer anderen Zeitzone als dieser PC (z. B. im Urlaub nicht umgestellt), „Kamerauhr in anderer Zeitzone“ einschalten und deren UTC-Versatz eintragen (Berlin im Sommer: 2).
3. **Höchstens … Minuten:** So weit darf ein Trackpunkt von der Aufnahmezeit entfernt sein. Zwischen zwei Punkten wird der Ort berechnet, sonst gilt der nächste.
4. Die Vorschau zeigt für jedes Foto den neuen Ort oder den Grund, warum keiner zugeordnet wird. Fotos mit Standort bleiben unberührt, außer „Vorhandene Standorte überschreiben“ ist an.

Der Standort wird verlustfrei in die EXIF-Daten geschrieben. Zurücknehmen: „Letzte Änderung rückgängig“.
"""),
        ("clean", "Aufräumen", """
Menü „Werkzeuge“ › **Aufräumen** (Strg+Umschalt+K) sucht in den hinzugefügten Ordnern – inklusive Unterordnern und versteckter Dateien – nach Überflüssigem:

• Systemdateien wie `desktop.ini`, `Thumbs.db`, `.DS_Store`, `._*`-Reste, `.picasa.ini`, `@eaDir`, `__MACOSX` u. ä.
• **Eigene Muster**, z. B. `*.tmp, *.bak` (mit * und ? als Platzhalter, Enter übernimmt).
• **Leere Ordner** – auch solche, die nur aus Systemdateien bestehen.

Alle Fundstellen werden aufgelistet und sind vorgewählt. Einzelne lassen sich abwählen. „In Papierkorb legen“ räumt auf; endgültig gelöscht wird nichts.
"""),
        ("undo", "Rückgängig machen", """
• **Letzte Änderung rückgängig** (Schaltfläche unter „Ändern“ oder Menü „Aktion“) stellt Erstell- und Änderungsdatum, ein geändertes Aufnahmedatum und einen geänderten Standort der letzten Aktion wieder her. Eine Ausnahme: Hatte ein Foto vorher gar kein Aufnahmedatum, bleibt das neue bestehen. Bei wichtigen Fotos vorher trotzdem eine Kopie anlegen.
• **Letzte Sortierung rückgängig** und **Letzte Umbenennung rückgängig** (Menü „Werkzeuge“) legen Dateien zurück bzw. entfernen Kopien.
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
`Strg+Umschalt+R`  Nach Datum umbenennen
`Strg+Umschalt+G`  Geotagging per GPX-Track
`Strg+Umschalt+E`  Liste als CSV exportieren
`Strg+Umschalt+K`  Aufräumen
`F1`  Anleitung
"""),
        ("faq", "Hinweise & Fehlerbehebung", """
• **Zugriff verweigert / Fehler beim Ändern:** Schreibgeschützte Dateien (Eigenschaften › Schreibgeschützt) lassen sich nicht umschreiben; das gilt für Aufnahmedatum und Standort. In Systemordnern sind Administratorrechte nötig.
• **Aufnahmedatum und Standort:** Das Aufnahmedatum lässt sich bei JPEG-Fotos und MP4-/MOV-Videos ändern, der Standort nur bei JPEG-Fotos. Bei HEIC, PNG, RAW und anderen Videos ändern sich nur die Datei-Daten (Erstellt, Geändert).
• **OneDrive & Co.:** Dateien, die nur online verfügbar sind, werden beim Duplikatvergleich heruntergeladen. Vorher „Immer auf diesem Gerät behalten“ wählen oder diese Ordner auslassen.
• **Karte bleibt leer:** Für Karte und Ortssuche ist eine Internetverbindung nötig.
• **Datensicherung:** Vor größeren Aktionen wie Sortieren oder Löschen empfiehlt sich ein Backup.
• **Datenschutz:** Alle Dateien werden nur lokal verarbeitet; es werden keine Daten an den Entwickler übertragen. Nur Karte und Ortssuche senden Kartenausschnitte und Suchbegriffe an OpenStreetMap.
"""),
    ];
}
