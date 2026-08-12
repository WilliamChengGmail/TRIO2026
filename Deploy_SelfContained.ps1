param(
    [string]$deployDir = "F:\TRIO2026_Deploy"
)
$projectRoot = "D:\TRIO2026"

Write-Host "=========================================="
Write-Host "  TRIO2026 準備獨立部署環境 (含 Tools)    "
Write-Host "  目標資料夾: $deployDir"
Write-Host "=========================================="

if (Test-Path $deployDir) {
    Write-Host "[1/6] 清除舊目錄..."
    Remove-Item -Path $deployDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $deployDir | Out-Null

Write-Host "[2/6] 發佈 TRIO2026.App (Self-Contained)..."
dotnet publish "$projectRoot\src\TRIO2026.App\TRIO2026.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$deployDir\App" > $null
if ($LASTEXITCODE -ne 0) { Write-Host "App 發佈失敗!"; exit 1 }

Write-Host "[3/6] 發佈 PrivilegedService..."
if (Test-Path "$projectRoot\src\TRIO2026.PrivilegedService\TRIO2026.PrivilegedService.csproj") {
    dotnet publish "$projectRoot\src\TRIO2026.PrivilegedService\TRIO2026.PrivilegedService.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$deployDir\Service" > $null
} else {
    Write-Host "  未找到 PrivilegedService，跳過。"
}

Write-Host "[4/6] 複製資料庫..."
New-Item -ItemType Directory -Force -Path "$deployDir\App\Database" | Out-Null
Copy-Item -Path "$projectRoot\Database\*.db" -Destination "$deployDir\App\Database\" -Force

Write-Host "[5/6] 發佈所有 Tools (Self-Contained)..."
$toolsDir = "$deployDir\Tools"
New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null

$toolProjects = Get-ChildItem -Path "$projectRoot\Tools" -Recurse -Filter *.csproj
foreach ($proj in $toolProjects) {
    $toolName = $proj.BaseName
    if ($proj.FullName -match "\\temp\\") { continue }
    Write-Host "  - 發佈 $toolName ..."
    dotnet publish $proj.FullName -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$toolsDir\${toolName}_bin" > $null
}

Write-Host "[6/6] 轉換並複製 .bat 執行腳本..."
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

Write-Host "=========================================="
Write-Host "  部署準備完成！"
Write-Host "=========================================="
