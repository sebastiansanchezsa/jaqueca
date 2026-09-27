@echo off
rem Compila en Release y abre el juego.
cd /d "%~dp0"
dotnet run --project src\Jaqueca.Client -c Release -- %*
