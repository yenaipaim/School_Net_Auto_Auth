$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dotnetCandidates = @(
    (Join-Path $root '.dotnet10\dotnet.exe'),
    'C:\Program Files\dotnet\dotnet.exe',
    (Join-Path $root '.dotnet\dotnet.exe')
)
$dotnet = $dotnetCandidates | Where-Object {
    (Test-Path $_) -and ((& $_ --list-sdks) -match '^10\.0\.')
} | Select-Object -First 1
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $root 'NuGet\packages'
$env:USERPROFILE = $env:DOTNET_CLI_HOME
$env:APPDATA = Join-Path $env:DOTNET_CLI_HOME 'AppData\Roaming'
$env:LOCALAPPDATA = Join-Path $env:DOTNET_CLI_HOME 'AppData\Local'
$publishOutput = Join-Path $root 'artifacts\publish\win-x64'
$installerOutput = Join-Path $root 'artifacts\installer'
$setupName = (-join ([char[]](0x6821,0x56ED,0x7F51,0x81EA,0x52A8,0x8BA4,0x8BC1))) + '-Setup.exe'

if (-not $dotnet) { throw '.NET 10 SDK is required.' }
if (Test-Path $publishOutput) { Remove-Item -LiteralPath $publishOutput -Recurse -Force }

& $dotnet restore (Join-Path $root 'SchoolNetAutoAuth.sln') --configfile (Join-Path $root 'NuGet.Config') --force-evaluate
if ($LASTEXITCODE -ne 0) { throw 'Solution restore failed.' }

& $dotnet test (Join-Path $root 'SchoolNetAutoAuth.sln') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

& $dotnet restore (Join-Path $root 'src\SchoolNetAutoAuth.App\SchoolNetAutoAuth.App.csproj') -r win-x64 --configfile (Join-Path $root 'NuGet.Config') --force-evaluate
if ($LASTEXITCODE -ne 0) { throw 'Application restore failed.' }

& $dotnet publish (Join-Path $root 'src\SchoolNetAutoAuth.App\SchoolNetAutoAuth.App.csproj') -c Release -r win-x64 --self-contained false --no-restore -o $publishOutput
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
if (-not (Test-Path (Join-Path $publishOutput 'App.ico'))) { throw 'Published App.ico is missing.' }
if (-not (Test-Path (Join-Path $publishOutput 'SchoolNetAutoAuth.App.pri'))) { throw 'Published WinUI resource index is missing.' }
if (-not (Test-Path (Join-Path $publishOutput 'Views\MainWindow.xbf'))) { throw 'Published WinUI compiled views are missing.' }

$dotnetRoot = Split-Path -Parent $dotnet
$coreRuntime = Get-ChildItem (Join-Path $dotnetRoot 'shared\Microsoft.NETCore.App') -Directory |
    Where-Object Name -Like '10.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$desktopRuntime = Get-ChildItem (Join-Path $dotnetRoot 'shared\Microsoft.WindowsDesktop.App') -Directory |
    Where-Object Name -Like '10.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$hostFxr = Get-ChildItem (Join-Path $dotnetRoot 'host\fxr') -Directory |
    Where-Object Name -Like '10.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $coreRuntime -or -not $desktopRuntime -or -not $hostFxr) { throw '.NET 10 desktop runtime files are missing.' }
if ($coreRuntime.Name -ne $desktopRuntime.Name -or $coreRuntime.Name -ne $hostFxr.Name) { throw '.NET 10 runtime versions do not match.' }

$appLocalDotnet = Join-Path $publishOutput '.dotnet'
$appLocalCore = Join-Path $appLocalDotnet "shared\Microsoft.NETCore.App\$($coreRuntime.Name)"
$appLocalDesktop = Join-Path $appLocalDotnet "shared\Microsoft.WindowsDesktop.App\$($desktopRuntime.Name)"
$appLocalFxr = Join-Path $appLocalDotnet "host\fxr\$($hostFxr.Name)"
New-Item -ItemType Directory -Force -Path $appLocalCore, $appLocalDesktop, $appLocalFxr | Out-Null
Copy-Item -Path (Join-Path $coreRuntime.FullName '*') -Destination $appLocalCore -Recurse -Force
Copy-Item -Path (Join-Path $desktopRuntime.FullName '*') -Destination $appLocalDesktop -Recurse -Force
Copy-Item -Path (Join-Path $hostFxr.FullName '*') -Destination $appLocalFxr -Recurse -Force

$payload = Join-Path $root 'src\SchoolNetAutoAuth.Installer\payload.zip'
if (Test-Path $payload) { Remove-Item -LiteralPath $payload -Force }
Compress-Archive -Path (Join-Path $publishOutput '*') -DestinationPath $payload -CompressionLevel Optimal

& $dotnet restore (Join-Path $root 'src\SchoolNetAutoAuth.Installer\SchoolNetAutoAuth.Installer.csproj') -r win-x64 --configfile (Join-Path $root 'NuGet.Config') --force-evaluate
if ($LASTEXITCODE -ne 0) { throw 'Installer restore failed.' }

& $dotnet publish (Join-Path $root 'src\SchoolNetAutoAuth.Installer\SchoolNetAutoAuth.Installer.csproj') -c Release -r win-x64 --self-contained true --no-restore -o $installerOutput
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }

$setupPath = Join-Path $installerOutput $setupName
Copy-Item -LiteralPath (Join-Path $installerOutput 'SchoolNetAutoAuth.Setup.exe') -Destination $setupPath -Force
if (-not (Test-Path $setupPath)) { throw 'Final setup executable is missing.' }

Write-Host "Application: $publishOutput"
Write-Host "Installer: $setupPath"
$allFiles = Get-ChildItem -LiteralPath $publishOutput -Recurse -File
$playwrightBytes = ($allFiles | Where-Object FullName -Like '*\.playwright\*' | Measure-Object Length -Sum).Sum
$windowsAppSdkBytes = ($allFiles | Where-Object { $_.Name -like 'Microsoft.WindowsAppRuntime*' -or $_.FullName -like '*\Microsoft.WindowsAppRuntime*' } | Measure-Object Length -Sum).Sum
$runtimeBytes = ($allFiles | Where-Object FullName -Like '*\.dotnet\*' | Measure-Object Length -Sum).Sum
$applicationBytes = ($allFiles | Measure-Object Length -Sum).Sum
$setupBytes = (Get-Item -LiteralPath $setupPath).Length
Write-Host ("Published application bytes: {0:N0}" -f $applicationBytes)
Write-Host ("Playwright bytes: {0:N0}" -f $playwrightBytes)
Write-Host ("Windows App SDK bytes: {0:N0}" -f $windowsAppSdkBytes)
Write-Host (".NET runtime bytes: {0:N0}" -f $runtimeBytes)
Write-Host ("Setup bytes: {0:N0}" -f $setupBytes)
