rmdir C:\Users\Digi\AppData\Roaming\SpaceEngineers\Mods\BuildInfo\Data\Scripts\BuildInfo\ /S /Q

robocopy.exe .\ C:\Users\Digi\AppData\Roaming\SpaceEngineers\Mods\BuildInfo\Data\Scripts\BuildInfo\ *.cs "API Information.txt" /xd "bin" "obj" ".vs" "DoNotCopy_VanillaDataCompare" /S

pause