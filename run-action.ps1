if($IsWindows -eq $True)
{
    $filePath = Join-Path $PSScriptRoot "/dist/win-x64/HugoChecker.exe"
}

if($IsLinux -eq $True)
{
    $filePath = Join-Path $PSScriptRoot "/dist/linux-x64/HugoChecker"
    chmod +x $filePath
}

if($IsMacOS -eq $True)
{
    $filePath = Join-Path $PSScriptRoot "/dist/osx-x64/HugoChecker"
    chmod +x $filePath
}

Write-Output "Running $filePath..."
& $filePath

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}