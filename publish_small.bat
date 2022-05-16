REM del /F /Q bin\Debug\net5.0-windows\win-x64\publish\*
dotnet publish -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true
copy bin\Debug\net6.0\win-x64\publish\CPU7Plus.exe Output\Windows-x64\CPU7Plus.exe