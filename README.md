
<h1><img src="https://i.imgur.com/ZtrM2U5.png" alt="OpenCAGE Asset Editor" align="right" width="100px" style="float:right;">OpenCAGE - Alien: Isolation Mod Tools</h1>

### OpenCAGE is an open-source modding toolkit for Alien: Isolation which enables custom scripting, configuration, and content modification through graphical interfaces.

<img src="https://i.imgur.com/DaWN23F.png" alt="OpenCAGE Script Editor" width="100%"/>

* Download the tools for free from [Steam](https://store.steampowered.com/app/3367530/)!
* Join the [Discord](https://discord.gg/JJ4ECu9hpY) to share your mods, and get tips & tricks from the community!
* Follow on [X (Twitter)](https://twitter.com/MattFiler) or [Bluesky](https://bsky.app/profile/mattfiler.co.uk) to get the latest news, including update previews and modding videos!

## Getting started

OpenCAGE can be downloaded for free from [Steam](https://store.steampowered.com/app/3367530/). On first launch, locate Alien: Isolation's `AI.exe` if requested. 

Once the toolkit has set itself up you will be prompted to load a level into the editor. After loading you can browse all of the level's scripts (called Composites) within the main window to modify the logic. From the toolbar at the top you will find the ability to launch into the game, manage backups, and modify a variety of configurations for characters, items, and more. In the toolbar you can also find tools to edit behaviour trees and assets (models, textures, materials, and more) within the loaded level, or globally across the game.

Documentation can be found on the [OpenCAGE website](https://opencage.co.uk/docs/) for most functionality within the toolkit.

If there is a section missing from the docs currently that you'd like to see added, feel free to open an issue on GitHub and it will be prioritised. Additionally, join the [Discord](https://discord.gg/JJ4ECu9hpY) to get direct help from the community!

## Additional recommended tools

When you're importing/exporting content with OpenCAGE, the following tools are recommended for working with the files:

 * [JPEXS Flash Decompiler](https://github.com/jindrapetrik/jpexs-decompiler) - for UI `.GFX` files
 * [NVidia Texture Tools Exporter](https://developer.nvidia.com/texture-tools-exporter) - for texture `.DDS` files
 
## Roadmap

OpenCAGE is a project that is constantly evolving - here's a vague roadmap of things I'm currently working towards, or have plans to start in the near future:

- **Contextual script editing**: follow composite chains to modify instanced mover entities
- **Extended script resource editing**: support for physics/collision resources
- **Content porter**: ability to port composites and all contained resources and assets between levels
- **New level creator**: create a new game level from scratch
- **Mod installers**: compile your maps and modifications out to installer executables
- **Navmeshes**: generate custom navmeshes for your modified levels to support AI
- **Skinned meshes**: import and export skinned meshes for custom animated models
- **Extended animation support**: view animations by name and display animated skeleton previews
- **Sound import/export**: integrated support for importing and exporting sounds

I don't expect donations, but if you'd like to contribute you can do so via [GitHub Sponsors](https://github.com/sponsors/MattFiler), where a variety of options are available! Similarly, this project is fully open source, and any code contributions are welcome! 

## Final mentions

OpenCAGE includes libraries and code from the following 3rd party sources:

 * [Brainiac Designer](https://github.com/learno/Brainiac-Designer): created by [Daniel Kollmann](https://twitter.com/dkollmann). 
 * [Cinematic Tools](https://github.com/MattFiler/CinematicTools): created by [Matti Hietanen](https://github.com/Hattiwatti).
 * [AlienBML](https://github.com/x1nixmzeng/AlienBML): created by [x1nixmzeng](https://github.com/x1nixmzeng).
 * [STNodeEditor](https://github.com/DebugST/STNodeEditor): created by [st233](http://st233.com/).
 * [Assimp-net](https://github.com/assimp/assimp-net): created by [Tesla3D](https://twitter.com/Tesla3D/).
 * [DirectXTexNet](https://github.com/deng0/DirectXTexNet): created by [deng0](https://github.com/deng0).

This code is used either with permission, or under license.
 
Additionally I'd like to thank [Daniel Maciel](https://github.com/danielmaciel), [Ryan Gray](https://github.com/RyanJGray/), [Henry Ramsey](https://github.com/ghramsey), and [Jeff](https://github.com/ttvjeffnl) for their help in expanding and testing the toolkit.

---

<img src="https://i.imgur.com/TeVWaE6.png" alt="OpenCAGE Logo" width="100%">

<i><p align="center">OpenCAGE is in no way related to (or endorsed by) Creative Assembly or SEGA.<br>Alien: Isolation must be purchased and installed to use OpenCAGE.</p></i>
