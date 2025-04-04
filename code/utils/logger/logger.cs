/* v ---------------------------------------------------------------------- v */
/* v Logging API                                                            v */
/* v ---------------------------------------------------------------------- v */
/// <summary>
/// Logger class provides a simple logging interface with three log levels.
/// It writes output to LCD panels tagged with "[LOG]" on the same grid,
/// falls back to program.Echo if none are found (if enabled), and optionally logs
/// to the programmable block's CustomData (keeping only the 100 most recent messages).
/// </summary>
public class Logger
{
    // Reference to the parent MyGridProgram.
    private MyGridProgram program;
    // List of LCD panels to which logs will be written.
    private List<IMyTextPanel> lcdPanels;
    // Internal log message storage.
    private List<string> messages = new List<string>();
    // Maximum number of messages to store.
    private const int MaxMessages = 100;

    // Configurable options.
    public bool UseEchoFallback = true;
    public bool LogToCustomData = false;

    /// <summary>
    /// Constructor – automatically finds LCD panels with "[LOG]" in their name on the same grid.
    /// </summary>
    public Logger(MyGridProgram program)
    {
        this.program = program;

        // Find all IMyTextPanel blocks with "[LOG]" in the name.
        lcdPanels = new List<IMyTextPanel>();
        List<IMyTextPanel> allPanels = new List<IMyTextPanel>();
        program.GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(allPanels, panel => panel.CustomName.Contains("[LOG]"));

        // Filter out panels not on the same grid as the programmable block.
        foreach (var panel in allPanels)
        {
            if (panel.CubeGrid == program.Me.CubeGrid)
            {
                lcdPanels.Add(panel);
            }
        }
    }

    /// <summary>
    /// Appends a formatted log message and updates all outputs.
    /// </summary>
    /// <param name="formattedMessage">Message string (including log level prefix).</param>
    private void AppendMessage(string formattedMessage)
    {
        messages.Add(formattedMessage);
        // Ensure we only keep up to MaxMessages.
        if (messages.Count > MaxMessages)
        {
            messages.RemoveAt(0);
        }
        UpdateOutputs();
    }

    /// <summary>
    /// Updates all configured outputs (LCDs, Echo, CustomData) with the current log.
    /// </summary>
    private void UpdateOutputs()
    {
        // Combine all messages into a single text block.
        string logText = string.Join("\n", messages);

        // Write the log text to each LCD panel (if any are available).
        if (lcdPanels.Count > 0)
        {
            foreach (var lcd in lcdPanels)
            {
                lcd.WritePublicText(logText, false);
                // Ensure the LCD is set to display the public text.
                lcd.ShowPublicTextOnScreen();
            }
        }
        // If no LCDs are available and fallback is enabled, use program.Echo.
        else if (UseEchoFallback)
        {
            program.Echo(logText);
        }

        // Optionally write the log to the programmable block's CustomData.
        if (LogToCustomData)
        {
            program.Me.CustomData = logText;
        }
    }

    /// <summary>
    /// Returns string with current UTC time in HH:mm:ss format, prepended by a capital "T".
    /// </summary>
    /// <returns>Formatted timestamp string.</returns>
    /// <remarks>Example: "T12:34:56"</remarks>
    public string Timestamp()
    {
        DateTime now = System.DateTime.UtcNow;
        return "T" + now.ToString("HH:mm:ss");        
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public void Info(string message)
    {
        AppendMessage("[INFO " + Timestamp() "]:" + message);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public void Warning(string message)
    {
        AppendMessage("[WARNING " + Timestamp() "]:" + message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public void Error(string message)
    {
        AppendMessage("[ERROR " + Timestamp() "]:" + message);
    }

    /// <summary>
    /// Clears all log messages.
    /// </summary>
    public void Clear()
    {
        messages.Clear();
        UpdateOutputs();
    }
}
/* ^ ---------------------------------------------------------------------- ^ */
/* ^ Logging API                                                            ^ */
/* ^ ---------------------------------------------------------------------- ^ */