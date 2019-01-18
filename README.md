# Alien: Isolation Mod Tools

### This project allows simple creation, distribution and installation of mods for Alien: Isolation.

The Alien: Isolation mod tools allow the modification of...

* **AI Behaviour Trees** - full access to view and edit all behaviour trees in the game to change reactions and encounters.
* **Difficulty Settings** - difficulty specific modifiers for character senses and alien configurations.
* **Alien Behaviour Parameters** - values such as search radius, vent roam ranges, search time and director AI settings.
* **NPC & Player Attributes** - hostility, attack groups, peek speeds, max health, health regeneration and more.
* **Locomotion Editor** - steering speeds such as cornering weight, acceleration, warping and more.
* **Vision Editor** - viewcones are vision settings, letting you change the view distance and field of view for a character.
* **Sense Editor** - flamethrower sense, movement sense, flashlight sense, visual sense, touch sense and more for NPCs.
* **Item and Inventory Editor** - maximum items able to be carried, base ammo count, weapon names and more.
* **Weapon Ammo** - fuel consumption, physics impact effects, damage per character type and accuracy.
* **Movie Playlist Editor** - map/sequence specific loadscreen and cutscene movie playlists.
* **Blueprint Recipe Editor** - blueprint required item types and amounts as well as number of items produced.
* **Hack Tool Difficulties** - hacking speed, pickup range, ease to complete the hacking minigame.
* **Environment Lighting** - environment radiosity, deferred lighting, skin shading and hair shading settings.
* **Graphics Settings** - custom FOV, resolutions, LOD and shadow settings - plus ability to enable hidden options.
* **More coming soon!**

... all through a simple user interface with super easy-to-use import/export features. 

### Mods can be saved to archives which are able to be loaded into the game through these tools.

## Setting up the tools

Assuming you already have Alien: Isolation installed...

