@echo off
chcp 65001 > nul
title Compilando Lynow Mic para Windows...
echo =======================================================
echo          COMPILANDO LYNOW MIC WINDOWS (v0.1)
echo =======================================================
echo.

where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [ERRO] O .NET SDK 8 nao foi encontrado no sistema.
    echo Baixe e instale gratuitamente em: https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

set SCRIPT_DIR=%~dp0
set PROJ_DIR=%SCRIPT_DIR%windows\LynowMic.Windows
set OUT_DIR=%SCRIPT_DIR%..\Windows

echo [1/2] Compilando executavel nativo independente com novo design...
dotnet publish "%PROJ_DIR%\LynowMic.Windows.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o "%OUT_DIR%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERRO] Falha na compilacao. Verifique se o LynowMic.exe antigo esta aberto e feche-o.
    pause
    exit /b 1
)

echo.
echo [2/2] SUCESSO! Executavel atualizado com o novo design em:
echo %OUT_DIR%\LynowMic.exe
echo.
echo Pode abrir o LynowMic.exe agora para ver a interface exatamente igual a imagem!
echo =======================================================
pause
