echo off
cls
C:\Programs\Texconv\texconv.exe *.png -nologo -y -f BC7_UNORM_SRGB -pmalpha -if LINEAR
ren *.DDS *.dds
pause