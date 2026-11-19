@echo off
cd /d "%~dp0"
echo Adds a Windows Firewall rule for PocketPad on private networks.
echo Right-click this file and choose Run as administrator if access is denied.
netsh advfirewall firewall add rule name="PocketPad Local Controller" dir=in action=allow program="%~dp0runtime\python.exe" protocol=TCP localport=8765 profile=private
pause
