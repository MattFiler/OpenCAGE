<img src="https://i.imgur.com/6G2KyF2.png" width="100%">

# Alien: Isolation Mod Tools

### The Alien: Isolation Mod Tools allow for the modification of a large range of game configurations and content through a simple user friendly interface.

## Moddable configurations include:

* **AI Behaviour Trees** - full access to view and edit all behaviour trees in the game through a flowchart UI.
* **Difficulty Settings** - difficulty specific modifiers for character senses and alien configurations.
* **Alien Behaviour Parameters** - search radius, vent roam ranges, search time, and director AI settings.
* **NPC & Player Attributes** - hostility, attack groups, peek speeds, max health, health regeneration, and more.
* **Locomotion** - steering speeds such as cornering weight, acceleration, warping, and more.
* **AI Vision** - viewcone parameters, letting you change the view distance and field of view for a character.
* **AI Senses** - flamethrower sense, movement sense, flashlight sense, visual sense, touch sense, and more for NPCs.
* **Items and Inventory** - maximum items able to be carried, base ammo count, weapon names, and more.
* **Weapon Ammo** - fuel consumption, physics impact effects, damage per character type, and accuracy.
* **Movie Playlists** - map/sequence specific loadscreen and cutscene movie playlists.
* **Blueprint Recipes** - blueprint required item types and amounts as well as number of items produced.
* **Hack Tool Difficulties** - hacking speed, pickup range, ease to complete the hacking minigame.
* **Environment Lighting** - environment radiosity, deferred lighting, skin shading, and hair shading settings.
* **Graphics Settings** - custom FOV, resolutions, LOD, and shadow settings - plus ability to enable hidden options.
* **In-Game Text** - any text in the game including Sevastolink logs, mission names, subtitles, and more.
* **Door Codes** - any keycode in the game for doors and lockers, with supporting UI text.
* **Material Properties** - material-specific settings such as impact effects, physics, and more. [WIP]

## Moddable content includes:

* **In-Game UI** - ability to import/export the game's GFX UI files which can be edited by a Flash decompiler.
* **Textures** - ability to import/export textures for any map, some formats still in testing (please report issues!).
* **Models** - ability to export models for any map. [WIP]

--- 

<img align="right" src="https://i.imgur.com/Nl2qPOV.png" width="40%">

For any game configuration, the toolkit provides options to back-up and reset user-made changes. Backed-up changes can be distributed to other users of the mod tools to try and promote a healthy community!

To learn how to distribute any backed-up configurations, check out the [Mod Tools Wiki](https://github.com/MattFiler/LegendPlugin/wiki).

Edited game content is not included in the back-up/reset functionality due to the size of some of the files. Manually backing up certain files is recommended, or alternatively, Steam can provide the option to reset to vanilla by validating files.

---

This is a live project and something I'm developing in my free time. Certain things may be unfinished or temperamental. Any in-development sections of the tools will always be labelled as such!

There are a number of new features currently in the pipeline that will be available soon - stay tuned.

[AlienPAK](https://github.com/MattFiler/AlienPAK) is a work-in-progress standalone tool aiming to support a range of Alien: Isolation PAK types from scripts, to textures, to shaders. AlienPAK's features will be ported to LegendPlugin as they become stable! Check that repo out to see some upcoming file handling functionality.

## Setting up the tools

Assuming you already have Alien: Isolation installed...

1. Download the mod tools by [clicking here](https://github.com/MattFiler/LegendPlugin/raw/master/Mod%20Tools/Mod%20Tools.exe).
2. Copy to your Alien: Isolation directory.
3. Open!

The tools will automatically set themselves up on first launch, and any future updates will be automatically downloaded and applied if you are connected to the internet.

Running the mod tools requires administrator privileges in order to modify game files without conflicts.

<img align="right" src="https://i.imgur.com/KG2nlpX.png" width="40%">

## Getting help

The [Mod Tools Wiki](https://github.com/MattFiler/LegendPlugin/wiki) is currently being put together to explain a number of functions within the toolkit, however most are pretty simple to understand through tooltips and added descriptions.

If there is a section missing from the Wiki currently that you'd like to see added, feel free to open an issue on GitHub and it will be prioritised.

## Recommended content tools

When you've exported content from the game with the mod tools, there are a number of other tools available that are best suited to deal with the content's formats. A few are listed below...

 * [JPEXS Flash Decompiler](https://github.com/jindrapetrik/jpexs-decompiler) is recommended for editing exported UI .GFX files.
 * [Pico Pixel](https://pixelandpolygon.com/) is recommended for viewing exported texture .DDS files.
 * [DirectXTex](https://github.com/microsoft/DirectXTex/releases) compiled binary is recommended for converting to/from .DDS formats.
 * [io_scene_aliens](https://forum.xentax.com/viewtopic.php?t=12079&start=90#p103131) Blender plugin is recommended for viewing exported models.
 
## Recommended mods

There are a number of mods that improve the experience of playing Alien: Isolation, or assist with modding. My best recommendations are listed below...

 * [MotherVR](https://github.com/Nibre/MotherVR) is recommended for the ability to enable some debug options in-game.
 * [Alias Isolation](https://github.com/aliasIsolation/aliasIsolation) is recommended to improve visuals and allow the injection of a patched cinematic tool.

## Final mentions

 * The mod tools unpack and repack the game's BML files through a script known as [AlienBML](https://github.com/x1nixmzeng/AlienBML), originally created by the awesome [x1nixmzeng](https://github.com/x1nixmzeng).
 * [Brainiac Designer](https://archive.codeplex.com/?p=brainiac) was originally created by Daniel Kollmann. 
 * Thanks to Andy Bray for [his breakdown](https://archives.nucl.ai/recording/its-in-the-vents-the-ai-of-alien-isolation/) of the Alien's AI systems, and Clive Gratton's [explanation](https://www.youtube.com/watch?v=FXKEiFUXBIo) of the CATHODE Engine.
 * The DDS header compiler used to export textures was created by [Cra0kalo](https://github.com/cra0kalo) and [Volfin](https://github.com/volfin). 
