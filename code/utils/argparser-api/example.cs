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
namespace SpaceEngineers.UWBlockPrograms.ArgParserExample {
    public sealed class Program : MyGridProgram {
    // Your code goes between the next #endregion and #region
#endregion

        /* v ---------------------------------------------------------------------- v */
        /* v ArgParser API                                                          v */
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
            private System.Collections.Generic.Dictionary<string, ArgDefinition> registeredArgs = new System.Collections.Generic.Dictionary<string, ArgDefinition>();

            // Dictionary holding the parsed arguments and their values.
            // For single value arguments, the value is stored as object; for lists, it is a List<T>.
            private System.Collections.Generic.Dictionary<string, object> parsedArgs = new System.Collections.Generic.Dictionary<string, object>();

            // List of errors that occurred during parsing.
            public System.Collections.Generic.List<string> Errors { get; private set; } = new System.Collections.Generic.List<string>();

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
            public System.Collections.Generic.Dictionary<string, object> ParsedArgs { get { return parsedArgs; } }

            /// <summary>
            /// Provides an enumerable to iterate over parsed arguments.
            /// </summary>
            public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>> GetParsedArgs()
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
                    System.Collections.Generic.List<string> values = new System.Collections.Generic.List<string>();

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
                            System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
                            foreach (var val in values)
                            {
                                list.Add(val);
                            }
                            parsedArgs[token] = list;
                        }
                        else if (def.ArgType == typeof(int))
                        {
                            System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
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
                            System.Collections.Generic.List<float> list = new System.Collections.Generic.List<float>();
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
                            System.Collections.Generic.List<double> list = new System.Collections.Generic.List<double>();
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
                            System.Collections.Generic.List<bool> list = new System.Collections.Generic.List<bool>();
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


        // Create an instance of ArgParser.
        ArgParser parser = new ArgParser();

        public Program()
        {
            // Register arguments using simple names:
            // --foo expects an int value.
            parser.RegisterArg("foo", typeof(int));
            
            // --bar accepts multiple string values (a list of strings).
            parser.RegisterArg("bar", typeof(string), isList: true);
            
            // --baz is a boolean flag; if no value is provided, it defaults to true.
            parser.RegisterArg("baz", typeof(bool));
        }

        public void Main(string argument)
        {
            // Parse the input argument string.
            if (!parser.Parse(argument))
            {
                // Output errors if parsing fails.
                foreach (string error in parser.Errors)
                {
                    Echo("Error: " + error);
                }
                return;
            }

            // Iterate over parsed arguments using the iterator and a switch statement.
            foreach (var kvp in parser.GetParsedArgs())
            {
                switch (kvp.Key)
                {
                    case "--foo":
                        int fooValue = (int)kvp.Value;
                        Echo("Foo: " + fooValue);
                        break;
                    case "--bar":
                        // Since --bar is registered as a list, cast it to List<string>.
                        var barValues = kvp.Value as List<string>;
                        Echo("Bar: " + string.Join(", ", barValues));
                        break;
                    case "--baz":
                        bool bazValue = (bool)kvp.Value;
                        Echo("Baz: " + bazValue);
                        break;
                    default:
                        Echo("Unknown argument: " + kvp.Key);
                        break;
                }
            }
        }
        
#region PreludeFooter
    }
}
#endregion