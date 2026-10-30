@echo off
chcp 65001 > nul
title Compilando Lynow Mic Android APK...
echo =======================================================
echo          COMPILANDO LYNOW MIC ANDROID APK
echo =======================================================
echo.

set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%Codigo-fonte\android"

echo Compilando APK via Gradle...
call gradlew.bat --no-daemon :app:assembleDebug

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERRO] A compilacao falhou. Verifique se o JDK 17 e o Android SDK estao configurados.
    pause
    exit /b 1
)

copy /y "app\build\outputs\apk\debug\app-debug.apk" "%SCRIPT_DIR%Android\LynowMic-android-alpha.apk" >nul

echo.
echo [SUCESSO] APK atualizado com sucesso em:
echo %SCRIPT_DIR%Android\LynowMic-android-alpha.apk
echo =======================================================
pause
