# OpenCAGE Source

This folder contains all source code for the main OpenCAGE application and other core tools. 

The solution is intended to be opened in Visual Studio 2019.

* PackagingTool
  * The main OpenCAGE tool itself, including the source code of AlienPAK and AlienBML. AlienBML was originally created by [x1nixmzeng](https://github.com/x1nixmzeng/AlienBML).
* SceneIO
  * The CATHODE scripting editor, with functionality to load game maps as well as edit scripts.
* LegendPlugin
  * A plugin for Brainiac Designer which can load Alien: Isolation behaviour trees.
* Updater
  * The updater for OpenCAGE which downloads new content from GitHub after a version check.
* Packager
  * A tool which packages resources for OpenCAGE into categorised archives, useful for reducing update sizes.
* Brainiac Designer / Brainiac Designer Base / Brainiac Exporter / External
	* A node editor for behaviour trees - code is used within license. Originally created by [Daniel Kollmann](https://archive.codeplex.com/?p=brainiac).
