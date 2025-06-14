/*
 * OVERVIEW: This script calculates how long it will take to stop a ship's
 * descent under full thrust and engages thrusters to bring the ship to a
 * hover at a specified altitude. It accounts for changes in ship mass and
 * natural gravitational acceleration.
 *
 * NOTES: 
 * Assumptions
 *    - Max speed of 100m/s enforced by sim engine.
 *    - Ignore aero drag.
 *    - 
 *
 * Calculations
 * Speeds and accelerations will be projected onto the gravitational vector.
 */

private IMyShipController shipController;
private const UpdateFrequency SAMPLE_RATE = UpdateFrequency.Update10;
private const UpdateFrequency CHECK_RATE = UpdateFrequency.Update100;
private const UpdateFrequency STOP_RATE = UpdateFrequency.None;

private double tolerance = 5e-4; // Tolerance for checking small doubles.
private double finalAlt = 20.0d; // Hover altitude at end of retro burn [meters].

// Structure containing burn parameters.
private struct RetroBurn{
   public double burnDistance; // [meters].
   public TimeSpan burnTime; // [hours:minutes:seconds].
   public double fuelRequired; // [liters].
   
   public RetroBurn(double burnDistance, TimeSpan burnTime, double fuelRequired){
      this.burnDistance = burnDistance;
      this.burnTime = burnTime;
      this.fuelRequired = fuelRequired;
   }

   public RetroBurn(double burnDistance, double burnTime, double fuelRequired){
      this.burnDistance = burnDistance;
      this.burnTime = TimeSpan.FromSeconds(burnTime);
      this.fuelRequired = fuelRequired;
   }
}

// Create an instance of ArgParser.
private ArgParser argParser = new ArgParser();

private List<IMyThrust> thrusters = new List<IMyThrust>();

private Logger logger; // Add [DDS_LOG] tag to LCD name to display logging.

public Program() {

    // Initialize the logger, enabling all output types.
    logger = new Logger(this){
        UseEchoFallback = false,    // Fallback to program.Echo if no LCDs are available.
        LogToCustomData = false     // Enable logging to CustomData.
        
    };

    // Register command line arguments
    argParser.RegisterArg("run", typeof(bool), false, false); // Run the controller.
    argParser.RegisterArg("stop", typeof(bool), false, false); // Stop the controller.
    argParser.RegisterArg("final_alt", typeof(double), false, false); // Set desired hover altitude following retro burn [meters].

    // Register config parameters
    ConfigFile.RegisterProperty("Thrusters", ConfigValueType.String, "Thrusters (AtmoProbe)");
    ConfigFile.RegisterProperty("ShipController", ConfigValueType.String, "RemoteControl (AtmoProbe)");

    ((IMyBlockGroup)GridTerminalSystem.GetBlockGroupWithName(ConfigFile.Get<string>("Thrusters"))).GetBlocksOfType<IMyMotorSuspension>(thrusters);
    shipController = (IMyRemoteControl)GridTerminalSystem.GetBlockWithName(ConfigFile.Get<string>("ShipController"));

    Runtime.UpdateFrequency = STOP_RATE;
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
            case "--run":
                Runtime.UpdateFrequency = SAMPLE_RATE;
                break;
            case "--stop":
                Runtime.UpdateFrequency = STOP_RATE;
                break;
            case "--final_alt":
                finalAlt = (double)kvp.Value;
                break;
            default:
                logger.Warning("Unknown argument: " + kvp.Key);
                break;
        }
    }
}

/// <summary>
/// Calculate total vertical thrust.
/// <returns> double - total thrust value in [Newtons].</returns>
/// </summary>
private double CalculateTotalThrust(){
   float currentThrust = 0.0f;
   foreach(var thuster in thrusters){
      currentThrust += thuster.CurrentThrust;
   }
   return (double)currentThrust;
}
   
/// <summary>
/// Calculate max total vertical thrust.
/// <returns> double - max total thrust value in [Newtons].</returns>
/// </summary>
private double CalculateMaxTotalThrust(){
   float maxTotalThrust = 0.0f;
   foreach(var thuster in thrusters){
      maxTotalThrust += thuster.MaxThrust;
   }
   return (double)maxTotalThrust;
}

/// <summary>
/// Calculate current ship mass.
/// <returns> double - total mass of the ship in kilograms.</returns>
/// </summary>
private double CalculateShipMass(){
   return (double)(MyShipMass)(shipController.CalculateShipMass()).TotalMass;
}

/// <summary>
/// Calculate local gravitational acceleration.
/// <returns> double - gravitational acceleration in [meters/second^2].</returns>
/// </summary>
private double GetGravitationalAccel(){
   return -shipController.GetNaturalGravity().Length();
}

