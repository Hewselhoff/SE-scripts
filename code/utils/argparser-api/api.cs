/* v ---------------------------------------------------------------------- v */
/* v ArgParser API                                                          v */
/* v ---------------------------------------------------------------------- v */
public class ArgParser
{
    // Nested class representing a registered argument or command.
    public class ArgDefinition
    {
        public string Name;      // For arguments, this includes the "--" prefix; for commands, it’s left as provided.
        public Type ArgType;     // The expected type (int, float, string, bool). For commands, this is always string.
        public bool IsList;      // True if this argument should accept multiple values (not used for commands).
        public bool IsRequired;  // True if this argument must be provided.
        public bool IsCommand;   // True if this definition represents a command rather than a "--" argument.

        // Added an optional isCommand flag (default is false) to support both commands and arguments.
        public ArgDefinition(string name, Type argType, bool isList = false, bool isRequired = false, bool isCommand = false)
        {
            // For normal arguments, enforce the "--" prefix; commands are left as provided.
            if (!isCommand && !name.StartsWith("--"))
                Name = "--" + name;
            else
                Name = name;
            ArgType = argType;
            IsList = isList;
            IsRequired = isRequired;
            IsCommand = isCommand;
        }
    }

    // Dictionary holding all registered standard argument definitions.
    private Dictionary<string, ArgDefinition> registeredArgs = new Dictionary<string, ArgDefinition>();

    // Dictionary holding all registered command definitions.
    private Dictionary<string, ArgDefinition> registeredCommands = new Dictionary<string, ArgDefinition>();

    // Dictionary holding the parsed arguments and commands.
    // For single value arguments, the value is stored as object;
    // for lists, it is a List<T>; for commands, it is stored as a string.
    private Dictionary<string, object> parsedArgs = new Dictionary<string, object>();

    // List of errors that occurred during parsing.
    public List<string> Errors { get; private set; } = new List<string>();

    // If true, the parser will only allow one argument per call (applies to standard arguments).
    public bool OnlyAllowSingleArg { get; set; } = false;

    /// <summary>
    /// Registers a new standard argument definition.
    /// </summary>
    /// <param name="name">The argument name (with or without "--" prefix)</param>
    /// <param name="argType">The expected type (int, float, string, bool)</param>
    /// <param name="isList">If true, the argument accepts multiple space-separated values</param>
    /// <param name="isRequired">If true, the argument must be provided</param>
    public void RegisterArg(string name, Type argType, bool isList = false, bool isRequired = false)
    {
        var argDef = new ArgDefinition(name, argType, isList, isRequired, false);
        registeredArgs[argDef.Name] = argDef;
    }

    /// <summary>
    /// Registers a new command.
    /// A command is an argument that:
    /// 1. Is not prefixed with "--".
    /// 2. Always has a value type of string.
    /// 3. Must be the first token in the input.
    /// 4. Only one command is allowed per call.
    /// 5. The parser captures everything after the command name (spaces allowed) as its value.
    /// </summary>
    /// <param name="commandName">The command name (without the "--" prefix)</param>
    /// <param name="isRequired">If true, the command must be provided</param>
    public void RegisterCommand(string commandName, bool isRequired = false)
    {
        // Create an ArgDefinition with IsCommand set to true.
        var cmdDef = new ArgDefinition(commandName, typeof(string), isList: false, isRequired: isRequired, isCommand: true);
        registeredCommands[commandName] = cmdDef;
    }

    /// <summary>
    /// Gets the dictionary of parsed arguments and commands.
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
    /// Parses the input string into arguments or a command.
    /// Returns true if parsing is successful (i.e. no errors); otherwise, false.
    /// </summary>
    /// <param name="input">The argument string passed to Main</param>
    public bool Parse(string input)
    {
        // Clear previous errors and parsed arguments.
        Errors.Clear();
        parsedArgs.Clear();

        if (string.IsNullOrWhiteSpace(input))
            return true; // Nothing to parse

        string trimmedInput = input.Trim();

        // Check if this is a command call.
        if (!trimmedInput.StartsWith("--"))
        {
            // Extract the command token and the remainder of the string.
            int spaceIndex = trimmedInput.IndexOf(' ');
            string commandToken;
            string remainder;
            if (spaceIndex == -1)
            {
                commandToken = trimmedInput;
                remainder = "";
            }
            else
            {
                commandToken = trimmedInput.Substring(0, spaceIndex);
                remainder = trimmedInput.Substring(spaceIndex + 1);
            }

            // Check if the command is registered.
            if (!registeredCommands.ContainsKey(commandToken))
            {
                Errors.Add("Unrecognized command: " + commandToken);
                return false;
            }

            // Only one command is allowed per call.
            parsedArgs[commandToken] = remainder;

            // Check if any required commands are missing.
            foreach (var kvp in registeredCommands)
            {
                if (kvp.Value.IsRequired && !parsedArgs.ContainsKey(kvp.Key))
                {
                    Errors.Add("Missing required command: " + kvp.Key);
                }
            }
            return Errors.Count == 0;
        }
        else
        {
            // Process as standard "--" arguments.
            string[] tokens = trimmedInput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int countArgsParsed = 0;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];

                // Each standard argument should start with "--"
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
                i = j - 1; // Advance to the last token processed

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

                    if (def.ArgType == typeof(string))
                    {
                        List<string> list = new List<string>(values);
                        parsedArgs[token] = list;
                    }
                    else if (def.ArgType == typeof(int))
                    {
                        List<int> list = new List<int>();
                        foreach (var val in values)
                        {
                            if (!int.TryParse(val, out int parsed))
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
                            if (!float.TryParse(val, out float parsed))
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
                            if (!double.TryParse(val, out double parsed))
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
                            if (!bool.TryParse(val, out bool parsed))
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