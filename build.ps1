# 无为截 build script — compiles src\Snip.cs into Snip.exe using the C# compiler bundled with Windows.
# Usage:  powershell -ExecutionPolicy Bypass -File build.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root "src\Snip.cs"
$out  = Join-Path $root "Snip.exe"

# Locate csc.exe (prefer 64-bit v4)
$candidates = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw "csc.exe not found. Requires the .NET Framework 4.x (ships with Windows 8+)." }

Write-Host "Compiling with $csc ..."
& $csc /nologo /target:winexe /out:$out `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $src

if ($?) {
    Write-Host "Build OK ->" $out
    Get-Item $out | Select-Object Name, Length
} else {
    throw "Build failed."
}
