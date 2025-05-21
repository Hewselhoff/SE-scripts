public IMyRemoteControl shipRefBlock;
public IMyShipConnector gridRefBlock;
public const UpdateFrequency SAMPLE_RATE = UpdateFrequency.Update1;
public const UpdateFrequency CHECK_RATE = UpdateFrequency.Update100;
public const UpdateFrequency STOP_RATE = UpdateFrequency.None;

// Create an instance of ArgParser.
ArgParser argParser = new ArgParser();

private List<IMyMotorSuspension> wheels = new List<IMyMotorSuspension>();
private IMyShipConnector dockingConnector;
private IMyExtendedPistonBase piston;
private IMyMotorAdvancedStator toolHinge;
private IMyShipWelder welder; // Enabled = true/false.
private IMyLandingGear magPlate;
private string statusBroadCastTag = "[GANTRY_STATUS]";

public Program() {

    // Register command line arguments
    argParser.RegisterArg("forward", typeof(float), false, false); // Forward movement
    argParser.RegisterArg("fwd", typeof(bool), false, false); // Forward movement at default speed
    argParser.RegisterArg("back", typeof(float), false, false); // Backward movement
    argParser.RegisterArg("bck", typeof(bool), false, false); // Backward movement at default speed
    argParser.RegisterArg("up", typeof(bool), false, false); // Raise piston
    argParser.RegisterArg("down", typeof(bool), false, false); // Lower piston
    argParser.RegisterArg("brake", typeof(bool), false, false); // Apply brakes
    argParser.RegisterArg("dock", typeof(bool), false, false); // Apply E-Brake and dock
    argParser.RegisterArg("speed", typeof(float), false, false); // Set speed override
    argParser.RegisterArg("hinge_up", typeof(bool), false, false); // Raise tool hinge
    argParser.RegisterArg("hinge_dwn", typeof(bool), false, false); // Lower tool hinge
    argParser.RegisterArg("weld", typeof(bool), false, false); // Toggle welder
    argParser.RegisterArg("toggle_mag_lock", typeof(bool), false, false); // Toggle mag plate
    
    argParser.OnlyAllowSingleArg = true;

    // Register config parameters
    ConfigFile.RegisterProperty("wheels", ConfigValueType.String, "Wheels (Trolley)");
    ConfigFile.RegisterProperty("connector", ConfigValueType.String, "ConnectorDock (Trolley)");
    ConfigFile.RegisterProperty("piston", ConfigValueType.String, "V-Piston (Trolley)");
    ConfigFile.RegisterProperty("tool_hinge", ConfigValueType.String, "ToolHinge (Trolley)");
    ConfigFile.RegisterProperty("welder", ConfigValueType.String, "Welder (Trolley)");
    ConfigFile.RegisterProperty("mag_plate", ConfigValueType.String, "MagPlate (Trolley)");
    ConfigFile.RegisterProperty("statusTag", ConfigValueType.String, "[GANTRY_STATUS]");
    
    Echo("wheels: " + ConfigFile.Get<string>("wheels"));

    // Grab the Block Group that includes all accessible turrets. Do all of this here in the ctor
    // so we don't allocate new memory every time the main script executes.
    ((IMyBlockGroup)GridTerminalSystem.GetBlockGroupWithName(ConfigFile.Get<string>("wheels"))).GetBlocksOfType<IMyMotorSuspension>(wheels);
    dockingConnector = (IMyShipConnector)GridTerminalSystem.GetBlockWithName(ConfigFile.Get<string>("connector"));

    statusBroadCastTag = ConfigFile.Get<string>("statusTag");
    
    // These components are not present in end truck assemblies.  
    try{
       piston = (IMyExtendedPistonBase)GridTerminalSystem.GetBlockWithName(ConfigFile.Get<string>("piston"));
       toolHinge = (IMyMotorAdvancedStator)GridTerminalSystem.GetBlockWithName(ConfigFile.Get<string>("tool_hinge"));
       welder = (IMyShipWelder)GridTerminalSystem.GetBlockWithName(ConfigFile.Get<string>("welder"));
       magPlate = (IMyLandingGear)GridTerminalSystem.GetBlockWithName(ConfigFile.Get<string>("mag_plate"));
    }catch(Exception e){
       Echo("WARNING: No piston/hinge/welder/mag-plate detected." + e.ToString());
       throw;
    }

    // Initialize the program
    Runtime.UpdateFrequency = STOP_RATE;
    gridRefBlock = null;
    shipRefBlock = FindShipRefBlock(this);
    if (shipRefBlock == null) {
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
            Echo("Error: " + error);
        }
        return;
    }

    // Iterate over parsed arguments using the iterator and a switch statement.
    foreach (var kvp in argParser.GetParsedArgs())
    {
        switch (kvp.Key)
        {
            case "--forward":
                Forward((float)kvp.Value);
                break;
            case "--fwd":
                Forward(speedOverride);
                break;
            case "--back":
                Back((float)kvp.Value);
                break;
            case "--bck":
                Back(speedOverride);
                break;
            case "--up":
                Up();
                break;
            case "--down":
                Down();
                break;
            case "--brake":
                bool apply = (bool)kvp.Value;
                Brake(apply);
                break;
            case "--dock":
                bool dock = (bool)kvp.Value;
                Dock(dock);
                break;
            case "--speed":
                float speed = (float)kvp.Value;
                SetSpeed(speed);
                break;
            case "--hinge_up":
                HingeUp();
                break;
            case "--hinge_dwn":
                HingeDown();
                break;
            case "--weld":
                ToggleWelder();
                break;
            case "--toggle_mag_lock":
                ToggleMagPlate();
                break;
            default:
                Echo("Unknown argument: " + kvp.Key);
                break;
        }
    }
    ReportStatus();
}

