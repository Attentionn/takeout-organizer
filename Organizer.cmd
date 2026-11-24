@echo off
setlocal
if exist "%~dp0Organizer.exe" goto run
call "%~dp0Build.cmd"
if errorlevel 1 goto done
:run
"%~dp0Organizer.exe" %*
:done
set "result=%errorlevel%"
if not "%result%"=="0" if "%~1"=="" pause
exit /b %result%
