/* 
 * OVERVIEW: This script is designed to write the contents of broadcast messages
 * to IMyTextPanels. The primary application being to facilitate the sharing of
 * statuses between physically unconnected grids.
 *
 * IMPLEMENTATION: This script must be run an a Programmable Block that is located
 * on the same grid as the IMyTextPanels that will display the contents of the 
 * messages it receives. These IMyTextPanels must contain the Broadcast Tag
 * associated with the broadcast channel corresponding to the data they are to
 * display. In order for scripts on remote grids to send messages to
 * this program, they need only invoke the IGC.SendBroadcastMessage() method and
 * provide it with the appropriate broadcast tag and message payload.
 *
 * CONFIGURATION: Broadcast Tags must be specified in the CustomData of the
 * PB that hosts this script. E.g.,
 *
 *           Channels: [CHANNEL_A], [CHANNEL_B]
 *
 * This script must be recompiled following any changes made to CustomData.
 *
 * TODO: Add capability to write messages from multiple channels to a single
 * display and implement it so that content of latest message is preserved
 * on the display for all channels.
 *
 * Mark message displays with a single tag. Obtain their broadcast channel(s)
 * from their CustomData!!!!!
 *
 * This script uses the Wico Modular IGC Example code (see below) for its IGC 
 * functions.
 *
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
// This tag must appear in the CustomNames of IMyTextPanels that 
// are reserved for displaying broadcast messages. The channel(s)
// they subscribe to must be listed in their CustomData fields.
private string DISPLAY_TAG = "[MSG_DISPLAY]";
// These are the broadcast channels listed in this script's
// PB's config in its CustomData field.
private List<string> broadcastTags = new List<string>();
// List of displays for each broadcast channel/tag. Note that
// there will be overlap for displays that subscribe to multiple
// channels and that's ok.
private Dictionary<string, List<IMyTextPanel>> displays = new Dictionary<string, List<IMyTextPanel>>();
// Dictionary with keys that are broadcast channel tags and values that are strings 
// that hold the content of the most recent message that was received.
private Dictionary<string, string> buffers = new Dictionary<string, string>();
public ConfigFile config;


/// <summary>
/// The combined set of UpdateTypes that count as a 'trigger'
/// </summary>
UpdateType _utTriggers = UpdateType.Terminal | UpdateType.Trigger | UpdateType.Mod | UpdateType.Script;

/// <summary>
/// the combined set of UpdateTypes and count as an 'Update'
/// </summary>
UpdateType _utUpdates = UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100 | UpdateType.Once;

public Program(){
    _wicoIGC = new WicoIGC(this);
    config = new ConfigFile(Me, this);

    // Register properties for the configuration.
    // These are our unique IDs for our messages.  We've defined the 
    // format for the message data (it's just a string)
    config.RegisterProperty("BroadcastTags", ConfigValueType.StringList, new List<string> {"[GANTRY_STATUS]", "[GANTRY_INVENTORY]"});
    // Finalize the config to lock the schema.
    config.FinalizeRegistration();

    // Get list of Broadcast Tags from ConfigFile.
    broadcastTags = config.Get<List<string>>("BroadcastTags"); 

    // Get displays that have been labeled to display messages
    // for specific broadcast tags (channels.) Also init
    // the buffers.
    foreach(var broadcastTag in broadcastTags){
       displays[broadcastTag] = FindDisplayPanels(broadcastTag);
       buffers[broadcastTag] = "";
    }

    // cause ourselves to run again so we can do the init
    Runtime.UpdateFrequency = UpdateFrequency.Once;
}

/// <summary>
/// Has everything been initialized?
/// </summary>
bool _areWeInited=false;

public void Main(string argument, UpdateType updateSource){
    // Echo some information about 'me' and why we were run
    Echo("Source=" + updateSource.ToString());
    Echo("Me=" + Me.EntityId.ToString("X"));
    Echo(Me.CubeGrid.CustomName);

    if(!_areWeInited){
        InitMessageHandlers();
        _areWeInited = true;
    }

    // always check for IGC messages in case some aren't using callbacks
    _wicoIGC.ProcessIGCMessages();
    if((updateSource & UpdateType.IGC) > 0){
        // We got a callback for an IGC message.
        // but we already processed them.
    }else if((updateSource & _utTriggers) > 0){
        // STUB: This script does not currently publish messages.
        // if we got a 'trigger' source, send out the received argument
        // IGC.SendBroadcastMessage(_StatusBroadcastTag, argument);
        // Echo("Sending Message:\n" + argument);
    }else if((updateSource & _utUpdates) > 0){
        // it was an automatic update
        // this script doesn't have anything to do
    }
}

/// <summary>
/// Discover display panels associated with the specified broadcast channel.
/// <param name="channelTag">The tag for the channel. This should be unique to the use of the channel.</param>
/// <returns> List<IMyTextPanels> - List of display panels associated with provided broadcast channel.</returns>
/// </summary>
private List<IMyTextPanel> FindDisplayPanels(string tag){
    List<IMyTextPanel> panels = new List<IMyTextPanel>();
    GridTerminalSystem.GetBlocksOfType(panels, p => p.CustomName.Contains(DISPLAY_TAG) && p.IsSameConstructAs(Me));
    List<IMyTextPanel> subscribedPanels = new List<IMyTextPanel>();
    foreach(var panel in panels){
       List<string> channels = GetDisplayChannels(panel);
       if(channels.Count > 0 && channels.Contains(tag)){
          AddChannelBuffer(panel, tag);
          subscribedPanels.Add(panel);
       }
    }
    return subscribedPanels;
}        

/// <summary>
/// Write message content to the relevant displays.
/// <param name="tag"> Broadcast channel tag that specifies which displays are to be updated.</param>
/// <param name="data"> Message content that is to be written to the displays.</param>
/// </summary>
private void UpdateDisplays(string tag, string data){
    // If we have display panels, update them
    foreach(var display in displays[tag]){
       // Check buffer for this tag and use StringBuilder object to update
       // the message data for the provided channel.
       StringBuilder sb = new StringBuilder();
       foreach(var channel in GetDisplayChannels(display, tag)){
          if(channel == tag){
             sb.AppendLine(data);
             UpdateChannelBuffer(display, tag, data);
          }else{
             sb.AppendLine(GetChannelBuffer(display, channel));
          }
       }
       display.ContentType = ContentType.TEXT_AND_IMAGE;
       display.WriteText(sb.ToString());
    }
}

/// <summary>
/// Update display's buffer text for the provided channel.
/// <param name="display"> IMyTextPanel to update buffer for.</param>
/// <param name="tag"> string containing the broadcast channel tag.</param>
/// <param name="data"> Message content that is to be written buffer.</param>
/// </summary>
/// Need a way to track parsing phases in UpdateChannelBuffer(). 
private enum Semaphore {LOOKING, REPLACE, REPLACED, DONE};
private void UpdateChannelBuffer(IMyTextPanel display, string tag, string data){
   var lines = display.CustomData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
   StringBuilder sb = new StringBuilder();
   Semaphore semaphore = Semaphore.LOOKING;
   foreach(var line in lines){
      // If we hit the opening tag for the buffer, then signal
      // that the following lines are to be overwritten with
      // the new data and make sure the opening tag is included.
      if(line.TrimStart().StartsWith("<"+tag.ToLower()+">")){
         semaphore = Semaphore.REPLACE;
         sb.AppendLine(line);
         continue;
      // If the closing tag for the buffer is hit, then signal that
      // we are done overwriting the buffer and make sure the closing
      // tag is included.
      }else if(line.TrimStart().StartsWith("</"+tag.ToLower()+">")){
         semaphore = Semaphore.DONE;
         sb.AppendLine(line);
         continue;
      // If we have found the buffer, then overwrite it.
      }else if(semaphore == Semaphore.REPLACE){
         semaphore = Semaphore.REPLACED;
         sb.AppendLine(data);
         continue;
      }

      // If this line is not part of the buffer, then make sure it
      // gets transfered.
      if(semaphore == Semaphore.LOOKING || semaphore == Semaphore.DONE ){
         sb.AppendLine(line);
      }
   }

   display.CustomData = sb.ToString();
}

/// <summary>
/// Get the display's buffer text for the provided channel.
/// <param name="display"> IMyTextPanel to pull buffer from.</param>
/// <param name="tag"> string containing the broadcast channel tag.</param>
/// </summary>
private string GetChannelBuffer(IMyTextPanel display, string tag){
   var lines = display.CustomData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
   StringBuilder sb = new StringBuilder();
   bool readBuffer = false;
   foreach(var line in lines){
      if(line.TrimStart().StartsWith("<"+tag.ToLower()+">")){
         readBuffer = true;
         continue;
      }else if(line.TrimStart().StartsWith("</"+tag.ToLower()+">")){
         break;
      }

      if(readBuffer){
         sb.AppendLine(line);
      }
   }
   return sb.ToString();
}

/// <summary>
/// If the provided IMyTextPanel does not have a buffer for the specified
/// broadcast channel tag, then add it.
/// <param name="display"> IMyTextPanel to add buffer to.</param>
/// <param name="tag"> string containing the broadcast channel tag.</param>
/// </summary>
private void AddChannelBuffer(IMyTextPanel display, string tag){
   var lines = display.CustomData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
   // Check to see if we already have a buffer for this channel.
   foreach(var line in lines){
      if(line.Contains(tag.ToLower())){
         return;
      }
   }
   // If we don't have a buffer, then add one.
   string channelTagId = tag.ToLower();
   string channelTag = "\n<" + channelTagId + ">" + tag + " Buffer</" + channelTagId + ">";
   String.Concat(display.CustomData, channelTag); 
}

/// <summary>
/// Obtain list of broadcast channels for provided display.
/// <param name="display"> IMyTextPanel whose channel tags must be extracted.</param>
/// <returns> List<string> - List of broadcast channels for the display.</returns>
/// </summary>
private List<string> GetDisplayChannels(IMyTextPanel display){
   // Parse CustomData for this display in order to obtain channels.
   var lines = display.CustomData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
   List<string> channels = new List<string>();
   foreach(var line in lines){
      // Skip blank or commented lines.
      if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")){
         continue;
      }
      // If the line bearing the channel names is found.
      if(line.TrimStart().StartsWith("Channels")){
         var fields = line.Split(":",StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
         if(fields.Length == 2){
             channels.AddRange(fields[1].Split(",",StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
         }else{
            Echo("WARNING: No channel tags defined for " + display.CustomName + "!");
         }
         break;
      }
   }

   return channels;
}

/// <summary>
/// Register handlers for messages on the broadcast channels.
/// </summary>
private void InitMessageHandlers(){
    // Create handlers for messages received on the broadcast channels.
    foreach(var broadcastTag in broadcastTags){
       _wicoIGC.AddPublicHandler(broadcastTag, BroadcastHandler);
    }
}

/// <summary>
/// Handler for the broadcast messages.
/// <param name="msg"> MyIGCMessage object containing meta data and message payload.</param>
/// </summary>
private void BroadcastHandler(MyIGCMessage msg){
    // NOTE: called on ALL received messages; not just 'our' tag

    // Only process messages on recognized channels.
    if(!broadcastTags.Contains(msg.Tag)){
        Echo("WARNING: Unrecognized tag (" + msg.Tag + ").");
        return; // not our message
    }

    if(msg.Data is string){
        Echo("Received Test Message");
        Echo(" Source=" + msg.Source.ToString("X"));
        Echo(" Data=\"" + msg.Data + "\"");
        Echo(" Tag=" + msg.Tag);
        UpdateDisplays(msg.Tag, msg.Data.ToString());
    }
}

// Source is available from: https://github.com/Wicorel/WicoSpaceEngineers/tree/master/Modular/IGC
public class WicoIGC{
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
    public WicoIGC(MyGridProgram myProgram, bool debug = false){
        _gridProgram = myProgram;
        _debug = debug;
        _debugTextPanel = _gridProgram.GridTerminalSystem.GetBlockWithName("IGC Report") as IMyTextPanel;
        if(_debug) _debugTextPanel?.WriteText("");
    }

    /// <summary>
    /// Call to add a handler for public messages.  Also registers the tag with IGC for reception.
    /// </summary>
    /// <param name="channelTag">The tag for the channel.  This should be unique to the use of the channel.</param>
    /// <param name="handler">The handler for messages when received. Note that this handler will be called with ALL broadcast messages; not just the one from ChannelTag</param>
    /// <param name="setCallback">Should a callback be set on the channel. The system will call Main() when the IGC message is received.</param>
    /// <returns></returns>
    public bool AddPublicHandler(string channelTag, Action<MyIGCMessage> handler, bool setCallback = true){
        IMyBroadcastListener publicChannel;
        // IGC Init
        publicChannel = _gridProgram.IGC.RegisterBroadcastListener(channelTag); // What it listens for
        if(setCallback) publicChannel.SetMessageCallback(channelTag); // What it will run the PB with once it has a message

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
    public bool AddUnicastHandler(Action<MyIGCMessage> handler){
        _unicastListener = _gridProgram.IGC.UnicastListener;
        _unicastListener.SetMessageCallback("UNICAST");
        _unicastMessageHandlers.Add(handler);
        return true;
    }

    /// <summary>
    /// Process all pending IGC messages.
    /// </summary>
    public void ProcessIGCMessages(){
        bool bFoundMessages = false;
        if(_debug) _gridProgram.Echo(_broadcastChannels.Count.ToString() + " broadcast channels");
        if(_debug) _gridProgram.Echo(_broadcastMessageHandlers.Count.ToString() + " broadcast message handlers");
        if(_debug) _gridProgram.Echo(_unicastMessageHandlers.Count.ToString() + " unicast message handlers");
        // TODO: make this a yield return thing if processing takes too long
        do{
            bFoundMessages = false;
            foreach(var channel in _broadcastChannels){
                if(channel.HasPendingMessage){
                    bFoundMessages = true;
                    var msg = channel.AcceptMessage();
                    if(_debug){
                        _gridProgram.Echo("Broadcast received. TAG:" + msg.Tag);
                        _debugTextPanel?.WriteText("IGC:" +msg.Tag+" SRC:"+msg.Source.ToString("X")+"\n",true);
                    }
                    foreach(var handler in _broadcastMessageHandlers){
                        handler(msg);
                    }
                }
            }
        }while (bFoundMessages); // Process all pending messages

        if(_unicastListener != null){
            // TODO: make this a yield return thing if processing takes too long
            do{
                // since there's only one channel, we could just use .HasPendingMessages directly.. but this keeps the code loops the same
                bFoundMessages = false;

                if(_unicastListener.HasPendingMessage){
                    bFoundMessages = true;
                    var msg = _unicastListener.AcceptMessage();
                    if(_debug) _gridProgram.Echo("Unicast received. TAG:" + msg.Tag);
                    foreach(var handler in _unicastMessageHandlers){
                        // Call each handler
                        handler(msg);
                    }
                }
            }while (bFoundMessages); // Process all pending messages
        }
    }
}

/* v ---------------------------------------------------------------------- v */
/* v Caml Config File API                                                   v */
/* v ---------------------------------------------------------------------- v */
// The supported property types.
public enum ConfigValueType
{
    Int,
    Float,
    String,
    ListInt,
    ListFloat,
    ListString
}

