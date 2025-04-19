// Global constants
public const string COMMAND_REGEX_PATTERN = @"^@(?<block>[^?]+)\?(?<payload>.*)$";

// Global variables
public GridRouter router;

public class BlockMessage{
    // URI components
    public string block;
    public string payload;

    /// <summary>
    /// Constructor for the BlockMessage class.
    /// 
    /// The constructor takes a block message string in the form of
    /// 
    ///  "@[block]?[payload]" 
    /// 
    /// and splits it into its components for validation.
    /// </summary>
    /// <param name="message">The message string representation.</param>
    /// <returns>True if the message is valid, otherwise false.</returns>
    public BlockMessage(string message)
    {
        block = null;
        payload = null;
        // Split the URI into its components
        var match = System.Text.RegularExpressions.Regex.Match(message, COMMAND_REGEX_PATTERN);
        if (!match.Success)
        {
            return;
        }
        block = match.Groups["block"].Value;
        payload = match.Groups["payload"].Value;
        return;
    }

    public bool IsValid()
    {
        return block != null && payload != null;
    }
}

public class GridRouter
{
    // Reference to the main program instance
    private MyGridProgram program;
    // Reference to the grid this programmable block is on
    private IMyCubeGrid grid;
    // The grid name
    private string gridName;

    // Constructor
    public GridRouter(MyGridProgram program)
    {
        this.program = program;
        Initialize();
        if (!IsInitialized())
        {
            program.Echo("Router not initialized. Exiting.");
        }
        program.Echo($"Router initialized on grid: {gridName}");
    }

    /// <summary>
    /// Check if the router is initialized.
    /// </summary>
    public bool IsInitialized()
    {
        return grid != null;
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
    /// Initialize the modem for this grid.
    /// </summary>
    private void Initialize()
    {
         // Get the grid this programmable block is on
        GetGrid();
    }

    /// <summary>
    /// search this grid for a programmable block by name.
    /// </summary>
    /// <param name="blockName">The name of the block to search for.</param>
    private IMyProgrammableBlock GetTargetBlock(string blockName)
    {
       
        var blocks = new List<IMyTerminalBlock>();
        program.GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(blocks, block => block.CustomName.Equals(blockName) && block.CubeGrid == grid);
        return blocks.Count > 0 ? blocks[0] as IMyProgrammableBlock : null;
    }

    /// <summary>
    /// Route a message to a target block on this grid.
    /// </summary>
    /// <param name="raw_message">The raw message string.</param>
    public void RouteMessage(string raw_message)
    {
        // Parse the message
        BlockMessage message = new BlockMessage(raw_message);

        // Check if the message is valid
        if (!message.IsValid())
        {
            program.Echo("Invalid message format. Please use the format: @block?[payload]");
            return;
        }

        // Get the target block
        IMyProgrammableBlock block = GetTargetBlock(message.block);

        // Check if the target block is valid and functional
        if (block != null && block.IsFunctional)
        {
            block.TryRun(message.payload);
            program.Echo($"Message routed to {block.CustomName}");
        }
        else
        {
            program.Echo("Target block is not functional or does not exist.");
        }
    }
}

public Program()
{  
    Echo("Initializing Grid Router...");
   router = new GridRouter(this);
}

public void Main(string argument, UpdateType updateSource)
{
    // Check if the router is initialized
    if (!router.IsInitialized())
    {
        Echo("Router not initialized. Exiting.");
        return;
    }
    // route the message to the target block
    router.RouteMessage(argument);
    return;
}