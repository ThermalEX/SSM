param(
    [string]$Version = "1.0.0"
)

$proj    = "src\SSM\SSM.csproj"
$publish = "build\publish"
$out     = "build\installer"

# 1. publish
Write-Host "Publishing..."
dotnet publish $proj -c Release -r win-x64 --self-contained -o $publish
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed"; exit 1 }

# 2. portable zip
Write-Host "Creating portable zip..."
New-Item -ItemType Directory -Force -Path $out | Out-Null
$zipPath = "$out\SSM-Portable-$Version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$publish\*" -DestinationPath $zipPath
Write-Host "  -> $zipPath"

# 3. installer (requires Inno Setup)
$iscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "F:\software\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    Write-Host "Building installer..."
    & $iscc /DAppVersion=$Version installer.iss
    Write-Host "  -> $out\SSM-Setup-$Version.exe"
} else {
    Write-Host "Inno Setup not found, skipping installer."
}

Write-Host "`nDone."
