@echo off
chcp 65001 >nul
echo ==================================================
echo   TRIO2026 PrivilegedService (Console Mode)
echo   ** Run this as ADMINISTRATOR **
echo ==================================================
echo.

:: Check admin
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Please run as Administrator!
    echo         Right-click - Run as administrator
    pause
    exit /b 1
)

echo [OK] Running as Administrator
echo [INFO] Starting PrivilegedService...
echo [INFO] Named Pipe: TRIO2026_PrivilegedService
echo [INFO] Press Ctrl+C to stop
echo.

dotnet run --project "%~dp0..\src\TRIO2026.PrivilegedService\TRIO2026.PrivilegedService.csproj"
pause
