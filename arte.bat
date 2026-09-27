@echo off
rem Genera las hojas de revisión de los modelos (screenshots\vecino.png, maestra.png, armas.png).
cd /d "%~dp0"
dotnet run --project src\Jaqueca.ArtGen -c Release -- %*
