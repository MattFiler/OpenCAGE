<img src="https://i.imgur.com/TeVWaE6.png" width="100%">

# OpenCAGE - Alien: Isolation Mod Tools

### OpenCAGE is an open source modding toolkit for Alien: Isolation, powered by [CathodeLib](https://github.com/OpenCAGE/CathodeLib), which allows access to a range of configurations and content through a graphical interface.

The toolkit is broken down into four main sections: assets, configurations, scripts, and behaviour trees.

## Asset editor supports:

* **Models** - ability to import and export models, with support for custom materials & previews.
* **Textures** - ability to import and export textures for use with custom materials.
* **Animations** - ability to import and export Havok animations and skeletons.
* **In-Game UI** - ability to import and export the game's GFX UI files which can be edited with a Flash decompiler.
* **Sounds** - functionality to edit sounds is coming soon, however you can already extract sounds using [this tool](https://github.com/MattFiler/Alien-Isolation-Audio-Extractor).

## Configuration editor supports:

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
* **In-Game Text** - edit any text including Sevastolink logs, mission names, subtitles, and more.
* **Material Properties** - material-specific settings such as impact effects, physics, and more. *[COMING SOON]*
* **Mod Packaging** - package any modifications into a mod package able to be shared & installed by others.
* **Reset Functionality** - ability to reset specific changes back to default.

## Script editor supports:

* **Particles and VFX** - particles and other FX can be created, edited, and instanced.
* **Trigger Volumes** - 3D trigger volumes can be placed and resized to trigger custom events.
* **Objectives** - objectives can be set and cleared through custom triggers.
* **Scripted Sequences** - ability to play animations and create scripted sequences.
* **AI Control** - full control over the AI systems to change behaviours/responses.
* **Mathematic Logic** - support for mathematic logic such as addition of integers, floats, and vectors.
* **Collision Volumes** - dynamic 3D collision volumes can be edited.
* **Sevastolink Terminals** - functionality to populate Sevastolink terminals with content.
* **Character Spawning** - ability to spawn existing and new custom characters.
* **Resource References** - ability to place and animate models and other game assets.
* and more... the only limit is your imagination!

## Behaviour tree editor supports:

* **AI Behaviour Trees** - full access to view and edit all behaviour trees in the game through a flowchart UI.
* **Reset Functionality** - ability to reset behaviour trees back to default and undo changes.

Check out the "coming soon" section below to learn more about upcoming features.

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
 
## Roadmap

OpenCAGE is a project that is constantly evolving - here's a vague roadmap of things I'm currently working towards, or have plans to start in the near future:

### Script editor:

- Extended Enum population
- Improved resource reference display
- Contextual animation selection for CMD_PlayAnimation
- Performance improvements
- Support for custom navmeshes
- "Share Map" option which builds a custom map installer
- Support for extended Mover editing
- Improvements to the [Unity level viewer](https://github.com/OpenCAGE/LevelEditor)

### Asset editor:

- Nicer handling for file locks
- Ability to import textures as PNG/JPG images
- Ability to select texture usage
- Cubemap texture support
- Skinned mesh support

### Configuration editor:

- UI overhaul
- Ability to edit inherited members
- Material properties editor
- Links through to scripting tools

### Behaviour tree editor

- Integrate compilation and reset from the launcher into the editor

This is an ongoing project, supported financially by the community. I don't expect donations, but if you'd like to contribute you can do so via [GitHub Sponsors](https://github.com/sponsors/MattFiler), where a variety of options are available! Similarly, this project is fully open source, and any code contributions are welcome! 

## Final mentions

OpenCAGE includes libraries and code from the following 3rd party sources:

 * [Brainiac Designer](https://archive.codeplex.com/?p=brainiac): created by [Daniel Kollmann](https://twitter.com/dkollmann). 
 * [Cinematic Tools](https://github.com/MattFiler/CinematicTools): created by [Matti Hietanen](https://github.com/Hattiwatti).
 * [AlienBML](https://github.com/x1nixmzeng/AlienBML): created by [x1nixmzeng](https://github.com/x1nixmzeng).
 * [STNodeEditor](https://github.com/DebugST/STNodeEditor): created by [st233](http://st233.com/).
 * [Assimp-net](https://github.com/assimp/assimp-net): created by [Tesla3D](https://twitter.com/Tesla3D/).
 * [DirectXTexNet](https://github.com/deng0/DirectXTexNet): created by [deng0](https://github.com/deng0).

This code is used either with permission, or under license.
 
Additionally I'd like to thank [Daniel Maciel](https://github.com/danielmaciel), [Ryan Gray](https://github.com/RyanJGray/), and [Jeff](https://github.com/ttvjeffnl) for their help in expanding and testing the toolkit.
 
---

<p align="center">OpenCAGE is in no way related to (or endorsed by) Creative Assembly or SEGA.</p>
