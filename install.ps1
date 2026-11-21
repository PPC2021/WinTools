<#
.SYNOPSIS
  Build and install WinTools for the current user (no admin rights needed).

.EXAMPLE
  .\install.ps1                  # install to %LOCALAPPDATA%\Programs\WinTools, start at sign-in, launch in the tray
  .\install.ps1 -NoStartup       # install without starting at sign-in
  .\install.ps1 -Portable        # also drop a standalone WinTools.exe on the Desktop
  .\install.ps1 -PortableOnly    # only build the standalone exe to the Desktop
  .\install.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [switch]$NoStartup,
    [switch]$Portable,
    [switch]$PortableOnly,
    [switch]$Uninstall,
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\WinTools'),
    [string]$PortableDir = [Environment]::GetFolderPath('Desktop')
)
$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot
$exe = Join-Path $InstallDir 'WinTools.exe'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'WinTools.lnk'

function Set-LoginItem([string]$state) {
    if (Test-Path $exe) { Start-Process $exe -ArgumentList '--login-item', $state -Wait -WindowStyle Hidden }
}

function Stop-WinTools {
    Get-Process WinTools -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

if ($Uninstall) {
    Set-LoginItem off
    Stop-WinTools
    Remove-Item $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $shortcut -Force -ErrorAction SilentlyContinue
    Write-Host "Removed $InstallDir, the Start menu shortcut and the sign-in entry."
    Write-Host "Settings are kept in $env:APPDATA\WinTools (delete that folder to reset them)."
    return
}

$build = Join-Path ([IO.Path]::GetTempPath()) ('WinTools-build-' + [guid]::NewGuid().ToString('N'))
dotnet build (Join-Path $repo 'WinToolsClassic.csproj') -c Release -o $build --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

try {
    if ($Portable -or $PortableOnly) {
        $dest = Join-Path $PortableDir 'WinTools.exe'
        Get-Process WinTools -ErrorAction SilentlyContinue | Where-Object Path -eq $dest | Stop-Process -Force
        Copy-Item (Join-Path $build 'WinTools.exe') $dest -Force
        Write-Host "Portable exe: $dest"
    }
    if ($PortableOnly) { return }

    Stop-WinTools
    New-Item -ItemType Directory -Force $InstallDir | Out-Null
    Copy-Item (Join-Path $build 'WinTools.exe'), (Join-Path $build 'WinTools.exe.config') $InstallDir -Force

    $ws = New-Object -ComObject WScript.Shell
    $lnk = $ws.CreateShortcut($shortcut)
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = $InstallDir
    $lnk.Description = 'WinTools - keep the PC awake during working hours'
    $lnk.Save()

    if ($NoStartup) { Set-LoginItem off } else { Set-LoginItem on }
    Write-Host "Installed: $exe"
    Write-Host ("Start at sign-in: " + $(if ($NoStartup) { 'off' } else { 'on (runs in the system tray)' }))
    Start-Process $exe -ArgumentList '--tray'
    Write-Host 'WinTools is running in the system tray.'
}
finally {
    Remove-Item $build -Recurse -Force -ErrorAction SilentlyContinue
}
