# Alien: Isolation Mod Tools

### This project unlocks the ability to modify Alien: Isolation in a number of ways:

* **AI Behaviour Trees** - full access to view and edit all behaviour trees in the game to change reactions and encounters.
* **Difficulty Settings** - difficulty specific modifiers for character senses and alien configurations **(WIP)**.
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
* **Environment Lighting** - environment radiosity, differed lighting, skin shading and hair shading settings.
* **Graphics Settings** - all available in-game graphics settings as well as custom FOV and other extended options **(WIP)**.

More to be added in future!

## Getting started

To be able to use all features of the mod tools, the following is required:

 * Brainiac Designer [(download here)](https://brainiac.codeplex.com/releases/view/24156)
 * .NET Framework 4.5.2 or higher [(download here)](https://www.microsoft.com/en-gb/download/details.aspx?id=42642)
 * Alien: Isolation [(buy here)](http://store.steampowered.com/app/214490/)

## Setting up the tools

Assuming you already have Brainiac Designer and Alien: Isolation installed...

1. Download and extract the mod tools from Github.
2. Open the "LegendPlugin" folder and copy "LegendPlugin.dll".
3. Navigate to your Brainiac Designer install directory. This is typically located in "C:\Program Files (x86)\Brainiac Designer".
4. Paste "LegendPlugin.dll" into the "plugins" folder within the Brainiac Designer install directory.
5. Head back to where you extracted the mod tools and open up the folder named "Mod Tools".
6. Copy "Mod Tools.exe" and place it within your Alien: Isolation directory, typically located in "C:\Program Files (x86)\Steam\steamapps\common\Alien Isolation". 
7. Launch the mod tools. If you haven't placed "Mod Tools.exe" in your Alien: Isolation directory, initially it will ask you to locate your Alien: Isolation executable. Navigate to your game install folder and double click on "AI.exe".
8. Close the mod tools and open up Brainiac Designer.
9. In Brainiac Designer, press "New". Enter the workspace name as "Alien Isolation" or something else memorable.
10. Select LegendPlugin.dll from the plugins list. If you don't see LegendPlugin, make sure you copied the dll to the correct folder in the previous steps.
11. Your "Behaviour Folder" and "Default Export Folder" need to be set to the "Behaviour Tree Directory" that the mod tools have created for you. Click browse next to both input boxes and navigate to where your "Mod Tools.exe" is located, choosing "Behaviour Tree Directory" as the location for each. 
12. Click "Done".

The mod tools are all set up!

## Modifying behaviour trees

Learning the abilities of every node in LegendPlugin (the Alien: Isolation plugin for Brainiac Designer) is an entirely separate tutorial, but frankly it's just easier to pick up as you mess around with it and experiment different combinations of node groups and parameters. This tutorial will however show you how to use the mod tools to export behaviour trees, LegendPlugin to modify them, and then how to reimport them back to the game.

<div style="float: right; width: 100%; max-width: 500px; margin-left: 20px;">

![Screenshot](https://i.imgur.com/j4xsCzu.png)

</div>

1. Open up the mod tools.
2. Click "Behaviour Tree Packager" then "Export Behaviour Trees". This will begin exporting the behaviours from the game, be patient as this may take some time depending on the speed of your computer (especially write speed).
3. When the behaviour trees are exported, open up Brainiac Designer.
4. In the dropdown box that appears on first load, make sure you select your "Alien Isolation" project (or whatever you named it) from the previous tutorial.
5. Select "Open Workspace".
6. In the left-hand pane you can now see all behaviour trees under the "Behaviours" category, and all available node groups under each "NodeGroup" category. Take some time to browse through all of the behaviours and check out how many nodes are available to use!
7. To add a new node, drag it from a node group onto the workspace (with a behaviour loaded). Existing nodes will show you arrows where you can place a new node. To modify an existing node, click on it and use the parameters box on the right hand side of the Brainiac Designer window. To remove a node, click on it and press the delete key.
8. When you're done with your modifications, click save on every behaviour you have modified (or alternatively close Brainiac Designer and it will prompt you to save all).
9. Re-open the mod tools, press "Behaviour Tree Packager" then "Import Behaviour Trees". Again, be patient while this processes - it shouldn't take long.
10. That's it! Your modified behaviours are now in the game, launch it and try them out!

If you ever need to reset behaviours to defaults, press "Reset Behaviour Trees" within the mod tools and you'll be set back to the standard behaviours. This may come in handy!

## Modifying other game attributes

The mod tools also have a wide variety of other things to edit within Alien: Isolation as well as behaviour trees. Check out the options on the main window of the mod tools to see what is moddable. Each editor screen is different, but they all operate in a similar way - I've done my best to explain below, but it's pretty self explanatory. 

<div style="float: right; width: 100%; max-width: 500px; margin-left: 20px;">

![Screenshot](https://i.imgur.com/RLB4kVP.png)

</div>

1. Open up the mod tools.
2. Choose the editor you wish to use. You will then be presented with a window full of empty textboxes and dropdowns.
3. Select a configuration to load from the available dropdown in the top right of the window and press the load button. Be patient while the data is loaded in to the program. Some editors may have a different load process, but most follow this routine. Some, for example the Lighting Settings, load the data when you start the editor automatically.
4. Tweak the values in the editor to your choice. If you hover over an editable field you will see a description of what you are modifying.
5. Press the save button when finished. Your changes will be added the game.

The editors within the mod tools will not create backups of original files before your modifications - be aware of this! It's always worth taking copies of your original files in case you need to revert back later.

## Final mentions

 * The mod tools unpack and repack the game's BML files through a script known as [AlienBML](https://github.com/x1nixmzeng/AlienBML), originally created by the awesome [x1nixmzeng](https://github.com/x1nixmzeng).
 * The "Brainiac Designer Base" source code is included with its license in order to compile LegendPlugin.
 * Thanks to Andy Bray for [his breakdown](https://archives.nucl.ai/recording/its-in-the-vents-the-ai-of-alien-isolation/) of the Alien's AI systems, and Clive Gratton's [explanation](https://www.youtube.com/watch?v=FXKEiFUXBIo) of the CATHODE Engine.
 * Also, thanks to the Creative Assembly for not only creating an awesome game, but using an open-source editor for their behaviour trees! 