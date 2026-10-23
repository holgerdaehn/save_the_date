#!/bin/bash
# Baut "Save the Date.app" (Release) im Ordner build/
set -euo pipefail
cd "$(dirname "$0")"
APP="build/Save the Date.app"

swift build -c release
[ -f AppIcon.icns ] || { swift icon.swift && iconutil -c icns AppIcon.iconset && rm -rf AppIcon.iconset; }

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$(swift build -c release --show-bin-path)/SaveTheDate" "$APP/Contents/MacOS/"
cp AppIcon.icns "$APP/Contents/Resources/"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>Save the Date</string>
  <key>CFBundleDisplayName</key><string>Save the Date</string>
  <key>CFBundleIdentifier</key><string>de.savethedate.app</string>
  <key>CFBundleExecutable</key><string>SaveTheDate</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>1.0.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>CFBundleDevelopmentRegion</key><string>de</string>
  <key>NSHumanReadableCopyright</key><string>© 2026 Kundida (Holger Dähn). MIT-Lizenz.</string>
  <key>LSMinimumSystemVersion</key><string>15.0</string>
  <key>NSPrincipalClass</key><string>NSApplication</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>CFBundleDocumentTypes</key><array><dict>
    <key>CFBundleTypeName</key><string>Dateien und Ordner</string>
    <key>CFBundleTypeRole</key><string>Viewer</string>
    <key>LSItemContentTypes</key><array><string>public.item</string><string>public.folder</string></array>
  </dict></array>
  <key>LSApplicationCategoryType</key><string>public.app-category.photography</string>
</dict></plist>
PLIST
codesign --force --sign - "$APP"
echo "Fertig: $APP"
