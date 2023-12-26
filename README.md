# Simpit
Provides a serial connection interface for custom controllers.

This is the KSP2 version of [Kerbal Simpit Revamped]https://github.com/Simpit-team/KerbalSimpitRevamped

It works with an accompanying [Arduino library]https://github.com/Simpit-team/KerbalSimpitRevamped-Arduino to make building hardware devices simpler.


## Setup for building
- You must have the dotnet-sdk-7 installed.
- Download this repo and open the .sln with Visual Studio 2022. 
- Go to Extras -> NuGet Package Manager -> Manage NuGet packages for solution
	- Search for System.IO.Ports and install version 7.0.0
	- Search for System.ComponentModel.Primitives and install version 4.3.0
- Have the mod dependencies BepInEx, SpaceWarp and UITK for KSP 2 installed (you can use CKAN to get those)
- Have the environment variable KSP2DIR set to the KSP2 game folder where the dependencies are installed
- run scripts/build-run.bat to build the mod and launch the game