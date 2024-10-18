echo off
cls

C:\Programs\Texconv\texconv.exe *.png -nologo -y -f BC7_UNORM -pmalpha -if LINEAR_DITHER

C:\Programs\Texconv\texconv.exe TransparentGradientSRGB.png -nologo -y -f BC7_UNORM_sRGB -pmalpha -if LINEAR_DITHER
C:\Programs\Texconv\texconv.exe LaserGradientSRGB.png -nologo -y -f BC7_UNORM_sRGB -pmalpha -if LINEAR_DITHER

ren *.DDS *.dds

pause