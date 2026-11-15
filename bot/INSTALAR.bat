@echo off
setlocal
cd /d "%~dp0"
title JBLEZA - Instalador
echo ==================================================
echo   JBLEZA - Instalador (so precisa rodar 1 vez)
echo ==================================================
echo.
echo Este instalador baixa as pecas que o bot precisa.
echo Precisa de internet e demora alguns minutos.
echo.

set "PY=%LOCALAPPDATA%\Programs\Python\Python312\python.exe"

echo [1/4] Python...
if exist "%PY%" goto py_ok
echo Baixando o Python 3.12...
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing 'https://www.python.org/ftp/python/3.12.10/python-3.12.10-amd64.exe' -OutFile '%TEMP%\python-jbleza.exe'"
if errorlevel 1 goto erro_download
echo Instalando o Python (pode demorar)...
start /wait "" "%TEMP%\python-jbleza.exe" /quiet InstallAllUsers=0 PrependPath=0 Include_launcher=0 Include_test=0 Include_doc=0
if not exist "%PY%" goto erro_python
:py_ok
echo Python pronto.
echo.

echo [2/4] Pecas do bot (discord e youtube)...
if exist "venv\Scripts\python.exe" goto venv_ok
"%PY%" -m venv venv
if errorlevel 1 goto erro_venv
:venv_ok
"venv\Scripts\python.exe" -m pip install --upgrade pip --disable-pip-version-check -q
"venv\Scripts\python.exe" -m pip install --upgrade "discord.py[voice]>=2.7.1" "davey==0.1.7" "yt-dlp[default]" --disable-pip-version-check
if errorlevel 1 goto erro_pip
echo Pecas prontas.
echo.

if not exist "ferramentas" mkdir "ferramentas"

echo [3/4] Deno (ajuda a abrir o YouTube)...
if exist "ferramentas\deno.exe" goto deno_ok
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing 'https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip' -OutFile '%TEMP%\deno-jbleza.zip'"
if errorlevel 1 goto erro_download
powershell -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -Force '%TEMP%\deno-jbleza.zip' 'ferramentas'"
if not exist "ferramentas\deno.exe" goto erro_deno
:deno_ok
echo Deno pronto.
echo.

echo [4/4] FFmpeg (o programa que toca o audio)...
if exist "ferramentas\ffmpeg.exe" goto ffmpeg_ok
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile '%TEMP%\ffmpeg-jbleza.zip'"
if errorlevel 1 goto ffmpeg_reserva
goto ffmpeg_extrair
:ffmpeg_reserva
echo O primeiro site falhou. Tentando o site reserva (arquivo grande)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing 'https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip' -OutFile '%TEMP%\ffmpeg-jbleza.zip'"
if errorlevel 1 goto erro_download
:ffmpeg_extrair
if exist "%TEMP%\ffmpeg-jbleza" rmdir /s /q "%TEMP%\ffmpeg-jbleza"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -Force '%TEMP%\ffmpeg-jbleza.zip' '%TEMP%\ffmpeg-jbleza'; Copy-Item (Get-ChildItem '%TEMP%\ffmpeg-jbleza' -Recurse -Filter ffmpeg.exe | Select-Object -First 1).FullName 'ferramentas\ffmpeg.exe' -Force"
if not exist "ferramentas\ffmpeg.exe" goto erro_ffmpeg
:ffmpeg_ok
echo FFmpeg pronto.
echo.

echo ==================================================
echo   Tudo instalado!
echo   Proximo passo: de duplo clique em INICIAR.bat
echo ==================================================
echo.
pause
exit /b 0

:erro_download
echo.
echo ERRO: nao consegui baixar um arquivo. Confira sua internet e rode o INSTALAR.bat de novo.
echo Se continuar, tire um print desta janela e me mande.
pause
exit /b 1

:erro_python
echo.
echo ERRO: o Python nao foi instalado. Rode o INSTALAR.bat de novo.
echo Se continuar, tire um print desta janela e me mande.
pause
exit /b 1

:erro_venv
echo.
echo ERRO: nao consegui preparar a pasta do bot. Tire um print desta janela e me mande.
pause
exit /b 1

:erro_pip
echo.
echo ERRO: nao consegui instalar as pecas do bot. Tire um print desta janela e me mande.
pause
exit /b 1

:erro_deno
echo.
echo ERRO: o Deno nao foi extraido. Tire um print desta janela e me mande.
pause
exit /b 1

:erro_ffmpeg
echo.
echo ERRO: o FFmpeg nao foi extraido. Tire um print desta janela e me mande.
pause
exit /b 1
