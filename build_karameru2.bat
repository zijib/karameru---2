@echo off
REM Script de compilation pour Karameru2
REM Place ce fichier à la racine de karameru2

SETLOCAL

where dotnet >nul 2>nul
IF %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Le SDK .NET n'est pas installe ou n'est pas dans le PATH.
    echo Installe-le depuis https://dotnet.microsoft.com/download
    goto :wait
)

echo [INFO] Compilation de Karameru2 en mode Release...
dotnet build Karameru2/Karameru2.csproj -c Release

IF %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] La compilation a echoue.
    goto :wait
)

SET EXE_PATH=
for /d %%V in ("%CD%\Karameru2\bin\Release\net*-windows") do (
    if exist "%%V\Karameru2.exe" (
        SET EXE_PATH=%%V\Karameru2.exe
        goto :found
    )
)
:found

echo [SUCCES] Compilation terminee !
if defined EXE_PATH (
    echo Chemin de l'executable :
    echo "%EXE_PATH%"
    echo.
    echo [INFO] Lancement de Karameru2.exe...
    start "" "%EXE_PATH%"
) else (
    echo [ERREUR] Impossible de trouver l'executable !
)

:wait
echo.
echo Appuie sur une touche pour fermer cette fenetre...
pause >nul
ENDLOCAL 