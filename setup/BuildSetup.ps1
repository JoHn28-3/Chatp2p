$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$setupDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projDir = Split-Path -Parent $setupDir
$pubDir = Join-Path $env:TEMP "ChatP2P_Publish"
$zipPath = Join-Path $env:TEMP "ChatP2P_payload.zip"
$outExe = Join-Path $setupDir "ChatP2P_Setup.exe"

if (Test-Path $pubDir) { Remove-Item -Recurse -Force $pubDir }
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

Push-Location $projDir
dotnet publish Chatp2pWPF.csproj -c Release -o $pubDir --disable-build-servers
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Echec de dotnet publish" }
Pop-Location

Copy-Item -Force (Join-Path $projDir "app.ico") (Join-Path $pubDir "app.ico")
Copy-Item -Force (Join-Path $projDir "logo.png") (Join-Path $pubDir "logo.png")

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($pubDir, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fwRef = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$icoFile = Join-Path $setupDir "app.ico"
$logoFile = Join-Path $setupDir "logo.png"
$srcFile = Join-Path $setupDir "InstallerProgram.cs"

& $csc /nologo /optimize+ /target:winexe "/out:$outExe" `
    "/win32icon:$icoFile" `
    "/reference:$fwRef\System.IO.Compression.dll" `
    "/reference:$fwRef\System.IO.Compression.FileSystem.dll" `
    "/reference:$fwRef\Microsoft.CSharp.dll" `
    "/resource:$zipPath,payload.zip" `
    "/resource:$logoFile,logo.png" `
    "$srcFile"

if ($LASTEXITCODE -ne 0) { throw "Echec de compilation de ChatP2P_Setup.exe" }

Remove-Item -Recurse -Force $pubDir -ErrorAction SilentlyContinue
Remove-Item -Force $zipPath -ErrorAction SilentlyContinue
Write-Output "Setup genere : $outExe ($((Get-Item $outExe).Length) octets)"
