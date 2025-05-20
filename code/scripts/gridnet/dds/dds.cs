/*
 * Wico Modular IGC Example
 * 
 * November 28, 2019
 * Updated Feb 4, 2020 to be MDK IGC example 3
 * 
 * Steam workshop link: 
 * https://steamcommunity.com/sharedfiles/itemedittext/?id=1923270132
 * 
 * 
 * Source available at:
 * https://github.com/Wicorel/WicoSpaceEngineers/tree/master/Modular/IGC%20Modular%20Example
 */

WicoIGC _wicoIGC;
//private List<IMyTextPanel> displays = new List<IMyTextPanel>();
private Dictionary<string, List<IMyTextPanel>> displays = new Dictionary<string, List<IMyTextPanel>>();

/// <summary>
/// This is our unique ID for our message.  We've defined the format for the message data (it's just a string)
/// </summary>
string _STATUSbroadCastTag = "[GANTRY_STATUS]";
string _INVENTORYbroadCastTag = "[GANTRY_INVENTORY]";

/// <summary>
/// The combined set of UpdateTypes that count as a 'trigger'
/// </summary>
UpdateType _utTriggers = UpdateType.Terminal | UpdateType.Trigger | UpdateType.Mod | UpdateType.Script;

/// <summary>
/// the combined set of UpdateTypes and count as an 'Update'
/// </summary>
UpdateType _utUpdates = UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100 | UpdateType.Once;

public Program()
{
    _wicoIGC = new WicoIGC(this);

    displays[_STATUSbroadCastTag] = FindDisplayPanels(_STATUSbroadCastTag);
    displays[_INVENTORYbroadCastTag] = FindDisplayPanels(_INVENTORYbroadCastTag);

    // cause ourselves to run again so we can do the init
    Runtime.UpdateFrequency = UpdateFrequency.Once;
}

public void Save()
{
}

/// <summary>
/// Has everything been initialized?
/// </summary>
bool _areWeInited=false;

public void Main(string argument, UpdateType updateSource)
{
    // Echo some information about 'me' and why we were run
    Echo("Source=" + updateSource.ToString());
    Echo("Me=" + Me.EntityId.ToString("X"));
    Echo(Me.CubeGrid.CustomName);

    if (!_areWeInited)
    {
        InitMessageHandlers();
        _areWeInited = true;
    }

    // always check for IGC messages in case some aren't using callbacks
    _wicoIGC.ProcessIGCMessages();
    if ((updateSource & UpdateType.IGC) > 0)
    {
        // we got a callback for an IGC message.
        // but we already processed them.
    }
    else if((updateSource & _utTriggers) > 0)
    {
        // if we got a 'trigger' source, send out the received argument
        IGC.SendBroadcastMessage(_STATUSbroadCastTag, argument);
        Echo("Sending Message:\n" + argument);
    }
    else if((updateSource & _utUpdates) > 0)
    {
        // it was an automatic update

        // this script doesn't have anything to do
    }

    foreach(var display in displays[_STATUSbroadCastTag]){
        display.ContentType = ContentType.TEXT_AND_IMAGE;
        display.WriteText("TEST");
    }
}

private List<IMyTextPanel> FindDisplayPanels(string tag)
{
    List<IMyTextPanel> panels = new List<IMyTextPanel>();
    GridTerminalSystem.GetBlocksOfType(panels, p => p.CustomName.Contains(tag) && p.IsSameConstructAs(Me));
    foreach(var panel in panels){
       Echo("Panel: " + panel.CustomName);
    }
    
    return panels;
}        

private void UpdateDisplays(string tag, string data)
{
    // If we have display panels, update them
    foreach(var display in displays[tag]){
        display.ContentType = ContentType.TEXT_AND_IMAGE;
        display.WriteText(data);
    }
}


void InitMessageHandlers()
{
    // creates a broadcast channel with the specified tag and calls the handler when messages are processed
    _wicoIGC.AddPublicHandler(_STATUSbroadCastTag, StatusBroadcastHandler);
    _wicoIGC.AddPublicHandler(_INVENTORYbroadCastTag, StatusBroadcastHandler);
}

// Handler for the test broadcast messages.
void StatusBroadcastHandler(MyIGCMessage msg)
{
    // NOTE: called on ALL received messages; not just 'our' tag

    if (msg.Tag != _STATUSbroadCastTag && msg.Tag != _INVENTORYbroadCastTag)
        return; // not our message

    if (msg.Data is string)
    {
        Echo("Received Test Message");
        Echo(" Source=" + msg.Source.ToString("X"));
        Echo(" Data=\"" + msg.Data + "\"");
        Echo(" Tag=" + msg.Tag);
        UpdateDisplays(msg.Tag, msg.Data.ToString());
    }
}

