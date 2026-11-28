@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo No se encontro el SDK de .NET. Descargalo de https://dotnet.microsoft.com/download
  exit /b 1
)

dotnet build src\Turbix\Turbix.csproj -c Release -nologo
if errorlevel 1 exit /b 1

if not exist dist mkdir dist
copy /y src\Turbix\bin\Release\Turbix.exe dist\TURBiX.exe >nul
if errorlevel 1 goto copyfail
fc /b src\Turbix\bin\Release\Turbix.exe dist\TURBiX.exe >nul
if errorlevel 1 goto copyfail

rem Instalador: el mismo programa con el Turbix.exe recien compilado dentro (ver Core\Installer.cs).
dotnet build src\Turbix\Turbix.csproj -c Release -nologo -p:TurbixSetup=true
if errorlevel 1 exit /b 1
copy /y src\Turbix\bin\Setup\Turbix.exe dist\TURBiX-Setup.exe >nul
if errorlevel 1 goto copyfail
fc /b src\Turbix\bin\Setup\Turbix.exe dist\TURBiX-Setup.exe >nul
if errorlevel 1 goto copyfail

echo.
echo Listo: dist\TURBiX.exe y dist\TURBiX-Setup.exe
exit /b 0

:copyfail
echo.
echo ERROR: no se pudo copiar el resultado a dist (archivo en uso o permisos). Cierra TURBiX y vuelve a compilar.
exit /b 1
