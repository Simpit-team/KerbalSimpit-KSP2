# Simpit
This is a Kerbal Space Program 2 plugin to provide a serial connection interface for custom controllers.

This is the KSP2 version of [Kerbal Simpit Revamped](https://github.com/Simpit-team/KerbalSimpitRevamped)

It works with the accompanying [Arduino library](https://github.com/Simpit-team/KerbalSimpitRevamped-Arduino) to make building hardware devices simpler.

We have a Discord Server! [Invite Link](https://discord.gg/ZwcPdNcaRN). We have an [online documentation](https://kerbalsimpitrevamped-arduino.readthedocs.io/) for using this mod.

## How to install
This mod comes in two parts: the KSP mod and the Arduino lib.
To install the KSP mod, you can either:
- Install it through [CKAN](https://github.com/KSP-CKAN/CKAN) by installing Simpit (easier)
- Download it from github and install it manually (harder)
	- Download and install the dependencies (SpaceWarp, UITK for KSP2, BepInEx).
	- Go the release tab and dowload the latest version of this mod. 
	- Copy the Simpit folder next to its dependencies into the BepInEx/plugins/ folder of your KSP2 install.

After that start KSP2 and go to the MODS section in the main menu to change the Serial port to the port of your Arduino in the Simpit settings. You can find the right port name by copying the port name you are using in the Arduino IDE.

The Serial connection does not yet start automatically, you have to open it manually. When in flight mode, you can find the Simpit-UI in the App bar under the 9 dots.

To install the Arduino lib, you can download it [here](https://github.com/Simpit-team/KerbalSimpitRevamped-Arduino) and install it in your Arduino IDE by going to Sketch -> Include library -> Add .ZIP library. Then you  should find some Simpit examples in the example list.
 
Simpit for KSP2 is built very similar to Simpit-revamped for KSP1. If you already have a controller for KSP1, most of your controller should work with KSP2 without any changes. For differences look at the [online documentation](https://kerbalsimpitrevamped-arduino.readthedocs.io/) .

## Setup for building
- You must have the dotnet-sdk-7 installed.
- Download this repo and open the .sln with Visual Studio 2022. 
- Go to Extras -> NuGet Package Manager -> Manage NuGet packages for solution
	- Search for System.IO.Ports and install version 7.0.0
	- Search for System.ComponentModel.Primitives and install version 4.3.0
- Have the mod dependencies BepInEx, SpaceWarp and UITK for KSP 2 installed (you can use CKAN to get those)
- Have the environment variable KSP2DIR set to the KSP2 game folder where the dependencies are installed
- run scripts/build-run.bat to build the mod and launch the game