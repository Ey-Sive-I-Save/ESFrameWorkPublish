@echo off
setlocal
set WORKSPACE=%~dp0..
set LUBAN_DLL=%WORKSPACE%\LubanConfig\Luban\Luban.dll
set CONF_ROOT=%WORKSPACE%\LubanConfig
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
