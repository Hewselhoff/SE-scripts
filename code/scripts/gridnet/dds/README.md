# OVERVIEW:
This script is designed to write the contents of broadcast messages
to IMyTextPanels. The primary application being to facilitate the sharing of
statuses between physically unconnected grids.

# IMPLEMENTATION:
This script must be run an a Programmable Block that is located
on the same grid as the IMyTextPanels that will display the contents of the 
messages it receives. Broadcast messages can originate from remote grids. All participating grids must have antennas! 


# CONFIGURATION:
Broadcast Tags must be specified in the CustomData of the
PB that hosts this script as well as the IMyTextPanels that will
display the broadcast data. E.g.,

         Channels: [CHANNEL_A], [CHANNEL_B]

This script allows IMyTextPanels to display feeds from multiple channels
simultaneously. The Custom Names of the IMyTextPanels must contain the [MSG_DISPLAY] tag.
This script must be recompiled following any changes made to CustomData.

# USAGE:
In order for scripts on remote grids to send messages to
this program, they need only invoke the IGC.SendBroadcastMessage() method and
provide it with the appropriate broadcast tag and message payload.

# Credit:
This script uses the Wico Modular IGC Example code (see below) for its IGC 
functions.

Wico Modular IGC Example

November 28, 2019
Updated Feb 4, 2020 to be MDK IGC example 3

Steam workshop link: 
https://steamcommunity.com/sharedfiles/itemedittext/?id=1923270132
https://github.com/Wicorel/WicoSpaceEngineers/tree/master/Modular/IGC%20Modular%20Example

