@echo off
cls

del *.dds

ImageBundler.exe "D:\Programs\ImageMagick\convert.exe" "C:\Programs\Texconv\texconv.exe"

echo Converting to DDS...

C:\Programs\Texconv\texconv.exe *.png -nologo -y -f BC7_UNORM -pmalpha -m 1  >NUL

ren *.DDS *.dds

echo Deleting temporary PNG...

del *.png

echo All done!
pause