// A definition for one config property.
public class ConfigProperty
{
    public string Name;
    public ConfigValueType ValueType;
    public object DefaultValue;
    public object Value; // Will be filled after parsing
}

/// <summary>
/// A configuration file API that supports root-level properties and one level of sub configurations.
/// Sub configs are automatically created by name. A centralized error logging system is provided,
/// plus a finalization phase that prevents usage until registration is complete.
/// </summary>
public class ConfigFile
{
    // Fields for the programmable block and program.
    private IMyProgrammableBlock pb;
    private MyGridProgram program;

    // Dictionaries for root-level properties and sub configurations.
    private Dictionary<string, ConfigProperty> properties = new Dictionary<string, ConfigProperty>();
    private Dictionary<string, ConfigFile> subConfigs = new Dictionary<string, ConfigFile>();

    // Raw config text for change detection.
    private string configText = null;

    // Internal flag to indicate that registration has been finalized.
    private bool isFinalized = false;

    // A logging delegate which scripts can set to route errors as they wish.
    // By default, it is set to a no‑op.
    public Action<string> Logger { get; set; } = message => { };

    /// <summary>
    /// Constructor that initializes the configuration file with a programmable block and program.
    /// </summary>
    public ConfigFile(IMyProgrammableBlock pb, MyGridProgram program)
    {
        this.pb = pb;
        this.program = program;
    }