// Source is available from: https://github.com/Wicorel/WicoSpaceEngineers/tree/master/Modular/IGC
class WicoIGC
{
    // the one and only unicast listener.  Must be shared amoung all interested parties
    IMyUnicastListener _unicastListener;

    /// <summary>
    /// the list of unicast message handlers. All handlers will be called on pending messages
    /// </summary>
    List<Action<MyIGCMessage>> _unicastMessageHandlers = new List<Action<MyIGCMessage>>();

    /// <summary>
    /// List of 'registered' broadcst message handlers.  All handlers will be called on each message received
    /// </summary>
    List<Action<MyIGCMessage>> _broadcastMessageHandlers = new List<Action<MyIGCMessage>>();

    /// <summary>
    /// List of broadcast channels.  All channels will be checked for incoming messages
    /// </summary>
    List<IMyBroadcastListener> _broadcastChannels = new List<IMyBroadcastListener>();

    MyGridProgram _gridProgram;
    bool _debug = false;
    IMyTextPanel _debugTextPanel;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="myProgram"></param>
    /// <param name="debug"></param>
    public WicoIGC(MyGridProgram myProgram, bool debug = false)
    {
        _gridProgram = myProgram;
        _debug = debug;
        _debugTextPanel = _gridProgram.GridTerminalSystem.GetBlockWithName("IGC Report") as IMyTextPanel;
        if (_debug) _debugTextPanel?.WriteText("");
    }

    /// <summary>
    /// Call to add a handler for public messages.  Also registers the tag with IGC for reception.
    /// </summary>
    /// <param name="channelTag">The tag for the channel.  This should be unique to the use of the channel.</param>
    /// <param name="handler">The handler for messages when received. Note that this handler will be called with ALL broadcast messages; not just the one from ChannelTag</param>
    /// <param name="setCallback">Should a callback be set on the channel. The system will call Main() when the IGC message is received.</param>
    /// <returns></returns>
    public bool AddPublicHandler(string channelTag, Action<MyIGCMessage> handler, bool setCallback = true)
    {
        IMyBroadcastListener publicChannel;
        // IGC Init
        publicChannel = _gridProgram.IGC.RegisterBroadcastListener(channelTag); // What it listens for
        if (setCallback) publicChannel.SetMessageCallback(channelTag); // What it will run the PB with once it has a message

        // add broadcast message handlers
        _broadcastMessageHandlers.Add(handler);

        // add to list of channels to check
        _broadcastChannels.Add(publicChannel);
        return true;
    }

    /// <summary>
    /// Add a unicast handler.
    /// </summary>
    /// <param name="handler">The handler for messages when received. Note that this handler will be called with ALL Unicast messages. Always sets a callback handler</param>
    /// <returns></returns>
    public bool AddUnicastHandler(Action<MyIGCMessage> handler)
    {
        _unicastListener = _gridProgram.IGC.UnicastListener;
        _unicastListener.SetMessageCallback("UNICAST");
        _unicastMessageHandlers.Add(handler);
        return true;

    }
    /// <summary>
    /// Process all pending IGC messages.
    /// </summary>
    public void ProcessIGCMessages()
    {
        bool bFoundMessages = false;
        if (_debug) _gridProgram.Echo(_broadcastChannels.Count.ToString() + " broadcast channels");
        if (_debug) _gridProgram.Echo(_broadcastMessageHandlers.Count.ToString() + " broadcast message handlers");
        if (_debug) _gridProgram.Echo(_unicastMessageHandlers.Count.ToString() + " unicast message handlers");
        // TODO: make this a yield return thing if processing takes too long
        do
        {
            bFoundMessages = false;
            foreach (var channel in _broadcastChannels)
            {
                if (channel.HasPendingMessage)
                {
                    bFoundMessages = true;
                    var msg = channel.AcceptMessage();
                    if (_debug)
                    {
                        _gridProgram.Echo("Broadcast received. TAG:" + msg.Tag);
                        _debugTextPanel?.WriteText("IGC:" +msg.Tag+" SRC:"+msg.Source.ToString("X")+"\n",true);
                    }
                    foreach (var handler in _broadcastMessageHandlers)
                    {
                        handler(msg);
                    }
                }
            }
        } while (bFoundMessages); // Process all pending messages

        if (_unicastListener != null)
        {
            // TODO: make this a yield return thing if processing takes too long
            do
            {
                // since there's only one channel, we could just use .HasPendingMessages directly.. but this keeps the code loops the same
                bFoundMessages = false;

                if (_unicastListener.HasPendingMessage)
                {
                    bFoundMessages = true;
                    var msg = _unicastListener.AcceptMessage();
                    if (_debug) _gridProgram.Echo("Unicast received. TAG:" + msg.Tag);
                    foreach (var handler in _unicastMessageHandlers)
                    {
                        // Call each handler
                        handler(msg);
                    }
                }
            } while (bFoundMessages); // Process all pending messages
        }

    }
}
