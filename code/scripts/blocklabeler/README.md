# Block Labeling Script

## Overview
The BlockLabeler script is designed to append a user-specified tag to the CustomNames of each block in a target grid. This eliminates ambiguity in the Control Panel when several grids are connected. It facilitates easy filtering of blocks that belong to specific grids; thereby reducing clutter within the Control Panel.

## Installation
1. Add a ProgrammableBlock (PB) to the target grid (viz., the grid whose block names are to be modified) or any grid that it is connected to.
2. Cut and paste the contents of [BlockLabeler.cs](https://github.com/Hewselhoff/SE-scripts/blob/main/code/scripts/blocklabeler/BlockLabeler.cs) into the "Edit" field of the PB.
3. Click "Check Code" and verify that the script compiles without error.
4. Click "OK.
5. Follow the instructions in the CustomData field and click "OK" when finished.
6. Click "Run."

## In-game Usage
The Custom Data field in the PB will contain the following instructions:

\# OVERVIEW: This script is designed to loop through all IMyTerminalBlocks  
\# in a specified grid and append or replace a tag that can be used to  
\# identify the grid or construct that the blocks belong to.  
\#  
\# INSTRUCTIONS: Edit the parameter values below, click "OK", and click  
\# the "Run" Button.  
\#  
\# PARAMETER DEFINITIONS:  
\# Target Grid - The name of the grid whose blocks' CustomNames are to  
\# be updated.   
\#  
\# Old Tag - Optional substring within the blocks' CustomNames that can  
\# be replaced with the New Tag.  
\#  
\# New Tag - Tag to replace Old Tag (if defined) or append to the blocks'  
\# CustomNames.  
\# PB Grid: PB Grid Name  
  
  Target Grid:  
  Old Tag:  
  New Tag:  
  
## Example 1:
You've installed your PB on your base whose grid name is "Bravo Station." You've built a small grid ship that you want to call "The Bee-Dodge." The Bee-Dodge's blocks as listed in the Control Panel (along with all blocks belonging to any other grids  that are connected to it directly or indirectly) are:    
  
Battery  
Event Controller  
Remote Control  
Cockpit  
Landing Gear  
Connector 1  
Connector 2  
  
If you access the Custom Data for the Programmable Block (PB) where the BlockLabeler script is installed, you will see  
  
\# OVERVIEW: This script is designed to loop through all IMyTerminalBlocks  
...    
\# CustomNames.  
\# PB Grid: Bravo Station  
  
  Target Grid:  
  Old Tag:  
  New Tag:  
  
To append the string, "(Bee-Dodge)," to each of the block names that comprise the Bee-Dodge, you would edit the last three lines of Custom Data as follows:  

  Target Grid: The Bee-Dodge  
  Old Tag:  
  New Tag:  (Bee-Dodge)  
  
Then click "OK" to save the Custom Data and click "Run" in the PB's menu. The list of blocks belonging to the Bee-Dodge will now be:  
  
Battery (Bee-Dodge)  
Event Controller (Bee-Dodge)  
Remote Control (Bee-Dodge)  
Cockpit (Bee-Dodge)  
Landing Gear (Bee-Dodge)  
Connector 1 (Bee-Dodge)  
Connector 2 (Bee-Dodge)  

## Example 2:
You've decided that you don't like the name "Bee-Dodge." You've decided to go with something less offensive like, "Pubix Cube." As you may recall, the Bee-Dodge's block names (as listed in the Control Panel) were:    
  
Battery (Bee-Dodge)  
Event Controller (Bee-Dodge)  
Remote Control (Bee-Dodge)  
Cockpit (Bee-Dodge)  
Landing Gear (Bee-Dodge)  
Connector 1 (Bee-Dodge)  
Connector 2 (Bee-Dodge)  

Accessing the Custom Data for the Programmable Block (PB) where the BlockLabeler script is installed, you will see:  

\# OVERVIEW: This script is designed to loop through all IMyTerminalBlocks  
...    
\# CustomNames.  
\# PB Grid: Bravo Station  
  
  Target Grid: The Bee-Dodge  
  Old Tag:    
  New Tag:  (Bee-Dodge)  
  
To replace the string, "(Bee-Dodge)," with "(Pubix Cube)" in each of the block names that comprise the soon-to-be-christened, Pubix Cube, you would edit the last three lines of Custom Data as follows:  

  Target Grid: The Bee-Dodge  
  Old Tag:  (Bee-Dodge)  
  New Tag:  (Pubix Cube)  
  
Then click "OK" to save the Custom Data and click "Run" in the PB's menu. The list of blocks belonging to the Bee-Dodge will now be:  

Battery (Pubix Cube)  
Event Controller (Pubix Cube)  
Remote Control (Pubix Cube)  
Cockpit (Pubix Cube)  
Landing Gear (Pubix Cube)  
Connector 1 (Pubix Cube)  
Connector 2 (Pubix Cube)  

Note that the Target Grid key above retains the value "The Bee-Dodge" because the grid name was still "The Bee-Dodge" when we ran the script. If, however, you had changed the grid name before re-labeling the blocks, then you would, of course, have set Target Grid to "Pubix Cube."
