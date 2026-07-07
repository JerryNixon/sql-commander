@echo off
setlocal

pushd "%~dp0"

set "PROJECT=SqlCmdr.Web\SqlCmdr.Web.csproj"
set "PROFILE=http"
set "URL=http://localhost:5101"

if not exist "%PROJECT%" (
    echo Could not find %PROJECT% from %CD%.
    echo Please run this script from the SQL Commander repository root.
    popd
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET SDK was not found on PATH.
    echo Install the .NET 8 SDK, then run this script again.
    popd
    exit /b 1
)

echo Starting SQL Commander...
echo URL: %URL%
echo.

dotnet run --project "%PROJECT%" --launch-profile %PROFILE%
set "EXITCODE=%ERRORLEVEL%"

popd
exit /b %EXITCODE%
