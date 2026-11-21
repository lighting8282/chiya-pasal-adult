# Builds dist\ChiyaPasal.exe: the game, embedded in a small Windows launcher (see desktop\Program.cs).
# Run from anywhere:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\build_exe.ps1
# A git post-commit hook runs this after every commit, so the .exe follows the repo.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null

# every file the game serves, embedded under its web path
$files = @('index.html', 'manifest.webmanifest', 'sw.js', 'icons\icon-192.png', 'icons\icon-512.png', 'icons\apple-touch-icon.png')
$cscArgs = @('/nologo', '/target:winexe', '/optimize+', "/out:$dist\ChiyaPasal.new.exe",
             "/win32icon:$root\desktop\app.ico", '/r:System.Windows.Forms.dll')
foreach ($f in $files) { $cscArgs += ('/resource:' + (Join-Path $root $f) + ',' + ($f -replace '\\', '/')) }
$cscArgs += (Join-Path $root 'desktop\Program.cs')

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw 'The C# compiler reported errors.' }
try {
    Move-Item "$dist\ChiyaPasal.new.exe" "$dist\ChiyaPasal.exe" -Force
    Write-Host "Built $dist\ChiyaPasal.exe"
} catch {
    Write-Warning 'ChiyaPasal.exe is open, so it was not replaced. Close it and run this again (the new build is dist\ChiyaPasal.new.exe).'
}
