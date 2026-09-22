@echo off
setlocal

rem Entfernt alle von "dotnet build"/"dotnet publish" 
rem erzeugten Ausgabe-/Zwischendateien aus der Community Edition.

cd /d "%~dp0"

for %%D in (bin obj publish) do (
    if exist "%%D" (
        echo Entferne "%%D" ...
        rmdir /s /q "%%D"
    )
)

echo Aufgeraeumt.
exit /b 0
