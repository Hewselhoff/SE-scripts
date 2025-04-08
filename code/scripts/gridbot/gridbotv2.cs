#region Prelude
using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using VRageMath;
using VRage.Game;
using VRage.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.CommonLibs;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;

// Change this namespace for each script you create.
namespace SpaceEngineers.UWBlockPrograms.GridBot {
    public sealed class Program : MyGridProgram {
#endregion

        public IMyShipConnector gridRefBlock;
        public IMyRemoteControl shipRefBlock;
        public RefAxes refAxes;
        // Sampling constants
        public const UpdateFrequency CHECK_RATE = UpdateFrequency.Update100;
        public const UpdateType CHECK_TYPE = UpdateType.Update100;
        public const UpdateFrequency SAMPLE_RATE = UpdateFrequency.Update1;
        public const UpdateType SAMPLE_TYPE = UpdateType.Update1;
        public const UpdateFrequency STOP_RATE = UpdateFrequency.None; 
        public const UpdateType STOP_TYPE = ~(SAMPLE_TYPE | CHECK_TYPE); // Anything thats not SAMPLE_TYPE or CHECK_TYPE
        // Create an instance of ArgParser.
        ArgParser argParser = new ArgParser();

        /* v ---------------------------------------------------------------------- v */
        /* v Logging API                                                            v */
        /* v ---------------------------------------------------------------------- v */
        /// <summary>
        /// Logger class provides a simple logging interface with three log levels.
        /// It writes output to LCD panels tagged with "[LOG]" on the same grid,
        /// falls back to program.Echo if none are found (if enabled), and optionally logs
        /// to the programmable block's CustomData (keeping only the 100 most recent messages).
        /// This version caches each panel’s wrapped text so that if the panel’s font/size
        /// haven’t changed, only new messages are wrapped. Additionally, if a message wraps,
        /// every wrapped line after the first is indented by two spaces for readability.
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
            /// Appends a formatted log message and updates all outputs.
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
                UpdateOutputs();
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
                        lcd.WriteText(logText, false);
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

            // Error log for the last parse or API call.
            public List<string> ErrorLog { get; private set; } = new List<string>();

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
            /// Logs an error by adding it to the ErrorLog and calling the Logger.
            /// </summary>
            private void LogError(string message)
            {
                ErrorLog.Add(message);
                Logger(message);
            }

            /// <summary>
            /// Checks that this instance has been finalized; if not, throws an exception.
            /// </summary>
            private void EnsureFinalized()
            {
                if (!isFinalized)
                    throw new Exception("Configuration file has not been finalized. Call FinalizeRegistration() before using the config.");
            }

