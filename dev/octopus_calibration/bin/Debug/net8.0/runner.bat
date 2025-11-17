@echo off
setlocal enabledelayedexpansion

REM ====== CONFIGURAZIONE ======
set "RUNNER=octoPusAI_calibration.exe"
set "TEMPLATE=octoPus.json"
set "MODELS=Rule310 Magarey EPI IPI DMCast UCSC Misfits Laore"

REM Controlli minimi
if not exist "%RUNNER%" (
  echo ERRORE: non trovo "%RUNNER%" nella cartella: %CD%
  exit /b 1
)
if not exist "%TEMPLATE%" (
  echo ERRORE: non trovo il template "%TEMPLATE%" nella cartella: %CD%
  exit /b 2
)

for %%M in (%MODELS%) do (
  set "CONFIG=octoPus_%%M.json"
  echo Creazione "!CONFIG!" per modello %%M

  REM Clona il template e imposta un solo modello
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$j = Get-Content '%TEMPLATE%' -Raw | ConvertFrom-Json; " ^
    "$j.settings.modelsToRun = @('%%M'); " ^
    "$j | ConvertTo-Json -Depth 64 | Set-Content -Path '%CD%\!CONFIG!' -Encoding UTF8; " ^
    "Write-Output ('OK: ' + '%CD%\!CONFIG!' + ' creato')"

  REM Avvia il runner passando il nome file come argomento
  echo Avvio: %RUNNER% "!CONFIG!"
  start "octopus-%%M" "%RUNNER%" "!CONFIG!"

  REM Piccolo delay per evitare burst
  timeout /t 6 >nul
)

echo Tutti i job sono stati inviati.
pause
endlocal
