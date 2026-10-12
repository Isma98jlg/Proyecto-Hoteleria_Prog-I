@echo off
REM =============================================
REM commit-push.bat - Commit y push rápido (CMD)
REM Uso: commit-push.bat "tipo: mensaje" [rama]
REM =============================================

setlocal enabledelayedexpansion

if "%~1"=="" (
    echo.
    echo Uso: commit-push.bat "tipo: mensaje" [rama]
    echo.
    echo Tipos: feat, fix, chore, docs, refactor, test, style, perf, ci, build, revert
    echo Ejemplo: commit-push.bat "feat: agregar filtro habitaciones" bedrooms-browser
    echo.
    exit /b 1
)

set MENSAJE=%~1
set RAMA=%~2
if "%RAMA%"=="" for /f "delims=" %%a in ('git branch --show-current') do set RAMA=%%a

echo =============================================
echo  Commit ^& Push - Hoteleria
echo Rama: %RAMA%
echo Mensaje: %MENSAJE%
echo =============================================
echo.

echo [1/4] Estado del repo:
git status --short
echo.

echo [2/4] Agregando cambios...
git add -A
echo.

echo [3/4] Creando commit...
git commit -m "%MENSAJE%"
if errorlevel 1 (
    echo Error en commit (¿nada para commitear?)
    exit /b 1
)
echo.

echo [4/4] Push a rep-origin/%RAMA%...
git push rep-origin %RAMA%
if errorlevel 1 (
    echo.
    echo Push fallo. ¿Force push? (s/N)
    set /p FORCE=
    if /i "%FORCE%"=="s" (
        git push rep-origin %RAMA% --force-with-lease
        if errorlevel 1 exit /b 1
    ) else (
        exit /b 1
    )
)

echo.
echo =============================================
echo  Listo!
echo =============================================