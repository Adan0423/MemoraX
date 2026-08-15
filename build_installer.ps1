<#
.SYNOPSIS
    Script de automatizacion de compilacion y empaquetado (.exe e portable .zip) para MemoraX.
.DESCRIPTION
    1. Ejecuta dotnet publish en modo Release Self-Contained para win-x64.
    2. Genera el paquete Portable ZIP en 'dist/'.
    3. Detecta o instala Inno Setup via winget y compila el instalador ejecutable '.exe' en 'dist/'.
#>

[CmdletBinding()]
param (
    [switch]$SkipWingetInstall
)

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
Set-Location -Path $ProjectDir

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host " MemoraX - Empaquetador de Produccion (.exe & ZIP)" -ForegroundColor Cyan
Write-Host "========================================================`n" -ForegroundColor Cyan

# 1. Limpieza de carpetas previas
Write-Host "[1/4] Limpiando directorios de salida antiguos..." -ForegroundColor Yellow
$PublishDir = Join-Path $ProjectDir "bin\x64\Release\net9.0-windows10.0.19041.0\win-x64\publish"
$DistDir    = Join-Path $ProjectDir "dist"

if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir | Out-Null }

# 2. Publicar aplicacion con .NET CLI
Write-Host "[2/4] Compilando y publicando MemoraX (Self-Contained win-x64)..." -ForegroundColor Yellow
$dotnetArgs = @(
    "publish",
    "StandbyMemoryManager.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:Platform=x64",
    "-p:PublishSingleFile=false"
)

& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo en la publicacion de .NET."
    exit $LASTEXITCODE
}

# Asegurar archivos .pri requeridos por WinUI 3 en publicaciones unpackaged
$PriSource = Join-Path $ProjectDir "bin\x64\Release\net9.0-windows10.0.19041.0\win-x64\StandbyMemoryManager.pri"
if (Test-Path $PriSource) {
    Copy-Item $PriSource (Join-Path $PublishDir "StandbyMemoryManager.pri") -Force
    Copy-Item $PriSource (Join-Path $PublishDir "resources.pri") -Force
}

Write-Host "      [OK] Publicacion completada con exito." -ForegroundColor Green

# 3. Generar version Portable (.zip)
Write-Host "[3/4] Creando paquete Portable (.zip)..." -ForegroundColor Yellow
$ZipPath = Join-Path $DistDir "MemoraX_v1.0.0_Portable_x64.zip"
if (Test-Path $ZipPath) {
    try { Remove-Item -Force $ZipPath -ErrorAction SilentlyContinue } catch {}
}
if (Test-Path $ZipPath) {
    $ZipPath = Join-Path $DistDir "MemoraX_v1.0.0_Portable_x64_new.zip"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($PublishDir, $ZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$ZipItem = Get-Item $ZipPath
$ZipSizeMB = [math]::Round($ZipItem.Length / 1MB, 2)
Write-Host "      [OK] Paquete portable generado: $ZipPath ($ZipSizeMB MB)" -ForegroundColor Green

# 4. Buscar / Instalar Inno Setup y compilar el Instalador .exe
Write-Host "[4/4] Verificando compilador de Inno Setup (ISCC.exe)..." -ForegroundColor Yellow

function Find-ISCC {
    $found = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($found) { return $found.Path }

    $paths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $paths) {
        if (Test-Path $p) { return $p }
    }
    return $null
}

$IsccPath = Find-ISCC

if (-not $IsccPath -and -not $SkipWingetInstall) {
    Write-Host "      ISCC.exe no encontrado. Intentando instalar Inno Setup via winget..." -ForegroundColor Cyan
    try {
        winget install --id JRSoftware.InnoSetup -e --accept-source-agreements --accept-package-agreements --silent
        Start-Sleep -Seconds 4
        $IsccPath = Find-ISCC
    } catch {
        Write-Warning "No se pudo instalar Inno Setup automaticamente mediante winget."
    }
}

if ($IsccPath) {
    Write-Host "      Compilando instalador .exe con Inno Setup ($IsccPath)..." -ForegroundColor Yellow
    & "$IsccPath" /O"$DistDir" "installer.iss"
    if ($LASTEXITCODE -eq 0) {
        $ExePath = Join-Path $DistDir "MemoraX_Setup_v1.0.0.exe"
        if (Test-Path $ExePath) {
            $ExeItem = Get-Item $ExePath
            $ExeSizeMB = [math]::Round($ExeItem.Length / 1MB, 2)
            Write-Host "      [OK] Instalador .exe generado con exito: $ExePath ($ExeSizeMB MB)" -ForegroundColor Green
        }
    } else {
        Write-Warning "Ocurrio un error al compilar installer.iss con Inno Setup."
    }
} else {
    Write-Warning "Inno Setup 6 no esta instalado. Instala Inno Setup 6 (https://jrsoftware.org/isdl.php) para compilar el instalador .exe."
    Write-Host "Nota: El paquete portable ZIP ya se ha generado correctamente en dist/." -ForegroundColor Cyan
}

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host " Proceso finalizado. Archivos listos en 'dist/':" -ForegroundColor Green
Get-ChildItem -Path $DistDir | Select-Object Name, @{Name="Tamano (MB)"; Expression={[math]::Round($_.Length/1MB, 2)}} | Format-Table -AutoSize
Write-Host "========================================================`n" -ForegroundColor Cyan