/// <summary>
/// Calculate current ship altitude in meters.
/// <throws> InvalidOperationException if not within planetary gravitational field.</throws> /// <returns> double - current altitude in meters.</returns>
/// </summary>
private double GetAltitude(){
   double currentAlt;
   if(!shipController.TryGetPlanetElevation(MyPlanetElevation.Surface, out currentAlt)){
      logger.Warning(
      throw InvalidOperationException("Unable to calculate altitude outside of planetary gravity field!.");
   }

   return currentAlt;
}

/// <summary>
/// EQUATION (3)
/// Calculate standard gravity for planet.
/// You must be within the planet's gravity field for this calculation to be possible.
/// <throws> InvalidOperationException if not within planetary gravitational field.</throws>
/// <returns> float - gravitational acceleration at planet surface in meters per second-squared.</returns>
/// </summary>
private double CalculateStandardGravity(){
   float gravAccelLocal = GetGravitationalAccel();
   if(IsZero(gravAccelLocal, tolerance)){
      throw InvalidOperationException("Unable to calculate planetary gravity outside of gravity field!.");
   }

   double currentAlt = GetAltitude();
   double planetRadius = CalculatePlanetRadius();
   return gravAccelLocal * (Math.Pow((planetRadius + currentAlt),2d)/Math.Pow(planetRadius, 2d));
}

/// <summary>
/// EQUATION (2)
/// Calculate planet radius.
/// You must be within the planet's gravity field for this calculation to be possible.
/// <throws> InvalidOperationException if not within planetary gravitational field.</throws>
/// <returns> double - Radius of planet in meters.</returns>
/// </summary>
private double CalculatePlanetRadius(){
   Vector3D planetPositionW = null;
   if(!shipController.TryGetPlanetPosition(out planetPositionW)){
      logger.Warning("Unable to calculate planet radius outside of planetary gravity field!.");
      throw InvalidOperationException("Unable to calculate planet radius outside of planetary gravity field!.");
   }
   Vector3D shipPositionW = shipController.GetPosition();
   double distanceToPlanetCenter = Vector3D.Subtract(planetPositionW, shipPositionW).Length();
   return distanceToPlanetCenter - GetAltitude();
}

/// <summary>
/// Test double precision value to see if it is approximately zero.
/// <param name="val"> (double) double precision value to check.</param>
/// <param name="tol"> (double) tolerance within which val is determined to be approximately zero.</param>
/// <returns> bool - true if val is within tol of zero.</returns>
/// </summary>
private bool IsZero(double val, double tol){
   return (Math.Abs(val) < tol);
}

/// <summary>
/// CONDITION (11)
/// Check to see if vertical thrust is sufficient to ascend against standard gravity.
/// <returns> bool - true of vertical thurst acceleration is greater than standard gravitational acceleration.</returns>
/// </summary>
private bool HaveSufficientThrust(){
   double shipMass = CalculateShipMass();
   double thrustAccel = CalculateMaxTotalThrust()/shipMass;
   return thrustAccel > Math.Abs(CalculateStandardGravity());
}

/// <summary>
/// EQUATION (9)
/// Calculate max retro burn distance under full thrust, standard gravity, and current ship mass.
/// <returns> double - max burn distance to arrest descent at specified hover altitude [meters].</returns>
/// </summary>
private double CalculateMaxBurnDistance(){
   double vZero = GetShipDescentSpeed();
   double shipMass = CalculateShipMass();
   double verticalThrust = CalculateMaxTotalThrust();
   double thrustAccel = verticalThrust/shipMass;
   double standardGravity = CalculateStandardGravity();
   return -Math.Pow(vZero,2d)/(2*(thrustAccel + standardGravity));
}

/// <summary>
/// EQUATION (8)
/// Calculate retro burn distance under full thrust, local gravity, and current ship mass.
/// <returns> double - burn distance to arrest descent at specified hover altitude [meters].</returns>
/// </summary>
private double CalculateBurnDistance(){
   double vZero = GetShipDescentSpeed();
   double shipMass = CalculateShipMass();
   double verticalThrust = CalculateMaxTotalThrust();
   double thrustAccel = verticalThrust/shipMass;
   double gravAccelLocal = GetGravitationalAccel();
   return -Math.Pow(vZero,2d)/(2*(thrustAccel + gravAccelLocal));
}

/// <summary>
/// CONDITION (14)
/// Make sure the retro burn can arrest descent at target hover altitude.
/// <param name="burnDistance"> (double) distance travelled during retru burn [meters].</param>
/// <param name="finalAltitude"> (double) target altitude at end of retro burn [meters].</param>
/// <returns> bool - true ship will be able to stop descent at target altitude.</returns>
/// </summary>
private bool CanStopAtTargetAltitude(double burnDistance, double finalAltitude){
   return burnDistance <= (GetAltitude() - finalAltitude);
}
   
/// <summary>
/// Calculate downward (descent) speed of ship.
/// <returns> double - descent speed of ship [meters/second].</returns>
/// </summary>
private double GetShipDescentSpeed(){
   Vector3D shipVelocityW = shipController.GetShipVelocities().LinearVelocity; // Ship velocity vector in World Frame??
   Vector3D gravVecW = shipController.GetNaturalGravity();
   return Vector3D.ProjectOnVector(shipVelocityW, gravVecW).Length();
}

/// <summary>
/// CONDITION (20)(21)
/// Check to see if it is time to start retro burn.
/// <param name="burnDistance"> (double) distance travelled during retru burn [meters].</param>
/// <param name="finalAltitude"> (double) target altitude at end of retro burn [meters].</param>
/// <returns> bool - true if it is time to start retro burn.</returns>
/// </summary>
private bool InitiateRetroBurn(double burnDistance, double finalAltitude){
   return GetAltitude() <= (burnDistance + finalAltitude);
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
        program.GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(allPanels, panel => panel.CustomName.Contains("[DDS_LOG]"));

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
