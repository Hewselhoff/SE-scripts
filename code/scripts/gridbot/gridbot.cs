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
        public List<IMyGyro> gyros;
        public bool running, orienting;
        // Set a update frequency constants
        public const UpdateFrequency SAMPLE_RATE = UpdateFrequency.Update1;
        public const UpdateFrequency CHECK_RATE = UpdateFrequency.Update100;
        public const UpdateFrequency STOP_RATE = UpdateFrequency.None;
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
                        lcd.WriteText(logText, false);
                        // Ensure the LCD is set to display the public text.
                        lcd.ContentType = ContentType.TEXT_AND_IMAGE;
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

        // The reusable config file parser.
        public static class ConfigFile
        {
            // The schema is a mapping from property names to their definitions.
            private static Dictionary<string, ConfigProperty> schema = new Dictionary<string, ConfigProperty>();
            private static string configText = null; // Holds the raw config text

            /// <summary>
            /// Registers a new configuration property.
            /// </summary>
            /// <param name="name">Name/key of the property.</param>
            /// <param name="type">Expected type.</param>
            /// <param name="defaultValue">Default value if not provided.</param>
            public static void RegisterProperty(string name, ConfigValueType type, object defaultValue)
            {
                if (schema.ContainsKey(name))
                    throw new Exception("Property already registered: " + name);

                schema[name] = new ConfigProperty
                {
                    Name = name,
                    ValueType = type,
                    DefaultValue = defaultValue,
                    Value = null
                };
            }

            /// <summary>
            /// Generates a YAML‐like text for all registered properties using their default values.
            /// </summary>
            public static string GenerateDefaultConfigText()
            {
                StringBuilder sb = new StringBuilder();
                foreach (var kvp in schema)
                {
                    string valueStr = ValueToString(kvp.Value.DefaultValue, kvp.Value.ValueType);
                    sb.AppendLine($"{kvp.Key}: {valueStr}");
                }
                return sb.ToString();
            }

            // Converts a value to a string representation.
            private static string ValueToString(object value, ConfigValueType type)
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
            /// Parses a YAML-like configuration string.
            /// Returns true if no errors were found; otherwise, errors are collected in the out parameter.
            /// </summary>
            /// <param name="configText">The configuration text to parse.</param>
            /// <param name="errors">List to collect any parsing errors.</param>
            /// <returns>True if parsing was successful, false otherwise.</returns>
            public static bool ParseConfig(string configText, out List<string> errors)
            {
                errors = new List<string>();

                // Store the raw config text for later use.
                ConfigFile.configText = configText;
                // Split the input into lines (ignoring empty lines)
                var lines = configText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                // To check for duplicate keys.
                HashSet<string> encountered = new HashSet<string>();

                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    // Skip blank lines or comments (lines starting with '#')
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    // Expect a colon separator.
                    int colonIndex = trimmed.IndexOf(':');
                    if (colonIndex < 0)
                    {
                        errors.Add($"Syntax error: Missing ':' in line: {line}");
                        continue;
                    }

                    string key = trimmed.Substring(0, colonIndex).Trim();
                    string valuePart = trimmed.Substring(colonIndex + 1).Trim();

                    // Check for duplicate property definitions.
                    if (encountered.Contains(key))
                    {
                        errors.Add($"Duplicate property: {key}");
                        continue;
                    }
                    encountered.Add(key);

                    // Unknown property?
                    if (!schema.ContainsKey(key))
                    {
                        errors.Add($"Unknown property: {key}");
                        continue;
                    }

                    var prop = schema[key];
                    object parsedValue = null;
                    bool parseSuccess = false;

                    // Parse according to the expected type.
                    switch (prop.ValueType)
                    {
                        case ConfigValueType.Int:
                            {
                                int intResult;
                                parseSuccess = int.TryParse(valuePart, out intResult);
                                parsedValue = intResult;
                            }
                            break;
                        case ConfigValueType.Float:
                            {
                                float floatResult;
                                parseSuccess = float.TryParse(valuePart, out floatResult);
                                parsedValue = floatResult;
                            }
                            break;
                        case ConfigValueType.String:
                            {
                                // For strings, we simply take the trimmed value.
                                parsedValue = valuePart;
                                parseSuccess = true;
                            }
                            break;
                        case ConfigValueType.ListInt:
                            {
                                if (valuePart.StartsWith("[") && valuePart.EndsWith("]"))
                                {
                                    string inner = valuePart.Substring(1, valuePart.Length - 2);
                                    var items = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    List<int> list = new List<int>();
                                    parseSuccess = true;
                                    foreach (var item in items)
                                    {
                                        int itemInt;
                                        if (int.TryParse(item.Trim(), out itemInt))
                                        {
                                            list.Add(itemInt);
                                        }
                                        else
                                        {
                                            errors.Add($"Invalid integer in list for property '{key}': {item}");
                                            parseSuccess = false;
                                            break;
                                        }
                                    }
                                    parsedValue = list;
                                }
                                else
                                {
                                    errors.Add($"Invalid list syntax for property '{key}'. Expected format: [item1, item2, ...]");
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
                                    parseSuccess = true;
                                    foreach (var item in items)
                                    {
                                        float itemFloat;
                                        if (float.TryParse(item.Trim(), out itemFloat))
                                        {
                                            list.Add(itemFloat);
                                        }
                                        else
                                        {
                                            errors.Add($"Invalid float in list for property '{key}': {item}");
                                            parseSuccess = false;
                                            break;
                                        }
                                    }
                                    parsedValue = list;
                                }
                                else
                                {
                                    errors.Add($"Invalid list syntax for property '{key}'. Expected format: [item1, item2, ...]");
                                }
                            }
                            break;
                        case ConfigValueType.ListString:
                            {
                                if (valuePart.StartsWith("[") && valuePart.EndsWith("]"))
                                {
                                    string inner = valuePart.Substring(1, valuePart.Length - 2);
                                    // Split by commas, but handle quoted strings.
                                    List<string> items = new List<string>();
                                    bool inQuotes = false;
                                    StringBuilder currentItem = new StringBuilder();
                                    
                                    for (int i = 0; i < inner.Length; i++)
                                    {
                                        char c = inner[i];
                                        if (c == '"')
                                        {
                                            inQuotes = !inQuotes;
                                            currentItem.Append(c);
                                        }
                                        else if (c == ',' && !inQuotes)
                                        {
                                            items.Add(currentItem.ToString().Trim());
                                            currentItem.Clear();
                                        }
                                        else
                                        {
                                            currentItem.Append(c);
                                        }
                                    }
                                    
                                    // Add the last item.
                                    if (currentItem.Length > 0)
                                    {
                                        items.Add(currentItem.ToString().Trim());
                                    }
                                    
                                    parsedValue = items;
                                    parseSuccess = true;
                                }
                                else
                                {
                                    errors.Add($"Invalid list syntax for property '{key}'. Expected format: [item1, item2, ...]");
                                }
                            }
                            break;
                        default:
                            errors.Add($"Unsupported property type for '{key}'.");
                            break;
                    }

                    if (parseSuccess)
                    {
                        prop.Value = parsedValue;
                    }
                    else
                    {
                        errors.Add($"Failed to parse value for property '{key}': {valuePart}");
                    }
                }

                return errors.Count == 0;
            }

            /// <summary>
            /// Gets the value of a property by name.
            /// </summary>
            /// <typeparam name="T">The expected type of the property.</typeparam>
            /// <param name="name">The property name.</param>
            /// <returns>The property value, or the default value if not found.</returns>
            public static T Get<T>(string name)
            {
                if (schema.ContainsKey(name) && schema[name].Value != null)
                {
                    return (T)schema[name].Value;
                }
                else if (schema.ContainsKey(name))
                {
                    return (T)schema[name].DefaultValue;
                }
                throw new Exception($"Property not found: {name}");
            }

            /// <summary>
            /// Writes default configuration to the programmable block's CustomData if it's empty.
            /// </summary>
            /// <param name="pb">The programmable block.</param>
            /// <param name="program">The grid program instance.</param>
            /// <returns>True if successful, false otherwise.</returns>
            public static bool CheckAndWriteDefaults(IMyProgrammableBlock pb, MyGridProgram program)
            {
                if (string.IsNullOrWhiteSpace(pb.CustomData))
                {
                    pb.CustomData = GenerateDefaultConfigText();
                    configText = pb.CustomData;
                    return true;
                }
                return true;
            }

            /// <summary>
            /// Checks if the CustomData has changed and reparses it if necessary.
            /// </summary>
            /// <param name="pb">The programmable block.</param>
            /// <param name="program">The grid program instance.</param>
            /// <returns>True if successful, false otherwise.</returns>
            public static bool CheckAndReparse(IMyProgrammableBlock pb, MyGridProgram program)
            {
                if (configText != pb.CustomData)
                {
                    List<string> errors;
                    if (!ParseConfig(pb.CustomData, out errors))
                    {
                        foreach (var error in errors)
                        {
                            logger.Error(error);
                        }
                        return false;
                    }
                }
                else if (configText != null && pb.CustomData == configText)
                {
                    // No changes detected, no need to re-parse.
                    return true;
                }

                // Update the stored config text for future comparisons.
                configText = pb.CustomData;
                return true;
            }
        }
        /* ^ ---------------------------------------------------------------------- ^ */ 
        /* ^ Caml Config File API                                                   ^ */ 
        /* ^ ---------------------------------------------------------------------- ^ */

        /* v ---------------------------------------------------------------------- v */ 
        /* v ArgParser API                                                          v */ 
        /* v ---------------------------------------------------------------------- v */
        // A general purpose argument parser for Space Engineers programmable blocks.
        public class ArgParser
        {
            // Nested class representing a registered argument definition.
            public class ArgDefinition
            {
                public string Name;      // The argument name (including the "--" prefix)
                public System.Type ArgType;     // The expected type (int, float, string, bool)
                public bool IsList;      // True if this argument should accept multiple values
                public bool IsRequired;  // True if this argument must be provided

                public ArgDefinition(string name, System.Type argType, bool isList = false, bool isRequired = false)
                {
                    // Ensure the name starts with "--"
                    Name = name.StartsWith("--") ? name : "--" + name;
                    ArgType = argType;
                    IsList = isList;
                    IsRequired = isRequired;
                }
            }

            // Dictionary holding all registered argument definitions.
            private System.Collections.Generic.Dictionary<string, ArgDefinition> registeredArgs = new System.Collections.Generic.Dictionary<string, ArgDefinition>();

            // Dictionary holding the parsed arguments and their values.
            // For single value arguments, the value is stored as object; for lists, it is a List<T>.
            private System.Collections.Generic.Dictionary<string, object> parsedArgs = new System.Collections.Generic.Dictionary<string, object>();

            // List of errors that occurred during parsing.
            public System.Collections.Generic.List<string> Errors { get; private set; } = new System.Collections.Generic.List<string>();

            // If true, the parser will only allow one argument per call.
            public bool OnlyAllowSingleArg { get; set; } = false;

            /// <summary>
            /// Registers a new argument definition.
            /// </summary>
            /// <param name="name">The argument name (with or without "--" prefix)</param>
            /// <param name="argType">The expected type (int, float, string, bool)</param>
            /// <param name="isList">If true, the argument accepts multiple space-separated values</param>
            /// <param name="isRequired">If true, the argument must be provided</param>
            public void RegisterArg(string name, System.Type argType, bool isList = false, bool isRequired = false)
            {
                var argDef = new ArgDefinition(name, argType, isList, isRequired);
                registeredArgs[argDef.Name] = argDef;
            }

            /// <summary>
            /// Gets the dictionary of parsed arguments.
            /// </summary>
            public System.Collections.Generic.Dictionary<string, object> ParsedArgs { get { return parsedArgs; } }

            /// <summary>
            /// Provides an enumerable to iterate over parsed arguments.
            /// </summary>
            public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>> GetParsedArgs()
            {
                return parsedArgs;
            }

            /// <summary>
            /// Parses the input string (from the Main method) into arguments.
            /// Returns true if parsing is successful (i.e. no errors); otherwise false.
            /// </summary>
            /// <param name="input">The argument string passed to Main</param>
            public bool Parse(string input)
            {
                // Clear previous errors and parsed arguments.
                Errors.Clear();
                parsedArgs.Clear();

                if (string.IsNullOrWhiteSpace(input))
                    return true; // Nothing to parse

                // Split the input by spaces.
                // (Note: for more advanced parsing, you might need to handle quoted strings.)
                var tokens = input.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                int countArgsParsed = 0;

                // Loop through tokens.
                for (int i = 0; i < tokens.Length; i++)
                {
                    string token = tokens[i];

                    // Each argument should start with "--"
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
                    System.Collections.Generic.List<string> values = new System.Collections.Generic.List<string>();

                    // Gather all following tokens that are not another argument.
                    int j = i + 1;
                    while (j < tokens.Length && !tokens[j].StartsWith("--"))
                    {
                        values.Add(tokens[j]);
                        j++;
                    }
                    i = j - 1; // Move index to the last token processed

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
                        // Manually create lists for each supported type.
                        if (def.ArgType == typeof(string))
                        {
                            System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
                            foreach (var val in values)
                            {
                                list.Add(val);
                            }
                            parsedArgs[token] = list;
                        }
                        else if (def.ArgType == typeof(int))
                        {
                            System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
                            foreach (var val in values)
                            {
                                int parsed;
                                if (!int.TryParse(val, out parsed))
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
                            System.Collections.Generic.List<float> list = new System.Collections.Generic.List<float>();
                            foreach (var val in values)
                            {
                                float parsed;
                                if (!float.TryParse(val, out parsed))
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
                            System.Collections.Generic.List<double> list = new System.Collections.Generic.List<double>();
                            foreach (var val in values)
                            {
                                double parsed;
                                if (!double.TryParse(val, out parsed))
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
                            System.Collections.Generic.List<bool> list = new System.Collections.Generic.List<bool>();
                            foreach (var val in values)
                            {
                                bool parsed;
                                if (!bool.TryParse(val, out parsed))
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

            /// <summary>
            /// Helper method that converts a string to the target type.
            /// Returns null if conversion fails.
            /// </summary>
            private object ConvertValue(string value, System.Type targetType)
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
            public const MatrixD ROT_PLUS_90_X = MatrixD.CreateRotationX(Math.PI / 2);
            public const MatrixD ROT_MINUS_90_X = MatrixD.CreateRotationX(-Math.PI / 2);
            public const MatrixD ROT_PLUS_90_Y = MatrixD.CreateRotationY(Math.PI / 2);
            public const MatrixD ROT_MINUS_90_Y = MatrixD.CreateRotationY(-Math.PI / 2);
            public const MatrixD ROT_PLUS_90_Z = MatrixD.CreateRotationZ(Math.PI / 2);
            public const MatrixD ROT_MINUS_90_Z = MatrixD.CreateRotationZ(-Math.PI / 2);

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
                return TransformLocalToWorldVector(localRef.Up);
            }

            public Vector3D GetWorldForward() {
                return TransformLocalToWorldVector(localRef.Forward);
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

        public static void GetMyGyros(MyGridProgram program, out List<IMyGyro> gyros) {
            List<IMyGyro> allGyros = new List<IMyGyro>();
            program.GridTerminalSystem.GetBlocksOfType(allGyros);
            gyros = allGyros.Where(g => g.CubeGrid == program.Me.CubeGrid).ToList();
            if (gyros.Count == 0) {
                logger.Error("No gyros found on the grid.");
            } else {
                logger.Info("Found " + gyros.Count + " gyros on the grid.");
            }
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
            return (shipRefBlock != null && gridRefBlock != null && gyros != null && gyros.Count != 0 && refAxes != null);
        }
        /* ^ ---------------------------------------------------------------------- ^ */ 
        /* ^ Grid Inspection Utilities                                              ^ */ 
        /* ^ ---------------------------------------------------------------------- ^ */

        /* v ---------------------------------------------------------------------- v */ 
        /* v Auto Orientation                                                       v */ 
        /* v ---------------------------------------------------------------------- v */
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
                Runtime.UpdateFrequency = SAMPLE_RATE;
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
            Runtime.UpdateFrequency = CHECK_RATE;
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
            Runtime.UpdateFrequency = STOP_RATE;
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
            Runtime.UpdateFrequency = CHECK_RATE;
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
        public void PerformAutoOrientation() {
            // if we're not running, just return.
            if (!running) {
                return;
            }
            // Call the alignment function to update gyro overrides.    
            double errorAngle = ApplyOrientationUpdate(shipRefBlock, gridRefBlock, gyros, ConfigFile.Get<float>("Kp"));
            
            logger.Error("Alignment error: " + (errorAngle * 180.0 / Math.PI).ToString("F2") + " degrees");

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
        /* ^ ---------------------------------------------------------------------- ^ */ 
        /* ^ Auto Orientation                                                       ^ */ 
        /* ^ ---------------------------------------------------------------------- ^ */

        /* v ---------------------------------------------------------------------- v */
        /* v Command Handlers                                                       v */
        /* v ---------------------------------------------------------------------- v */
        /// <summary>
        /// Handles starting or stopping the auto-orientation behavior.
        /// </summary>
        /// <param name="val">True to start auto-orientation, false to stop.</param>
        public void Orient(bool val) {
            if (!IsInitialized()) {
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
            if (IsInitialized()) {
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
            if (!IsInitialized()) {
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
            if (!IsInitialized()) {
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
            if (!IsInitialized()) {
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
            if (!IsInitialized()) {
                logger.Error("No reference grid set. Cannot set home orientation.");
                return;
            }
            refAxes.SetHomeOrientation();
        }

        /// <summary>
        /// Handles the home command.
        /// </summary>
        public void GoHome() {
            if (!IsInitialized()) {
                logger.Error("No reference grid set. Cannot go to home orientation.");
                return;
            }
            refAxes.GoToHomeOrientation();
        }

        /// <summary>
        /// Handles the reset-home command.
        /// </summary>
        public void ResetHome() {
            if (!IsInitialized()) {
                logger.Error("No reference grid set. Cannot reset home orientation.");
                return;
            }
            refAxes.ResetHomeOrientation();
        }
        /* ^ ---------------------------------------------------------------------- ^ */ 
        /* ^ Command Handlers                                                       ^ */ 
        /* ^ ---------------------------------------------------------------------- ^ */
        // Create an instance of Logger.
        public static Logger logger;

        public Program() {
            // Initialize the logger, enabling all output types.
            logger = new Logger(this)
            {
                UseEchoFallback = true,    // Fallback to program.Echo if no LCDs are available.
                LogToCustomData = false    // Disable logging to CustomData.
            };

            // Register config parameters
            ConfigFile.RegisterProperty("Kp", ConfigValueType.Float, 0.5f);
            ConfigFile.RegisterProperty("tolerance", ConfigValueType.Float, 0.01f);

            // Register command line arguments
            argParser.RegisterArg("orient", typeof(bool), false, false); // Turns auto-orientation on/off
            argParser.RegisterArg("init", typeof(bool), false, false); // Updates the reference grid
            argParser.RegisterArg("az", typeof(int), false, false); // Azimuth rotation
            argParser.RegisterArg("el", typeof(int), false, false); // Elevation rotation
            argParser.RegisterArg("roll", typeof(int), false, false); // Roll rotation
            argParser.RegisterArg("set-home", typeof(bool), false, false); // Set home orientation
            argParser.RegisterArg("home", typeof(bool), false, false); // Go to home orientation
            argParser.RegisterArg("reset-home", typeof(bool), false, false); // Reset home orientation
            
            argParser.OnlyAllowSingleArg = true;

            // Write default config if custom data is empty, and parse values.
            ConfigFile.CheckAndWriteDefaults(Me, this);
            if (!ConfigFile.CheckAndReparse(Me, this)) {
                logger.Error("Configuration parsing failed. Please fix the errors and run again.");
                return;
            }

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
            GetMyGyros(this, out gyros);
            if (gyros == null || gyros.Count == 0) {
                logger.Error("No gyros found.");
                return;
            }

        }

        public void Main(string args) {
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
                    case "--orient":
                        bool argval = (bool)kvp.Value;
                        Orient(argval);
                        break;
                    case "--init":
                        bool initval = (bool)kvp.Value;
                        Init(initval);
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
                        logger.Error("Unknown argument: " + kvp.Key);
                        break;
                }
            }

            // Perform the auto-orientation if running.
            PerformAutoOrientation();
            
        }

#region PreludeFooter
    }
}
#endregion
