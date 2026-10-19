$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location "$root/android"
try {
    & .\gradlew.bat --no-daemon :app:assembleDebug :app:testDebugUnitTest
    if ($LASTEXITCODE -ne 0) { throw 'A compilação falhou. Confira JDK 17 e Android SDK no Android Studio.' }
    New-Item -ItemType Directory -Force "$root/releases/android" | Out-Null
    Copy-Item 'app/build/outputs/apk/debug/app-debug.apk' "$root/releases/android/LynowMic-android-alpha.apk" -Force
    Write-Host "Pronto: $root/releases/android/LynowMic-android-alpha.apk"
} finally { Pop-Location }
