# Alien: Isolation Behaviour Modding ToolKit

### This project unlocks the ability to modify AI behaviours in Alien: Isolation with two tools:

* **PackagingTool**, a program that can export and import behaviour trees from Alien: Isolation to allow for modification in Brainiac Designer.
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
6. Either open "PackagingTool.exe" now, or copy it to a more temporary location first.
7. When PackagingTool launches it will ask you to locate your Alien: Isolation exe. Navigate to your game install folder and double click on AI.exe. This is typically located in "C:\Program Files (x86)\Steam\steamapps\common\Alien Isolation".
8. You should now see three buttons in PackagingTool. Close PackagingTool and open up Brainiac Designer.
9. In Brainiac Designer, press "New". Enter the name as "Alien Isolation" or something else memorable.
10. Select LegendPlugin.dll from the plugins list. If you don't see LegendPlugin, make sure you copied the dll to the correct folder in the previous steps.
11. Our "Behaviour Folder" and "Default Export Folder" need to be set to the "Conversion Directory" that PackagingTool just created for us. Click browse next to both and navigate to where PackagingTool is installed, choosing "Conversion Directory" as the folder location for each one.
12. Click "Done".

You're now ready to move on to extracting behaviour trees and creating mods!

## Using the tools

Learning the abilities of every node in LegendPlugin is an entirely separate tutorial, but frankly it's just easier to pick up as you mess around with it and experement different combinations of node groups and parameters. This tutorial will however show you how to use PackagingTool to export the behaviour trees, LegendPlugin to modify them, and PackagingTool to then reimport them back to the game.

<div style="float: right; width: 100%; max-width: 500px; margin-left: 20px;">

![Screenshot](https://i.imgur.com/j4xsCzu.png)

</div>

1. Open up PackagingTool.
2. Click "Export Behaviour Trees". This will begin exporting the behaviours from the game, be patient as this may take some time depending on the speed of your computer.
3. Open up Brainiac Designer.
4. In the dropdown box that appears on first load, make sure you have selected your "Alien Isolation" project (or whatever you named it) from the previous tutorial.
5. Select "Open Workspace".
6. In the left-hand pane you can now see all behaviour scripts under the "Behaviours" category, and all node groups under each "NodeGroup" category. Take some time to browse through all of the behaviours and check out how many nodes are available to use!
7. To add a new node, drag it from a node group onto the workspace (with a behaviour loaded). Existing nodes will show you arrows where you can place a new node. To modify an existing node, click on it and use the parameters box on the right hand side of the Brainiac Designer window. To remove a node, click on it and press the delete key.
8. When you're done with your modifications, click save on every behaviour you have modified (or alternatively close Brainiac Designer and it will prompt you to save all).
9. Re-open PackagingTool and press "Import Behaviour Trees". Again, be patient while this processes - it shouldn't take long.
10. That's it! Your modified behaviours are now in the game, launch it and try them out! 

If you ever need to reset behaviours to defaults, press "Reset Behaviour Trees" within PackagingTool and you'll be set back to the standard behaviours. This may come in handy!

## Final mentions

 * PackagingTool unpacks and repacks the game's BML files through a script known as [AlienBML](https://github.com/x1nixmzeng/AlienBML), originally created by the awesome [x1nixmzeng](https://github.com/x1nixmzeng).
 * The "Brainiac Designer Base" source code is included with its license.
 * Also, thanks to the Creative Assembly for not only creating an awesome game, but using an open-source editor for their behaviour trees! 