@echo off
setlocal
cd /d "%~dp0"
set "PATH=%~dp0ferramentas;%PATH%"
set PYTHONUTF8=1

if exist "venv\Scripts\pythonw.exe" goto instalado
echo O bot ainda nao foi instalado. Rode o INSTALAR.bat primeiro.
pause
exit /b 1

:instalado
echo Verificando atualizacoes... (so um instante)
"venv\Scripts\python.exe" -m pip install --upgrade "yt-dlp[default]" -q --disable-pip-version-check
"venv\Scripts\python.exe" -m pip install "davey==0.1.7" pystray pillow -q --disable-pip-version-check
start "" "venv\Scripts\pythonw.exe" "FFBL_Painel.pyw"
exit /b 0
