private IMyShipController shipController;
private List<IMyGyro> gyros = new List<IMyGyro>(); // H2 Retro Thrusters.
private Vector3D gravVecW; // Gravity vector in World Frame [m/s^2].
private double anglePitch = 0;
private double angleRoll = 0;
private Vector3D alignmentVec = new Vector3D(0,0,0);

// Constants
private const UpdateFrequency SAMPLE_RATE = UpdateFrequency.Update1;
private const UpdateFrequency CHECK_RATE = UpdateFrequency.Update100;
private const UpdateFrequency STOP_RATE = UpdateFrequency.None;
private const double tolerance = 1.0e-5; // Zero approximation check tolerance.

private Logger logger; // Add [AC_LOG] tag to LCD name to display logging.
private StringBuilder sbLog = new StringBuilder(); // StringBuilder object to hold logging output.

// Create an instance of ArgParser.
private ArgParser argParser = new ArgParser();

public Program() {

   // Initialize the logger, enabling all output types.
   logger = new Logger(this){
       UseEchoFallback = false,    // Fallback to program.Echo if no LCDs are available.
       LogToCustomData = false     // Enable logging to CustomData.

   };

   // Register command line arguments
   argParser.RegisterArg("run", typeof(bool), false, false); // Run the controller.
   argParser.RegisterArg("stop", typeof(bool), false, false); // Stop the controller.

   // Register config parameters
   ConfigFile.RegisterProperty("Gyros", ConfigValueType.String, "Gyroscopes (AtmoProbe)");
   ConfigFile.RegisterProperty("ShipController", ConfigValueType.String, "RemoteControl (AtmoProbe)");

   // Write Config to Custom Data if is empty.
   if(string.IsNullOrWhiteSpace(Me.CustomData)){
      Me.CustomData = ConfigFile.GenerateDefaultConfigText();
   }

   // Load config parameters.
   ((IMyBlockGroup)GridTerminalSystem.GetBlockGroupWithName(ConfigFile.Get<string>("Gyros"))).GetBlocksOfType<IMyGyro>(gyros);
   shipController = (IMyRemoteControl)GridTerminalSystem.GetBlockWithName(ConfigFile.Get<string>("ShipController"));

   // We only want to run continously after event controller detects presence of
   // natural gravity.
   Runtime.UpdateFrequency = STOP_RATE;
}
bool running = false;

public void Main(string args) {

    logger.Clear(); 
    sbLog.Clear();

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
            case "--run":
                Runtime.UpdateFrequency = CHECK_RATE;
                running = true;
                break;
            case "--stop":
                ClearGyroOverrides();
                Runtime.UpdateFrequency = STOP_RATE;
                running = false;
                logger.Info("Running: " + running);
                return;
            default:
                logger.Warning("Unknown argument: " + kvp.Key);
                break;
        }
    }

    // Some logging for sanity checks.
    sbLog.AppendLine("Running: " + running);

    sbLog.AppendLine("Show Horiz Indicator: " + shipController.ShowHorizonIndicator);
    sbLog.AppendLine("Roll Indicator: " + shipController.RollIndicator);
    sbLog.AppendLine("Gyro Override Status:");

    foreach(var gyro in gyros){
       sbLog.AppendLine("  " + gyro.CustomName + ": " + gyro.GyroOverride);
    }

    sbLog.AppendLine("\nGyro Overrides:");

    foreach(var gyro in gyros){
       sbLog.AppendLine("  " + gyro.CustomName + ": ");
       sbLog.AppendLine("    Roll: " + gyro.Roll);
       sbLog.AppendLine("    Pitch: " + gyro.Pitch);
       sbLog.AppendLine("    Yaw: " + gyro.Yaw);
    }

    logger.Info("\n" + sbLog.ToString());

    // Make sure we are within planetary gravitational field!
    gravVecW = shipController.GetNaturalGravity();
    if(Math.Abs(gravVecW.Length()) < tolerance){
       logger.Clear(); 
       logger.Warning("CANNOT PERFORM STABILIZATION CALCULATIONS OUTSIDE OF GRAVITY WELL.");
       logger.Warning("ACTING PLANETARY GRAVITY: " + gravVecW.Length() + " [m/s^2].");
       // Just in case. Don't want to exit the gravity well and find out that
       // we can't manually control attitude.
       ClearGyroOverrides();
       return;
    }

    // We want to align body-frame down with gravity.
    alignmentVec = gravVecW;

    // World Matrix for the ship.
    MatrixD refMatrix = shipController.WorldMatrix;

    //---Get Roll and Pitch Angles using trigonometric dot product definition.
    anglePitch = Math.Acos(MathHelper.Clamp(alignmentVec.Dot(refMatrix.Forward) / alignmentVec.Length(), -1, 1)) - Math.PI / 2;

    Vector3D planetRelativeLeftVec = refMatrix.Forward.Cross(alignmentVec);

    angleRoll = Math.Acos(MathHelper.Clamp(refMatrix.Left.Dot(planetRelativeLeftVec) / planetRelativeLeftVec.Length(), -1, 1));
    angleRoll *= Math.Sign(VectorProjection(refMatrix.Left, alignmentVec).Dot(alignmentVec)); //ccw is positive 

    anglePitch *= -1; 
    angleRoll *= -1;

    double roll_deg = Math.Round(angleRoll / Math.PI * 180);
    double pitch_deg = Math.Round(anglePitch / Math.PI * 180);

    //---Angle controller    
    double rollSpeed = Math.Round(angleRoll, 2);
    double pitchSpeed = Math.Round(anglePitch, 2);

    //---Enforce rotation speed limit
    if(Math.Abs(rollSpeed) + Math.Abs(pitchSpeed) > 2 * Math.PI){
        double scale = 2 * Math.PI / (Math.Abs(rollSpeed) + Math.Abs(pitchSpeed));
        rollSpeed *= scale;
        pitchSpeed *= scale;
    }

    ApplyGyroOverride(pitchSpeed, 0, -rollSpeed, gyros, shipController);
}


