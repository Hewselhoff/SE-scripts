/* v --------------------------------------------------------------------- v */
/* GridNet NIC                                                               */
/* v --------------------------------------------------------------------- v */
public class GridNIC
{
    // Constants
    private const string MODEM_NAME_TAG = "[MODEM]";
    private const string URI_PROTOCOL = "grid";
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
    /// <param name="grid">the destination grid name.</param>
    /// <param name="block">the name of the destination programmable block.</param>
    /// <param name="payload">the message payload</param>
    public void Send(string grid, string block, string payload)
    {

        if (IsConnected())
        {
            // Assemble URI
            program.Echo($"Sending message to {block} on {grid}");
            string uri = $"{URI_PROTOCOL}://{grid}/{block}?{payload}";
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