@echo off
rem Bir klasoru bu dosyanin uzerine surukleyip birakin.
chcp 65001 >nul
if "%~1"=="" (
  echo Fotograf klasorunu bu dosyanin uzerine surukleyip birakin.
  pause
  exit /b 2
)
"%~dp0eokul-cli.exe" %* --var-olan uzerine-yaz --karsilastirma "%~dp0karsilastirma"
echo.
echo Ciktilar: her klasorun icindeki E_Okul
echo Karsilastirma gorselleri: "%~dp0karsilastirma"
echo Not: karsilastirma gorsellerinde ogrenci yuzleri vardir; isiniz bitince silin.
pause
