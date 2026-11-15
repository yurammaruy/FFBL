@echo off
cd /d "%~dp0"
title Compilar o FFBL
echo Compilando o FFBL...
echo.
dotnet build -c Release
if errorlevel 1 (
  echo.
  echo ERRO ao compilar. Tire um print desta janela e me mande.
  pause
  exit /b 1
)
echo.
echo ================================================================
echo Pronto! Copie o caminho abaixo e cole no Dalamud em:
echo /xlsettings ^> Experimental ^> Dev Plugin Locations
echo.
echo %~dp0bin\Release\FFBL.dll
echo ================================================================
echo %~dp0bin\Release\FFBL.dll> "%~dp0CAMINHO_DA_DLL.txt"
echo (O caminho tambem foi salvo no arquivo CAMINHO_DA_DLL.txt)
echo|set /p="%~dp0bin\Release\FFBL.dll"| clip
echo (E ja foi copiado: e so colar com Ctrl+V)
echo.
pause
