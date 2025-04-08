# ArgParser API for Programmable Block Scripts

**ArgParser** is a lightweight, general-purpose argument parser designed specifically for Space Engineers programmable blocks. It lets you register and parse command-line style arguments (prefixed with `--`) as well as commands (which have no prefix) with support for type conversion, list values, required arguments, and more.

## Table of Contents
1. [Features](#features)
2. [API](#api)
   - [ArgParser Class](#argparser-class)
     - [Properties](#properties)
     - [Methods](#methods)
     - [Nested Types](#nested-types)
3. [Usage Example](#usage-example)
4. [Handling Errors](#handling-errors)

## Features

- **Argument Prefix**: Only arguments starting with `--` are processed as standard arguments.
- **Command Parsing**: Supports a single command per call. Commands do **not** include the `--` prefix, always expect a string value, and capture everything following the command name (allowing spaces).
- **Type Conversion**: Automatically converts argument values to `int`, `float`, `string`, or `bool` types.
- **List Support**: Define arguments that accept multiple space-separated values.
- **Required Arguments**: Mark arguments or commands as required.
- **Single Argument Enforcement**: Optionally restrict calls to one standard argument only.
- **Error Reporting**: Provides a list of parsing errors for troubleshooting.

## API

### ArgParser Class

#### Properties

- **`Dictionary<string, object> ParsedArgs`**  
  Read-only property that returns a dictionary of parsed arguments and commands.  
  - Standard arguments are stored with keys prefixed by `--` and values converted to their specified type (or as a `List<T>` if the argument accepts multiple values).  
  - Commands are stored with their plain name (without any prefix) and have a value type of `string` containing everything after the command name.

- **`List<string> Errors`**  
  Read-only property that holds any errors encountered during parsing.

- **`bool OnlyAllowSingleArg`**  
  When set to `true`, the parser will only allow one standard argument (prefixed with `--`) per call. If more than one is provided, parsing will fail and an error will be added.

#### Methods

- **`void RegisterArg(string name, Type argType, bool isList = false, bool isRequired = false)`**  
  Registers a new standard argument with the parser.
  - `name`: The argument name (with or without the `--` prefix; the prefix is added automatically if missing).
  - `argType`: The expected data type (`typeof(int)`, `typeof(float)`, `typeof(string)`, or `typeof(bool)`).
  - `isList`: (Optional) Set to `true` if the argument accepts multiple values.
  - `isRequired`: (Optional) Set to `true` if the argument must be provided when parsing.

- **`void RegisterCommand(string commandName, bool isRequired = false)`**  
  Registers a command with the parser.  
  A command has the following special properties:
  - It does **not** use the `--` prefix.
  - It always expects a value of type `string`.
  - It must be the first token in the input string.
  - Only one command is allowed per call.
  - The parser captures everything after the command name (allowing spaces) as its value.
  - `commandName`: The name of the command (without `--`).
  - `isRequired`: (Optional) Set to `true` if the command must be provided when parsing.

- **`bool Parse(string input)`**  
  Parses the input string (typically passed to the Main method) into arguments or a command, based on registered definitions.
  - If the trimmed input begins with `--`, the parser processes standard arguments.
  - Otherwise, it checks if the first token matches a registered command and captures the remainder of the input as its value.
  - Returns `true` if parsing succeeds (i.e., no errors are detected); otherwise returns `false`.

- **`IEnumerable<KeyValuePair<string, object>> GetParsedArgs()`**  
  Returns an enumerable collection of the parsed arguments, making it easy to iterate through each key/value pair.

#### Nested Types

- **`ArgDefinition`**  
  Internal class representing a registered argument or command definition.
  - **Properties**:
    - `string Name`:  
      - For standard arguments, the name is always prefixed with `--`.
      - For commands, the name is used as provided.
    - `Type ArgType`: The expected data type. For commands, this is always `string`.
    - `bool IsList`: Indicates whether the argument accepts multiple space-separated values (not applicable for commands).
    - `bool IsRequired`: Indicates whether the argument or command is mandatory.
    - `bool IsCommand`: Indicates whether this definition represents a command rather than a standard argument.

## Usage Example

The following example demonstrates how to register both standard arguments and a command, parse an input string, and iterate over the parsed arguments. The example also shows how to use a command to delegate further processing (e.g., routing nested parameters to another parser).

```csharp
public class Program
{
    // Create an instance of ArgParser.
    ArgParser parser = new ArgParser();

    public Program()
    {
        // Register standard arguments.
        // --foo expects an int value.
        parser.RegisterArg("foo", typeof(int));
        
        // --bar accepts multiple string values (a list of strings).
        parser.RegisterArg("bar", typeof(string), isList: true);
        
        // --baz is a boolean flag; if no value is provided, it defaults to true.
        parser.RegisterArg("baz", typeof(bool));

        // Register a command to delegate nested arguments.
        // Commands do not include the '--' prefix and capture all following text.
        parser.RegisterCommand("nested");
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

        // Iterate over parsed arguments and commands.
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
                case "nested":
                    // For a command, everything after the command name is captured as a string.
                    string nestedArgs = kvp.Value as string;
                    Echo("Command 'nested' with arguments: " + nestedArgs);

                    // Optionally, delegate parsing of nested arguments to a new ArgParser.
                    ArgParser nestedParser = new ArgParser();
                    nestedParser.RegisterArg("param", typeof(int));
                    if (nestedParser.Parse(nestedArgs))
                    {
                        if (nestedParser.ParsedArgs.TryGetValue("--param", out object paramValue))
                        {
                            Echo("Nested parameter (--param): " + paramValue);
                        }
                    }
                    else
                    {
                        foreach (string err in nestedParser.Errors)
                        {
                            Echo("Error in nested parsing: " + err);
                        }
                    }
                    break;
                default:
                    Echo("Unknown argument: " + kvp.Key);
                    break;
            }
        }
    }
}
```

## Handling Errors

After calling `Parse`, check the `Errors` property to determine if any issues occurred during parsing. Common errors include:

- **Value Provided Without Preceding Argument**: A value is encountered that is not preceded by an argument flag.
- **Unrecognized Argument**: An argument flag that hasn’t been registered is used.
- **Missing Value**: No value is provided for an argument that requires one.
- **Invalid Value**: The provided value cannot be converted to the expected type.
- **Single Argument Restriction**: More than one standard argument was provided when `OnlyAllowSingleArg` is set to true.
- **Unrecognized Command**: The command specified is not registered.