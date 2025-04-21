/* v --------------------------------------------------------------------- v */
/* GridNet NIC                                                               */
/* v --------------------------------------------------------------------- v */
public class GridNIC
{
    // Constants
    private const string MODEM_NAME_TAG = "[MODEM]";
    private const string GRID_URI_PROTOCOL = "grid";
    // Properties
    private MyGridProgram program;
    private IMyCubeGrid grid;
    private IMyProgrammableBlock modem;
    private IMyRadioAntenna antenna;

    // Constructor
    public GridNIC(MyGridProgram program)
    {
        this.program = program;
        Initialize();
    }

    /// <summary>
    /// Returns true if the NIC has a modem on its grid and is connected.
    /// </summary>
    public bool IsConnected()
    {
        return modem != null && antenna != null && antenna.EnableBroadcasting;
    }

    /// <summary>
    /// Get the grid this programmable block is on.
    /// </summary>
    private void GetGrid()
    {
        grid = program.Me.CubeGrid;
    }
    

    /// <summary>
    /// Finds the modem on the grid. The modem is a programmable block with the tag "[MODEM]" in its name.
    /// </summary>
    private void FindModem()
    {
        var modems = new List<IMyProgrammableBlock>();
        program.GridTerminalSystem.GetBlocksOfType(modems, modem => modem.CustomName.Contains(MODEM_NAME_TAG));
        if (modems.Count > 0)
        {
            modem = modems[0];
        }
        else
        {
            program.Echo("No modem found on the grid.");
        }
    }

    /// <summary>
    /// Checks for at least one antenna on the grid with broadcasting enabled.
    /// </summary>
    private void FindAntenna()
    {
        var antennas = new List<IMyRadioAntenna>();
        program.GridTerminalSystem.GetBlocksOfType(antennas, antenna => antenna.EnableBroadcasting);
        if (antennas.Count > 0)
        {
            antenna = antennas[0];
        }
        else
        {
            program.Echo("No antenna found on the grid.");
        }
    }

    /// <summary>
    /// Initializes the NIC by finding the modem and antenna on the grid.
    /// </summary>
    private void Initialize()
    {
        program.Echo("Initializing NIC...");
        GetGrid();
        FindModem();
        FindAntenna();
        if (!IsConnected())
        {
            program.Echo("NIC initialization failed. Modem or antenna not found.");
        }
        else if (modem == null || antenna == null)
        {
            program.Echo("NIC initialization failed. Modem or antenna not found.");
        }
        else
        {
            program.Echo("NIC initialized successfully.");
        }
    }

    /// <summary>
    /// Send a message over the network.
    /// </summary>
    /// <param name="grid_name">the destination grid name.</param>
    /// <param name="pb_name">the name of the destination programmable block name.</param>
    /// <param name="endpoint_path">optional endpoint path for Server PBs.</param>
    /// <param name="query_string">optional query string to use as the arguments for the destination PB</param>
    public void Send(string grid_name, string pb_name, string endpoint_path = null, string query_string = null)
    {
        if (IsConnected())
        {
            program.Echo($"Sending message to {pb_name} on {grid_name}");
            
            // Build the URI with proper formatting based on what's provided
            string uri = $"{GRID_URI_PROTOCOL}://{grid_name}/{pb_name}";
            
            // Add endpoint path if provided
            if (!string.IsNullOrEmpty(endpoint_path))
            {
                // Ensure endpoint_path starts with a slash
                if (!endpoint_path.StartsWith("/"))
                    endpoint_path = "/" + endpoint_path;
                uri += endpoint_path;
            }
            
            // Add query string if provided
            if (!string.IsNullOrEmpty(query_string))
            {
                // Ensure query_string starts with a question mark
                if (!query_string.StartsWith("?"))
                    uri += "?" + query_string;
                else
                    uri += query_string;
            }
            
            modem.TryRun(uri);
        }
        else
        {
            program.Echo("Cannot send message. NIC is not connected.");
        }
    }
}
/* ^ --------------------------------------------------------------------- ^ */
/* GridNet NIC                                                               */
/* ^ --------------------------------------------------------------------- ^ */