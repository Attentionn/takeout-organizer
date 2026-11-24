@echo off
setlocal
set "compiler=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%compiler%" set "compiler=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%compiler%" /nologo /optimize+ /codepage:65001 /r:System.Web.Extensions.dll /r:Microsoft.VisualBasic.dll /out:"%~dp0Organizer.exe" "%~dp0src\Organizador.cs" "%~dp0src\Fechas.cs" "%~dp0src\Program.cs"
exit /b %errorlevel%
