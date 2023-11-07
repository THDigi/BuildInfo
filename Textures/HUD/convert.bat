echo off
cls

C:\Programs\Texconv\texconv.exe *.png -nologo -y -f BC7_UNORM -pmalpha -m 3 -if TRIANGLE_DITHER_DIFFUSION

ren *.DDS *.dds

pause