    /// <summary>
    /// Checks that this instance has been finalized; if not, throws an exception.
    /// </summary>
    /// <returns>True if finalized, false otherwise.</returns>
    private bool EnsureFinalized()
    {
        if (!isFinalized) {
            Logger("Configuration file has not been finalized. Call FinalizeRegistration() before using the config.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Registers a new root-level configuration property.
    /// </summary>
    public void RegisterProperty(string name, ConfigValueType type, object defaultValue)
    {
        if (isFinalized)
        {
            Logger("Cannot register new property after finalization: " + name);
            return;
        }
        if (properties.ContainsKey(name))
        {
            Logger("Property already registered: " + name);
            return;
        }
        properties[name] = new ConfigProperty
        {
            Name = name,
            ValueType = type,
            DefaultValue = defaultValue,
            Value = null
        };
    }

    /// <summary>
    /// Overloaded method to register a property into a sub configuration.
    /// Logs an error if the sub config does not exist.
    /// </summary>
    public void RegisterProperty(string subConfigName, string name, ConfigValueType type, object defaultValue)
    {
        if (isFinalized)
        {
            Logger("Cannot register new property after finalization: " + subConfigName + "/" + name);
            return;
        }
        if (!subConfigs.ContainsKey(subConfigName))
        {
            Logger("Sub config '" + subConfigName + "' does not exist. Register the sub config first.");
            return;
        }
        // Forward the registration to the sub config.
        subConfigs[subConfigName].RegisterProperty(name, type, defaultValue);
    }

    /// <summary>
    /// Registers a sub configuration by name.
    /// Internally creates a new ConfigFile instance for the sub config.
    /// </summary>
    public void RegisterSubConfig(string name)
    {
        if (isFinalized)
        {
            Logger("Cannot register new sub config after finalization: " + name);
            return;
        }
        if (subConfigs.ContainsKey(name))
        {
            Logger("Sub config already registered: " + name);
            return;
        }
        // Create a new sub config with the same programmable block and program.
        subConfigs[name] = new ConfigFile(pb, program);
    }

    /// <summary>
    /// Finalizes the registration process.
    /// This checks that every registered sub config contains at least one property.
    /// It then marks the config file as ready for use and writes default config if necessary.
    /// </summary>
    ///
    public bool FinalizeRegistration()
    {
        if (isFinalized)
        {
            Logger("Configuration file already finalized.");
            return false;
        }

        // Finalize all sub configs first.
        foreach (var kvp in subConfigs)
        {
            ConfigFile subCfg = kvp.Value;
            // Finalize sub config registrations recursively.
            if (!subCfg.FinalizeRegistration());
            {
                Logger($"Failed to finalize sub config '{kvp.Key}'.");
                return false;
            }
            // Ensure the sub config has at least one property or sub config.
            if (subCfg.properties.Count == 0 && subCfg.subConfigs.Count == 0)
            {
                Logger($"Sub config '{kvp.Key}' is empty. It must contain at least one property.");
                return false;
            }
        }

        isFinalized = true;
        // Write default config to CustomData if needed.
        if(!CheckAndWriteDefaults())
        {
            Logger("Failed to write default config to CustomData.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Generates the default CAML config text, including both root properties and sub configurations.
    /// Sub config blocks are indented using their own default indent (typically detected at parse time).
    /// </summary>
    /// <returns>The generated config text. Null if we failed to generate.</returns>
    public string GenerateDefaultConfigText()
    {
        if (!EnsureFinalized()) return null;

        StringBuilder sb = new StringBuilder();
        // Output root-level properties.
        foreach (var kvp in properties)
        {
            string valueStr = ValueToString(kvp.Value.DefaultValue, kvp.Value.ValueType);
            sb.AppendLine($"{kvp.Key}: {valueStr}");
        }
        // Output sub config blocks.
        foreach (var kvp in subConfigs)
        {
            sb.AppendLine($"{kvp.Key}:");
            string subText = kvp.Value.GenerateDefaultConfigText();
            // Indent each line by two spaces (default output format for generated text).
            using (StringReader sr = new StringReader(subText))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    sb.AppendLine("  " + line);
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Parses a CAML configuration string.
    /// Supports root-level properties and one level of sub configs with dynamic indent detection.
    /// Returns true if no errors occurred;
    /// </summary>
    public bool ParseConfig(string configText)
    {
        if (!EnsureFinalized()) return false;

        // Split the text into lines.
        var lines = configText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        // For sub configs, accumulate each sub config’s indented text into blocks.
        Dictionary<string, StringBuilder> subConfigBlocks = new Dictionary<string, StringBuilder>();
        // Record the expected indent (in spaces) for each sub config.
        Dictionary<string, int> subConfigIndentSizes = new Dictionary<string, int>();

        // For root-level properties.
        List<string> rootLines = new List<string>();

        string currentSubConfigName = null;
        foreach (var line in lines)
        {
            // Skip blank or comment lines.
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                continue;

            int indent = CountLeadingSpaces(line);
            if (indent == 0)
            {
                // At root level.
                string trimmed = line.Trim();
                int colonIndex = trimmed.IndexOf(':');
                // Sub config header (no value) is detected if the colon is the last character.
                if (colonIndex >= 0 && colonIndex == trimmed.Length - 1)
                {
                    currentSubConfigName = trimmed.Substring(0, colonIndex).Trim();
                    if (!subConfigs.ContainsKey(currentSubConfigName))
                    {
                        Logger("Unknown sub config: " + currentSubConfigName);
                        return false;
                    }
                    if (!subConfigBlocks.ContainsKey(currentSubConfigName))
                    {
                        subConfigBlocks[currentSubConfigName] = new StringBuilder();
                    }
                    // Reset any previously recorded indent for this block.
                    if (subConfigIndentSizes.ContainsKey(currentSubConfigName))
                        subConfigIndentSizes.Remove(currentSubConfigName);
                }
                else
                {
                    // Regular root property.
                    currentSubConfigName = null;
                    rootLines.Add(line);
                }
            }
            else
            {
                // Indented line: should belong to an active sub config.
                if (currentSubConfigName == null)
                {
                    Logger("Unexpected indentation without an active sub config header: " + line);
                    return false;
                }
                else
                {
                    // Determine or enforce the expected indent size for this sub config.
                    int expectedIndent;
                    if (!subConfigIndentSizes.TryGetValue(currentSubConfigName, out expectedIndent))
                    {
                        // First indented line under this sub config.
                        expectedIndent = indent;
                        subConfigIndentSizes[currentSubConfigName] = expectedIndent;
                    }
                    else if (indent != expectedIndent)
                    {
                        Logger($"Inconsistent indentation for sub config '{currentSubConfigName}'. Expected {expectedIndent} spaces but found {indent} spaces in line: {line}");
                        return false;
                    }

                    if (line.Length >= expectedIndent)
                    {
                        string subLine = line.Substring(expectedIndent);
                        subConfigBlocks[currentSubConfigName].AppendLine(subLine);
                    }
                    else
                    {
                        Logger("Line is indented but too short relative to the expected indent: " + line);
                        return false;
                    }
                }
            }
        }

        // Process root-level lines.
        HashSet<string> encounteredRoot = new HashSet<string>();
        foreach (var line in rootLines)
        {
            string trimmed = line.Trim();
            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex < 0)
            {
                Logger($"Syntax error in root property (missing ':'): {line}");
                return false;
            }
            string key = trimmed.Substring(0, colonIndex).Trim();
            string valuePart = trimmed.Substring(colonIndex + 1).Trim();
            if (encounteredRoot.Contains(key))
            {
                Logger($"Duplicate root property: {key}");
                return false;
            }
            encounteredRoot.Add(key);

            if (!properties.ContainsKey(key))
            {
                Logger($"Unknown root property: {key}");
                return false;
            }
            var prop = properties[key];
            object parsedValue;
            if (!ParseValue(key, valuePart, prop.ValueType, out parsedValue))
            {
                Logger($"Failed to parse root property '{key}' with value '{valuePart}'");
                return false;
            }
            prop.Value = parsedValue;
        }

        // Process sub config blocks.
        foreach (var kvp in subConfigBlocks)
        {
            string subName = kvp.Key;
            string subText = kvp.Value.ToString();
            var subConfig = subConfigs[subName];
            if (!subConfig.ParseConfig(subText))
            {
                Logger($"Errors in sub config '{subName}':");
                return false;
            }
        }

        // Verify that every required root property was encountered; assign default values if missing.
        foreach (var kvp in properties)
        {
            if (!encounteredRoot.Contains(kvp.Key))
            {
                Logger($"Missing root property: {kvp.Key}. Using default value.");
                kvp.Value.Value = kvp.Value.DefaultValue;
                return false;
            }
        }

        // Update the stored config text if no errors occurred.
        this.configText = configText;
        return true;
    }

    /// <summary>
    /// Retrieves the value of a root property.
    /// If the property is missing or a type mismatch occurs, logs an error and returns default(T).
    /// </summary>
    public T Get<T>(string key)
    {
        if (!EnsureFinalized()) return default(T);

        if (!properties.ContainsKey(key))
        {
            Logger("Unknown property requested: " + key);
            return default(T);
        }
        try
        {
            return (T)properties[key].Value;
        }
        catch
        {
            Logger("Type mismatch for property: " + key);
            return default(T);
        }
    }

    /// <summary>
    /// Retrieves a sub configuration instance.
    /// Returns null if the sub config is not registered.
    /// </summary>
    /// <param name="name">A sub config instance. Null if failed to get sub config.</param>
    public ConfigFile GetSubConfig(string name)
    {
        if (!EnsureFinalized()) return null;

        if (!subConfigs.ContainsKey(name))
        {
            Logger("Unknown sub config requested: " + name);
            return null;
        }
        return subConfigs[name];
    }

    /// <summary>
    /// Gets a reference to a sub configuration by name.
    /// Can be called before finalization.
    /// Returns null if the sub config is not registered.
    /// </summary>
    public ConfigFile GetSubConfigRef(string name)
    {
        if (!subConfigs.ContainsKey(name))
        {
            Logger("Unknown sub config requested: " + name);
            return null;
        }
        return subConfigs[name];
    }

    /// <summary>
    /// Checks the programmable block's CustomData.
    /// If empty, writes the default config.
    /// </summary>
    /// <returns>True if the config was written or already present.</returns>
    public bool CheckAndWriteDefaults()
    {
        if (!EnsureFinalized()) return false;

        if (string.IsNullOrWhiteSpace(pb.CustomData))
        {
            string defaultConfig = GenerateDefaultConfigText();
            pb.CustomData = defaultConfig;
            Logger("No configuration data found. Default config added to Custom Data.");
            // going to return true anyway since we wrote the default config
        }
        return true;
    }

    /// <summary>
    /// Checks if the CustomData has changed since the last parse, and if so, re-parses it.
    /// </summary>
    /// <returns>True if the config was re-parsed.</returns>
    public bool CheckAndReparse()
    {
        if (!EnsureFinalized()) return false;

        if (string.IsNullOrWhiteSpace(pb.CustomData))
            return false;

        if (configText == null || pb.CustomData != configText)
        {
            if (!ParseConfig(pb.CustomData))
            {
                return false;
            }
            configText = pb.CustomData;
        }
        return true;
    }

    /// <summary>
    /// Converts a value to its string representation, based on its type.
    /// </summary>
    private string ValueToString(object value, ConfigValueType type)
    {
        switch (type)
        {
            case ConfigValueType.Int:
            case ConfigValueType.Float:
            case ConfigValueType.String:
                return value.ToString();
            case ConfigValueType.ListInt:
                var listInt = value as List<int>;
                return $"[{string.Join(", ", listInt)}]";
            case ConfigValueType.ListFloat:
                var listFloat = value as List<float>;
                return $"[{string.Join(", ", listFloat)}]";
            case ConfigValueType.ListString:
                var listStr = value as List<string>;
                return $"[{string.Join(", ", listStr)}]";
            default:
                return "";
        }
    }

    /// <summary>
    /// Counts the number of leading spaces in the provided line.
    /// </summary>
    private int CountLeadingSpaces(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ')
                count++;
            else
                break;
        }
        return count;
    }

    /// <summary>
    /// Parses a value string into the expected type.
    /// </summary>
    private bool ParseValue(string key, string valuePart, ConfigValueType type, out object parsedValue)
    {
        parsedValue = null;
        bool success = false;
        switch (type)
        {
            case ConfigValueType.Int:
                {
                    int intResult;
                    success = int.TryParse(valuePart, out intResult);
                    parsedValue = intResult;
                }
                break;
            case ConfigValueType.Float:
                {
                    float floatResult;
                    success = float.TryParse(valuePart, out floatResult);
                    parsedValue = floatResult;
                }
                break;
            case ConfigValueType.String:
                {
                    parsedValue = valuePart;
                    success = true;
                }
                break;
            case ConfigValueType.ListInt:
                {
                    if (valuePart.StartsWith("[") && valuePart.EndsWith("]"))
                    {
                        string inner = valuePart.Substring(1, valuePart.Length - 2);
                        var items = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        List<int> list = new List<int>();
                        success = true;
                        foreach (var item in items)
                        {
                            int itemInt;
                            if (int.TryParse(item.Trim(), out itemInt))
                            {
                                list.Add(itemInt);
                            }
                            else
                            {
                                success = false;
                                break;
                            }
                        }
                        parsedValue = list;
                    }
                }
                break;
            case ConfigValueType.ListFloat:
                {
                    if (valuePart.StartsWith("[") && valuePart.EndsWith("]"))
                    {
                        string inner = valuePart.Substring(1, valuePart.Length - 2);
                        var items = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        List<float> list = new List<float>();
                        success = true;
                        foreach (var item in items)
                        {
                            float itemFloat;
                            if (float.TryParse(item.Trim(), out itemFloat))
                            {
                                list.Add(itemFloat);
                            }
                            else
                            {
                                success = false;
                                break;
                            }
                        }
                        parsedValue = list;
                    }
                }
                break;
            case ConfigValueType.ListString:
                {
                    if (valuePart.StartsWith("[") && valuePart.EndsWith("]"))
                    {
                        string inner = valuePart.Substring(1, valuePart.Length - 2);
                        var items = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        List<string> list = new List<string>();
                        success = true;
                        foreach (var item in items)
                        {
                            list.Add(item.Trim());
                        }
                        parsedValue = list;
                    }
                }
                break;
            default:
                success = false;
                break;
        }
        return success;
    }
}
/* ^ ---------------------------------------------------------------------- ^ */
/* ^ Caml Config File API                                                   ^ */
/* ^ ---------------------------------------------------------------------- ^ */
