@echo off
setlocal
set WORKSPACE=%~dp0..\..\..
set LUBAN_DLL=%~dp0Luban\Luban.dll
set CONF_ROOT=%~dp0
set CODE_DIR=%WORKSPACE%\Assets\Plugins\ES\Generated\Luban\CSharp
set DATA_DIR=%WORKSPACE%\Assets\Plugins\ES\Generated\Luban\Json

dotnet "%LUBAN_DLL%" ^
    -t all ^
    -d json ^
    -c cs-newtonsoft-json ^
    --conf "%CONF_ROOT%\luban.conf" ^
    -x outputCodeDir="%CODE_DIR%" ^
    -x outputDataDir="%DATA_DIR%"

exit /b %ERRORLEVEL%
