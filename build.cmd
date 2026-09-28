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

set RES=/resource:assets\fonts\Oswald-Bold.ttf,font.Oswald-Bold.ttf
for %%K in (placement bronze silver gold platinum diamond master royalty royalty-one) do call set RES=%%RES%% /resource:assets\ranks\%%K.png,rank.%%K.png

"%CSC%" %COMMON% /target:winexe /out:dist\TottiGol.exe /win32manifest:app.manifest ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll %RES% ^
  src\Core\*.cs src\App\*.cs
if errorlevel 1 exit /b 1

"%CSC%" %COMMON% /target:exe /out:dist\checks.exe src\Core\*.cs tests\*.cs
if errorlevel 1 exit /b 1

"%CSC%" %COMMON% /target:exe /out:dist\IconExtract.exe /r:System.Drawing.dll tools\IconExtract.cs
if errorlevel 1 exit /b 1

echo Built dist\TottiGol.exe, dist\checks.exe and dist\IconExtract.exe
