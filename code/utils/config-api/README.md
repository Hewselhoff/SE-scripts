# Caml API for Script Configuration
This API provides a simple interface for adding config file support to your scripts. It is designed to be easy to use and understand, allowing you to quickly integrate configuration systems into your SE scripts. The config system supports a simplified YAML-like config syntax called ["Caml"](./caml.md) that is easy to read and write. For more information on the syntax, see the [Caml language documentation](./caml.md).

## Table of Contents

1. [Overview](#overview)
2. [Supported Data Types](#supported-data-types)
3. [API Components](#api-components)  
   - [Registering Properties](#registering-properties)  
   - [Generating Default Configuration](#generating-default-configuration)  
   - [Parsing the Configuration](#parsing-the-configuration)  
   - [Retrieving Parsed Values](#retrieving-parsed-values)
4. [Example Usage](#example-usage)
5. [Error Handling](#error-handling)
6. [Integration Tips](#integration-tips)

---

## Overview

The Caml API is structured around a schema of configuration properties defined in the `ConfigLibrary` namespace. Each property has a name, an expected data type, a default value, and (after parsing) an associated value. The API exposes methods for:

- **Registering** expected configuration properties.
- **Generating** a default configuration file (in Caml syntax) using default values.
- **Parsing** a Caml configuration string, validating its content, and storing parsed values.
- **Retrieving** configuration values in a type-safe manner.

This design makes it simple to implement configuration management in Space Engineers programmable blocks.

---

## Supported Data Types

Caml supports the following data types:

- **Integers:** Whole numbers (e.g., `42`).
- **Floats:** Numbers with decimals (e.g., `10.5`).
- **Strings:** Text values (e.g., `Enterprise`).
- **Lists:** Ordered collections of values. The API supports:
  - **List of Integers:** e.g., `[100, 200, 300]`
  - **List of Floats:** e.g., `[0.25, 0.50, 0.75]`
  - **List of Strings:** e.g., `[Hello, Welcome]`

Each property is associated with one of these types via the `ConfigValueType` enum.

---

## API Components

### Registering Properties

Before parsing any configuration data, your script must register the expected configuration properties. Use the static method:

```csharp
ConfigFile.RegisterProperty(string name, ConfigValueType type, object defaultValue);
```

- **Parameters:**
  - `name`: A unique identifier for the property.
  - `type`: The expected data type (from the `ConfigValueType` enum).
  - `defaultValue`: The default value to use if the property is not provided in the configuration.

> **Example:**
>
> ```csharp
> ConfigFile.RegisterProperty("MaxSpeed", ConfigValueType.Float, 10.0f);
> ConfigFile.RegisterProperty("ShipName", ConfigValueType.String, "DefaultShip");
> ConfigFile.RegisterProperty("AllowedIDs", ConfigValueType.ListInt, new List<int> { 100, 200, 300 });
> ```

### Generating Default Configuration

If the configuration is missing (for example, if the programmable block’s Custom Data is empty), you can generate a default configuration file using:

```csharp
string defaultConfig = ConfigFile.GenerateDefaultConfigText();
```

This method returns a string formatted in Caml syntax that contains all registered properties with their default values.

> **Usage Tip:**  
> Write this default configuration to the Custom Data and exit so that users can edit it before the next run.

### Parsing the Configuration

To parse a Caml configuration string, call:

```csharp
bool success = ConfigFile.ParseConfig(string configText, out List<string> errors);
```

- **Parameters:**
  - `configText`: The configuration text (typically from Custom Data).
  - `errors`: An output list that collects any errors encountered during parsing.

- **Returns:**
  - `true` if the configuration was parsed successfully without errors; otherwise, `false`.

The parser validates the syntax (ensuring proper use of colons, brackets, etc.), checks for unknown or duplicate properties, and ensures each property matches the expected data type.

### Retrieving Parsed Values

After successful parsing, you can retrieve configuration values using the generic getter method:

```csharp
T value = ConfigFile.Get<T>(string propertyName);
```

- **Parameters:**
  - `propertyName`: The name of the configuration property.
- **Returns:**
  - The value of the configuration property cast to the desired type `T`.

> **Example:**
>
> ```csharp
> float maxSpeed = ConfigFile.Get<float>("MaxSpeed");
> string shipName = ConfigFile.Get<string>("ShipName");
> List<int> allowedIDs = ConfigFile.Get<List<int>>("AllowedIDs");
> ```

---

## Error Handling

The Caml API is designed to provide clear and actionable error messages during the parsing process. Common error conditions include:

- **Syntax Errors:**  
  Missing colons, improper formatting of lists, etc.
  
- **Unknown Properties:**  
  Keys that have not been registered.
  
- **Duplicate Properties:**  
  The same key defined more than once.
  
- **Type Mismatches:**  
  Values that do not conform to the expected data type (e.g., non-numeric text for a float).

During parsing, all encountered errors are collected in a list and can be echoed or logged. This helps in diagnosing configuration issues before the script continues execution.

---

## Usage
To use the Caml API in your script, follow these steps:
1. Place the [API code](./api.cs) to an appropriate location in your script.
    > **TIP:** You may want to place easily identifiable comments around the API code to make it easier to differentiate from the rest of your script code.
2. In your `Program()` method, register your script's configuration properties using `ConfigFile.RegisterProperty(...)`.
3. In your `Main()` method, use `ConfigFile.CheckAndWriteDefaults(...)` to auto-generate a default config if the custom data is empty.
4. In your `Main()` method, use `ConfigFile.CheckAndReparse(...)` to check for changes to the custom data and reparse if necessary.
5. Use `ConfigFile.Get<T>(...)` to retrieve the values of your configuration properties as needed by your script.

### Simple Example
```csharp
/* v ---------------------------------------------------------------------- v */
/* v Caml Config File API                                                   v */
/* v ---------------------------------------------------------------------- v */

<CONFIG API CODE GOES HERE>

/* ^ ---------------------------------------------------------------------- ^ */
/* ^ Caml Config File API                                                   ^ */
/* ^ ---------------------------------------------------------------------- ^ */

public Program() {
    // Register expected configuration properties.
    ConfigFile.RegisterProperty("foo", ConfigValueType.Float, 10.0f);
    ConfigFile.RegisterProperty("bar", ConfigValueType.Float, 1.0f);
    ConfigFile.RegisterProperty("baz", ConfigValueType.String, "Default Baz");
    ConfigFile.RegisterProperty("qux", ConfigValueType.ListInt, new List<int> { 100, 200, 300 });
    ConfigFile.RegisterProperty("quux", ConfigValueType.ListFloat, new List<float> { 0.25f, 0.50f, 0.75f });
    ConfigFile.RegisterProperty("fooBar", ConfigValueType.ListString, new List<string> { "Foo", "Bar" });
}

public void Main(string args) {
    ConfigFile.CheckAndWriteDefaults(Me, this);
    // Re-parse in case of changes in Custom Data.
    if (!ConfigFile.CheckAndReparse(Me, this)) {
        Echo("Configuration parsing failed. Please fix the errors and run again.");
        return;
    }

    // Retrieve the config parameters from the config
    float foo = ConfigFile.Get<float>("foo");
    float bar = ConfigFile.Get<float>("bar");
    string baz = ConfigFile.Get<string>("baz");
    List<int> qux = ConfigFile.Get<List<int>>("qux");
    List<float> quux = ConfigFile.Get<List<float>>("quux");
    List<string> fooBar = ConfigFile.Get<List<string>>("fooBar");

    // For demonstration, echo the values for verification
    Echo($"foo: {foo}");
    Echo($"bar: {bar}");
    Echo($"baz: {baz}");
    Echo($"qux: [{string.Join(", ", qux)}]");
    Echo($"quux: [{string.Join(", ", quux)}]");
    Echo($"fooBar: [{string.Join(", ", fooBar)}]");
}
```
