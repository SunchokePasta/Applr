@echo off
cd /d "%~dp0"

echo.
echo ============================================================
echo   Applr  to  Applr    (launcher)
echo ============================================================
echo.

if not exist "%~dp0rename-to-applr.ps1" (
    echo ERROR: rename-to-applr.ps1 was not found next to this file.
    echo.
    pause
    exit /b 1
)

set PS=powershell.exe
where pwsh.exe >nul 2>&1 && set PS=pwsh.exe

echo Using %PS%
%PS% -NoProfile -Command "'PowerShell ' + $PSVersionTable.PSVersion.ToString()"
%PS% -NoProfile -ExecutionPolicy Bypass -Command "try { Unblock-File -LiteralPath '%~dp0rename-to-applr.ps1' -ErrorAction SilentlyContinue } catch {}"
echo.

echo ------------------------------------------------------------
echo  STEP 1 of 2 - PREVIEW (nothing will be changed)
echo ------------------------------------------------------------
echo.

%PS% -NoProfile -ExecutionPolicy Bypass -File "%~dp0rename-to-applr.ps1"

if errorlevel 1 goto previewfailed

echo ------------------------------------------------------------
echo  STEP 2 of 2 - APPLY
echo ------------------------------------------------------------
echo.
echo  The list above is exactly what will change.
echo.

set GO=n
set /p GO=Apply these changes now? (y/N):

if /i not "%GO%"=="y" goto cancelled

echo.
%PS% -NoProfile -ExecutionPolicy Bypass -File "%~dp0rename-to-applr.ps1" -Apply

if errorlevel 1 goto applyfailed

echo.
echo ------------------------------------------------------------
echo  Finished. See the next steps printed above.
echo ------------------------------------------------------------
echo.
pause
exit /b 0

:previewfailed
echo.
echo ------------------------------------------------------------
echo  The preview did not complete. Nothing has been changed.
echo  Copy the text above and send it back.
echo ------------------------------------------------------------
echo.
pause
exit /b 1

:applyfailed
echo.
echo ------------------------------------------------------------
echo  The rename did not complete cleanly.
echo  This script is safe to run again - it resumes where it
echo  stopped. Copy the text above and send it back.
echo ------------------------------------------------------------
echo.
pause
exit /b 1

:cancelled
echo.
echo Cancelled. Nothing was changed.
echo.
pause
exit /b 0
