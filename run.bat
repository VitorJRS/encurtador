@echo off
chcp 65001 > nul
title Encurtador de Links & QR Code

echo ========================================================
echo   Encurtador de Links & Gerador de QR Code (C# / .NET 8)
echo   100%% Gratuito - Permanente - Sem Limites
echo ========================================================
echo.

set DOTNET_EXE=dotnet
where dotnet >nul 2>&1
if errorlevel 1 (
    if exist "%LOCALAPPDATA%\dotnet\dotnet.exe" (
        set "DOTNET_EXE=%LOCALAPPDATA%\dotnet\dotnet.exe"
        set "DOTNET_ROOT=%LOCALAPPDATA%\dotnet"
    ) else (
        echo [ERRO] .NET SDK não encontrado!
        pause
        exit /b 1
    )
)

echo [OK] Iniciando o servidor local...
echo [INFO] Abrindo o navegador em: http://localhost:5000
echo.
start "" http://localhost:5000

"%DOTNET_EXE%" run --urls "http://0.0.0.0:5000"
pause
