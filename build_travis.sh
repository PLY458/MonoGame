mono Protobuild.exe -generate Linux
msbuild /t:restore Test/MonoGame.Tests.Linux.csproj
msbuild Test/MonoGame.Tests.Linux.csproj
cd Test/bin/Linux/AnyCPU/Debug/
MONO_LOG_LEVEL=debug mono MonoGame.Tests.exe

