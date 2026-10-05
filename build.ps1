param([switch]$SkipPublish)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$localSdk = Join-Path $taskRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $taskRoot '.tools\nuget'
$appProject = Join-Path $taskRoot 'src\DungeonCoopFix\DungeonCoopFix.csproj'
$checksProject = Join-Path $taskRoot 'checks\DungeonCoopFix.Checks.csproj'

& $dotnet build $appProject -c Release
if ($LASTEXITCODE) { throw 'Application build failed.' }
& $dotnet run --project $checksProject -c Release
if ($LASTEXITCODE) { throw 'Focused verification failed.' }
if (!$SkipPublish) {
    & $dotnet publish $appProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $taskRoot 'dist\DungeonCoopFix')
    if ($LASTEXITCODE) { throw 'Portable publish failed.' }
    & $dotnet publish $appProject -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $taskRoot 'dist\DungeonCoopFix-small')
    if ($LASTEXITCODE) { throw 'Small publish failed.' }
    foreach ($folder in @('DungeonCoopFix', 'DungeonCoopFix-small')) {
        $destination = Join-Path $taskRoot "dist\$folder"
        Copy-Item -LiteralPath (Join-Path $taskRoot 'README.md') -Destination $destination
        Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE') -Destination $destination
        Copy-Item -LiteralPath (Join-Path $taskRoot 'RELEASE_NOTES.md') -Destination $destination
        Copy-Item -LiteralPath (Join-Path $taskRoot 'docs') -Destination $destination -Recurse -Force
        if ($folder -eq 'DungeonCoopFix') {
            Copy-Item -LiteralPath (Join-Path (Split-Path $dotnet) 'LICENSE.txt') -Destination (Join-Path $destination 'licenses\Microsoft-NET.txt')
            Copy-Item -LiteralPath (Join-Path (Split-Path $dotnet) 'ThirdPartyNotices.txt') -Destination (Join-Path $destination 'licenses\Microsoft-NET-ThirdParty.txt')
        }
        Compress-Archive -Path (Join-Path $destination '*') -DestinationPath (Join-Path $taskRoot "dist\$folder.zip") -Force
    }
}
