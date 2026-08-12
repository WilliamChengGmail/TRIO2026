$deployDir = "D:\_$([char]0x4EA4)$([char]0x4ED8)\Package_20260805\TRIO2026_Deploy"
$projectRoot = "D:\TRIO2026"

Write-Host "Deploying to $deployDir"

if (Test-Path $deployDir) {
    Remove-Item -Path $deployDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $deployDir | Out-Null

Write-Host "Publishing TRIO2026.App..."
dotnet publish "$projectRoot\src\TRIO2026.App\TRIO2026.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$deployDir\App" > $null
if ($LASTEXITCODE -ne 0) { Write-Host "App Deploy Failed!"; exit 1 }

Write-Host "Publishing PrivilegedService..."
if (Test-Path "$projectRoot\src\TRIO2026.PrivilegedService\TRIO2026.PrivilegedService.csproj") {
    dotnet publish "$projectRoot\src\TRIO2026.PrivilegedService\TRIO2026.PrivilegedService.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$deployDir\Service" > $null
}

Write-Host "Copying Database..."
New-Item -ItemType Directory -Force -Path "$deployDir\App\Database" | Out-Null
Copy-Item -Path "$projectRoot\Database\*.db" -Destination "$deployDir\App\Database\" -Force

Write-Host "Publishing Tools..."
$toolsDir = "$deployDir\Tools"
New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null

$toolProjects = Get-ChildItem -Path "$projectRoot\Tools" -Recurse -Filter *.csproj
foreach ($proj in $toolProjects) {
    $toolName = $proj.BaseName
    if ($proj.FullName -match "\\temp\\") { continue }
    Write-Host "Publishing $toolName ..."
    dotnet publish $proj.FullName -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$toolsDir\${toolName}_bin" > $null
}

Write-Host "Copying bat scripts..."
$batFiles = Get-ChildItem -Path "$projectRoot\Tools\*.bat"
foreach ($bat in $batFiles) {
    $content = Get-Content $bat.FullName -Raw
    $content = $content -replace 'dotnet run --project\s+"?%~dp0([A-Za-z0-9_]+)"?(\s+--)?', '"%~dp0$1_bin\$1.exe"'
    $content = $content -replace 'dotnet run -c Release --project\s+([A-Za-z0-9_]+)\\[A-Za-z0-9_]+\.csproj', '"%~dp0$1_bin\$1.exe"'
    Set-Content -Path "$toolsDir\$($bat.Name)" -Value $content -Encoding UTF8
}

$startBatPath = "$deployDir\RunApp.bat"
$startContent = "@echo off`r`ncd /d `"%~dp0`"`r`nstart `"`" `"%~dp0App\TRIO2026.App.exe`""
Set-Content -Path $startBatPath -Value $startContent -Encoding UTF8

Write-Host "Deploy complete!"
