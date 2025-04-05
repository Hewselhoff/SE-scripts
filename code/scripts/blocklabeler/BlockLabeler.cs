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
            //program.Echo("No configuration data found. Default config added to Custom Data.");
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


private List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
private string PB_GRID_NAME = "";

public Program() {
    Me.CustomData = "";

    Runtime.UpdateFrequency = UpdateFrequency.Once;
    
    PB_GRID_NAME = Me.CubeGrid.CustomName;

    // Register expected configuration properties.
    ConfigFile.RegisterProperty("Target Grid", ConfigValueType.String, "");
    ConfigFile.RegisterProperty("Old Tag", ConfigValueType.String, "");
    ConfigFile.RegisterProperty("New Tag", ConfigValueType.String, "");

    StringBuilder sb = DisplayInstructions();
    sb.Append($"# PB Grid: {PB_GRID_NAME}").AppendLine().AppendLine();
    sb.Append($"  Target Grid: ").AppendLine();
    sb.Append($"  Old Tag: ").AppendLine();
    sb.Append($"  New Tag: ").AppendLine();

    Me.CustomData = sb.ToString();

}

public void Main(string args) {
   ConfigFile.CheckAndWriteDefaults(Me, this);
   // Re-parse in case of changes in Custom Data.
   if(!ConfigFile.CheckAndReparse(Me, this)){
       Echo("Configuration parsing failed. Please fix the errors and run again.");
       return;
   }


   // get config parameters and Echo them to the console
   string GRID = ConfigFile.Get<string>("Target Grid");
   string OLD_TAG = ConfigFile.Get<string>("Old Tag");
   string NEW_TAG = ConfigFile.Get<string>("New Tag");

   GridTerminalSystem.GetBlocksOfType(blocks, block => block.CubeGrid.CustomName == GRID);

   // For demonstration, echo the values for verification
   Echo($"See CustomData for detailed instructions.");     
   Echo($"PB Grid: {PB_GRID_NAME}");
   Echo($"Target Grid: {GRID}");
   Echo($"Old Tag: {OLD_TAG}");
   Echo($"New Tag: {NEW_TAG}");

   if(string.IsNullOrEmpty(GRID)){
      Echo("ERROR: Target Grid must be defined!");
      return;
   }

   // Loop through the blocks in the Target Grid.
   foreach(IMyTerminalBlock block in blocks){
      Echo("Block Name: " + block.CustomName);
      Echo("Grid: " + block.CubeGrid.CustomName);
/*
      // If block's CustomeName does not already contain New Tag, then
      // proceed.
      if(!block.CustomName.Contains(NEW_TAG)){
         // If Old Tag has not been defined, then append New Tag to the 
         // block's CustomName.
         if(string.IsNullOrEmpty(OLD_TAG)){
            block.CustomName += NEW_TAG; 
         // If Old Tag has been defined, then replace it with New Tag.
         }else{
            block.CustomName = block.CustomName.Replace(OLD_TAG,NEW_TAG);
         }
      }
      Echo("Block Name After: " + block.CustomName);
      Echo("");
      */
   }
}


/// <summary>
/// Generates instructions for CustomData field.
/// </summary>
/// <returns>StringBuilder object with instructions.</returns>
private StringBuilder DisplayInstructions(){
   StringBuilder sb = new StringBuilder();
   sb.Append("# OVERVIEW: This script is designed to loop through all IMyTerminalBlocks").AppendLine();
   sb.Append("# in a specified grid and append or replace a tag that can be used to ").AppendLine();
   sb.Append("# identify the grid or construct that the blocks belong to.").AppendLine().AppendLine("#");
   sb.Append("# INSTRUCTIONS: Edit the parameter values below, click \"OK\", and click").AppendLine();
   sb.Append("# the \"Run\" Button.").AppendLine().AppendLine("#");
   sb.Append("# PARAMETER DEFINITIONS:").AppendLine();
   sb.Append("# Target Grid - The name of the grid whose blocks' CustomNames are to ").AppendLine();
   sb.Append("# be updated.").AppendLine().AppendLine("#");
   sb.Append("# Old Tag - Optional substring within the blocks' CustomNames that can ").AppendLine();
   sb.Append("# be replaced with the New Tag.").AppendLine().AppendLine("#");
   sb.Append("# New Tag - Tag to replace Old Tag (if defined) or append to the blocks' ").AppendLine();
   sb.Append("# CustomNames.").AppendLine().AppendLine("#");
   return sb;
}


