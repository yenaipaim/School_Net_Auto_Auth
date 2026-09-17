$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $root 'NuGet\packages'
$env:USERPROFILE = $env:DOTNET_CLI_HOME
$env:APPDATA = Join-Path $env:DOTNET_CLI_HOME 'AppData\Roaming'
$env:LOCALAPPDATA = Join-Path $env:DOTNET_CLI_HOME 'AppData\Local'
$publishOutput = Join-Path $root 'artifacts\publish\win-x64'
$installerOutput = Join-Path $root 'artifacts\installer'
$uninstallerOutput = Join-Path $root 'artifacts\uninstaller'
$setupName = (-join ([char[]](0x6821,0x56ED,0x7F51,0x81EA,0x52A8,0x8BA4,0x8BC1))) + '-Setup.exe'

if (-not (Test-Path $dotnet)) { throw 'Project-local .NET SDK is missing.' }
if (Test-Path $publishOutput) { Remove-Item -LiteralPath $publishOutput -Recurse -Force }

& $dotnet test (Join-Path $root 'SchoolNetAutoAuth.sln') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

& $dotnet publish (Join-Path $root 'src\SchoolNetAutoAuth.App\SchoolNetAutoAuth.App.csproj') -c Release -r win-x64 --self-contained true --no-restore -o $publishOutput
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
if (-not (Test-Path (Join-Path $publishOutput 'App.ico'))) { throw 'Published App.ico is missing.' }

if (Test-Path $uninstallerOutput) { Remove-Item -LiteralPath $uninstallerOutput -Recurse -Force }
& $dotnet restore (Join-Path $root 'src\SchoolNetAutoAuth.Uninstaller\SchoolNetAutoAuth.Uninstaller.csproj') --configfile (Join-Path $root 'NuGet.Config') -r win-x64
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller restore failed.' }
& $dotnet publish (Join-Path $root 'src\SchoolNetAutoAuth.Uninstaller\SchoolNetAutoAuth.Uninstaller.csproj') -c Release -r win-x64 --self-contained true --no-restore -o $uninstallerOutput
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller publish failed.' }
Copy-Item -LiteralPath (Join-Path $uninstallerOutput 'SchoolNetAutoAuth.Uninstaller.exe') -Destination (Join-Path $publishOutput 'Uninstall.exe') -Force
if (-not (Test-Path (Join-Path $publishOutput 'Uninstall.exe'))) { throw 'Published Uninstall.exe is missing.' }

$payload = Join-Path $root 'src\SchoolNetAutoAuth.Installer\payload.zip'
if (Test-Path $payload) { Remove-Item -LiteralPath $payload -Force }
Compress-Archive -Path (Join-Path $publishOutput '*') -DestinationPath $payload -CompressionLevel Optimal

& $dotnet restore (Join-Path $root 'src\SchoolNetAutoAuth.Installer\SchoolNetAutoAuth.Installer.csproj') --configfile (Join-Path $root 'NuGet.Config')
if ($LASTEXITCODE -ne 0) { throw 'Installer restore failed.' }

& $dotnet publish (Join-Path $root 'src\SchoolNetAutoAuth.Installer\SchoolNetAutoAuth.Installer.csproj') -c Release -r win-x64 --self-contained true --no-restore -o $installerOutput
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }

$setupPath = Join-Path $installerOutput $setupName
Copy-Item -LiteralPath (Join-Path $installerOutput 'SchoolNetAutoAuth.Setup.exe') -Destination $setupPath -Force
if (-not (Test-Path $setupPath)) { throw 'Final setup executable is missing.' }

Write-Host "Application: $publishOutput"
Write-Host "Installer: $setupPath"
