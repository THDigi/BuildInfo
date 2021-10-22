@echo off
rem	Testing mods in DS by yourself can be done without the need to re-publish every time.
rem	You can simply update the files that are on your machine!
rem	This will only work for you, anyone else joining the server will of course download the mod from the workshop.

rem	To use:
rem	1. Copy this .bat file in the ROOT folder of your local mod (e.g. SpaceEngineers/Mods/YourLocalMod/<HERE>)

rem	2. Edit this variable if applicable (do not add quotes or end with backslash).
set STEAM_PATH=C:\Steam

rem	3. Edit this with your mod's workshop id.
set WORKSHOP_ID=513466522

rem	Now you can run it every time you want to update the mod on DS and client.



rem 	Don't edit the below unless you really need different paths.
rem	NOTE: don't add quotes and don't end with a backslash!

set CLIENT_PATH=%STEAM_PATH%\steamapps\workshop\content\244850\%WORKSHOP_ID%
set DS_PATH=%APPDATA%\SpaceEngineersDedicated\content\244850\%WORKSHOP_ID%

rmdir "%CLIENT_PATH%" /S /Q
rem rmdir "%DS_PATH%" /S /Q

robocopy.exe .\ "%CLIENT_PATH%" *.* /S /xd .git bin obj .vs ignored /xf *.lnk *.git* *.bat *.zip *.7z *.blend* *.png *.md *.log *.sln *.csproj *.csproj.user *.ruleset desktop.ini

rem client path is junctioned to DS path too, less copying
rem -------robocopy.exe "%CLIENT_PATH%" "%DS_PATH%" *.* /S
rem mklink /J "%DS_PATH%" "%CLIENT_PATH%"

pause