1. Download the mod tools by [clicking here](https://github.com/MattFiler/LegendPlugin/raw/master/Mod%20Tools/Mod%20Tools.exe).
2. Copy to your Alien: Isolation directory.
3. Open!

The tools will automatically set themselves up on first launch, and any future updates will be automatically downloaded and applied if you are connected to the internet.

Running the mod tools requires administrator privileges in order to modify game files without conflicts.

## Loading a mod

The mod tools have a built in mod loader which makes it super easy to switch between different mods without messing with game files.

<img align="right" src="https://i.imgur.com/2BtWtbd.png" width="50%">

1. Visit the Alien: Isolation Mod Tools [ModDB page](http://www.moddb.com/mods/alien-isolation-mod-tools).
2. Find a mod you want to install and download it.
3. Extract the ZIP and copy the mod folder within it. Do not copy the individual files, but the directory they are in.
4. Navigate to your Alien: Isolation directory and go to "DATA", "MODS". Paste your mod folder here.
5. Launch the mod tools and press "Load". Find your newly downloaded mod in the list and select it.
6. You will be shown an overview of the mod - press the install button and yes when asked to confirm.

Your mod is now installed! You can easily switch to other mods by selecting them from the same load list and pressing install again.

Only one mod can be used at any one time.

## Saving your mod

When you're finished creating a mod you can save it for either personal use or to share it with others.

<img align="right" src="https://i.imgur.com/MlmW8pR.png" width="50%">

1. Load the mod tools and select "Save".
2. Enter the mod name (used to identify the mod), description (shown in the tools when previewing the mod), author (your name) and tagline (the description used in-game).
3. Select an image to use as the mod's preview picture and select what configurations you wish to include in your mod. You may have changed something you don't want included. Be careful not to include/exclude something important!
4. Press save. Your mod will be created and the file explorer will open.
5. Convert your mod's folder to a ZIP (right click the folder, "Send to", "Compressed (zipped) folder").
6. Visit the Mod Tools [ModDB page](http://www.moddb.com/mods/alien-isolation-mod-tools) and upload the zip to share your mod with the community!

## Creating a mod

The mod tools have a wide variety of things to edit within Alien: Isolation. Each editor screen is different, but they all operate in a similar way - I've done my best to explain below, but it's pretty self explanatory. 

When editing game files, you will be editing whatever mod is currently loaded into the game. Similarly, if you load in a mod, your changes will be overwritten.

If you wish to revert back to default configurations and undo your changes at any point, you can follow the "Removing mods" section of this guide.

<img align="right" src="https://i.imgur.com/yOTMIPt.png" width="50%">

1. Load the mod tools and press "Create".
2. Choose the editor you wish to use. You will then be presented with a window full of empty textboxes and dropdowns.
3. Select a configuration to load from the available dropdown in the top right of the window and press the load button. Be patient while the data is loaded in to the program. Some editors may have a different load process, but most follow this routine. Some, for example the Lighting Settings, load the data when you start the editor automatically.
4. Tweak the values in the editor to your choice. If you hover over an editable field you will see a description of what you are modifying. Some fields may be disabled in the editor but show values - these are taken from the loaded configuration's template. Load the template to change these values.
5. Press the save button when finished. Your changes will be added the game.

The save button in each editor will save what is currently loaded in the form. Do not load another configuration or part of a configuration within an editor without pressing the save button unless you wish to erase your current work.

## Creating a mod: Behaviour trees

Learning the abilities of every node in LegendPlugin (the Alien: Isolation plugin for Brainiac Designer) is an entirely separate tutorial, but frankly it's just easier to pick up as you mess around with it and experiment different combinations of node groups and parameters. This tutorial will however show you how to use the mod tools to export behaviour trees, LegendPlugin to modify them, and then how to reimport them back to the game.

<img align="right" src="https://i.imgur.com/j4xsCzu.png" width="50%">

1. Open up the mod tools.
2. Click "Create", "Behaviour Tree Tool" then "Open Editor". On first load this will export all behaviour trees, so be patient while it processes.
3. In the left-hand pane you can now see all behaviour trees under the "Behaviours" category, and all available node groups under each "NodeGroup" category. Take some time to browse through all of the behaviours and check out how many nodes are available to use!
4. To add a new node, drag it from a node group onto the workspace (with a behaviour loaded). Existing nodes will show you arrows where you can place a new node. To modify an existing node, click on it and use the parameters box on the right hand side of the Brainiac Designer window. To remove a node, click on it and press the delete key.
5. When you're done with your modifications, click save on the top right of every behaviour you have modified (or alternatively close Brainiac Designer and it will prompt you to save all).
6. In the mod tools window you opened the editor from, select "Import Changes". Be patient while this processes - it shouldn't take long.
7. That's it! Your modified behaviours are now in the game, launch it and try them out!

If you ever need to reset behaviours to defaults, press "Reset Behaviour Trees" within the mod tools and you'll be set back to the standard behaviours. This may come in handy!

## Removing mods

Removing installed mods or reverting changes you've made through the editing tools is easy!

<img align="right" src="https://i.imgur.com/9MsW6cE.png" width="50%">

1. Open up the mod tools.
2. Click "Reset".
3. Select the files you wish to remove and press "Yes" when asked to confirm. If you want to remove all files (a whole mod or all changes you've made), select "Reset All Files".
4. The selected file(s) will now be reverted back to defaults and the game can be played as normal.

Remember that there is no undo! Re-installing mods is easy, but re-creating something you've made yourself may take a while. 

It's worth saving your mod before resetting so that you can load the configurations back at any time if you wish to revisit the project.

## Final mentions

 * The mod tools unpack and repack the game's BML files through a script known as [AlienBML](https://github.com/x1nixmzeng/AlienBML), originally created by the awesome [x1nixmzeng](https://github.com/x1nixmzeng).
 * [Brainiac Designer](https://archive.codeplex.com/?p=brainiac) was originally created by Daniel Kollmann. 
 * Thanks to Andy Bray for [his breakdown](https://archives.nucl.ai/recording/its-in-the-vents-the-ai-of-alien-isolation/) of the Alien's AI systems, and Clive Gratton's [explanation](https://www.youtube.com/watch?v=FXKEiFUXBIo) of the CATHODE Engine.