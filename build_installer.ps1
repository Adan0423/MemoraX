<#
.SYNOPSIS
    Publica Veltrixa para Windows x64 y crea un ZIP portable y, si existe ISCC, un instalador.
.DESCRIPTION
    Cada ejecucion utiliza una carpeta nueva en artifacts/publish. Conserva los paquetes
    existentes en dist y no instala ni modifica herramientas del equipo.
#>

[CmdletBinding()]
param (
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ProjectDir = [IO.Path]::GetFullPath($PSScriptRoot)
$ProjectFile = Join-Path $ProjectDir 'Veltrixa.csproj'
$DistDir = Join-Path $ProjectDir 'dist'
$RunId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$PublishDir = Join-Path $ProjectDir "artifacts\publish\Veltrixa-$RunId"

function Find-ISCC {
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    return $null
}

function Get-UnusedArtifactPath {
    param([string]$BaseName, [string]$Extension)

    $candidate = Join-Path $DistDir "$BaseName$Extension"
    if (-not (Test-Path -LiteralPath $candidate)) { return $candidate }

    $candidate = Join-Path $DistDir "$BaseName-$RunId$Extension"
    $index = 1
    while (Test-Path -LiteralPath $candidate) {
        $candidate = Join-Path $DistDir "$BaseName-$RunId-$index$Extension"
        $index++
    }
    return $candidate
}

if (-not (Get-Command 'dotnet' -ErrorAction SilentlyContinue)) {
    throw 'No se encontro dotnet. Instala el SDK de .NET 9 para compilar Veltrixa.'
}

[xml]$ProjectXml = Get-Content -LiteralPath $ProjectFile -Raw
$AppVersion = [string]$ProjectXml.Project.PropertyGroup.Version
if ($AppVersion -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "La version '$AppVersion' del proyecto no es valida para el empaquetado."
}

New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

Write-Host 'Veltrixa: publicando Release autocontenido para Windows x64...' -ForegroundColor Cyan
$dotnetArgs = @(
    'publish', $ProjectFile,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:Platform=x64',
    '-p:PublishSingleFile=false',
    '--output', $PublishDir
)
& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish fallo con codigo $LASTEXITCODE. No se generaron paquetes."
}

foreach ($requiredFile in @('Veltrixa.exe', 'Veltrixa.dll', 'Veltrixa.pri', 'resources.pri')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PublishDir $requiredFile) -PathType Leaf)) {
        throw "La publicacion esta incompleta: falta $requiredFile en $PublishDir."
    }
}

$ZipPath = Get-UnusedArtifactPath -BaseName "Veltrixa_v${AppVersion}_Portable_x64" -Extension '.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory(
    $PublishDir, $ZipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host "Portable: $ZipPath" -ForegroundColor Green

if (-not $SkipInstaller) {
    $IsccPath = Find-ISCC
    if ($IsccPath) {
        $SetupPath = Get-UnusedArtifactPath -BaseName "Veltrixa_Setup_v${AppVersion}_x64" -Extension '.exe'
        $SetupBaseName = [IO.Path]::GetFileNameWithoutExtension($SetupPath)
        $isccArgs = @(
            "/DMyAppVersion=$AppVersion",
            "/DPublishDir=$PublishDir",
            "/O$DistDir",
            "/F$SetupBaseName",
            (Join-Path $ProjectDir 'installer.iss')
        )
        & $IsccPath @isccArgs
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $SetupPath -PathType Leaf)) {
            throw "No se pudo crear el instalador. El paquete portable sigue disponible: $ZipPath"
        }
        Write-Host "Instalador: $SetupPath" -ForegroundColor Green
    }
    else {
        Write-Warning 'Inno Setup 6 (ISCC.exe) no esta instalado. Se genero el portable; el instalador es opcional. Instala Inno Setup 6 y vuelve a ejecutar el script para generarlo.'
    }
}

Write-Host "Carpeta de publicacion: $PublishDir"
