@echo off
cd /d "%~dp0"
dotnet run --project UuidTool\UuidTool.csproj -c Debug
pause
