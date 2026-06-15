@echo off
chcp 65001 >nul
title TRIO2026 部署打包工具

:: ====================================================
:: TRIO2026 部署打包工具
:: 用途：將 App + Service + Database 打包到隨身碟
:: 製作者: Office of William
:: ====================================================

:: 偵測隨身碟
set USB_DRIVE=
for /f "tokens=1" %%d in ('wmic logicaldisk where "DriveType=2" get DeviceID /value 2^>nul ^| findstr "="') do (
    for /f "tokens=2 delims==" %%v in ("%%d") do set USB_DRIVE=%%v
)

if "%USB_DRIVE%"=="" (
    echo [錯誤] 未偵測到隨身碟，請插入後重試
    pause
    exit /b 1
)

echo ============================================
echo   TRIO2026 部署打包
echo   目標: %USB_DRIVE%\TRIO2026_Deploy
echo ============================================
echo.

set DEPLOY_DIR=%USB_DRIVE%\TRIO2026_Deploy
set PROJECT_ROOT=%~dp0..

:: 清除舊部署
if exist "%DEPLOY_DIR%" (
    echo [1/5] 清除舊部署...
    rmdir /s /q "%DEPLOY_DIR%"
)

:: 發布 App
echo [2/5] 發布 TRIO2026.App (Release)...
dotnet publish "%PROJECT_ROOT%\src\TRIO2026.App\TRIO2026.App.csproj" -c Release -r win-x64 --self-contained true -o "%DEPLOY_DIR%\App" >nul 2>&1
if errorlevel 1 (
    echo [錯誤] App 發布失敗
    pause
    exit /b 1
)
echo       完成

:: 發布 PrivilegedService
echo [3/5] 發布 PrivilegedService (Release)...
dotnet publish "%PROJECT_ROOT%\src\TRIO2026.PrivilegedService\TRIO2026.PrivilegedService.csproj" -c Release -r win-x64 --self-contained true -o "%DEPLOY_DIR%\Service" >nul 2>&1
if errorlevel 1 (
    echo [錯誤] Service 發布失敗
    pause
    exit /b 1
)
echo       完成

:: 複製資料庫
echo [4/5] 複製資料庫...
mkdir "%DEPLOY_DIR%\App\Database" 2>nul
copy /y "%PROJECT_ROOT%\Database\*.db" "%DEPLOY_DIR%\App\Database\" >nul
echo       完成

:: 清理非 Windows runtimes
echo [5/5] 清理非 Windows 平台檔案...
for /d %%d in ("%DEPLOY_DIR%\App\runtimes\*") do (
    echo %%~nxd | findstr /i "^win" >nul || rmdir /s /q "%%d"
)
echo       完成

:: 計算大小
set /a TOTAL=0
for /r "%DEPLOY_DIR%" %%f in (*) do set /a TOTAL+=%%~zf 2>nul

echo.
echo ============================================
echo   部署完成！
echo   路徑: %DEPLOY_DIR%
echo   啟動: %DEPLOY_DIR%\App\TRIO2026.App.exe
echo ============================================
echo.
pause
