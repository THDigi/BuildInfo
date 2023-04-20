@echo off
set REPLACE_IN_PATH=%APPDATA%\SpaceEngineers\Mods\BuildInfo

rmdir "%REPLACE_IN_PATH%" /S /Q

robocopy.exe .\ "%REPLACE_IN_PATH%" *.* /S /xd .git bin obj .vs ignored DoNotCopy_VanillaDataCompare /xf *.exe *.dll *.lnk *.git* *.bat *.zip *.7z *.blend* *.png *.pdn *.md *.log *.sln *.csproj *.csproj.user *.ruleset *.ps1 desktop.ini


rem Write the modinfo.sbmi for normal release workshop ID

(
  echo ^<?xml version="1.0"?^>
  echo ^<MyObjectBuilder_ModInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"^>
  echo   ^<SteamIDOwner^>76561197985528887^</SteamIDOwner^>
  echo   ^<WorkshopId^>0^</WorkshopId^>
  echo   ^<WorkshopIds^>
  echo     ^<WorkshopId^>
  echo       ^<Id^>514062285^</Id^>
  echo       ^<ServiceName^>Steam^</ServiceName^>
  echo     ^</WorkshopId^>
  echo   ^</WorkshopIds^>
  echo ^</MyObjectBuilder_ModInfo^>
) > "%REPLACE_IN_PATH%\modinfo.sbmi"

echo Generated modinfo.sbmi

rem Write buildinfo-priority.txt for normal buildinfo

(
  echo 100
  echo.
  echo first line of this file must be a number.
  echo when multiple buildinfo mods exist, the one with the highest number gets to run.
  echo feature provided by this mod ^(GameSession.cs file^) not by SE.
) > "%REPLACE_IN_PATH%\buildinfo-priority.txt"

echo Generated buildinfo-priority.txt

pause