// Turn off gyro overrides.
private void ClearGyroOverrides(){
   foreach (IMyGyro gyro in gyros){
      gyro.Pitch = 0.0f;
      gyro.Yaw = 0.0f;
      gyro.Roll = 0.0f;
      gyro.GyroOverride = false;
   }
}

//Whip's ApplyGyroOverride Method v9 - 8/19/17
private void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, IMyTerminalBlock reference){
   var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed); //because keen does some weird stuff with signs

   var shipMatrix = reference.WorldMatrix;
   var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix);

   foreach (var thisGyro in gyro_list)
   {
       var gyroMatrix = thisGyro.WorldMatrix;
       var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(gyroMatrix));

       thisGyro.Pitch = (float)transformedRotationVec.X;
       thisGyro.Yaw = (float)transformedRotationVec.Y;
       thisGyro.Roll = (float)transformedRotationVec.Z;
       thisGyro.GyroOverride = true;
   }
}

// Whip's projection method.
private Vector3D VectorProjection(Vector3D a, Vector3D b) //proj a on b
{
    Vector3D projection = a.Dot(b) / b.LengthSquared() * b;
    return projection;
}

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
                    //Echo(error);
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
    private Dictionary<string, ArgDefinition> registeredArgs = new Dictionary<string, ArgDefinition>();

    // Dictionary holding the parsed arguments and their values.
    // For single value arguments, the value is stored as object; for lists, it is a List<T>.
    private Dictionary<string, object> parsedArgs = new Dictionary<string, object>();

    // List of errors that occurred during parsing.
    public List<string> Errors { get; private set; } = new List<string>();

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
    public Dictionary<string, object> ParsedArgs { get { return parsedArgs; } }

    /// <summary>
    /// Provides an enumerable to iterate over parsed arguments.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>> GetParsedArgs()
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
            List<string> values = new List<string>();

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
                    List<string> list = new List<string>();
                    foreach (var val in values)
                    {
                        list.Add(val);
                    }
                    parsedArgs[token] = list;
                }
                else if (def.ArgType == typeof(int))
                {
                    List<int> list = new List<int>();
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
                    List<float> list = new List<float>();
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
                    List<double> list = new List<double>();
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
                    List<bool> list = new List<bool>();
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
/* v Logging API                                                            v */
/* v ---------------------------------------------------------------------- v */
/// <summary>
/// Logger class provides a simple logging interface with three log levels.
/// It writes output to LCD panels tagged with "[AC_LOG]" on the same grid,
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
    /// Constructor – automatically finds LCD panels with "[AC_LOG]" in their name on the same grid.
    /// Initializes the cache for each panel.
    /// </summary>
    public Logger(MyGridProgram program)
    {
        this.program = program;

        // Find all IMyTextPanel blocks with "[AC_LOG]" in the name.
        lcdPanels = new List<IMyTextPanel>();
        List<IMyTextPanel> allPanels = new List<IMyTextPanel>();
        program.GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(allPanels, panel => panel.CustomName.Contains("[AC_LOG]"));

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
                line = "  "+line;
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
