<img src="https://i.imgur.com/TeVWaE6.png" width="100%">

# OpenCAGE - Alien: Isolation Mod Tools

### OpenCAGE is an open source modding toolkit for Alien: Isolation, which allows access to a large range of game configurations and content through a simple user friendly interface.

This project was formerly known as "LegendPlugin", or the "Alien: Isolation Mod Tools". This new name for the project reflects goals being set out for upcoming functionality planned over the Summer of 2020.

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
* **Scripts** - coming soon!

--- 

<img align="right" src="https://i.imgur.com/Nl2qPOV.png" width="40%">

For any game configuration, the toolkit provides options to back-up and reset user-made changes. Backed-up changes can be distributed to other users of the mod tools to try and promote a healthy community!

To learn how to distribute any backed-up configurations, [check out the wiki](https://github.com/MattFiler/OpenCAGE/wiki).

Edited game content is not included in the back-up/reset functionality due to the size of some of the files. Manually backing up certain files is recommended, or alternatively, Steam can provide the option to reset to vanilla by validating files.

---

This is a live project and something I'm developing in my free time. Certain things may be unfinished or temperamental. Any in-development sections of the tools will always be labelled as such!

There are a number of new features currently in the pipeline that will be available soon, such as script editing and improved texture/model support. Stay tuned!

## Setting up OpenCAGE

Assuming you already have Alien: Isolation installed...

1. Download OpenCAGE by [clicking here](https://github.com/MattFiler/OpenCAGE/raw/master/OpenCAGE.exe).
2. Copy to your Alien: Isolation directory.
3. Open!

The tools will automatically set themselves up on first launch, and any future updates will be automatically downloaded and applied if you are connected to the internet.

<img align="right" src="https://i.imgur.com/KG2nlpX.png" width="40%">

## Getting help

The [wiki](https://github.com/MattFiler/OpenCAGE/wiki) is currently being put together to explain a number of functions within the toolkit, however most are pretty simple to understand through tooltips and added descriptions.

If there is a section missing from the wiki currently that you'd like to see added, feel free to open an issue on GitHub and it will be prioritised.

## Recommended content tools

When you've exported content from the game with OpenCAGE, there are a number of other tools available that are best suited to deal with the content's formats. A few are listed below...

 * [JPEXS Flash Decompiler](https://github.com/jindrapetrik/jpexs-decompiler) is recommended for editing exported UI .GFX files.
 * [Pico Pixel](https://pixelandpolygon.com/) is recommended for viewing exported texture .DDS files.
 * [DirectXTex](https://github.com/microsoft/DirectXTex/releases) compiled binary is recommended for converting to/from .DDS formats.
 * [io_scene_aliens](https://forum.xentax.com/viewtopic.php?t=12079&start=90#p103131) Blender plugin is recommended for viewing exported models.
 
## Recommended mods

There are a number of mods that improve the experience of playing Alien: Isolation, or assist with modding. My best recommendations are listed below...

 * [MotherVR](https://github.com/Nibre/MotherVR) is recommended for VR support.
 * [Alias Isolation](https://github.com/aliasIsolation/aliasIsolation) is recommended to improve visuals.
 * [Cinematic Tool](http://cinetools.xyz/games/) is recommended to allow some runtime control over the game.

## Final mentions

 * The mod tools unpack and repack the game's BML files through a script known as [AlienBML](https://github.com/x1nixmzeng/AlienBML), originally created by the awesome [x1nixmzeng](https://github.com/x1nixmzeng).
 * [Brainiac Designer](https://archive.codeplex.com/?p=brainiac) was originally created by Daniel Kollmann. 
 * Thanks to Andy Bray for [his breakdown](https://archives.nucl.ai/recording/its-in-the-vents-the-ai-of-alien-isolation/) of the Alien's AI systems, and Clive Gratton's [explanation](https://www.youtube.com/watch?v=FXKEiFUXBIo) of the CATHODE Engine.
 * The DDS header compiler used to export textures was created by [Cra0kalo](https://github.com/cra0kalo) and [Volfin](https://github.com/volfin). 
 
---

<p align="center">OpenCAGE is in no way related to (or endorsed by) Creative Assembly or SEGA.</p>
