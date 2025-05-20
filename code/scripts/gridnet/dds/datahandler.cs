// This script was a hack just to facilitate data transfer.
// This script is no longer relevant.
private GridNIC nic;
private List<IMyTextPanel> displays = new List<IMyTextPanel>();
private string DISPLAY_TEXT_PANEL_TAG = "[STATUS]";
private StringBuilder _outputBuilder = new StringBuilder();

// Create an instance of ArgParser.
ArgParser argParser = new ArgParser();
private bool LOGGING = false;

public Program(){
   Runtime.UpdateFrequency = UpdateFrequency.Once;
   nic = new GridNIC(this);

   // Register command line arguments
   argParser.RegisterArg("display", typeof(string), false, false); // Display provided telmetry 

   ConfigFile.RegisterProperty("DisplayTag", ConfigValueType.String, "[STATUS]");
   DISPLAY_TEXT_PANEL_TAG = ConfigFile.Get<string>("DisplayTag");


   if(LOGGING){
      Echo("Logging is ON");
   }else{
      Echo("Logging is OFF");
   }

}

public void Main(string args){

    _outputBuilder.Clear();
    _outputBuilder.AppendLine("Telemetry Exchange System");
    _outputBuilder.AppendLine();
    // Parse the input argument string.
    if (!argParser.Parse(args))
    {
       Echo("LOGS.......");
       foreach (string log in argParser.Logs)
       {
           Echo(log);
       }
       // Output errors if parsing fails.
       Echo("ERRORS.......");
       foreach (string error in argParser.Errors)
       {
           Echo("Error: " + error);
       }
       return;
    }
   
    // TODO: Remove
    if(LOGGING){
       Echo("LOGS.......");
       foreach (string log in argParser.Logs)
       {
           Echo(log);
       }
    }

    // Find the display panels
    displays = FindDisplayPanels();
    if (displays.Count == 0)
    {
        _outputBuilder.AppendLine("WARNING: No display panels found.");
        _outputBuilder.AppendLine($"Name a text panel with the tag {DISPLAY_TEXT_PANEL_TAG} to show status.");
        //return;
    }else{
       if(LOGGING){
          foreach(var display in displays){
             Echo("display: " + display.CustomName);
          }
       }
    }

    Echo(_outputBuilder.ToString());

    // Iterate over parsed arguments using the iterator and a switch statement.
    foreach (var kvp in argParser.GetParsedArgs())
    {
        switch (kvp.Key)
        {
            case "--display":
                UpdateDisplay((string)kvp.Value);
                break;
            default:
                Echo("Unknown argument: " + kvp.Key);
                break;
        }
    }

}

private List<IMyTextPanel> FindDisplayPanels()
{
    List<IMyTextPanel> panels = new List<IMyTextPanel>();
    GridTerminalSystem.GetBlocksOfType(panels, p => p.CustomName.Contains(DISPLAY_TEXT_PANEL_TAG) && p.IsSameConstructAs(Me));
    
    return panels;
}        


private void UpdateDisplay(string data)
{
    // If we have display panels, update them
    foreach(var display in displays){
        display.ContentType = ContentType.TEXT_AND_IMAGE;
        display.WriteText(data);
        if(LOGGING){
           Echo("Updating Display: " + display.CustomName);
           Echo("Data: " + data);
        }
    }
    
    // Also echo the output to the programmable block's terminal
}


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

        string customData = Me.CustomData;
        if (string.IsNullOrWhiteSpace(customData))
        {
            // Generate default configuration
            customData = GenerateDefaultConfigText();
            Me.CustomData = customData;
            Echo("No configuration found. Default config added to Custom Data.");
        }

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
    public List<string> Logs { get; private set; } = new List<string>();

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
        Logs.Clear();
        parsedArgs.Clear();

        if (string.IsNullOrWhiteSpace(input))
            return true; // Nothing to parse

        // Check for args enclosed in quotes.
        var tokens = input.Split(new char[] { '"' }, System.StringSplitOptions.RemoveEmptyEntries);
        Logs.Add("Quoted Tokens: ");
        foreach(var token in tokens){
           Logs.Add(" " + token);
        }

        // Split the input by spaces.
        if(tokens.Length <= 1){
           tokens = input.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
           Logs.Add("Tokens separated by whitespace: ");
           foreach(var token in tokens){
              Logs.Add(" " + token);
           }
        }

        int countArgsParsed = 0;

        // Loop through tokens.
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            Logs.Add("Token (" + i + "):|" + token + "|");
            Logs.Add("Token (" + i + ") Trimmed:|" + token.Trim() + "|");
            token = token.Trim();
            Logs.Add("Token (" + i + ") Trimmed2:|" + token.Trim() + "|");

            // Each argument should start with "--"
            if (!token.StartsWith("--"))
            {
                Errors.Add("Value provided without a preceding argument:(" + token + ")");
                continue;
            }

            // Check if the argument is registered.
            if (!registeredArgs.ContainsKey(token))
            {
                Errors.Add("Unrecognized argument:(" + token + ")");
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