            /// <summary>
            /// Registers a new root-level configuration property.
            /// </summary>
            public void RegisterProperty(string name, ConfigValueType type, object defaultValue)
            {
                if (isFinalized)
                {
                    LogError("Cannot register new property after finalization: " + name);
                    return;
                }
                if (properties.ContainsKey(name))
                {
                    LogError("Property already registered: " + name);
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
                    LogError("Cannot register new property after finalization: " + subConfigName + "/" + name);
                    return;
                }
                if (!subConfigs.ContainsKey(subConfigName))
                {
                    LogError("Sub config '" + subConfigName + "' does not exist. Register the sub config first.");
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
                    LogError("Cannot register new sub config after finalization: " + name);
                    return;
                }
                if (subConfigs.ContainsKey(name))
                {
                    LogError("Sub config already registered: " + name);
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
            public void FinalizeRegistration()
            {
                if (isFinalized)
                {
                    LogError("Configuration file already finalized.");
                    return;
                }

                // Finalize all sub configs first.
                foreach (var kvp in subConfigs)
                {
                    ConfigFile subCfg = kvp.Value;
                    // Finalize sub config registrations recursively.
                    subCfg.FinalizeRegistration();
                    // Ensure the sub config has at least one property or sub config.
                    if (subCfg.properties.Count == 0 && subCfg.subConfigs.Count == 0)
                    {
                        LogError($"Sub config '{kvp.Key}' is empty. It must contain at least one property.");
                    }
                }

                if (ErrorLog.Count > 0)
                    throw new Exception("Configuration registration finalization failed. Check ErrorLog for details.");

                isFinalized = true;
                // Write default config to CustomData if needed.
                CheckAndWriteDefaults();
            }

            /// <summary>
            /// Generates the default CAML config text, including both root properties and sub configurations.
            /// Sub config blocks are indented using their own default indent (typically detected at parse time).
            /// </summary>
            public string GenerateDefaultConfigText()
            {
                EnsureFinalized();

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
            /// Returns true if no errors occurred; errors are available via ErrorLog.
            /// </summary>
            public bool ParseConfig(string configText)
            {
                EnsureFinalized();
                // Clear errors for this parse run.
                ErrorLog.Clear();

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
                                LogError("Unknown sub config: " + currentSubConfigName);
                                // Optionally, auto-register a new sub config:
                                subConfigs[currentSubConfigName] = new ConfigFile(pb, program);
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
                            LogError("Unexpected indentation without an active sub config header: " + line);
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
                                LogError($"Inconsistent indentation for sub config '{currentSubConfigName}'. Expected {expectedIndent} spaces but found {indent} spaces in line: {line}");
                            }

                            if (line.Length >= expectedIndent)
                            {
                                string subLine = line.Substring(expectedIndent);
                                subConfigBlocks[currentSubConfigName].AppendLine(subLine);
                            }
                            else
                            {
                                LogError("Line is indented but too short relative to the expected indent: " + line);
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
                        LogError($"Syntax error in root property (missing ':'): {line}");
                        continue;
                    }
                    string key = trimmed.Substring(0, colonIndex).Trim();
                    string valuePart = trimmed.Substring(colonIndex + 1).Trim();
                    if (encounteredRoot.Contains(key))
                    {
                        LogError($"Duplicate root property: {key}");
                        continue;
                    }
                    encounteredRoot.Add(key);

                    if (!properties.ContainsKey(key))
                    {
                        LogError($"Unknown root property: {key}");
                        continue;
                    }
                    var prop = properties[key];
                    object parsedValue;
                    if (!ParseValue(key, valuePart, prop.ValueType, out parsedValue))
                    {
                        LogError($"Failed to parse root property '{key}' with value '{valuePart}'");
                        continue;
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
                        LogError($"Errors in sub config '{subName}':");
                        foreach (var err in subConfig.ErrorLog)
                        {
                            LogError("  " + err);
                        }
                    }
                }

                // Verify that every required root property was encountered; assign default values if missing.
                foreach (var kvp in properties)
                {
                    if (!encounteredRoot.Contains(kvp.Key))
                    {
                        LogError($"Missing root property: {kvp.Key}. Using default value.");
                        kvp.Value.Value = kvp.Value.DefaultValue;
                    }
                }

                // Update the stored config text if no errors occurred.
                if (ErrorLog.Count == 0)
                {
                    this.configText = configText;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            /// <summary>
            /// Retrieves the value of a root property.
            /// If the property is missing or a type mismatch occurs, logs an error and returns default(T).
            /// </summary>
            public T Get<T>(string key)
            {
                EnsureFinalized();

                if (!properties.ContainsKey(key))
                {
                    LogError("Unknown property requested: " + key);
                    return default(T);
                }
                try
                {
                    return (T)properties[key].Value;
                }
                catch
                {
                    LogError("Type mismatch for property: " + key);
                    return default(T);
                }
            }

            /// <summary>
            /// Retrieves a sub configuration instance.
            /// Returns null if the sub config is not registered.
            /// </summary>
            public ConfigFile GetSubConfig(string name)
            {
                EnsureFinalized();

                if (!subConfigs.ContainsKey(name))
                {
                    LogError("Unknown sub config requested: " + name);
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
                    LogError("Unknown sub config requested: " + name);
                    return null;
                }
                return subConfigs[name];
            }

            /// <summary>
            /// Checks the programmable block's CustomData.
            /// If empty, writes the default config.
            /// </summary>
            public void CheckAndWriteDefaults()
            {
                EnsureFinalized();

                if (string.IsNullOrWhiteSpace(pb.CustomData))
                {
                    string defaultConfig = GenerateDefaultConfigText();
                    pb.CustomData = defaultConfig;
                    program.Echo("No configuration data found. Default config added to Custom Data.");
                }
            }

            /// <summary>
            /// Checks if the CustomData has changed since the last parse, and if so, re-parses it.
            /// Any errors are output using the program's Echo method.
            /// </summary>
            public bool CheckAndReparse()
            {
                EnsureFinalized();

                if (string.IsNullOrWhiteSpace(pb.CustomData))
                    return false;

                if (configText == null || pb.CustomData != configText)
                {
                    if (!ParseConfig(pb.CustomData))
                    {
                        foreach (var error in ErrorLog)
                        {
                            program.Echo(error);
                        }
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

        /* v ---------------------------------------------------------------------- v */
        /* v ArgParser API                                                          v */
        /* v ---------------------------------------------------------------------- v */
        public class ArgParser
        {
            // Nested class representing a registered argument or command.
            public class ArgDefinition
            {
                public string Name;      // For arguments, this includes the "--" prefix; for commands, it’s left as provided.
                public Type ArgType;     // The expected type (int, float, string, bool). For commands, this is always string.
                public bool IsList;      // True if this argument should accept multiple values (not used for commands).
                public bool IsRequired;  // True if this argument must be provided.
                public bool IsCommand;   // True if this definition represents a command rather than a "--" argument.

                // Added an optional isCommand flag (default is false) to support both commands and arguments.
                public ArgDefinition(string name, Type argType, bool isList = false, bool isRequired = false, bool isCommand = false)
                {
                    // For normal arguments, enforce the "--" prefix; commands are left as provided.
                    if (!isCommand && !name.StartsWith("--"))
                        Name = "--" + name;
                    else
                        Name = name;
                    ArgType = argType;
                    IsList = isList;
                    IsRequired = isRequired;
                    IsCommand = isCommand;
                }
            }

            // Dictionary holding all registered standard argument definitions.
            private Dictionary<string, ArgDefinition> registeredArgs = new Dictionary<string, ArgDefinition>();

            // Dictionary holding all registered command definitions.
            private Dictionary<string, ArgDefinition> registeredCommands = new Dictionary<string, ArgDefinition>();

            // Dictionary holding the parsed arguments and commands.
            // For single value arguments, the value is stored as object;
            // for lists, it is a List<T>; for commands, it is stored as a string.
            private Dictionary<string, object> parsedArgs = new Dictionary<string, object>();

            // List of errors that occurred during parsing.
            public List<string> Errors { get; private set; } = new List<string>();

            // If true, the parser will only allow one argument per call (applies to standard arguments).
            public bool OnlyAllowSingleArg { get; set; } = false;

            /// <summary>
            /// Registers a new standard argument definition.
            /// </summary>
            /// <param name="name">The argument name (with or without "--" prefix)</param>
            /// <param name="argType">The expected type (int, float, string, bool)</param>
            /// <param name="isList">If true, the argument accepts multiple space-separated values</param>
            /// <param name="isRequired">If true, the argument must be provided</param>
            public void RegisterArg(string name, Type argType, bool isList = false, bool isRequired = false)
            {
                var argDef = new ArgDefinition(name, argType, isList, isRequired, false);
                registeredArgs[argDef.Name] = argDef;
            }

            /// <summary>
            /// Registers a new command.
            /// A command is an argument that:
            /// 1. Is not prefixed with "--".
            /// 2. Always has a value type of string.
            /// 3. Must be the first token in the input.
            /// 4. Only one command is allowed per call.
            /// 5. The parser captures everything after the command name (spaces allowed) as its value.
            /// </summary>
            /// <param name="commandName">The command name (without the "--" prefix)</param>
            /// <param name="isRequired">If true, the command must be provided</param>
            public void RegisterCommand(string commandName, bool isRequired = false)
            {
                // Create an ArgDefinition with IsCommand set to true.
                var cmdDef = new ArgDefinition(commandName, typeof(string), isList: false, isRequired: isRequired, isCommand: true);
                registeredCommands[commandName] = cmdDef;
            }

            /// <summary>
            /// Gets the dictionary of parsed arguments and commands.
            /// </summary>
            public Dictionary<string, object> ParsedArgs { get { return parsedArgs; } }

            /// <summary>
            /// Provides an enumerable to iterate over parsed arguments.
            /// </summary>
            public IEnumerable<KeyValuePair<string, object>> GetParsedArgs()
            {
                return parsedArgs;
            }

            /// <summary>
            /// Parses the input string into arguments or a command.
            /// Returns true if parsing is successful (i.e. no errors); otherwise, false.
            /// </summary>
            /// <param name="input">The argument string passed to Main</param>
            public bool Parse(string input)
            {
                // Clear previous errors and parsed arguments.
                Errors.Clear();
                parsedArgs.Clear();

                if (string.IsNullOrWhiteSpace(input))
                    return true; // Nothing to parse

                string trimmedInput = input.Trim();

                // Check if this is a command call.
                if (!trimmedInput.StartsWith("--"))
                {
                    // Extract the command token and the remainder of the string.
                    int spaceIndex = trimmedInput.IndexOf(' ');
                    string commandToken;
                    string remainder;
                    if (spaceIndex == -1)
                    {
                        commandToken = trimmedInput;
                        remainder = "";
                    }
                    else
                    {
                        commandToken = trimmedInput.Substring(0, spaceIndex);
                        remainder = trimmedInput.Substring(spaceIndex + 1);
                    }

                    // Check if the command is registered.
                    if (!registeredCommands.ContainsKey(commandToken))
                    {
                        Errors.Add("Unrecognized command: " + commandToken);
                        return false;
                    }

                    // Only one command is allowed per call.
                    parsedArgs[commandToken] = remainder;

                    // Check if any required commands are missing.
                    foreach (var kvp in registeredCommands)
                    {
                        if (kvp.Value.IsRequired && !parsedArgs.ContainsKey(kvp.Key))
                        {
                            Errors.Add("Missing required command: " + kvp.Key);
                        }
                    }
                    return Errors.Count == 0;
                }
                else
                {
                    // Process as standard "--" arguments.
                    string[] tokens = trimmedInput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int countArgsParsed = 0;

                    for (int i = 0; i < tokens.Length; i++)
                    {
                        string token = tokens[i];

                        // Each standard argument should start with "--"
                        if (!token.StartsWith("--"))
                        {
                            Errors.Add("Value provided without a preceding argument: " + token);
                            continue;
                        }

                        // Check if the argument is registered.
                        if (!registeredArgs.ContainsKey(token))
                        {
                            Errors.Add("Unrecognized argument: " + token);
                            continue;
                        }

                        countArgsParsed++;
                        if (OnlyAllowSingleArg && countArgsParsed > 1)
                        {
                            Errors.Add("Only one argument allowed per call.");
                            return false;
                        }

                        ArgDefinition def = registeredArgs[token];
                        List<string> values = new List<string>();

                        // Gather all following tokens that are not another argument.
                        int j = i + 1;
                        while (j < tokens.Length && !tokens[j].StartsWith("--"))
                        {
                            values.Add(tokens[j]);
                            j++;
                        }
                        i = j - 1; // Advance to the last token processed

                        // For bool type, if no value is provided, assume true.
                        if (def.ArgType == typeof(bool) && values.Count == 0)
                        {
                            parsedArgs[token] = true;
                            continue;
                        }

                        // Process single value arguments.
                        if (!def.IsList)
                        {
                            if (values.Count == 0)
                            {
                                Errors.Add("No value provided for argument: " + token);
                                continue;
                            }
                            object converted = ConvertValue(values[0], def.ArgType);
                            if (converted == null)
                            {
                                Errors.Add("Invalid value for argument " + token + ": " + values[0]);
                                continue;
                            }
                            parsedArgs[token] = converted;
                        }
                        else // Process list arguments without using reflection.
                        {
                            if (values.Count == 0)
                            {
                                Errors.Add("No values provided for list argument: " + token);
                                continue;
                            }

                            if (def.ArgType == typeof(string))
                            {
                                List<string> list = new List<string>(values);
                                parsedArgs[token] = list;
                            }
                            else if (def.ArgType == typeof(int))
                            {
                                List<int> list = new List<int>();
                                foreach (var val in values)
                                {
                                    if (!int.TryParse(val, out int parsed))
                                    {
                                        Errors.Add("Invalid value for argument " + token + ": " + val);
                                        continue;
                                    }
                                    list.Add(parsed);
                                }
                                parsedArgs[token] = list;
                            }
                            else if (def.ArgType == typeof(float))
                            {
                                List<float> list = new List<float>();
                                foreach (var val in values)
                                {
                                    if (!float.TryParse(val, out float parsed))
                                    {
                                        Errors.Add("Invalid value for argument " + token + ": " + val);
                                        continue;
                                    }
                                    list.Add(parsed);
                                }
                                parsedArgs[token] = list;
                            }
                            else if (def.ArgType == typeof(double))
                            {
                                List<double> list = new List<double>();
                                foreach (var val in values)
                                {
                                    if (!double.TryParse(val, out double parsed))
                                    {
                                        Errors.Add("Invalid value for argument " + token + ": " + val);
                                        continue;
                                    }
                                    list.Add(parsed);
                                }
                                parsedArgs[token] = list;
                            }
                            else if (def.ArgType == typeof(bool))
                            {
                                List<bool> list = new List<bool>();
                                foreach (var val in values)
                                {
                                    if (!bool.TryParse(val, out bool parsed))
                                    {
                                        Errors.Add("Invalid value for argument " + token + ": " + val);
                                        continue;
                                    }
                                    list.Add(parsed);
                                }
                                parsedArgs[token] = list;
                            }
                            else
                            {
                                Errors.Add("Unsupported list type for argument " + token);
                            }
                        }
                    }

                    // Check for missing required arguments.
                    foreach (var kvp in registeredArgs)
                    {
                        if (kvp.Value.IsRequired && !parsedArgs.ContainsKey(kvp.Key))
                            Errors.Add("Missing required argument: " + kvp.Key);
                    }

                    return Errors.Count == 0;
                }
            }

            /// <summary>
            /// Helper method that converts a string to the target type.
            /// Returns null if conversion fails.
            /// </summary>
            private object ConvertValue(string value, Type targetType)
            {
                try
                {
                    if (targetType == typeof(string))
                        return value;
                    if (targetType == typeof(int))
                        return int.Parse(value);
                    if (targetType == typeof(float))
                        return float.Parse(value);
                    if (targetType == typeof(double))
                        return double.Parse(value);
                    if (targetType == typeof(bool))
                        return bool.Parse(value);
                }
                catch
                {
                    return null;
                }
                return null;
            }
        }
        /* ^ ---------------------------------------------------------------------- ^ */
        /* ^ ArgParser API                                                          ^ */
        /* ^ ---------------------------------------------------------------------- ^ */

        /* v ---------------------------------------------------------------------- v */ 
        /* v RefAxes Transformations                                               v */ 
        /* v ---------------------------------------------------------------------- v */
        public class RefAxes {
            public MatrixD worldRef;
            public MatrixD localRef;
            public MatrixD localHome;
            public IMyCubeGrid refGrid;
            public Vector3I indexOffset;
            public Quaternion orientOffset;

            // Set local rotations constants
            public MatrixD ROT_PLUS_90_X = MatrixD.CreateRotationX(Math.PI / 2);
            public MatrixD ROT_MINUS_90_X = MatrixD.CreateRotationX(-Math.PI / 2);
            public MatrixD ROT_PLUS_90_Y = MatrixD.CreateRotationY(Math.PI / 2);
            public MatrixD ROT_MINUS_90_Y = MatrixD.CreateRotationY(-Math.PI / 2);
            public MatrixD ROT_PLUS_90_Z = MatrixD.CreateRotationZ(Math.PI / 2);
            public MatrixD ROT_MINUS_90_Z = MatrixD.CreateRotationZ(-Math.PI / 2);

            public RefAxes(IMyCubeGrid grid) {
                refGrid = grid;
                worldRef = grid.WorldMatrix;
                localRef = MatrixD.Identity;
                localHome = MatrixD.Identity;
                indexOffset = Vector3I.Zero;
                orientOffset = Quaternion.Identity;
            }

            public Vector3D TransformLocalToWorldVector(Vector3I dirIndexVect) {
                Vector3D fromPoint = refGrid.GridIntegerToWorld(indexOffset);
                Vector3D toPoint   = refGrid.GridIntegerToWorld(indexOffset - Vector3I.Transform(dirIndexVect, orientOffset));
                return fromPoint - toPoint;
            }

            public Vector3D GetWorldUp() {
                Vector3I up3I = new Vector3I(localRef.Up);
                return TransformLocalToWorldVector(up3I);
            }

            public Vector3D GetWorldForward() {
                Vector3I forward3I = new Vector3I(localRef.Forward);
                return TransformLocalToWorldVector(forward3I);
            }

            public void RotateAzimuth(int direction) {
                if (direction > 0) {
                    localRef *= ROT_PLUS_90_Y;
                } else {
                    localRef *= ROT_MINUS_90_Y;
                }
                UpdateWorldRef();
            }

            public void RotateElevation(int direction) {
                if (direction > 0) {
                    localRef *= ROT_PLUS_90_X;
                } else {
                    localRef *= ROT_MINUS_90_X;
                }
                UpdateWorldRef();
            }

            public void RotateRoll(int direction) {
                if (direction > 0) {
                    localRef *= ROT_PLUS_90_Z;
                } else {
                    localRef *= ROT_MINUS_90_Z;
                }
                UpdateWorldRef();
            }

            public void ResetHomeOrientation() {
                localRef = MatrixD.Identity;
                UpdateWorldRef();
            }

            public void SetHomeOrientation() {
                localHome = localRef;
                UpdateWorldRef();
            }

            public void GoToHomeOrientation() {
                localRef = localHome;
                UpdateWorldRef();
            }

            public void UpdateWorldRef() {
                Vector3 worldUp = GetWorldUp();
                Vector3 worldForward = GetWorldForward();
                worldRef = MatrixD.CreateWorld(refGrid.GridIntegerToWorld(indexOffset), worldForward, worldUp);
            }

        }
        /* ^ ---------------------------------------------------------------------- ^ */ 
        /* ^ RefAxes Transformations                                               ^ */ 
        /* ^ ---------------------------------------------------------------------- ^ */

        /* v ---------------------------------------------------------------------- v */ 
        /* v Grid Inspection Utilities                                              v */ 
        /* v ---------------------------------------------------------------------- v */
        public static IMyShipConnector FindTargetRefBlock(MyGridProgram program) {
            // Get all connectors on the grid
            List<IMyShipConnector> connectors = new List<IMyShipConnector>();
            program.GridTerminalSystem.GetBlocksOfType(connectors);

            // Loop through the connectors to find one that is locked to a static grid
            foreach (var connector in connectors) {
                if (connector.CubeGrid == program.Me.CubeGrid && connector.Status == MyShipConnectorStatus.Connected) {
                    var otherConnector = connector.OtherConnector;
                    if (otherConnector != null && otherConnector.CubeGrid.IsStatic) {
                        logger.Info("Found ref connector: " + connector.CustomName);
                        return otherConnector;
                    }
                }
            }
            logger.Error("Ship must be connected to a static grid.");
            return null;
        }

        // Find ship ref block (remote control)
        public static IMyRemoteControl FindShipRefBlock(MyGridProgram program) {
            List<IMyRemoteControl> remotes = new List<IMyRemoteControl>();
            program.GridTerminalSystem.GetBlocksOfType(remotes);
            var shipRefBlock = remotes.FirstOrDefault(r => r.CubeGrid == program.Me.CubeGrid);
            if (shipRefBlock == null) {
                logger.Error("No ship reference block found.");
                return null;
            }
            logger.Info("Found ship reference block: " + shipRefBlock.CustomName);
            return shipRefBlock;
        }

        public bool SetReferenceGrid(MyGridProgram program) {
            IMyShipConnector temp = FindTargetRefBlock(this);
            if (temp == null) {
                logger.Error("No target reference block found.");
                return false;
            }
            gridRefBlock = temp;
            logger.Info("Reference grid set to " + gridRefBlock.CubeGrid.CustomName + ".");
            return true;
        }

        public bool IsInitialized() {
            return (shipRefBlock != null && gridRefBlock != null && refAxes != null);
        }
        /* ^ ---------------------------------------------------------------------- ^ */ 
        /* ^ Grid Inspection Utilities                                              ^ */ 
        /* ^ ---------------------------------------------------------------------- ^ */

        public class GridOrient: {
            public List<IMyGyro> gyros;
            public bool running, orienting;
            public ArgParser argParser = new ArgParser();
            public UpdateFrequency updateFrequency;
            public UpdateType updateType;
            public ConfigFile config;

            // Constructor
            public GridOrient(MyGridProgram program) {
                GetMyGyros(program);
            }

            /* v Configuration Handling -------------------------------------------- v */
            public RegisterConfig(ConfigFile rootConfig) {
                rootConfig.RegisterSubConfig("GridOrient");
                rootConfig.RegisterProperty("GridOrient","Kp", ConfigValueType.Float, 0.5f);
                rootConfig.RegisterProperty("GridOrient","tolerance", ConfigValueType.Float, 0.05f);
                config = rootConfig.GetSubConfigRef("GridOrient");
            }
            /* ^ Configuration Handling -------------------------------------------- ^ */

            /* v Gyro Initialization ------------------------------------------------ v */
            public void GetMyGyros(MyGridProgram program) {
                List<IMyGyro> allGyros = new List<IMyGyro>();
                program.GridTerminalSystem.GetBlocksOfType(allGyros);
                gyros = allGyros.Where(g => g.CubeGrid == program.Me.CubeGrid).ToList();
                if (gyros.Count == 0) {
                    logger.Error("No gyros found on the grid.");
                } else {
                    logger.Info("Found " + gyros.Count + " gyros on the grid.");
                }
            }
            /* ^ Gyro Initialization ------------------------------------------------ ^ */

            /* v Auto Orientation -------------------------------------------------- v */ 
            // Alignment function that computes the rotation error and applies gyro overrides.
            // Returns the angle error in radians.
            public double ApplyOrientationUpdate(IMyTerminalBlock shipBlock, IMyTerminalBlock targetBlock, List<IMyGyro> gyros, double Kp = 1.0)
            {
                // Get ship and target orientations in world coords
                MatrixD shipMatrix = Me.CubeGrid.WorldMatrix;
                MatrixD targetMatrix = refAxes.worldRef;
                
                // Compute the relative rotation matrix (the error rotation from ship to target).
                MatrixD relativeMatrix = targetMatrix * MatrixD.Transpose(shipMatrix);
                
                // Convert the rotation matrix to a quaternion and then extract the axis–angle.
                QuaternionD rotationQuat = QuaternionD.CreateFromRotationMatrix(relativeMatrix);
                Vector3D rotationAxis;
                double rotationAngle;
                rotationQuat.GetAxisAngle(out rotationAxis, out rotationAngle);
                
                // If not orienting, just return the angle error.
                if (!orienting) {
                    return rotationAngle;
                }
                
                // Convert rotationAngle from radians to degrees for gyro override inputs.
                double overrideValue = Kp * rotationAngle * (180.0 / Math.PI);
                
                // For each gyro, transform the error (rotation axis) from world space to the gyro's local space.
                foreach (var gyro in gyros)
                {
                    Matrix gyroMatrix;
                    gyro.Orientation.GetMatrix(out gyroMatrix);
                    Vector3D localAxis = Vector3D.TransformNormal(rotationAxis, MatrixD.Transpose(gyroMatrix));
                    
                    // Set the gyro override values.
                    gyro.SetValueFloat("Pitch", (float)(localAxis.X * overrideValue));
                    gyro.SetValueFloat("Yaw",   (float)(-localAxis.Y * overrideValue));
                    gyro.SetValueFloat("Roll",  (float)(-localAxis.Z * overrideValue));
                    gyro.SetValueFloat("Power", 1f);
                    gyro.SetValueBool("Override", true);
                }
                return rotationAngle;
            }

            // Start active gyro overrides.
            public void StartActiveOrientation() {
                // Set update frequency if not already running the control loop.
                if (!orienting) {
                    updateFrequency = SAMPLE_RATE;
                    updateType = SAMPLE_TYPE;
                    orienting = true;
                    logger.Info("Reorienting to " + gridRefBlock.CubeGrid.CustomName + ".");
                }
            }

            // Stop active gyro overrides.
            public void StopActiveOrientation() {
                foreach (var gyro in gyros) {
                    gyro.SetValueBool("Override", false);
                    gyro.SetValueFloat("Power", 0f);
                    gyro.SetValueFloat("Pitch", 0f);
                    gyro.SetValueFloat("Yaw", 0f);
                    gyro.SetValueFloat("Roll", 0f);
                }
                updateFrequency = CHECK_RATE;
                updateType = CHECK_TYPE;
                orienting = false;
                logger.Info("Orientation complete.");
            }

            // Turn off Auto-orientation
            public void StopGridAutoOrientation() {
                if (!running) {
                    // if we're not running, just return.
                    return;
                }
                foreach (var gyro in gyros) {
                    gyro.SetValueBool("Override", false);
                    gyro.SetValueFloat("Pitch", 0f);
                    gyro.SetValueFloat("Yaw", 0f);
                    gyro.SetValueFloat("Roll", 0f);
                    gyro.SetValueFloat("Power", 1.0f);
                }
                updateFrequency = STOP_RATE;
                updateType = STOP_TYPE;
                orienting = false;
                running = false;
                logger.Info("Auto-orientation OFF.");
            }

            // Turn on Auto-orientation
            public void StartGridAutoOrientation() {
                if (running) {
                    // If we're already running, just return.
                    return;
                }
                // Reparse the config file to ensure we have the latest values.
                if (!ConfigFile.CheckAndReparse(Me, this)) {
                    logger.Error("Configuration parsing failed. Please fix the errors and run again.");
                    return;
                }
                updateFrequency = CHECK_RATE;
                orienting = false;
                running = true;
                // Enable gyro overrides for all gyros on the grid.
                foreach (var gyro in gyros) {
                    gyro.SetValueBool("Override", true);
                    gyro.SetValueFloat("Power", 1.0f);
                }            
                logger.Info("Auto-orientation ON.");
            }

            // Perform the grid auto-orientation
            public void PerformAutoOrientation(UpdateType updateSource) {
                // if we're not running, just return.
                if (!running) {
                    return;
                }
                // If update type doesn't match our current update frequency, return
                if ((updateSource & updateType) == 0) {
                    return;
                }
                // Call the alignment function to update gyro overrides.    
                double errorAngle = ApplyOrientationUpdate(shipRefBlock, gridRefBlock, gyros, ConfigFile.Get<float>("Kp"));
                
                logger.Info("Alignment error: " + (errorAngle * 180.0 / Math.PI).ToString("F2") + " degrees");

                if (orienting && Math.Abs(errorAngle) <= ConfigFile.Get<float>("tolerance")) {
                    StopActiveOrientation();
                } else if (!orienting && Math.Abs(errorAngle) > ConfigFile.Get<float>("tolerance")) {
                    StartActiveOrientation();
                } else {
                    // Re-parse in case of changes in Custom Data.
                    // we'll only do this in the slow update for performance reasons.
                    if (!ConfigFile.CheckAndReparse(Me, this)) {
                        logger.Error("Configuration parsing failed. Please fix the errors and run again.");
                        return;
                    }
                }
            }
            /* ^ Auto Orientation -------------------------------------------------- ^ */ 

            /* v Command Handling -------------------------------------------------- v */
            /// <summary>
            /// Registers command line arguments for orientation control.
            /// </summary>
            public void RegisterCommands() {
                argParser.RegisterArg("orient", typeof(bool), false, false); // Turns auto-orientation on/off
                argParser.RegisterArg("az", typeof(int), false, false); // Azimuth rotation
                argParser.RegisterArg("el", typeof(int), false, false); // Elevation rotation
                argParser.RegisterArg("roll", typeof(int), false, false); // Roll rotation
                argParser.RegisterArg("set-home", typeof(bool), false, false); // Set home orientation
                argParser.RegisterArg("home", typeof(bool), false, false); // Go to home orientation
                argParser.RegisterArg("reset-home", typeof(bool), false, false); // Reset home orientation
                argParser.OnlyAllowSingleArg = true;
            }
            
            /// <summary>
            /// Handles starting or stopping the auto-orientation behavior.
            /// </summary>
            /// <param name="val">True to start auto-orientation, false to stop.</param>
            public void Orient(bool val) {
                if (!(IsInitialized() && gyros != null && gyros.Count != 0)) {
                    logger.Error("No reference grid set. Cannot start auto orientation.");
                    return;
                }
                if (val) {
                    StartGridAutoOrientation();
                } else {            
                    StopGridAutoOrientation();
                }
                return;
            }

            /// <summary>
            /// Handles initializing/reinitializing the reference grid used for orientation alignment.
            /// </summary>
            public void Init(bool val) {
                if (!val) {
                    return;
                }
                // stop the current orientation process if running
                if (IsInitialized() && gyros != null && gyros.Count != 0) {
                    StopGridAutoOrientation();
                }
                StopGridAutoOrientation();
                // Set reference grid
                if (!SetReferenceGrid(this)) {
                    logger.Error("Failed to set reference grid.");
                    return;
                }
                // Initialize reference axes
                refAxes = new RefAxes(gridRefBlock.CubeGrid);            
            }

            /// <summary>
            /// Handles the azimuth rotation command.
            /// </summary>
            /// <param name="direction">Direction of rotation: +1 or -1.</param>
            public void AzimuthRotate(int direction) {
                if (!(IsInitialized() && gyros != null && gyros.Count != 0)) {
                    logger.Error("No reference grid set. Cannot rotate.");
                    return;
                }
                if (direction != 1 && direction != -1) {
                    logger.Error("Invalid azimuth direction. Must be +1 or -1.");
                    return;
                }
                refAxes.RotateAzimuth(direction);
            }

            /// <summary>
            /// Handles the elevation rotation command.
            /// </summary>
            /// <param name="direction">Direction of rotation: +1 or -1.</param>
            public void ElevationRotate(int direction) {
                if (!(IsInitialized() && gyros != null && gyros.Count != 0)) {
                    logger.Error("No reference grid set. Cannot rotate.");
                    return;
                }
                if (direction != 1 && direction != -1) {
                    logger.Error("Invalid elevation direction. Must be +1 or -1.");
                    return;
                }
                refAxes.RotateElevation(direction);
            }

            /// <summary>
            /// Handles the roll rotation command.
            /// </summary>
            /// <param name="direction">Direction of rotation: +1 or -1.</param>
            /// </summary>
            public void RollRotate(int direction) {
                if (!(IsInitialized() && gyros != null && gyros.Count != 0)) {
                    logger.Error("No reference grid set. Cannot rotate.");
                    return;
                }
                if (direction != 1 && direction != -1) {
                    logger.Error("Invalid roll direction. Must be +1 or -1.");
                    return;
                }
                refAxes.RotateRoll(direction);
            }
            /// <summary>
            /// Handles the set-home command.
            /// </summary>
            public void SetHome() {
                if (!(IsInitialized() && gyros != null && gyros.Count != 0)) {
                    logger.Error("No reference grid set. Cannot set home orientation.");
                    return;
                }
                refAxes.SetHomeOrientation();
            }

            /// <summary>
            /// Handles the home command.
            /// </summary>
            public void GoHome() {
                if (!(IsInitialized() && gyros != null && gyros.Count != 0)) {
                    logger.Error("No reference grid set. Cannot go to home orientation.");
                    return;
                }
                refAxes.GoToHomeOrientation();
            }

            /// <summary>
            /// Handles the reset-home command.
            /// </summary>
            public void ResetHome() {
                if (!(IsInitialized() && gyros != null && gyros.Count != 0)) {
                    logger.Error("No reference grid set. Cannot reset home orientation.");
                    return;
                }
                refAxes.ResetHomeOrientation();
            }
            
            /// <summary>
            /// Parse and handle orientation commands.
            /// </summary>
            /// <param name="args">The command line arguments.</param>
            public void HandleCommand(string args) {
                foreach (var kvp in argParser.GetParsedArgs())
                {
                    switch (kvp.Key)
                    {
                        case "--orient":
                            bool argval = (bool)kvp.Value;
                            Orient(argval);
                            break;
                        case "--az":
                            int azDirection = (int)kvp.Value;
                            AzimuthRotate(azDirection);
                            break;
                        case "--el":
                            int elDirection = (int)kvp.Value;
                            ElevationRotate(elDirection);
                            break;
                        case "--roll":
                            int rollDirection = (int)kvp.Value;
                            RollRotate(rollDirection);
                            break;
                        case "--set-home":
                            SetHome();
                            break;
                        case "--home":
                            GoHome();
                            break;
                        case "--reset-home":
                            ResetHome();
                            break;
                        default:
                            logger.Error("Unknown GridOrient argument: " + kvp.Key);
                            break;
                    }
                }
                    return;
            }
            /* ^ Command Handling -------------------------------------------------- ^ */ 
        }
        // Create an instance of Logger.
        public static Logger logger;
        public ConfigFile config;
        public GridOrient gridOrient;

        public Program() {
            // Initialize the logger, enabling all output types.
            logger = new Logger(this)
            {
                UseEchoFallback = true,    // Fallback to program.Echo if no LCDs are available.
                LogToCustomData = false    // Disable logging to CustomData.
            };

            // Initialize grid auto-orientation system
            gridOrient = new GridOrient(this);

            // Initialize config
            config = new ConfigFile(Me, this);
            gridOrient.RegisterConfig(config);
            config.Logger = logger.Error;
            config.FinalizeRegistration();

            // Initialize argument parser
            argParser.RegisterCommand("init"); // Updates the reference grid
            argParser.RegisterCommand("grid-orient"); // for commands sent to the GridOrient class

            // Initialize the program
            Runtime.UpdateFrequency = STOP_RATE;
            gridRefBlock = null;
            refAxes = null;
            running = false;
            orienting = false;    
            shipRefBlock = FindShipRefBlock(this);
            if (shipRefBlock == null) {
                return;
            }
        }

        public void Main(string args, UpdateType updateSource) {
            // Parse the input argument string.
            if (!argParser.Parse(args))
            {
                // Output errors if parsing fails.
                foreach (string error in argParser.Errors)
                {
                    logger.Error("Error: " + error);
                }
                return;
            }

            // Iterate over parsed arguments using the iterator and a switch statement.
            foreach (var kvp in argParser.GetParsedArgs())
            {
                switch (kvp.Key)
                {
                    case "init": // no "--" for commands
                        bool initval = true;
                        Init(initval);
                        break;
                    case "grid-orient": // no "--" for commands
                        string orient_args = (string)kvp.Value;
                        gridOrient.HandleCommand(orient_args);
                        break;
                    default:
                        logger.Error("Unknown argument or command: " + kvp.Key);
                        break;
                }
            }

            // Perform control inputs
            PerformAutoOrientation();


            // Update Runtime sample rate
            Runtime.UpdateFrequency = gridOrient.updateFrequency;            
        }

#region PreludeFooter
    }
}
#endregion
