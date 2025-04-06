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
        public List<IMyGyro> gyros;
        public bool running, orienting;
        // Set a global update frequency (sample rate)
        public const UpdateFrequency SAMPLE_RATE = UpdateFrequency.Update1;
        public const UpdateFrequency CHECK_RATE = UpdateFrequency.Update100;
        public const UpdateFrequency STOP_RATE = UpdateFrequency.None;

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
                                    var items = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    List<string> list = new List<string>();
                                    parseSuccess = true;
                                    foreach (var item in items)
                                    {
                                        // Simply trim the item (you might add unquoting logic if needed)
                                        list.Add(item.Trim());
                                    }
                                    parsedValue = list;
                                }
                                else
                                {
                                    errors.Add($"Invalid list syntax for property '{key}'. Expected format: [item1, item2, ...]");
                                }
                            }
                            break;
                        default:
                            errors.Add($"Unsupported type for property '{key}'");
                            break;
                    }

                    if (!parseSuccess)
                    {
                        errors.Add($"Failed to parse value for property '{key}': {valuePart}");
                    }
                    else
                    {
                        // Store the successfully parsed value.
                        prop.Value = parsedValue;
                    }
                }

                // Check for any missing properties in the config.
                foreach (var kvp in schema)
                {
                    if (!encountered.Contains(kvp.Key))
                    {
                        errors.Add($"Missing property: {kvp.Key}");
                        // Optionally, you can assign the default if missing:
                        kvp.Value.Value = kvp.Value.DefaultValue;
                    }
                }

                return errors.Count == 0;
            }

            /// <summary>
            /// Retrieves the parsed configuration value for the given property.
            /// </summary>
            /// <typeparam name="T">The expected type of the property.</typeparam>
            /// <param name="propertyName">The name of the property.</param>
            /// <returns>The value of the property.</returns>
            public static T Get<T>(string propertyName)
            {
                if (!schema.ContainsKey(propertyName))
                    throw new Exception("Property not registered: " + propertyName);
                return (T)schema[propertyName].Value;
            }

            /// <summary>
            /// Check if custom data is empty and write defaults if needed.
            /// </summary>
            /// <param name="pb">The programmable block instance.</param>
            /// <param name="program">The instance of MyGridProgram to call Echo.</param>
            /// <returns>True if defaults were written, false otherwise.</returns>
            public static void CheckAndWriteDefaults(IMyProgrammableBlock pb, MyGridProgram program)
            {
                if (string.IsNullOrWhiteSpace(pb.CustomData))
                {
                    string defaultConfig = GenerateDefaultConfigText();
                    pb.CustomData = defaultConfig;
                    program.Echo("No configuration data found. Default config added to Custom Data.");
                }
            }

            /// <summary>
            /// Check if custom data changed and re-parse if needed.
            /// </summary>
            /// <param name="pb">The programmable block instance.</param>
            /// <param name="program">The instance of MyGridProgram to call Echo.</param>
            /// <returns>True if re-parsing was successful, false otherwise.</returns>
            public static bool CheckAndReparse(IMyProgrammableBlock pb, MyGridProgram program)
            {
                // Check if the custom data has changed since the last parse.
                if (string.IsNullOrWhiteSpace(pb.CustomData))
                    return false; // No data to parse.

                // If the custom data is different from the last parsed config text, re-parse it.
                if (configText == null || pb.CustomData != configText)
                {
                    List<string> errors;
                    if (!ParseConfig(pb.CustomData, out errors))
                    {
                        // Print errors to the console.
                        foreach (var error in errors)
                        {
                            program.Echo(error);
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
                        program.Echo("Found ref connector: " + connector.CustomName);
                        return otherConnector;
                    }
                }
            }
            program.Echo("No suitable ref connector found.");
            return null;
        }

        // Find ship ref block (remote control)
        public static IMyRemoteControl FindShipRefBlock(MyGridProgram program) {
            List<IMyRemoteControl> remotes = new List<IMyRemoteControl>();
            program.GridTerminalSystem.GetBlocksOfType(remotes);
            var shipRefBlock = remotes.FirstOrDefault(r => r.CubeGrid == program.Me.CubeGrid);
            if (shipRefBlock == null) {
                program.Echo("No ship reference block found.");
                return null;
            }
            program.Echo("Found ship reference block: " + shipRefBlock.CustomName);
            return shipRefBlock;
        }

        public static void GetMyGyros(MyGridProgram program, out List<IMyGyro> gyros) {
            List<IMyGyro> allGyros = new List<IMyGyro>();
            program.GridTerminalSystem.GetBlocksOfType(allGyros);
            gyros = allGyros.Where(g => g.CubeGrid == program.Me.CubeGrid).ToList();
            if (gyros.Count == 0) {
                program.Echo("No gyros found on the grid.");
            } else {
                program.Echo("Found " + gyros.Count + " gyros on the grid.");
            }
        }
        /* ^ ---------------------------------------------------------------------- ^ */
        /* ^ Grid Inspection Utilities                                              ^ */
        /* ^ ---------------------------------------------------------------------- ^ */

        // Alignment function that computes the rotation error and applies gyro overrides.
        // Returns the angle error in radians.
        public double PerformGridAutoOrientation(IMyTerminalBlock shipBlock, IMyTerminalBlock targetBlock, List<IMyGyro> gyros, double Kp = 1.0)
        {
            // Use the ship grid's world matrix.
            MatrixD shipMatrix = Me.CubeGrid.WorldMatrix;
            MatrixD targetMatrix = targetBlock.CubeGrid.WorldMatrix;
            
            // Retrieve inversion flags from the config.
            int invX = ConfigFile.Get<int>("invert-x");
            int invY = ConfigFile.Get<int>("invert-y");
            int invZ = ConfigFile.Get<int>("invert-z");
            
            // Determine the multipliers for each axis.
            double factorX = (invX == 1 ? -1.0 : 1.0);
            double factorY = (invY == 1 ? -1.0 : 1.0);
            double factorZ = (invZ == 1 ? -1.0 : 1.0);
            
            // Pre-invert the target's orientation by modifying its basis vectors.
            MatrixD modTargetMatrix = targetMatrix;  // copy the original matrix
            modTargetMatrix.Right = targetMatrix.Right * factorX;
            modTargetMatrix.Up = targetMatrix.Up * factorY;
            modTargetMatrix.Forward = targetMatrix.Forward * factorZ;
            
            // Compute the relative rotation matrix (the error rotation from ship to target).
            MatrixD relativeMatrix = modTargetMatrix * MatrixD.Transpose(shipMatrix);
            
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


        public bool SetReferenceGrid(MyGridProgram program) {
            if (shipRefBlock == null) {
                program.Echo("No ship reference block found.");
                return false;
            }
            gridRefBlock = FindTargetRefBlock(this);
            if (gridRefBlock == null) {
                program.Echo("No target reference block found.");
                return false;
            }
            GetMyGyros(this, out gyros);
            if (gyros == null || gyros.Count == 0) {
                program.Echo("No gyros found.");
                return false;
            }
            program.Echo("Reference grid set to " + gridRefBlock.CubeGrid.CustomName + ".");
            return true;
        }

        public bool IsInitialized() {
            return (shipRefBlock != null && gridRefBlock != null && gyros != null && gyros.Count != 0);
        }

        // Start active gyro overrides.
        public void StartGridAutoOrientation() {
            if (!IsInitialized()) {
                Echo("Reference not initialized. Cannot start auto orientation.");
                return;
            }
            // Set update frequency if not already running the control loop.
            if (!orienting) {
                Runtime.UpdateFrequency = SAMPLE_RATE;
                orienting = true;
                Echo("Reorienting to " + gridRefBlock.CubeGrid.CustomName + ".");
            }
        }

        // Stop active gyro overrides.
        public void StopGridAutoOrientation() {
            foreach (var gyro in gyros) {
                gyro.SetValueBool("Override", false);
                gyro.SetValueFloat("Power", 0f);
                gyro.SetValueFloat("Pitch", 0f);
                gyro.SetValueFloat("Yaw", 0f);
                gyro.SetValueFloat("Roll", 0f);
            }
            Runtime.UpdateFrequency = CHECK_RATE;
            orienting = false;
            Echo("Orientation complete.");
        }

        public Program() {
            // Register config parameters
            ConfigFile.RegisterProperty("Kp", ConfigValueType.Float, 0.5f);
            ConfigFile.RegisterProperty("precision", ConfigValueType.Float, 0.01f);
            ConfigFile.RegisterProperty("invert-x", ConfigValueType.Int, 0);
            ConfigFile.RegisterProperty("invert-y", ConfigValueType.Int, 0);
            ConfigFile.RegisterProperty("invert-z", ConfigValueType.Int, 0);

            // Initialize the program
            Runtime.UpdateFrequency = STOP_RATE;
            gridRefBlock = null;
            running = false;
            orienting = false;    
            shipRefBlock = FindShipRefBlock(this);
            if (shipRefBlock == null) {
                return;
            }
        }

        public void Main(string args) {
            // If we're not initialized, try to initialize
            if (!IsInitialized()) {
                if (!SetReferenceGrid(this)) {
                    Echo("Initialization failed. Exiting.");
                    return;
                }
                Echo("Initialization complete.");
                return;
            }
            
            // Set update frequency if not already running the control loop.
            if (!running) {
                // Write default config if custom data is empty.
                ConfigFile.CheckAndWriteDefaults(Me, this);
                if (!ConfigFile.CheckAndReparse(Me, this)) {
                    Echo("Configuration parsing failed. Please fix the errors and run again.");
                    return;
                }
                running = true;
                Echo("Starting auto orientation.");
                return;
            }

            // Call the alignment function to update gyro overrides.    
            double errorAngle = PerformGridAutoOrientation(shipRefBlock, gridRefBlock, gyros, ConfigFile.Get<float>("Kp"));
            
            Echo("Alignment error: " + (errorAngle * 180.0 / Math.PI).ToString("F2") + " degrees");

            if (orienting && Math.Abs(errorAngle) <= ConfigFile.Get<float>("precision")) {
                StopGridAutoOrientation();
            } else if (!orienting && Math.Abs(errorAngle) > ConfigFile.Get<float>("precision")) {
                StartGridAutoOrientation();
            } else {
                // Re-parse in case of changes in Custom Data.
                // we'll only do this in the slow update for performance reasons.
                if (!ConfigFile.CheckAndReparse(Me, this)) {
                    Echo("Configuration parsing failed. Please fix the errors and run again.");
                    return;
                }
            }
        }


#region PreludeFooter
    }
}
#endregion