// Global constants
public const string IGC_TAG_PREFIX = "NET";
public const string URI_PROTOCOL = "grid";
public const string URI_REGEX_PATTERN = @"^grid://(?<grid>[^/]+)/(?<block>[^?]+)\?(?<payload>.*)$";
public const string ROUTER_NAME_TAG = "[ROUTER]";

// Global variables
public GridModem modem;


public class GridURI{
    // URI components
    public string grid;
    public string block;
    public string payload;

    /// <summary>
    /// Constructor for the GridURI class.
    /// 
    /// The constructor takes a grid URI in the form of
    /// 
    ///  "grid://[grid]/[block]?[payload]" 
    /// 
    /// and splits it into its components for validation.
    /// </summary>
    /// <param name="uri">The URI string representation".</param>
    /// <returns>True if the URI is valid, otherwise false.</returns>
    public GridURI(string uri)
    {
        grid = null;
        block = null;
        payload = null;

        // Split the URI into its components
        var match = System.Text.RegularExpressions.Regex.Match(uri, URI_REGEX_PATTERN);
        if (!match.Success)
        {
            return;
        }
        grid = match.Groups["grid"].Value;
        block = match.Groups["block"].Value;
        payload = match.Groups["payload"].Value;

        return;
    }

    public bool IsValid()
    {
        return grid != null && block != null && payload != null;
    }

    /// <summary>
    /// This method assembles the IGC tag string from the URI.
    /// </summary>
    /// <returns>The IGC tag string representation for the URI.</returns>
    public string CompileTag()
    {
        return $"{IGC_TAG_PREFIX}:{grid}";
    }

    /// <summary>
    /// This method assembles the IGC data string for the URI.
    /// </summary>
    /// <returns>The IGC data string representation of the URI.</returns>
    public string CompileData()
    {
        return $"@{block}?{payload}";
    }
}

public class GridModem
{
    // Reference to the main program instance
    private MyGridProgram program;
    // Reference to the grid this programmable block is on
    private IMyCubeGrid grid;
    // The grid name
    private string gridName;
    // IGC tag for this grid
    private string gridTag;
    // Reference to the grid router
    private IMyProgrammableBlock router;
    // IGC listener for this grid
    private IMyBroadcastListener broadcastListener;

    // Constructor
    public GridModem(MyGridProgram program)
    {
        this.program = program;
        this.router = null;
        Initialize();
        if (!IsInitialized())
        {
            this.program.Echo("Modem initialization failed.");
        }
        else
        {
            this.program.Echo("Modem initialized successfully.");
        }
    }

    public bool IsInitialized()
    {
        return grid != null && router != null && broadcastListener != null;
    }

    /// <summary>
    /// Get the grid this programmable block is on.
    /// </summary>
    private void GetGrid()
    {
        // Get the grid this programmable block is on
        grid = program.Me.CubeGrid;
        // Get the grid name
        gridName = grid.CustomName;
    }

    /// <summary>
    /// Get the grid router for this grid. The grid router is a programmable block 
    /// with the tag "[ROUTER]" in its name.
    /// </summary>
    /// <returns>True if the router was found, otherwise false.</returns>
    private bool GetRouter()
    {
        var blocks = new List<IMyTerminalBlock>();
        program.GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(blocks, block => block.CubeGrid == grid && block.CustomName.Contains(ROUTER_NAME_TAG));
        if (blocks.Count > 1)
        {
            program.Echo($"Multiple routers found on grid {gridName}. Please ensure only one router is present.");
            router = null;
        }
        else if (blocks.Count == 0)
        {
            program.Echo($"No router found on grid {gridName}. Please ensure a router is present.");
            router = null;
        }
        else
        {
            program.Echo($"Router found on grid {gridName}.");
            router = blocks[0] as IMyProgrammableBlock;
        }
        return router != null;
    }

    /// <summary>
    /// Initialize the modem for this grid.
    /// </summary>
    private void Initialize()
    {
         // Get the grid this programmable block is on
        GetGrid();
        // Get the grid router for this grid
        if (!GetRouter())
        {
            program.Echo("Router initialization failed. Exiting listener setup.");
            return;
        }
        // Set the IGC tag for this grid
        gridTag = IGC_TAG_PREFIX + ":" + gridName;
        // Initialize the IGC listener
        broadcastListener = program.IGC.RegisterBroadcastListener(gridTag);
        broadcastListener.SetMessageCallback(gridTag);
    }

    /// <summary>
    /// Send a message over the grid net.
    /// </summary>
    public void SendMessage(GridURI uri)
    {
        program.IGC.SendBroadcastMessage(uri.CompileTag(), uri.CompileData());
    }

    /// <summary>
    /// Handle messages received from the IGC.
    /// </summary>
    public void ServiceMessages()
    {
        // Check if there are any messages in the IGC listener
        while (broadcastListener.HasPendingMessage)
        {
            var message = broadcastListener.AcceptMessage();
            // Check if the message is for this grid
            if (message.Tag == gridTag)
            {
                // If message data is string
                if (message.Data is string)
                {
                    // call router with the message data as argument
                    router.TryRun(message.Data.ToString());
                }
            }
        }
    }
}

public Program()
{  
   Echo("Initializing grid modem...");
   modem = new GridModem(this);
}

public void Main(string argument, UpdateType updateSource)
{
    // Check if the modem is initialized
    if (!modem.IsInitialized())
    {
        return;
    }

    // If update was not from IGC..
   if (
        (updateSource & (UpdateType.Trigger | UpdateType.Terminal)) > 0
        || (updateSource & (UpdateType.Mod)) > 0 
        || (updateSource & (UpdateType.Script)) > 0
        )
    { 
        if (argument != "")
        {
            GridURI uri = new GridURI(argument);
            if (uri.IsValid() == false)
            {
                Echo("Invalid URI format. Please use the format: grid://[grid]/[block]?[payload]");
                return;
            }
            modem.SendMessage(uri);
        }

        return;
    }

    if( (updateSource & UpdateType.IGC) > 0)
    { 
        // Handle messages received from the IGC
        modem.ServiceMessages();
    }

    return;        
}