@echo off
echo Test de localisation Karameru2
echo =============================
echo.
echo Compilation du projet...
dotnet build --configuration Release
if %ERRORLEVEL% NEQ 0 (
    echo ERREUR: La compilation a echoue
    pause
    exit /b 1
)
echo.
echo Compilation reussie !
echo.
echo Pour tester les langues :
echo 1. Lancez l'application avec : dotnet run
echo 2. Allez dans le menu "Langue" 
echo 3. Testez chaque langue disponible
echo.
echo Langues disponibles :
echo - Francais (fr)
echo - English (en) 
echo - Espanol (es)
echo - Italiano (it)
echo - Nihongo (ja)
echo - Hangul (ko)
echo - Zhongwen (zh)
echo.
pause 