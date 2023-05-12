echo off
cls
C:\Programs\Texconv\texconv.exe FontData*.png -nologo -y -f BC7_UNORM_SRGB -pmalpha -if TRIANGLE_DITHER_DIFFUSION -m 2
ren *.DDS *.dds
pause