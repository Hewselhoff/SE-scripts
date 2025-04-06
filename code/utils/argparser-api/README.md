# ArgParser API for Programable Block Scripts

**ArgParser** is a lightweight, general-purpose argument parser designed specifically for Space Engineers programmable blocks. It lets you register and parse command-line style arguments (prefixed with `--`) with support for type conversion, list values, required arguments, and more.

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

- **Argument Prefix**: Only arguments starting with `--` are processed.
- **Type Conversion**: Automatically converts argument values to `int`, `float`, `string`, or `bool` types.
- **List Support**: Define arguments that accept multiple space-separated values.
- **Required Arguments**: Mark arguments as required.
- **Single Argument Enforcement**: Optionally restrict calls to one argument only.
- **Error Reporting**: Provides a list of parsing errors for troubleshooting.

## API

### ArgParser Class

#### Properties

- **`Dictionary<string, object> ParsedArgs`**  
  Read-only property that returns a dictionary of parsed arguments. Single-value arguments are stored as objects of their specified type, while list arguments are stored as a `List<T>`.

- **`List<string> Errors`**  
  Read-only property that holds any errors encountered during parsing.

- **`bool OnlyAllowSingleArg`**  
  When set to `true`, the parser will only allow one argument per call. If more than one argument is provided, parsing will fail and an error will be added.

#### Methods

- **`void RegisterArg(string name, Type argType, bool isList = false, bool isRequired = false)`**  
  Registers a new argument with the parser.
  - `name`: The argument name (with or without the `--` prefix; it will be prepended automatically if missing).
  - `argType`: The expected type of the argument (e.g., `typeof(int)`, `typeof(float)`, `typeof(string)`, `typeof(bool)`).
  - `isList`: (Optional) Set to `true` if the argument accepts multiple values.
  - `isRequired`: (Optional) Set to `true` if the argument must be provided when parsing.

- **`bool Parse(string input)`**  
  Parses the input string (typically passed to the Main method) into arguments based on registered definitions.
  - `input`: The raw argument string to parse.
  - Returns `true` if parsing succeeds (i.e., no errors are detected); otherwise returns `false`.

- **`IEnumerable<KeyValuePair<string, object>> GetParsedArgs()`**  
  Returns an enumerable collection of the parsed arguments, making it easy to iterate through each argument and its corresponding value.

#### Nested Types

- **`ArgDefinition`**  
  Internal class representing a registered argument definition.
  - **Properties**:
    - `string Name`: The argument name (always prefixed with `--`).
    - `Type ArgType`: The expected data type.
    - `bool IsList`: Indicates whether the argument accepts multiple space-separated values.
    - `bool IsRequired`: Indicates whether the argument is mandatory.

## Usage Example

The following example demonstrates how to register arguments, parse an input string, and iterate over the parsed arguments using an iterator with a switch statement.

```csharp
public class Program
{
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
}
```

## Handling Errors

After calling `Parse`, check the `Errors` property to determine if any issues occurred during parsing. Common errors include:

- **Value Provided Without Preceding Argument**: A value is encountered that is not preceded by an argument flag.
- **Unrecognized Argument**: An argument flag that hasn’t been registered is used.
- **Missing Value**: No value is provided for an argument that requires one.
- **Invalid Value**: The provided value cannot be converted to the expected type.
- **Single Argument Restriction**: More than one argument was provided when `OnlyAllowSingleArg` is set to true.
