# Packaging & Distribution Guide

This guide explains how to package TaskMGR for distribution on Windows and macOS.

## Quick Reference

| Platform | Command | Output |
|----------|---------|--------|
| macOS (current) | `dotnet publish -c Release -r osx-arm64 --self-contained` | Single folder app |
| macOS (Intel) | `dotnet publish -c Release -r osx-x64 --self-contained` | Single folder app |
| Windows x64 | `dotnet publish -c Release -r win-x64 --self-contained` | Single folder app |
| Windows ARM | `dotnet publish -c Release -r win-arm64 --self-contained` | Single folder app |

---

## Step 1: Self-Contained Publish

Self-contained apps include the .NET runtime, so users don't need to install .NET.

### macOS (Apple Silicon)
```bash
cd /Users/macuser/Desktop/code/taskMGR-clone
dotnet publish src/TaskMGR.UI -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

### macOS (Intel)
```bash
dotnet publish src/TaskMGR.UI -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true
```

### Windows x64
```bash
dotnet publish src/TaskMGR.UI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output location: `src/TaskMGR.UI/bin/Release/net8.0/{runtime}/publish/`

---

## Step 2: Create macOS App Bundle

To create a proper `.app` bundle for macOS:

### 2.1 Create Bundle Structure
```bash
APP_NAME="TaskMGR"
PUBLISH_DIR="src/TaskMGR.UI/bin/Release/net8.0/osx-arm64/publish"
BUNDLE_DIR="dist/${APP_NAME}.app"

mkdir -p "${BUNDLE_DIR}/Contents/MacOS"
mkdir -p "${BUNDLE_DIR}/Contents/Resources"

# Copy published files
cp -R ${PUBLISH_DIR}/* "${BUNDLE_DIR}/Contents/MacOS/"
```

### 2.2 Create Info.plist
```bash
cat > "${BUNDLE_DIR}/Contents/Info.plist" << 'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>TaskMGR</string>
    <key>CFBundleDisplayName</key>
    <string>Task Manager</string>
    <key>CFBundleIdentifier</key>
    <string>com.taskmgr.app</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundleExecutable</key>
    <string>TaskMGR.UI</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
</dict>
</plist>
EOF
```

### 2.3 Make Executable
```bash
chmod +x "${BUNDLE_DIR}/Contents/MacOS/TaskMGR.UI"
```

### 2.4 Create DMG (Optional)
```bash
# Create a DMG for easy distribution
hdiutil create -volname "TaskMGR" -srcfolder dist/TaskMGR.app -ov -format UDZO dist/TaskMGR.dmg
```

---

## Step 3: Windows Packaging

### Option A: Single Executable
The `PublishSingleFile` option creates a single .exe:
```bash
dotnet publish src/TaskMGR.UI -c Release -r win-x64 --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true
```

### Option B: MSIX Package (Windows Store)
1. Install the Windows Application Packaging Project in Visual Studio
2. Add the UI project as a reference
3. Configure Package.appxmanifest
4. Build the MSIX package

### Option C: Installer (Inno Setup)
Create `installer.iss`:
```iss
[Setup]
AppName=TaskMGR
AppVersion=1.0.0
DefaultDirName={autopf}\TaskMGR
DefaultGroupName=TaskMGR
OutputDir=dist
OutputBaseFilename=TaskMGR-Setup

[Files]
Source: "src\TaskMGR.UI\bin\Release\net8.0\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\TaskMGR"; Filename: "{app}\TaskMGR.UI.exe"
Name: "{commondesktop}\TaskMGR"; Filename: "{app}\TaskMGR.UI.exe"
```

---

## Step 4: Automated Build Script

Create `build-all.sh` for one-command builds:

```bash
#!/bin/bash
set -e

VERSION="1.0.0"
PROJECT="src/TaskMGR.UI"
DIST="dist"

rm -rf $DIST
mkdir -p $DIST

echo "Building macOS ARM64..."
dotnet publish $PROJECT -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o $DIST/osx-arm64

echo "Building macOS x64..."
dotnet publish $PROJECT -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o $DIST/osx-x64

echo "Building Windows x64..."
dotnet publish $PROJECT -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o $DIST/win-x64

echo "Creating archives..."
cd $DIST
tar -czvf TaskMGR-${VERSION}-osx-arm64.tar.gz osx-arm64
tar -czvf TaskMGR-${VERSION}-osx-x64.tar.gz osx-x64
zip -r TaskMGR-${VERSION}-win-x64.zip win-x64

echo "Done! Packages in $DIST/"
```

---

## Step 5: Code Signing (Production)

### macOS
```bash
# Sign the app (requires Apple Developer account)
codesign --deep --force --verify --verbose \
    --sign "Developer ID Application: Your Name (TEAM_ID)" \
    dist/TaskMGR.app

# Notarize for Gatekeeper
xcrun notarytool submit dist/TaskMGR.dmg \
    --apple-id "your@email.com" \
    --team-id "TEAM_ID" \
    --password "app-specific-password" \
    --wait
```

### Windows
```powershell
# Sign with certificate
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com dist\TaskMGR.UI.exe
```

---

## Distribution Checklist

- [ ] Test on clean machine without .NET installed
- [ ] Verify all platform builds work
- [ ] Code sign for production releases
- [ ] Create release notes
- [ ] Upload to GitHub Releases / distribution platform

---

## Troubleshooting

### "App is damaged" on macOS
```bash
xattr -cr /path/to/TaskMGR.app
```

### Missing runtime on target machine
Ensure `--self-contained` flag is used during publish.

### Large file size
Use trimming to reduce size:
```bash
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishTrimmed=true
```
⚠️ Test thoroughly as trimming may remove needed code.
