@echo off
chcp 65001 >nul
echo ==================================================
echo   TRIO2026 PrivilegedService IPC Test Tool
echo   No admin rights needed (simulates App side)
echo ==================================================
echo.

dotnet run --project "%~dp0ServiceTestTool\ServiceTestTool.csproj"
pause
