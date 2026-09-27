#!/bin/bash
# Prepara una máquina Linux de la nube para compilar Jaqueca (con los shaders) y sacar capturas:
# .NET 8, Wine con el compilador de shaders de Microsoft (el MGFXC de MonoGame lo pide en Linux) y Xvfb.
# Uso: bash tools/nube/preparar.sh ; después, antes de compilar:
#   export PATH=/opt/mgfxwine/bin:$PATH MGFXC_WINE_PATH=/opt/mgfxwine/prefix
# El MGFXC corre "wine64 dotnet c:\fxccs.dll ..."; el wine64 falso de acá (tools/nube/wine64) compila en
# cambio con fxc2.exe (un programita que llama a D3DCompile del D3DCompiler_47 que viene en el paquete
# de WPF de NuGet) bajo el wine de verdad. No hace falta instalar .NET de Windows adentro de Wine.
set -e
AQUI="$(cd "$(dirname "$0")" && pwd)"
export DEBIAN_FRONTEND=noninteractive
command -v dotnet >/dev/null || { apt-get update -qq; apt-get install -y -qq dotnet-sdk-8.0; }
[ -x /usr/lib/wine/wine64 ] || { apt-get update -qq; apt-get install -y -qq --no-install-recommends wine64 gcc-mingw-w64-x86-64; }
command -v x86_64-w64-mingw32-gcc >/dev/null || apt-get install -y -qq --no-install-recommends gcc-mingw-w64-x86-64
command -v xvfb-run >/dev/null || apt-get install -y -qq xvfb
mkdir -p /opt/mgfxwine/bin
if [ ! -f /opt/mgfxwine/D3DCompiler_47_cor3.dll ]; then
  T=$(mktemp -d)
  curl -sSL -o "$T/wd.nupkg" https://api.nuget.org/v3-flatcontainer/microsoft.windowsdesktop.app.runtime.win-x64/8.0.8/microsoft.windowsdesktop.app.runtime.win-x64.8.0.8.nupkg
  unzip -o -j -q "$T/wd.nupkg" runtimes/win-x64/native/D3DCompiler_47_cor3.dll -d /opt/mgfxwine/
  rm -rf "$T"
fi
x86_64-w64-mingw32-gcc -O2 -o /opt/mgfxwine/fxc2.exe "$AQUI/fxc2.c"
cp "$AQUI/wine64" "$AQUI/winepath" /opt/mgfxwine/bin/ && chmod +x /opt/mgfxwine/bin/*
[ -d /opt/mgfxwine/prefix ] || WINEPREFIX=/opt/mgfxwine/prefix WINEDEBUG=-all WINEDLLOVERRIDES="mscoree,mshtml=" /usr/lib/wine/wine64 wineboot -i >/dev/null 2>&1 || true
echo "Listo. Antes de compilar: export PATH=/opt/mgfxwine/bin:\$PATH MGFXC_WINE_PATH=/opt/mgfxwine/prefix"
