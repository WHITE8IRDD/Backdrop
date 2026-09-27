# Self-contained publish: host (single-file, trimmed) + app (INSTALLER.md §4).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'out'
New-Item -ItemType Directory -Path "$out\publish-host", "$out\publish-app" -Force | Out-Null

dotnet publish "$root\src\Backdrop.WallpaperHost\Backdrop.WallpaperHost.csproj" -c Release `
  -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:BackdropHostSingleFile=true -o "$out\publish-host"

dotnet publish "$root\src\Backdrop.App\Backdrop.App.csproj" -c Release `
  -r win-x64 --self-contained true `
  -p:WindowsPackageType=None -o "$out\publish-app"

Copy-Item "$out\publish-host\Backdrop.WallpaperHost.exe" "$out\publish-app\" -Force
Write-Host "Published to $out"
