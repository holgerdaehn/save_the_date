# Changelog

## 1.2.0 – 2026-11-27

**Neu**
- Skript `Scripts/datum-aus-name.swift`: setzt Erstell- und Änderungsdatum von Bildern wie `19860701_120000000_iOS.jpg` auf den Zeitstempel im Namen (mit Probelauf `-n`).
- macOS: Meldung, wenn der Speicherort (z. B. OneDrive) das Erstelldatum nicht übernimmt.

**Behoben**
- Datum im Dateinamen wird jetzt ab 1900 erkannt (bisher erst ab 1990).
- „Leere Ordner entfernen“ löscht keine Ordner mehr, in denen nur Unterordner wie `__MACOSX` oder `@eaDir` liegen.
- Aufräumen entfernt `._`-Dateien nur noch, wenn die zugehörige Datei fehlt (Finder-Tags auf USB-Sticks und NAS bleiben erhalten).
- Windows: EXIF-Block wächst bei wiederholten Änderungen nicht mehr.
- macOS: „Alle Werte verknüpfen“ ist standardmäßig aus.

## 1.1.0 – 2026-11-07

**Neu**
- **Übernehmen:** Datum aus Aufnahmedatum, **Dateiname** (z. B. `IMG-20190713-WA0001`, `PXL_20210101_120533`, `Bildschirmfoto 2020-02-18 um 14.22.05`), Erstell-, Änderungs- oder ältestem Datum setzen – ersetzt „Aus Aufnahme“.
- **Videos:** Aufnahmedatum von MP4/MOV/M4V/3GP wird gelesen, angezeigt und lässt sich ändern.
- **Geotagging per GPX-Track** mit Zeitzonen-Ausgleich, Interpolation und Vorschau.
- **Nach Datum umbenennen** mit Mustern, Zähler, Namenskonflikt-Behandlung, Vorschau und Rückgängig.
- **Duplikate:** ähnliche Bilder (Differenz-Hash, einstellbare Empfindlichkeit), Vorschaubilder, Regel „Größte Datei behalten“.
- **Sortieren:** neue Strukturen (`JJJJ`, `JJJJ-MM-TT`, `JJJJ/JJJJ-MM-TT`); Standard-Datum nutzt bei fehlendem EXIF das Datum im Dateinamen.
- **Anzeigen-Filter** (Fotos, Videos, ohne Aufnahmedatum, ohne Standort, mit Standort, Datum weicht ab), Spalte „Größe“ und **CSV-Export**.

**Verbessert**
- Ist ein Filter oder Suchtext aktiv und nichts markiert, wirken Änderungen und Werkzeuge nur noch auf die angezeigten Zeilen (bisher auf alle).
- „Rückgängig“ stellt jetzt auch Aufnahmedatum und Standort wieder her, nicht nur Erstell-/Änderungsdatum.
- Windows: Symbolleiste bricht bei schmalen Fenstern um.

## 1.0.0 – 2026-10-23

Erste Version für macOS und Windows.

- Erstell-, Änderungs- und Aufnahmedatum (EXIF) von Dateien, Ordnern und Ordnern inkl. Unterordnern ändern:
  festes Datum, Verschieben um einen Zeitraum oder Übernahme des Aufnahmedatums; Vorschau vor dem Anwenden.
- GPS-Standort von Fotos setzen (Ortssuche, Karte, Koordinaten) oder entfernen.
- Duplikate erkennen (Inhalt, nicht Name) und in den Papierkorb legen.
- Fotos und Videos nach `JJJJ/MM` sortieren (verschieben oder kopieren, mit Vorschau und Rückgängig).
- Aufräumen: `desktop.ini`, `Thumbs.db`, `.DS_Store`, `._*`, eigene Muster und leere Ordner.
- Anleitung und Info-Fenster.
