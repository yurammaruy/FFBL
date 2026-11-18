@echo off
setlocal
cd /d "%~dp0"
title JBLEZA - Bot de musica (modo console)
set "PATH=%~dp0ferramentas;%PATH%"
set PYTHONUTF8=1

if exist "venv\Scripts\python.exe" goto instalado
echo O JBLEZA ainda nao foi instalado. Rode o INSTALAR.bat primeiro.
pause
exit /b 1

:instalado
echo Verificando atualizacao do leitor do YouTube...
"venv\Scripts\python.exe" -m pip install --upgrade "yt-dlp[default]" -q --disable-pip-version-check
"venv\Scripts\python.exe" -m pip install "davey==0.1.7" -q --disable-pip-version-check
echo.
echo Iniciando o JBLEZA...
"venv\Scripts\python.exe" bot.py
echo.
echo O bot foi encerrado. Se foi sem querer, de duplo clique em INICIAR.bat de novo.
pause
