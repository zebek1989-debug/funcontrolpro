# FanControl Pro MSI Installer (WiX 4)

This folder contains the WiX Toolset v4 scaffold for packaging FanControl Pro as an MSI installer.

## Build Prerequisites

- Windows machine (recommended: `windows-latest` in GitHub Actions).
- .NET SDK 8.x.
- WiX Toolset SDK restored via NuGet (`WixToolset.Sdk`).

## Typical Build Flow

1. Publish application files:

```powershell
dotnet publish src/FanControlPro.Presentation/FanControlPro.Presentation.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o artifacts/publish/win-x64
```

2. Build MSI:

```powershell
pwsh -NoProfile -File scripts/release/Generate-WixFileManifest.ps1 `
  -PublishDir artifacts/publish/win-x64 `
  -OutputFile installer/wix/PublishedFiles.wxs

dotnet build installer/wix/FanControlPro.Installer.wixproj `
  -c Release `
  -p:PublishDir=artifacts/publish/win-x64 `
  -p:ProductVersion=1.0.0
```

Output MSI is created in the WiX project output directory.

3. Validate installer lifecycle:

```powershell
pwsh -NoProfile -File scripts/release/Validate-MsiLifecycle.ps1 `
  -MsiPath artifacts/release/<version>/installer/<file>.msi
```

4. Validate upgrade path from a baseline MSI:

```powershell
pwsh -NoProfile -File scripts/release/Validate-MsiUpgrade.ps1 `
  -OldMsiPath artifacts/release-baseline/<version>/installer/<old>.msi `
  -NewMsiPath artifacts/release/<version>/installer/<new>.msi
```

## Notes

- Installer scope is `perMachine` (elevated install).
- User data in `%APPDATA%\FanControlPro` is intentionally not removed on upgrade.
- Start Menu and Desktop shortcuts are included by default.
- `PublishedFiles.wxs` is generated from publish output and should not be hand-edited.
- WiX build/validation is expected to run on Windows runners (Linux/macOS do not provide MSI APIs).
