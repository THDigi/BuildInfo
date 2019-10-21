rmdir C:\Users\Digi\AppData\Roaming\SpaceEngineersDedicated\content\244850\513466522\Data\Scripts\BuildInfo\ /S /Q
rmdir C:\Steam\steamapps\workshop\content\244850\513466522\Data\Scripts\BuildInfo\ /S /Q

robocopy.exe .\ C:\Steam\steamapps\workshop\content\244850\513466522\Data\Scripts\BuildInfo\ *.cs /xd "bin" "obj" ".vs" "DoNotCopy_VanillaDataCompare" /S
robocopy.exe .\ C:\Users\Digi\AppData\Roaming\SpaceEngineersDedicated\content\244850\513466522\Data\Scripts\BuildInfo\ *.cs /xd "bin" "obj" ".vs" "DoNotCopy_VanillaDataCompare" /S

pause