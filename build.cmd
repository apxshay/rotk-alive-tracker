@echo off
setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo csc.exe from .NET Framework 4.x not found.
  exit /b 1
)

if not exist dist mkdir dist

set COMMON=/nologo /optimize+ /warn:4 /codepage:65001 /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll

"%CSC%" %COMMON% /target:winexe /out:dist\RotkAliveOverlay.exe /win32manifest:app.manifest ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  src\Core\*.cs src\App\*.cs
if errorlevel 1 exit /b 1

"%CSC%" %COMMON% /target:exe /out:dist\checks.exe src\Core\*.cs tests\*.cs
if errorlevel 1 exit /b 1

echo Built dist\RotkAliveOverlay.exe and dist\checks.exe