// Find ship ref block (remote control)
public static IMyRemoteControl FindShipRefBlock(MyGridProgram program) {
    List<IMyRemoteControl> remotes = new List<IMyRemoteControl>();
    program.GridTerminalSystem.GetBlocksOfType(remotes);
    var shipRefBlock = remotes.FirstOrDefault(r => r.CubeGrid == program.Me.CubeGrid);
    if (shipRefBlock == null) {
        return null;
    }
    return shipRefBlock;
}

private float speedOverride = 0.01f;
private bool isMoving = false;

/// <summary>
/// Handles the forward command.
/// </summary>
public void Forward(float spd) {
    // If carriage is already in motion, then stop it.
    if(isMoving){
       Brake(true);
       Echo("Stopping.");
       return;
    }

    if(spd != 0.0f){
       Echo("Going!");
       isMoving = true;
       Brake(false);
    }


    foreach(IMyMotorSuspension wheel in wheels){
       wheel.PropulsionOverride = spd;
    }
    Echo("Propulsion Override: " + spd);
}

/// <summary>
/// Handles the back command.
/// </summary>
public void Back(float spd) {
    Forward(-spd);
}

/// <summary>
/// Raises piston.
/// </summary>
public void Up() {
   if(piston.Status == PistonStatus.Retracting){
      SetPistonVelocity(0.0f);
      piston.MinLimit = piston.CurrentPosition;
      Runtime.UpdateFrequency = STOP_RATE;
   }else{
      Echo("Piston is retracting.");
      piston.MinLimit = piston.LowestPosition;
      SetPistonVelocity(0.5f);
      Runtime.UpdateFrequency = SAMPLE_RATE;
      piston.Retract();
   }
}

/// <summary>
/// Lowers piston.
/// </summary>
public void Down() {
   if(piston.Status == PistonStatus.Extending){
      piston.MaxLimit = piston.CurrentPosition;
      SetPistonVelocity(0.0f);
      Echo("Piston stopped.");
      Runtime.UpdateFrequency = STOP_RATE;
   }else{
      Echo("Piston is extending.");
      piston.MaxLimit = piston.HighestPosition;
      SetPistonVelocity(0.5f);
      Runtime.UpdateFrequency = SAMPLE_RATE;
      piston.Extend();
   }
}

