@echo off
chcp 65001 >nul
setlocal EnableExtensions
cd /d "%~dp0"

set "SRC=radiostations.txt"
set "OUT=radiostations.checked.txt"
if not "%~1"=="" set "SRC=%~1"
if not "%~2"=="" set "OUT=%~2"

if not exist "%SRC%" (
  echo ERROR: %SRC% not found
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0checkstations.ps1" "%SRC%" "%OUT%"
endlocal