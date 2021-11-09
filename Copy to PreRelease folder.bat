@echo off
set REPLACE_IN_PATH=%APPDATA%\SpaceEngineers\Mods\BuildInfoPreRelease

rmdir "%REPLACE_IN_PATH%" /S /Q

robocopy.exe .\ "%REPLACE_IN_PATH%" *.* /S /xd .git bin obj .vs ignored DoNotCopy_VanillaDataCompare /xf *.exe *.dll *.lnk *.git* *.bat *.zip *.7z *.blend* *.png *.md *.log *.sln *.csproj *.csproj.user *.ruleset desktop.ini

pause