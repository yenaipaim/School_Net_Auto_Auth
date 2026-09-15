$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $root 'NuGet\packages'
$env:USERPROFILE = $env:DOTNET_CLI_HOME
$env:APPDATA = Join-Path $env:DOTNET_CLI_HOME 'AppData\Roaming'
$env:LOCALAPPDATA = Join-Path $env:DOTNET_CLI_HOME 'AppData\Local'
$output = Join-Path $root 'artifacts\publish\win-x64'

if (-not (Test-Path $dotnet)) { throw '项目内未安装 .NET SDK，请先运行 .tools\dotnet-install.ps1。' }
if (Test-Path $output) { Remove-Item -LiteralPath $output -Recurse -Force }
& $dotnet test (Join-Path $root 'SchoolNetAutoAuth.sln') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw '测试失败。' }
& $dotnet publish (Join-Path $root 'src\SchoolNetAutoAuth.App\SchoolNetAutoAuth.App.csproj') -c Release -r win-x64 --self-contained true --no-restore -o $output
if ($LASTEXITCODE -ne 0) { throw '发布失败。' }
Write-Host "发布完成：$output"
$payload = Join-Path $root 'src\SchoolNetAutoAuth.Installer\payload.zip'
if (Test-Path $payload) { Remove-Item -LiteralPath $payload -Force }
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $payload -CompressionLevel Optimal
& $dotnet restore (Join-Path $root 'src\SchoolNetAutoAuth.Installer\SchoolNetAutoAuth.Installer.csproj') --configfile (Join-Path $root 'NuGet.Config')
if ($LASTEXITCODE -ne 0) { throw '安装器还原失败。' }
$installerOutput = Join-Path $root 'artifacts\installer'
& $dotnet publish (Join-Path $root 'src\SchoolNetAutoAuth.Installer\SchoolNetAutoAuth.Installer.csproj') -c Release -r win-x64 --self-contained true --no-restore -o $installerOutput
if ($LASTEXITCODE -ne 0) { throw '安装器生成失败。' }
Move-Item -LiteralPath (Join-Path $installerOutput 'SchoolNetAutoAuth.Setup.exe') -Destination (Join-Path $installerOutput '校园网自动认证-Setup.exe') -Force
Write-Host "安装包完成：$(Join-Path $installerOutput '校园网自动认证-Setup.exe')"
