/* v ---------------------------------------------------------------------- v */
/* v Logging API                                                            v */
/* v ---------------------------------------------------------------------- v */
/// <summary>
/// Logger class provides a simple logging interface with three log levels.
/// It writes output to LCD panels tagged with "[LOG]" on the same grid,
/// falls back to program.Echo if none are found (if enabled), and optionally logs
/// to the programmable block's CustomData (keeping only the 100 most recent messages).
/// This version caches each panel’s wrapped text so that if the panel’s font/size
/// haven’t changed, only new messages are wrapped. Wrapped lines after the first
/// are indented by two spaces for readability. In addition, the logger defers the
/// expensive wrapping and output writing until a configurable number of game ticks
/// have elapsed (default 30 ticks). This improves efficiency in high-frequency updates.
/// </summary>
public class Logger
{
    // Reference to the parent MyGridProgram.
    public MyGridProgram program;
    // List of LCD panels to which logs will be written.
    private List<IMyTextPanel> lcdPanels;
    // Internal log message storage.
    public List<string> messages = new List<string>();
    // Maximum number of messages to store.
    private const int MaxMessages = 100;

    // Configurable options.
    public bool UseEchoFallback = true;
    public bool LogToCustomData = false;

    // Property to control how many ticks between writes (default 30 ticks).
    public int WriteUpdateInterval { get; set; } = 30;
    // Internal counter for ticks since last write.
    private int tickCounter = 0;

    // Cache information for each LCD panel.
    private Dictionary<IMyTextPanel, PanelCache> panelCaches = new Dictionary<IMyTextPanel, PanelCache>();

    /// <summary>
    /// Caches the wrapped lines along with the LCD settings used to compute them.
    /// </summary>
    private class PanelCache
    {
        public string Font;
        public float FontSize;
        public float SurfaceWidth; // from panel.SurfaceSize.X
        public List<string> WrappedLines = new List<string>();
        // Index in the messages list up to which messages have been wrapped.
        public int LastMessageIndex = 0;
    }

