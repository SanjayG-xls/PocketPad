@echo off
cd /d "%~dp0"
echo Install the ViGEmBus virtual controller driver, then restart PocketPad.
echo Windows will ask for administrator approval for the driver installation.
start "" /wait "%~dp0tools\ViGEmBus_1.22.0_x64_x86_arm64.exe"
echo.
echo If the installer requests a restart, restart Windows before playing.
pause
