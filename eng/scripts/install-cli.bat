@echo off
setlocal

set ROOT=%~dp0..\..
set PROJECT=%ROOT%\src\SurfaceQ.Cli\SurfaceQ.Cli.csproj
set PACKAGE_ID=SurfaceQ
set OUTDIR=%ROOT%\artifacts\nupkg
set VERSION=0.1.0

echo === Cleaning ===
dotnet clean "%PROJECT%" -c Release || goto :error

echo === Packing fresh build ===
if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"
dotnet pack "%PROJECT%" -c Release -o "%OUTDIR%" || goto :error

echo === Uninstalling existing global tool (if installed) ===
dotnet tool uninstall --global %PACKAGE_ID% 2>nul

echo === Installing fresh tool ===
dotnet tool install --global %PACKAGE_ID% --version %VERSION% --add-source "%OUTDIR%" || goto :error

echo === Done ===
endlocal
exit /b 0

:error
echo Build/install failed with error %errorlevel%.
endlocal
exit /b %errorlevel%
