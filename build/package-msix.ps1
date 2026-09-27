# Backdrop MSIX packager: unpackaged outputs -> signed MSIX via Windows SDK only.
# Usage: pwsh build/package-msix.ps1 [-Version 1.0.0.0]
# Requires: Windows SDK 10.0.26100 (MakeAppx/SignTool), admin (cert install + Add-AppxPackage).
param([string]$Version = "1.0.0.0")
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'src\Backdrop.App\bin\Release\net8.0-windows10.0.22621.0\win-x64'
$stage = Join-Path $root 'out\msix-stage'
$outMsix = Join-Path $root "out\Backdrop_$Version`_x64.msix"
$certSubject = 'CN=BackdropDev'
$sdkBin = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64'

Write-Host '== 1. Build =='
dotnet build (Join-Path $root 'Backdrop.sln') -c Release --nologo -v minimal | Select-Object -Last 3

Write-Host '== 2. Stage payload =='
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path "$stage\Assets" -Force | Out-Null
Copy-Item "$bin\*" $stage -Recurse -Force -Exclude '*.pdb'

Write-Host '== 3. Assets (generated PNGs) =='
Add-Type -AssemblyName System.Drawing
function New-Logo($path, $w, $h, $bg, $letter, $fontSize) {
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear([System.Drawing.ColorTranslator]::FromHtml($bg))
  $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold)
  $brush = [System.Drawing.Brushes]::White
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
  $g.DrawString($letter, $font, $brush, [System.Drawing.RectangleF]::new(0, 0, $w, $h), $sf)
  $g.Dispose(); $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}
New-Logo "$stage\Assets\Logo.png" 150 150 '#1B1B1B' 'B' 72
New-Logo "$stage\Assets\SmallLogo.png" 44 44 '#1B1B1B' 'B' 22
New-Logo "$stage\Assets\Splash.png" 620 300 '#1B1B1B' 'Backdrop' 48
New-Logo "$stage\Assets\StoreLogo.png" 50 50 '#1B1B1B' 'B' 24

Write-Host '== 4. Manifest =='
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10">
  <Identity Name="BackdropDev.Backdrop" Version="$Version" Publisher="$certSubject" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>Backdrop</DisplayName>
    <PublisherDisplayName>Backdrop</PublisherDisplayName>
    <Description>Lightweight, native live wallpapers for Windows 11.</Description>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.22000.0" MaxVersionTested="10.0.26100.0" />
    <PackageDependency Name="Microsoft.WindowsAppRuntime.1.5" MinVersion="5001.214.1843.0" Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" />
  </Dependencies>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Applications>
    <Application Id="App" Executable="Backdrop.App.exe" EntryPoint="Backdrop.App.App">
      <uap:VisualElements DisplayName="Backdrop" Description="Live wallpapers" BackgroundColor="#1B1B1B"
                          Square150x150Logo="Assets\Logo.png" Square44x44Logo="Assets\SmallLogo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Logo.png" />
        <uap:SplashScreen Image="Assets\Splash.png" />
      </uap:VisualElements>
      <Extensions>
        <desktop:Extension Category="windows.startupTask" Executable="Backdrop.App.exe" EntryPoint="Backdrop.App.App">
          <desktop:StartupTask TaskId="BackdropStartup" Enabled="false" DisplayName="Backdrop" />
        </desktop:Extension>
      </Extensions>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
    <Capability Name="internetClient" />
  </Capabilities>
</Package>
"@
Set-Content -LiteralPath "$stage\AppxManifest.xml" -Value $manifest -Encoding UTF8

Write-Host '== 5. Cert (self-signed, TrustedPeople) =='
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq $certSubject | Select-Object -First 1
if (-not $cert) {
  $cert = New-SelfSignedCertificate -Type Custom -Subject $certSubject `
    -KeyUsage DigitalSignature -FriendlyName 'BackdropDev packaging' `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')
}
$pfx = Join-Path $root 'out\BackdropDev.pfx'
$pwd = ConvertTo-SecureString -String 'backdrop-dev-1234' -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $pwd -Force | Out-Null
$tp = Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq $cert.Thumbprint
if (-not $tp) {
  Import-PfxCertificate -FilePath $pfx -CertStoreLocation Cert:\LocalMachine\TrustedPeople -Password $pwd | Out-Null
  Write-Host 'Cert trusted.'
} else { Write-Host 'Cert already trusted.' }

Write-Host '== 6. Pack =='
New-Item -ItemType Directory -Path (Split-Path $outMsix) -Force | Out-Null
& "$sdkBin\MakeAppx.exe" pack /d $stage /p $outMsix /o
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed: $LASTEXITCODE" }

Write-Host '== 7. Sign =='
& "$sdkBin\SignTool.exe" sign /fd SHA256 /f $pfx /p 'backdrop-dev-1234' $outMsix
if ($LASTEXITCODE -ne 0) { throw "SignTool failed: $LASTEXITCODE" }

Write-Host '== 8. Install =='
Add-AppxPackage $outMsix -ForceApplicationShutdown
Get-AppxPackage -Name 'BackdropDev.Backdrop' | Select-Object Name,Version,Status
Write-Host "MSIX ready: $outMsix"
