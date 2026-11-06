# Save the Date

[![CI](https://github.com/holgerdaehn/save_the_date/actions/workflows/ci.yml/badge.svg)](https://github.com/holgerdaehn/save_the_date/actions/workflows/ci.yml)

Erstell-, Änderungs- und Aufnahmedatum (EXIF) sowie GPS-Standort von Fotos und Dateien ändern – dazu Duplikate
finden, Fotos nach `JJJJ/MM` sortieren und überflüssige Dateien aufräumen. Für **macOS** (Swift / SwiftUI) und
**Windows** (C# / WPF, .NET 10) im Apple-Stil.

## Funktionen

- **Datum ändern** für Dateien, Ordner und Ordner inkl. Unterordner: festes Datum, Verschieben um einen Zeitraum
  oder **Übernehmen** aus Aufnahmedatum, **Dateiname** (WhatsApp, Pixel, Screenshots …), Erstell-, Änderungs- oder
  ältestem Datum. Die Tabelle zeigt vorab, was sich ändern würde. Auch für MP4/MOV-Videos.
- **Standort setzen oder entfernen** per Ortssuche, Karte oder Koordinaten (GPS-Angabe in den EXIF-Daten).
- **Geotagging per GPX-Track:** Fotos bekommen ihren Ort anhand der Aufnahmezeit aus dem Tracklog eines GPS-Loggers oder
  einer Wander-/Lauf-App (mit Zeitzonen-Ausgleich und Vorschau).
- **Nach Datum umbenennen** (`2024-07-13_14-22-05.jpg`, eigene Muster mit `{JJJJ}{MM}{TT}{hh}{mm}{ss}{Name}{Nr}`) mit Vorschau und Rückgängig.
- **Filter und Export:** nur Fotos, Videos, ohne Aufnahmedatum/Standort oder mit abweichendem Datum anzeigen; Liste als CSV exportieren.
- **Rückgängig** stellt Datei-Daten, Aufnahmedatum und Standort der letzten Aktion wieder her.
- **Duplikate** erkennen – gleicher Inhalt, egal wie die Datei heißt (Größe → Hash der ersten 64 KB → SHA-256) – und
  in den Papierkorb legen – mit Vorschaubildern und optional auch **ähnliche Bilder** (skaliert, neu komprimiert). Pro Gruppe bleibt immer eine Datei.
- **Nach Datum sortieren** in `JJJJ/MM` (oder `JJJJ/MM/TT`, `JJJJ-MM`, `JJJJ`, `JJJJ-MM-TT`, `JJJJ/JJJJ-MM-TT`): verschieben oder kopieren, mit Vorschau,
  Namenskonflikten und Rückgängig.
- **Aufräumen**: `desktop.ini`, `Thumbs.db`, `.DS_Store`, `._*`, eigene Muster (`*.tmp`) und leere Ordner.
- Anleitung und Info-Fenster in der App.

| | macOS | Windows |
|---|---|---|
| Technik | Swift / SwiftUI, macOS 15+ | C# / WPF, .NET 10, Windows 10/11 (64 Bit) |
| Ordner | [`macOS/`](macOS) | [`Windows/`](Windows) |
| Foto-Metadaten | JPEG, HEIC, PNG, TIFF (ImageIO, verlustfrei) | nur JPEG (eigener EXIF-Writer, verlustfrei) |
| Video-Datum | MP4, MOV, M4V, 3GP | MP4, MOV, M4V, 3GP |
| Karte | Apple Karten (MapKit) | OpenStreetMap + Nominatim |

## Installation

Fertige Programme gibt es unter [Releases](https://github.com/holgerdaehn/save_the_date/releases).

- **macOS:** `SaveTheDate-macOS.zip` entpacken und in „Programme“ ziehen. Die App ist nicht notarisiert: beim ersten
  Start per Rechtsklick › „Öffnen“ bestätigen (oder `xattr -dr com.apple.quarantine "Save the Date.app"`).
- **Windows:** `SaveTheDate-Windows.zip` entpacken und `SaveTheDate.exe` starten (keine Installation, .NET ist enthalten).
  Windows SmartScreen meldet bei unbekannten Programmen ggf. „Weitere Informationen › Trotzdem ausführen“.

## Aus dem Quellcode bauen

macOS (Xcode 16+ / Swift 6):

```bash
cd macOS
swift test        # Unit-Tests (EXIF, Dateidaten, Dateiname-Datum, MP4, GPX, Umbenennen, Duplikate, Sortieren, Aufräumen)
./build.sh        # → build/Save the Date.app
```

Windows (.NET 10 SDK):

```powershell
cd Windows
dotnet run --project Tests/CoreCheck        # Logik-Check (EXIF, MP4, GPX, Umbenennen, Duplikate, …) (läuft auch auf macOS/Linux)
dotnet publish SaveTheDate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
# → bin/Release/net10.0-windows/win-x64/publish/SaveTheDate.exe
```

Eine neue Version erscheint automatisch unter Releases, sobald ein Tag `vX.Y.Z` gepusht wird
(`.github/workflows/release.yml`). Versionsnummern: `macOS/build.sh` (`CFBundleShortVersionString`) und
`Windows/SaveTheDate.csproj` (`<Version>`).

## Hinweise

- Ohne Auswahl wirken die Einstellungen auf alle Einträge der Liste, mit Auswahl nur auf die markierten.
- „Letzte Änderung rückgängig“ stellt Erstell-/Änderungsdatum sowie geändertes Aufnahmedatum und geänderten Standort der
  letzten Aktion wieder her (ein vorher fehlendes Aufnahmedatum lässt sich nicht entfernen).
- Ist ein Filter oder Suchtext aktiv und nichts markiert, wirken Änderungen nur auf die angezeigten Zeilen.
- Gelöscht wird ausschließlich in den Papierkorb.
- macOS setzt Erstell- und Änderungsdatum getrennt (erst „geändert“, dann „erstellt“), weil das System das
  Erstelldatum sonst auf ein früheres Änderungsdatum herunterzieht.
- Vor größeren Aktionen (Sortieren, Löschen) empfiehlt sich eine Datensicherung.

## Lizenz und Autor

© 2026 Kundida (Holger Dähn). Veröffentlicht unter der [MIT-Lizenz](LICENSE).
Kartendaten (Windows): © OpenStreetMap-Mitwirkende ([ODbL](https://www.openstreetmap.org/copyright)).
