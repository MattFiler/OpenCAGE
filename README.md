# Alien: Isolation Behaviour Modding ToolKit

### This project unlocks the ability to modify AI behaviours in Alien: Isolation with two tools:

* **PackagingTool**, a program built to import and export behaviour trees from the game as well as provide a GUI for editing some game attributes.
* **LegendPlugin**, a plugin for Brainiac Designer that enables the functionality to read behaviour trees from Alien: Isolation. 

## Getting started

Before being able to use LegendPlugin and PackagingTool, you must have the following things installed:

 * Brainiac Designer [(download here)](https://brainiac.codeplex.com/releases/view/24156)
 * .NET Framework 4.5.2 or higher [(download here)](https://www.microsoft.com/en-gb/download/details.aspx?id=42642)
 * Alien: Isolation [(buy here)](http://store.steampowered.com/app/214490/)

## Setting up the tools

Assuming you already have Brainiac Designer and Alien: Isolation installed, this tutorial will take you through the basic setup for LegendPlugin and PackagingTool.

1. Download and extract LegendPlugin from Github.
2. Open the "Plugin" folder and copy "LegendPlugin.dll".
3. Navigate to your Brainiac Designer install directory. This is typically located in "C:\Program Files (x86)\Brainiac Designer".
4. Paste "LegendPlugin.dll" into the "plugins" folder within the Brainiac Designer install directory.
5. Head back to where you extracted LegendPlugin and open up the folder named "Packager".
6. Copy "PackagingTool.exe" and place it within your Alien: Isolation directory, typically located in "C:\Program Files (x86)\Steam\steamapps\common\Alien Isolation". 
7. Launch PackagingTool. If you haven't placed PackagingTool in your Alien: Isolation directory, initially it will ask you to locate your Alien: Isolation executable. Navigate to your game install folder and double click on AI.exe.
8. Close PackagingTool and open up Brainiac Designer.
9. In Brainiac Designer, press "New". Enter the workspace name as "Alien Isolation" or something else memorable.
10. Select LegendPlugin.dll from the plugins list. If you don't see LegendPlugin, make sure you copied the dll to the correct folder in the previous steps.
11. Our "Behaviour Folder" and "Default Export Folder" need to be set to the "Behaviour Tree Directory" that PackagingTool just created for us. Click browse next to both and navigate to where PackagingTool is installed, choosing "Behaviour Tree Directory" as the folder location for each one. 
12. Click "Done".

You're now ready to move on to extracting behaviour trees and creating mods!

## Modifying behaviour trees

Learning the abilities of every node in LegendPlugin is an entirely separate tutorial, but frankly it's just easier to pick up as you mess around with it and experiment different combinations of node groups and parameters. This tutorial will however show you how to use PackagingTool to export the behaviour trees, LegendPlugin to modify them, and PackagingTool to then reimport them back to the game.

<div style="float: right; width: 100%; max-width: 500px; margin-left: 20px;">

![Screenshot](https://i.imgur.com/j4xsCzu.png)

</div>

1. Open up PackagingTool.
2. Click "Behaviour Tree Packager" then "Export Behaviour Trees". This will begin exporting the behaviours from the game, be patient as this may take some time depending on the speed of your computer (especially storage write speed).
3. When the behaviour trees are exported open up Brainiac Designer.
4. In the dropdown box that appears on first load, make sure you select your "Alien Isolation" project (or whatever you named it) from the previous tutorial.
5. Select "Open Workspace".
6. In the left-hand pane you can now see all behaviour trees under the "Behaviours" category, and all available node groups under each "NodeGroup" category. Take some time to browse through all of the behaviours and check out how many nodes are available to use!
7. To add a new node, drag it from a node group onto the workspace (with a behaviour loaded). Existing nodes will show you arrows where you can place a new node. To modify an existing node, click on it and use the parameters box on the right hand side of the Brainiac Designer window. To remove a node, click on it and press the delete key.
8. When you're done with your modifications, click save on every behaviour you have modified (or alternatively close Brainiac Designer and it will prompt you to save all).
9. Re-open PackagingTool, press "Behaviour Tree Packager" then "Import Behaviour Trees". Again, be patient while this processes - it shouldn't take long.
10. That's it! Your modified behaviours are now in the game, launch it and try them out! 

If you ever need to reset behaviours to defaults, press "Reset Behaviour Trees" within PackagingTool and you'll be set back to the standard behaviours. This may come in handy!

## Modifying other game attributes

PackagingTool can also modify other game attributes as well as import/export behaviour trees. This feature is planned to be expanded upon in the future to cover Alien behaviour types, viewconesets and weapon data. Currenly PackagingTool supports basic character attribute editing.

<div style="float: right; width: 100%; max-width: 500px; margin-left: 20px;">

![Screenshot](https://i.imgur.com/RLB4kVP.png)

</div>

1. Open up PackagingTool.
2. Click "Character Attribute Editor". You will be presented with a window full of empty textboxes and dropdowns.
3. Selected a character class from the available dropdown in the top right of the window and press "Load Class". Be patient while the data is loaded in.
4. Tweak the values as you like and press "Save Class Attributes" when finished. Your changes will be imported into the game.

The attribute editor within PackagingTool will not create a backup of original files before your modifications - be aware of this! It's always worth taking copies of your original files in case you need to revert back later.

## Final mentions

 * PackagingTool unpacks and repacks the game's BML files through a script known as [AlienBML](https://github.com/x1nixmzeng/AlienBML), originally created by the awesome [x1nixmzeng](https://github.com/x1nixmzeng).
 * The "Brainiac Designer Base" source code is included with its license.
 * Also, thanks to the Creative Assembly for not only creating an awesome game, but using an open-source editor for their behaviour trees! 