@echo off
setlocal

set "GTX_MAINTENANCE_FLAG=%~dp0App_Data\maintenance.flag"

echo GTX production maintenance recovery
echo Target: %GTX_MAINTENANCE_FLAG%
echo.

if not exist "%GTX_MAINTENANCE_FLAG%" (
    echo The maintenance flag does not exist. The application is already configured as online.
    pause
    exit /b 0
)

del /f /q "%GTX_MAINTENANCE_FLAG%"

if exist "%GTX_MAINTENANCE_FLAG%" (
    echo Failed to remove the maintenance flag. Run this file with permission to modify App_Data.
    pause
    exit /b 1
)

echo Maintenance mode is disabled. No app-pool reset is required.
echo Verify: https://usedcarscincinnati.com/
pause
exit /b 0
