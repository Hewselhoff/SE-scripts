
private IMyBlockGroup magPlateGroup1;
private IMyBlockGroup magPlateGroup2;
private IMyTextPanel display1;
private IMyTextPanel display2;
private StringBuilder displayText1 = new StringBuilder();
private StringBuilder displayText2 = new StringBuilder();

public Program() {

    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    // Register expected configuration properties.
    ConfigFile.RegisterProperty("Mag Plate Group 1", ConfigValueType.String, "");
    ConfigFile.RegisterProperty("Mag Plate Group 2", ConfigValueType.String, "");
    ConfigFile.RegisterProperty("Display 1", ConfigValueType.String, "");
    ConfigFile.RegisterProperty("Display 2", ConfigValueType.String, "");
    
    // Populate CustomData if it is empty.
    if(string.IsNullOrEmpty(Me.CustomData)){
       StringBuilder sb = new StringBuilder();
       sb.Append($"  Mag Plate Group 1: ").AppendLine();
       sb.Append($"  Mag Plate Group 2: ").AppendLine();
       sb.Append($"  Display 1: ").AppendLine();
       sb.Append($"  Display 2: ").AppendLine();
       Me.CustomData = sb.ToString();
    }

}

public void Main(string args) {
   ConfigFile.CheckAndWriteDefaults(Me, this);
   // Re-parse in case of changes in Custom Data.
   if(!ConfigFile.CheckAndReparse(Me, this)){
       Echo("Configuration parsing failed. Please fix the errors and run again.");
       return;
   }


   // get config parameters and Echo them to the console
   string MAG_PLATE_GROUP_1 = ConfigFile.Get<string>("Mag Plate Group 1");
   string MAG_PLATE_GROUP_2 = ConfigFile.Get<string>("Mag Plate Group 2");
   string DISPLAY_NAME_1 = ConfigFile.Get<string>("Display 1");
   string DISPLAY_NAME_2 = ConfigFile.Get<string>("Display 2");

   magPlateGroup1 = GridTerminalSystem.GetBlockGroupWithName(MAG_PLATE_GROUP_1);
   magPlateGroup2 = GridTerminalSystem.GetBlockGroupWithName(MAG_PLATE_GROUP_2);
   display1 = (IMyTextPanel) GridTerminalSystem.GetBlockWithName(DISPLAY_NAME_1);
   display2 = (IMyTextPanel) GridTerminalSystem.GetBlockWithName(DISPLAY_NAME_2);
   displayText1.Clear();
   displayText1.Append("Mag Plate Status           ").AppendLine().AppendLine();
   displayText2.Clear();
   displayText2.Append("Mag Plate Status").AppendLine().AppendLine();
   List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();


   // Get and report state of Front Mag Plates.
   magPlateGroup1.GetBlocksOfType<IMyTerminalBlock>(blocks); 
   foreach(var block in blocks){
      if(block.CustomName.Contains("Right")){
         if(magPlateReadyToLock(block)){
               displayText2.Append($"{block.CustomName}: READY").AppendLine();
         }else if(magPlateLocked(block)){
               displayText2.Append($"{block.CustomName}: LOCKED").AppendLine();
         }else{
               displayText2.Append($"{block.CustomName}: UNLOCKED").AppendLine();
         }
      }else if(block.CustomName.Contains("Left")){
         if(magPlateReadyToLock(block)){
               displayText1.Append($"{block.CustomName}: READY    ").AppendLine();
         }else if(magPlateLocked(block)){
               displayText1.Append($"{block.CustomName}: LOCKED   ").AppendLine();
         }else{
               displayText1.Append($"{block.CustomName}: UNLOCKED ").AppendLine();
         }
      }
   }

   // Get and report state of Bottom Mag Plates.
   magPlateGroup2.GetBlocksOfType<IMyTerminalBlock>(blocks); 
   foreach(var block in blocks){
      if(block.CustomName.Contains("Right")){
         if(magPlateReadyToLock(block)){
               displayText2.Append($"{block.CustomName}: READY").AppendLine();
         }else if(magPlateLocked(block)){
               displayText2.Append($"{block.CustomName}: LOCKED").AppendLine();
         }else{
               displayText2.Append($"{block.CustomName}: UNLOCKED").AppendLine();
         }
      }else if(block.CustomName.Contains("Left")){
         if(magPlateReadyToLock(block)){
               displayText1.Append($"{block.CustomName}: READY   ").AppendLine();
         }else if(magPlateLocked(block)){
               displayText1.Append($"{block.CustomName}: LOCKED  ").AppendLine();
         }else{
               displayText1.Append($"{block.CustomName}: UNLOCKED").AppendLine();
         }
      }
   }

   display1.WriteText(displayText1);
   display2.WriteText(displayText2);
}


bool magPlateGroupReadyToLock(IMyBlockGroup magPlateGroup){
   List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
   magPlateGroup.GetBlocksOfType<IMyTerminalBlock>(blocks); 
   foreach(var block in blocks){
      if(magPlateReadyToLock(block)){
         return true;
      }
   }

   return false;
}

bool magPlateReadyToLock(IMyTerminalBlock block)
{
   StringBuilder temp = new StringBuilder();
   ITerminalAction theAction;

   temp.Clear();
   theAction = block.GetActionWithName("Lock");
   block.GetActionWithName(theAction.Id.ToString()).WriteValue(block, temp);
   if (temp.ToString().Contains("Ready"))
      return true;
   return false;
}

bool magPlateLocked(IMyTerminalBlock block)
{
   StringBuilder temp = new StringBuilder();
   ITerminalAction theAction;

   temp.Clear();
   theAction = block.GetActionWithName("Lock");
   block.GetActionWithName(theAction.Id.ToString()).WriteValue(block, temp);
   if (temp.ToString().Contains("Locked"))
      return true;
   return false;
}

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
