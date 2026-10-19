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
set PROJ_DIR=%SCRIPT_DIR%Codigo-fonte\windows\LynowMic.Windows
set OUT_DIR=%SCRIPT_DIR%Windows

echo [1/2] Compilando executavel com o novo design da imagem...
dotnet publish "%PROJ_DIR%\LynowMic.Windows.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o "%OUT_DIR%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [AVISO] Se o LynowMic.exe estiver aberto na sua tela, FECHE-O e tente novamente para permitir a substituicao do arquivo.
    pause
    exit /b 1
)

echo.
echo [2/2] SUCESSO! O executavel foi recompilado com o novo design:
echo %OUT_DIR%\LynowMic.exe
echo.
echo Abra o LynowMic.exe na pasta Windows para conferir a nova interface!
echo =======================================================
pause
