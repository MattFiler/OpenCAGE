<img src="https://i.imgur.com/TeVWaE6.png" width="100%">

# OpenCAGE - Alien: Isolation Mod Tools

### OpenCAGE is an open source modding toolkit for Alien: Isolation which allows access to a large range of game configurations and content through a simple user friendly interface.

The toolkit is broken down into three main sections: configurations, content, and scripting.

## Configuration editors support:

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

## Content editors support:

* **In-Game UI** - ability to import/export the game's GFX UI files which can be edited by a Flash decompiler.
* **Textures** - ability to import/export textures, some formats still in testing (please report issues!).
* **Models** - coming soon!
* **Sounds** - coming soon!

## Scripting editors support:

* **Particles and VFX** - custom particles and other FX can be created and edited.
* **Trigger Volumes** - fully 3D trigger volumes are supported to trigger scripted events.
* **Objectives** - ability to implement, trigger, and manage the game's objective system.
* **Scripted Sequences** - ability to create scripted sequences combining animations, sounds, etc.
* **AI Control** - full control over the AI systems to change behaviours/responses on cue.
* **Mathematic Logic** - support for key mathematic logic such as addition of integers, floats, and vectors.
* **Collision Volumes** - fully 3D collision volumes can be created/edited through the 3D editor.
* ... and much, much more! The OpenCAGE scripting editor allows for fully custom campaigns.

--- 

## Setting up OpenCAGE

Assuming you already have Alien: Isolation installed...

1. Download OpenCAGE by [clicking here](https://github.com/MattFiler/OpenCAGE/raw/master/OpenCAGE.exe).
2. Copy to your Alien: Isolation directory.
3. Open!

The tools will automatically set themselves up on first launch, and any future updates will be automatically downloaded and applied if you are connected to the internet.

## Getting help

The [wiki](https://github.com/MattFiler/OpenCAGE/wiki) is currently being put together to explain a number of functions within the toolkit, however most are pretty simple to understand through tooltips and added descriptions.

If there is a section missing from the wiki currently that you'd like to see added, feel free to open an issue on GitHub and it will be prioritised.

As this is a live project and something I'm developing in my free time, certain things may be unfinished or temperamental. Any in-development sections of the tools will always be labelled as such!

## Additional recommended tools

When you've exported content from the game with OpenCAGE, there are a number of other tools available that are best suited to deal with the content's formats. A few are listed below...

 * [JPEXS Flash Decompiler](https://github.com/jindrapetrik/jpexs-decompiler) is recommended for editing exported UI .GFX files.
 * [Pico Pixel](https://pixelandpolygon.com/) is recommended for viewing exported texture .DDS files.
 * [DirectXTex](https://github.com/microsoft/DirectXTex/releases) compiled binary is recommended for converting to/from .DDS formats.
 * The [Cinematic Tools](https://github.com/MattFiler/CinematicTools) are available to allow some runtime control over the game.
 
## Coming soon

Eventually I'd like to have OpenCAGE support custom maps, however to unlock this I first need to finalise the 3D editor, as well as figure out the game's navmesh format, and a way to generate Havok collision maps.

This is an ongoing project, supported financially by the community. I don't expect donations, but if you'd like to contribute you can do so via [GitHub Sponsors](https://github.com/sponsors/MattFiler)!

To run OpenCAGE in "beta" mode and recieve the latest in-development updates, create a file called "DEBUG_MODE" in the directory where your OpenCAGE exe is located. Be aware that this branch of the tools may be unstable or incomplete, and also may require updates to be downloaded more regularly than the main branch.

## Final mentions

OpenCAGE includes code from the following 3rd party sources:

 * [AlienBML](https://github.com/x1nixmzeng/AlienBML): created by [x1nixmzeng](https://github.com/x1nixmzeng).
 * [Brainiac Designer](https://archive.codeplex.com/?p=brainiac): created by Daniel Kollmann. 
 * [DDS header generator](https://github.com/cra0kalo/AITexExtract/blob/master/AITexExtract/DDS.cs): created by [Cra0kalo](https://github.com/cra0kalo) and [Volfin](https://github.com/volfin). 
 * [Dear ImGui](https://github.com/ocornut/imgui): created by [ocornut](https://github.com/ocornut).

This code is used either with permission, or under license.
 
---

<p align="center">OpenCAGE is in no way related to (or endorsed by) Creative Assembly or SEGA.</p>
