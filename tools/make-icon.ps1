<#
.SYNOPSIS
  Regenerate WinTools\Resources\WinTools.ico (the .exe / shortcut icon) from the same
  drawing code the app uses for its tray and window icons (TrayGlyphs.RenderTile).

.EXAMPLE
  powershell -NoProfile -File tools\make-icon.ps1 -Exe bin\Release\net472\WinTools.exe
  Then rebuild so the new icon is embedded.
#>
param(
    [Parameter(Mandatory)] [string]$Exe,
    [string]$Out,
    [ValidateSet('Active', 'Waiting', 'Stopped')] [string]$State = 'Active'
)
$ErrorActionPreference = 'Stop'
if (-not $Out) { $Out = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\WinTools\Resources\WinTools.ico' }
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    # WinTools targets .NET Framework; load it in Windows PowerShell.
    & powershell.exe -NoProfile -File $PSCommandPath -Exe $Exe -Out $Out -State $State
    exit $LASTEXITCODE
}
Add-Type -AssemblyName System.Drawing
$asm = [Reflection.Assembly]::LoadFile((Resolve-Path $Exe).Path)
$glyphs = $asm.GetType('WinTools.TrayGlyphs')
$st = [Enum]::Parse($asm.GetType('WinTools.TrayState'), $State)
$render = $glyphs.GetMethod('RenderTile')

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = foreach ($s in $sizes) {
    $bmp = $render.Invoke($null, @($st, $s))
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , @{ Size = $s; Bytes = $ms.ToArray() }
}

$fs = [IO.File]::Create([IO.Path]::GetFullPath($Out))
$w = New-Object IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$f.Bytes.Length); $w.Write([UInt32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $w.Write($f.Bytes) }
$w.Close()
Write-Host "Wrote $Out"
