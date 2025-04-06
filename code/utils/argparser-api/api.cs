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
        public Type ArgType;     // The expected type (int, float, string, bool)
        public bool IsList;      // True if this argument should accept multiple values
        public bool IsRequired;  // True if this argument must be provided

        public ArgDefinition(string name, Type argType, bool isList = false, bool isRequired = false)
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
    public void RegisterArg(string name, Type argType, bool isList = false, bool isRequired = false)
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
        var tokens = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
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
            else // Process list arguments.
            {
                if (values.Count == 0)
                {
                    Errors.Add("No values provided for list argument: " + token);
                    continue;
                }
                // Create a generic list of the appropriate type.
                var listType = typeof(List<>).MakeGenericType(def.ArgType);
                var list = (System.Collections.IList)Activator.CreateInstance(listType);
                foreach (var val in values)
                {
                    object converted = ConvertValue(val, def.ArgType);
                    if (converted == null)
                    {
                        Errors.Add("Invalid value for argument " + token + ": " + val);
                        continue;
                    }
                    list.Add(converted);
                }
                parsedArgs[token] = list;
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
            // Additional types can be handled here.
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