    /// <summary>
    /// Constructor – automatically finds LCD panels with "[LOG]" in their name on the same grid.
    /// Initializes the cache for each panel.
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
                // Initialize cache for this panel.
                PanelCache cache = new PanelCache();
                cache.Font = panel.Font;
                cache.FontSize = panel.FontSize;
                cache.SurfaceWidth = panel.SurfaceSize.X;
                cache.LastMessageIndex = 0;
                panelCaches[panel] = cache;
            }
        }
    }

    /// <summary>
    /// Appends a formatted log message.
    /// If the runtime update frequency is None or Once (i.e. not high-frequency),
    /// the logger updates outputs immediately; otherwise, the update is deferred.
    /// </summary>
    /// <param name="formattedMessage">Message string (including log level prefix).</param>
    public void AppendMessage(string formattedMessage)
    {
        messages.Add(formattedMessage);
        // Ensure we only keep up to MaxMessages.
        if (messages.Count > MaxMessages)
        {
            messages.RemoveAt(0);
            // When messages are removed, the cached wrapped text is no longer valid.
            foreach (var cache in panelCaches.Values)
            {
                cache.WrappedLines.Clear();
                cache.LastMessageIndex = 0;
            }
        }
        // For infrequent update frequencies, update immediately.
        if (program.Runtime.UpdateFrequency == UpdateFrequency.None ||
            program.Runtime.UpdateFrequency == UpdateFrequency.Once)
        {
            UpdateOutputs();
        }
        // Otherwise, defer output update until the next Tick call.
    }

    /// <summary>
    /// Should be called on every script update.
    /// In high-frequency update modes, this method uses the current update frequency
    /// to increment an internal tick counter and determines when to wrap and write output.
    /// </summary>
    public void Tick()
    {
        MaybeUpdateOutputs();
    }

    /// <summary>
    /// Checks the current update frequency and increments the tick counter accordingly.
    /// If the next scheduled call (i.e. counter + increment) would exceed the write update interval,
    /// the logger wraps and writes log messages and resets the counter.
    /// For UpdateFrequency.None or Once, it always updates immediately.
    /// </summary>
    private void MaybeUpdateOutputs()
    {
        // Always update immediately if not in a continuous update mode.
        if (program.Runtime.UpdateFrequency == UpdateFrequency.None ||
            program.Runtime.UpdateFrequency == UpdateFrequency.Once)
        {
            UpdateOutputs();
            tickCounter = 0;
            return;
        }

        // Determine the tick increment based on the current update frequency.
        int increment = 0;
        if ((program.Runtime.UpdateFrequency & UpdateFrequency.Update1) != 0)
            increment = 1;
        else if ((program.Runtime.UpdateFrequency & UpdateFrequency.Update10) != 0)
            increment = 10;
        else if ((program.Runtime.UpdateFrequency & UpdateFrequency.Update100) != 0)
            increment = 100;
        else
            increment = 1; // Fallback

        // If the next update would exceed our desired interval, update now.
        if (tickCounter + increment > WriteUpdateInterval)
        {
            UpdateOutputs();
            tickCounter = 0;
        }
        else
        {
            tickCounter += increment;
        }
    }

    /// <summary>
    /// Updates all configured outputs (LCDs, Echo, CustomData) with the current log.
    /// Uses cached wrapping for each panel when possible.
    /// </summary>
    private void UpdateOutputs()
    {
        if (lcdPanels.Count > 0)
        {
            foreach (var lcd in lcdPanels)
            {
                PanelCache cache;
                if (!panelCaches.TryGetValue(lcd, out cache))
                {
                    cache = new PanelCache();
                    cache.Font = lcd.Font;
                    cache.FontSize = lcd.FontSize;
                    cache.SurfaceWidth = lcd.SurfaceSize.X;
                    cache.LastMessageIndex = 0;
                    panelCaches[lcd] = cache;
                }

                // Check if the panel settings have changed.
                bool propertiesChanged = (cache.Font != lcd.Font ||
                                            cache.FontSize != lcd.FontSize ||
                                            cache.SurfaceWidth != lcd.SurfaceSize.X);
                if (propertiesChanged)
                {
                    // Clear the cached wrapped lines and rewrap all messages.
                    cache.WrappedLines.Clear();
                    for (int i = 0; i < messages.Count; i++)
                    {
                        cache.WrappedLines.AddRange(WrapMessageForPanel(lcd, messages[i]));
                    }
                    cache.LastMessageIndex = messages.Count;
                    // Update cache with current settings.
                    cache.Font = lcd.Font;
                    cache.FontSize = lcd.FontSize;
                    cache.SurfaceWidth = lcd.SurfaceSize.X;
                }
                else if (cache.LastMessageIndex < messages.Count)
                {
                    // Only wrap and add new messages.
                    for (int i = cache.LastMessageIndex; i < messages.Count; i++)
                    {
                        cache.WrappedLines.AddRange(WrapMessageForPanel(lcd, messages[i]));
                    }
                    cache.LastMessageIndex = messages.Count;
                }

                // Determine how many lines can fit based on the panel’s height.
                float lineHeight = lcd.MeasureStringInPixels(new StringBuilder("W"), lcd.Font, lcd.FontSize).Y;
                int maxLines = Math.Max(1, (int)(lcd.SurfaceSize.Y / lineHeight));
                List<string> linesToShow = cache.WrappedLines;
                if (cache.WrappedLines.Count > maxLines)
                {
                    linesToShow = cache.WrappedLines.GetRange(cache.WrappedLines.Count - maxLines, maxLines);
                }
                string logText = string.Join("\n", linesToShow);
                lcd.WritePublicText(logText, false);
                lcd.ShowPublicTextOnScreen();
            }
        }
        else if (UseEchoFallback)
        {
            string logText = string.Join("\n", messages);
            program.Echo(logText);
        }

        if (LogToCustomData)
        {
            string logText = string.Join("\n", messages);
            program.Me.CustomData = logText;
        }
    }

    /// <summary>
    /// Wraps a single log message for a given text panel based on its width and font settings.
    /// Uses a binary search approach to minimize per-character iterations.
    /// If a message wraps onto multiple lines, all but the first line are prefixed with two spaces.
    /// </summary>
    /// <param name="panel">The text panel for which to wrap the message.</param>
    /// <param name="message">The message to wrap.</param>
    /// <returns>A list of lines after wrapping.</returns>
    private List<string> WrapMessageForPanel(IMyTextPanel panel, string message)
    {
        List<string> lines = new List<string>();
        int start = 0;
        bool firstLine = true;
        while (start < message.Length)
        {
            // Determine how many characters from 'start' fit on one line.
            int maxFit = FindMaxSubstringLengthThatFits(panel, message, start);
            int breakPoint = start + maxFit;

            // If the message continues and there is a space in the substring, break at the last space.
            if (breakPoint < message.Length)
            {
                int lastSpace = message.LastIndexOf(' ', breakPoint - 1, maxFit);
                if (lastSpace > start)
                {
                    maxFit = lastSpace - start;
                    breakPoint = start + maxFit;
                }
            }

            string line = message.Substring(start, maxFit);
            // For readability, indent all wrapped lines after the first.
            if (!firstLine)
            {
                line = "  " + line;
            }
            lines.Add(line);

            firstLine = false;
            // Move past the extracted substring and any subsequent space.
            start = breakPoint;
            if (start < message.Length && message[start] == ' ')
                start++;
        }
        return lines;
    }

    /// <summary>
    /// Uses binary search to determine the maximum number of characters (starting at 'start')
    /// that can fit on one line of the panel.
    /// </summary>
    /// <param name="panel">The text panel.</param>
    /// <param name="message">The full message.</param>
    /// <param name="start">The starting index in the message.</param>
    /// <returns>The number of characters that fit on one line.</returns>
    private int FindMaxSubstringLengthThatFits(IMyTextPanel panel, string message, int start)
    {
        int low = 1;
        int high = message.Length - start;
        int best = 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            string substring = message.Substring(start, mid);
            Vector2 size = panel.MeasureStringInPixels(new StringBuilder(substring), panel.Font, panel.FontSize);
            if (size.X <= panel.SurfaceSize.X)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        return best;
    }

    /// <summary>
    /// Returns string with current UTC time in HH:mm:ss format, prepended by a capital "T".
    /// </summary>
    /// <returns>Formatted timestamp string.</returns>
    /// <remarks>Example: "T12:34:56"</remarks>
    public string Timestamp()
    {
        DateTime now = DateTime.UtcNow;
        return "T" + now.ToString("HH:mm:ss");
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public void Info(string message)
    {
        AppendMessage("[INFO " + Timestamp() + "]:" + message);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public void Warning(string message)
    {
        AppendMessage("[WARNING " + Timestamp() + "]:" + message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public void Error(string message)
    {
        AppendMessage("[ERROR " + Timestamp() + "]:" + message);
    }

    /// <summary>
    /// Clears all log messages.
    /// </summary>
    public void Clear()
    {
        messages.Clear();
        // Clear the cache for each panel.
        foreach (var cache in panelCaches.Values)
        {
            cache.WrappedLines.Clear();
            cache.LastMessageIndex = 0;
        }
        UpdateOutputs();
    }
}
/* ^ ---------------------------------------------------------------------- ^ */
/* ^ Logging API                                                            ^ */
/* ^ ---------------------------------------------------------------------- ^ */