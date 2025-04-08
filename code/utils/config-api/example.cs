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
namespace SpaceEngineers.UWBlockPrograms.CamlApiExample {
    public sealed class Program : MyGridProgram {
    // Your code goes between the next #endregion and #region
#endregion

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

        public ConfigFile config;

        public Program() {
            // Initialize the configuration.
            config = new ConfigFile(Me, this);
            // Register root-level configuration properties.
            config.RegisterProperty("anInteger", ConfigValueType.Int, 42);
            config.RegisterProperty("aFloat", ConfigValueType.Float, 3.14f);
            config.RegisterProperty("anIntList", ConfigValueType.IntList, new List<int> { 1, 1, 2, 3, 5, 8, 13 });

            // Register a sub configuration.
            config.RegisterSubConfig("subConfig");

            // Register properties for the sub config.
            config.RegisterProperty("subConfig", "aString", ConfigValueType.String, "Foo");
            config.RegisterProperty("subConfig", "aFloatList", ConfigValueType.FloatList, new List<float> { 0.25f, 0.50f, 0.75f });
            config.RegisterProperty("subConfig", "aStringList", ConfigValueType.StringList, new List<string> { "Bar", "Baz" });

            // Finalize registration to lock the schema.
            config.FinalizeRegistration();
        }

        public void Main(string args) {    
            // Re-parse in case the Custom Data has changed.
            if (!config.CheckAndReparse()) {
                Echo("Configuration parsing failed. Please fix the errors and run again.");
                return;
            }

            // Retrieve values from the configuration.
            int anInteger = config.Get<int>("anInteger");
            float aFloat = config.Get<float>("aFloat");
            List<int> anIntList = config.Get<List<int>>("anIntList");
            string aString = config.Get<string>("subConfig", "aString");
            List<float> aFloatList = config.Get<List<float>>("subConfig", "aFloatList");
            List<string> aStringList = config.Get<List<string>>("subConfig", "aStringList");
            // Output the values to the terminal.
            Echo($"anInteger: {anInteger}");
            Echo($"aFloat: {aFloat}");
            Echo($"anIntList: {string.Join(", ", anIntList)}");
            Echo("subConfig:");
            Echo($"  aString: {aString}");
            Echo($"  aFloatList: {string.Join(", ", aFloatList)}");
            Echo($"  aStringList: {string.Join(", ", aStringList)}");
        }

#region PreludeFooter
    }
}
#endregion