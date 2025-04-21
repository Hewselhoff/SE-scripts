// Global constants
public const string BLOCK_URI_PROTOCOL = "block";
public const string SERVER_URI_PROTOCOL = "server";
public const string BLOCK_URI_REGEX_PATTERN = $@"^{BLOCK_URI_PROTOCOL}://(?<pb_name>[^/?]+)(/(?<endpoint_path>[^?]*))?(\?(?<query_string>.*))?$";


// Global variables
public GridRouter router;

public class BlockURI
{
    // URI components
    public string pb_name;
    private string endpoint_path;
    private string query_string;

    public BlockURI(string block_uri_string)
    {
        pb_name = null;
        endpoint_path = null;
        query_string = null;

        // Split the URI into its components
        var match = System.Text.RegularExpressions.Regex.Match(block_uri_string, BLOCK_URI_REGEX_PATTERN);
        if (!match.Success)
        {
            return;
        }
        pb_name = match.Groups["pb_name"].Value;
        endpoint_path = match.Groups["endpoint_path"].Value;
        query_string = match.Groups["query_string"].Value;
    }

    public bool IsValid()
    {
        return pb_name != null;
    }

    public bool IsValidServerURI()
    {
        return IsValid() && endpoint_path != null;
    }

    private string CompileServerUriString()
    {
        string data = $"{SERVER_URI_PROTOCOL}://endpoint_path}";
        if (query_string != null)
        {
            data += $"?{query_string}";
        }
        return data;
    }

    public string CompileBlockArgument()
    {
        if (IsValidServerURI())
        {
            return CompileServerUriString();
        }
        else
        {
            return $"{query_string}";
        }
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
    /// <param name="block_uri_string">The raw block URI string.</param>
    public void RouteMessage(string block_uri_string)
    {
        // Parse the block URI string
        BlockURI block_uri = new BlockURI(block_uri_string);

        // Check if the URI is valid
        if (!block_uri.IsValid())
        {
            program.Echo("Invalid block URI format. Please use the format: block://[block_name]?[payload]");
            return;
        }

        // Get the target block
        IMyProgrammableBlock target_block = GetTargetBlock(block_uri.pb_name);

        // Check if the target block is valid and functional
        if (target_block != null && target_block.IsFunctional)
        {
            target_block.TryRun(block_uri.CompileBlockArgument);
            program.Echo($"Message routed to {target_block.CustomName}");
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