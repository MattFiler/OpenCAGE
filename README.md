<img src="https://i.imgur.com/TeVWaE6.png" width="100%">

# OpenCAGE (Alien: Isolation Mod Tools)

### OpenCAGE is a powerful open-source modding toolkit for Alien: Isolation which enables custom scripting, configuration, and content modification through graphical interfaces.

* Join the [Discord](https://discord.gg/JJ4ECu9hpY) to share your mods, and get tips & tricks from the community!
* Follow on [X](https://twitter.com/MattFiler) to get the latest news, including update previews and modding videos!

<!--<br>-->
<!--<img src="https://i.imgur.com/TeVWaE6.png" width="100%">-->

## Getting started

Assuming you already have Alien: Isolation installed...

1. Download OpenCAGE by [clicking here](https://github.com/MattFiler/OpenCAGE/raw/master/OpenCAGE.exe)
2. Copy to your Alien: Isolation directory (where AI.exe is located)
3. Launch `OpenCAGE.exe`

The tools will automatically set themselves up on first launch, and any future updates will be automatically downloaded and applied if you are connected to the internet.

## What's included?

When you first launch OpenCAGE, you'll be presented with five options:

* **Edit Assets**: A tool to import/export textures, models, UI, and animations
* **Edit Configurations**: A tool to modify a range of game configurations (including weapons, ammo, NPC viewcones and locomotion, player attributes, and more)
* **Edit Cathode Scripts**: A tool allowing full access to the game's scripting system (including level population, missions, characters, weapons, and more)
* **Edit Behaviour Trees**: A tool giving full access to every behaviour tree in the game
* **Launch Game**: Functionality to launch the game directly to a level with various optional extras (including Cinematic Tools, debug helpers, and more)

There's also functionality for creating/managing/restoring backups of levels, and reverting configurations and behaviour trees.

Check out the "roadmap" section below to learn more about upcoming features.

## Getting help

The [docs](https://opencage.co.uk/docs/) are currently being put together to explain a number of functions within the toolkit, however most are pretty simple to understand through tooltips and added descriptions.

If there is a section missing from the docs currently that you'd like to see added, feel free to open an issue on GitHub and it will be prioritised.

As this is a live project and something I'm developing in my free time, certain things may be unfinished or temperamental. Any in-development sections of the tools will always be labelled as such!

## Additional recommended tools

When you've exported content from the game with OpenCAGE, there are a number of other tools available that are best suited to deal with the content's formats. A few are listed below...

 * [JPEXS Flash Decompiler](https://github.com/jindrapetrik/jpexs-decompiler) is recommended for editing exported UI .GFX files.
 * [Pico Pixel](https://pixelandpolygon.com/) is recommended for viewing exported texture .DDS files.
 * [DirectXTex](https://github.com/microsoft/DirectXTex/releases) compiled binary is recommended for converting to/from .DDS formats.
 
## Roadmap

OpenCAGE is a project that is constantly evolving - here's a vague roadmap of things I'm currently working towards, or have plans to start in the near future:

- **Contextual script editing**: follow composite chains to modify instanced mover entities
- **Extended script resource editing**: support for physics/collision resources
- **Content porter**: ability to port composites and all contained resources and assets between levels
- **New level creator**: create a new game level from scratch
- **Mod installers**: compile your maps and modifications out to installer executables
- **3D viewer improvements**: extended support for editing levels within [Unity](https://github.com/OpenCAGE/LevelEditor)
- **Navmeshes**: generate custom navmeshes for your modified levels to support AI
- **Skinned meshes**: import and export skinned meshes for custom animated models
- **Extended animation support**: view animations by name and display animated skeleton previews
- **Sound import/export**: integrated support for importing and exporting sounds
- **Unified asset/script experience**: modify assets within the Script Editor (Asset Editor to be deprecated)
- **Improved configuration editor UI**: improvements to the UI within the Config Editor

This is an ongoing project, supported financially by the community. I don't expect donations, but if you'd like to contribute you can do so via [GitHub Sponsors](https://github.com/sponsors/MattFiler), where a variety of options are available! Similarly, this project is fully open source, and any code contributions are welcome! 

## Final mentions

OpenCAGE includes libraries and code from the following 3rd party sources:

 * [Brainiac Designer](https://github.com/learno/Brainiac-Designer): created by [Daniel Kollmann](https://twitter.com/dkollmann). 
 * [Cinematic Tools](https://github.com/MattFiler/CinematicTools): created by [Matti Hietanen](https://github.com/Hattiwatti).
 * [AlienBML](https://github.com/x1nixmzeng/AlienBML): created by [x1nixmzeng](https://github.com/x1nixmzeng).
 * [STNodeEditor](https://github.com/DebugST/STNodeEditor): created by [st233](http://st233.com/).
 * [Assimp-net](https://github.com/assimp/assimp-net): created by [Tesla3D](https://twitter.com/Tesla3D/).
 * [DirectXTexNet](https://github.com/deng0/DirectXTexNet): created by [deng0](https://github.com/deng0).

This code is used either with permission, or under license.
 
Additionally I'd like to thank [Daniel Maciel](https://github.com/danielmaciel), [Ryan Gray](https://github.com/RyanJGray/), and [Jeff](https://github.com/ttvjeffnl) for their help in expanding and testing the toolkit.
 
---

<p align="center">OpenCAGE is in no way related to (or endorsed by) Creative Assembly or SEGA.</p>