/// <summary>
/// Toggles brakes on/off.
/// <param name="apply">boolean indicating whether brakes are to be applied</param>
/// </summary>
public void Brake(bool apply) {
    
    if(apply){
       isMoving = false;
       Forward(0.0f); // Reduce wheel speed to 0
    }
    // Apply brakes to all wheels.
    foreach(IMyMotorSuspension wheel in wheels){
       wheel.Brake = apply;
    }
    Echo("Brakes Engaged: " + apply);
}

/// <summary>
/// Dock carriage to connector.
/// <param name="dock">obsolete (TODO: Remove)</param>
/// </summary>
public void Dock(bool dock) {
    if(dockingConnector != null){
       Brake(true);
       dockingConnector.ToggleConnect();
       Echo("Connector Status: " + dockingConnector.Status.ToString());
    }else{
       Echo("ERROR: No docking connector detected.");
    }
}

/// <summary>
/// Set Override Speed.
/// <param name="speed">Override Speed as a percentage [0,1]</param>
/// </summary>
public void SetSpeed(float speed) {
   speedOverride = speed;
}

/// <summary>
/// Set Piston Velocity.
/// <param name="vel">Piston velocity in meters/second</param>
/// </summary>
private void SetPistonVelocity(float vel){
   piston.Velocity = vel;
}

/// <summary>
/// Raise hinge.
/// </summary>
public void HingeUp() {
   if(!toolHinge.RotorLock) {
      toolHinge.TargetVelocityRPM = 0.0f;
      toolHinge.RotorLock = true;
      Runtime.UpdateFrequency = STOP_RATE;
      return;
   }

   if(toolHinge.Angle > toolHinge.LowerLimitDeg) {
      toolHinge.RotorLock = false;
      toolHinge.TargetVelocityRPM = -2.0f;
      Runtime.UpdateFrequency = SAMPLE_RATE;
   }
   
   /* Hinge Fields and Methods
   toolHinge.LowerLimitDeg = 0.0f;
   toolHinge.UpperLimitDeg = 0.0f;
   toolHinge.Torque = 20.0f; // Newton-meters?
   toolHinge.RotorLock = true;
   toolHinge.RotateToAngle(MyRotationDirection,float angle, float absoluteVelocityRpm);
   toolHinge.Angle
   */
}

/// <summary>
/// Lower hinge.
/// </summary>
public void HingeDown() {
   if(!toolHinge.RotorLock) {
      toolHinge.TargetVelocityRPM = 0.0f;
      toolHinge.RotorLock = true;
      Runtime.UpdateFrequency = STOP_RATE;
      return;
   }

   if(toolHinge.Angle < toolHinge.UpperLimitDeg) {
      toolHinge.RotorLock = false;
      toolHinge.TargetVelocityRPM = 2.0f;
      Runtime.UpdateFrequency = SAMPLE_RATE;
   }
}

/// <summary>
/// Report hinge angle.
/// </summary>
private void GetHingeAngle(ref StringBuilder sb){
   int angle = Convert.ToInt32(MathHelper.ToDegrees(toolHinge.Angle));
   // The degree symbol (°) can be inserted by holding Alt and entering 176 
   // in the num-pad.
   sb.AppendLine("Hinge Angle: " + angle.ToString() + "°");

}

/// <summary>
/// Toggle Welder.
/// </summary>
public void ToggleWelder() {
   welder.Enabled = welder.Enabled ? false : true;
}

/// <summary>
/// Toggle Mag Plate Lock.
/// </summary>
public void ToggleMagPlate() {
   magPlate.ToggleLock();
}

/// <summary>
/// Report status of gantry crane systems.
/// </summary>
private void ReportStatus(){
   StringBuilder statusStr = new StringBuilder();
   GetHingeAngle(ref statusStr);
   // The ToString() formats the value to round to 2 decimal places.
   statusStr.AppendLine("Piston Position: " + piston.CurrentPosition.ToString("F", new System.Globalization.CultureInfo("en-US")) + "m");
   statusStr.AppendLine("Update Freq: " + Runtime.UpdateFrequency.ToString());
   IGC.SendBroadcastMessage(statusBroadCastTag, statusStr.ToString());
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

