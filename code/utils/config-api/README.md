# Caml API for Script Configuration

This API provides a simple interface for adding config file support to your scripts. It is designed to be easy to use and understand—allowing you to quickly integrate configuration systems into your SE scripts. The config system supports a simplified YAML-like config syntax called ["Caml"](./caml.md) that is easy to read and write. For more information on the syntax, see the [Caml language documentation](./caml.md).

## Table of Contents

1. [Overview](#overview)
2. [Supported Data Types](#supported-data-types)
3. [API Components](#api-components)  
   - [Registering Properties and Sub Configurations](#registering-properties-and-sub-configurations)  
   - [Generating Default Configuration](#generating-default-configuration)  
   - [Parsing the Configuration](#parsing-the-configuration)  
   - [Retrieving Parsed Values](#retrieving-parsed-values)
4. [Error Handling](#error-handling)
5. [Usage](#usage)  
   - [Simple Example](#simple-example)

---

## Overview

The Caml API is based on an instance of the `ConfigFile` class that you initialize with your programmable block and program reference. A configuration consists of:
 
- **Root-level properties**: Expected configuration values registered by name, type, and default value.
- **Sub configurations**: Optional additional sections to help organize settings. Each sub configuration is automatically created by name and handled as its own `ConfigFile` instance.

Before the API is used (parsing, retrieving values, or writing defaults), you must complete the property registration and call **FinalizeRegistration**. This final step also writes default configuration data to your programmable block’s Custom Data if needed.

---

## Supported Data Types

Caml supports the following data types:

- **Integers:** Whole numbers (e.g., `42`).
- **Floats:** Numbers with decimals (e.g., `10.5`).
- **Strings:** Text values (e.g., `Enterprise`).
- **Lists:** Ordered collections of uniform values:
  - **List of Integers:** e.g., `[100, 200, 300]`
  - **List of Floats:** e.g., `[0.25, 0.50, 0.75]`
  - **List of Strings:** e.g., `[Hello, Welcome]`

Each property is associated with one of these types via the `ConfigValueType` enum.

---

## API Components

### Registering Properties and Sub Configurations

Before parsing any configuration data, you must register the expected configuration properties.

1. **Initialization and Registration:**  
   Create an instance of `ConfigFile` by passing in your programmable block and your program instance:
   ```csharp
   ConfigFile config = new ConfigFile(Me, this);
   ```
   
2. **Register Root-Level Properties:**  
   Use the instance method:
   ```csharp
   config.RegisterProperty("PropertyName", ConfigValueType.Type, defaultValue);
   ```
   *Example:*
   ```csharp
   config.RegisterProperty("aFloat", ConfigValueType.Float, 3.14f);
   config.RegisterProperty("aString", ConfigValueType.String, "Foo");
   config.RegisterProperty("anIntList", ConfigValueType.ListInt, new List<int> { 1, 1, 2, 3, 5});
   ```

3. **Registering Sub Configurations:**  
   To group settings into sub modules, first register a sub configuration by name:
   ```csharp
   config.RegisterSubConfig("subConfig");
   ```
   Once registered, add properties directly into the sub config with the overloaded registration method:
   ```csharp
   config.RegisterProperty("subConfig", "anInt", ConfigValueType.Int, 42);
   ```

4. **Finalizing Registration:**  
   After all registrations are complete, call:
   ```csharp
   config.FinalizeRegistration();
   ```
   This marks the configuration as ready for use and writes the default configuration (if needed) to Custom Data. Any further registration attempts after this call are disallowed.

### Generating Default Configuration

The `FinalizeRegistration()` method automatically writes the default configuration to the programmable block’s Custom Data. However, if you need or want to perform this step again  in your `Main` function after the program has been initialized/compiled, you can call:
   
```csharp
string defaultConfig = config.GenerateDefaultConfigText();
```

This method returns a Caml-formatted string that contains all registered properties with their default values.

### Parsing the Configuration

To parse a Caml configuration string from the programmable block’s Custom Data, call:
   
```csharp
bool success = config.ParseConfig(pb.CustomData);
```

- **Behavior:**
  - Validates the syntax and structure (including proper indentation for sub configurations).
  - Logs errors (if any) that can later be reviewed from the `ErrorLog` property.
  - Uses dynamic indent detection for sub config entries.

### Retrieving Parsed Values

After successful parsing, retrieve configuration values using the generic getter:
   
```csharp
T value = config.Get<T>("PropertyName");
```

*Example:*
```csharp
float aFloat = config.Get<float>("aFloat");
string aString = config.Get<string>("aString");
List<int> anIntList = config.Get<List<int>>("anIntList");
```

For sub configurations, obtain the sub config instance:
   
```csharp
ConfigFile subConfig = config.GetSubConfig("subConfig");
int anInt = subModule.Get<int>("anInt");
```

---

## Error Handling

The API logs errors during registration, parsing, and retrieval. Common error conditions include:

- **Registration Errors:**  
  Attempting to register duplicate properties or adding properties after finalization.
- **Syntax Errors:**  
  Incorrect formatting (e.g., missing colons or inconsistent indentation in sub configs).
- **Parsing Errors:**  
  Type mismatches or unknown properties.
- **Usage Before Finalization:**  
  Attempting to parse or retrieve configuration values before finalizing registration will result in an exception.

All errors are recorded in the public `ErrorLog` and routed via the customizable `Logger` delegate.

---

## Usage

To integrate the Caml API into your script, follow these steps:

1. **Add the API Code:**  
   Place the [API code](./api.cs) into your script. Clearly demarcate it from your application code.

2. **Create and Configure the Instance:**  
   In your `Program()` constructor, initialize a `ConfigFile` instance with your programmable block (`Me`) and program reference (`this`). Register your properties and sub configurations, and then call `FinalizeRegistration()`.  
   *Note:* Finalization automatically writes default configuration data if necessary.

3. **Check for and Parse Changes:**  
   In your `Main()` method, call `config.CheckAndReparse()` to re-parse the configuration when the Custom Data changes.

4. **Retrieve Values:**  
   Access configuration values using `Get<T>()` or retrieve sub configurations as needed.

### Simple Example

```csharp
/* v ---------------------------------------------------------------------- v */
/* v Caml Config File API                                                   v */
/* v ---------------------------------------------------------------------- v */

<CONFIG API CODE GOES HERE>

/* ^ ---------------------------------------------------------------------- ^ */
/* ^ Caml Config File API                                                   ^ */
/* ^ ---------------------------------------------------------------------- ^ */

public ConfigFile config; // Global configuration instance

public Program() {
    // Initialize the configuration.
    config = new ConfigFile(Me, this);
    // Register root-level configuration properties.
    config.RegisterProperty("anInteger", ConfigValueType.Int, 42);
    config.RegisterProperty("aFloat", ConfigValueType.Float, 3.14f);
    config.RegisterProperty("anIntList", ConfigValueType.IntList, new List<int> { 1, 1, 2, 3, 5, 8, 13 });

    // Register a sub configuration.
    config.RegisterSubConfig("subConfig");
    // Register properties for the sub config.
    config.RegisterProperty("subConfig", "aString", ConfigValueType.String, "Foo");
    config.RegisterProperty("subConfig", "aFloatList", ConfigValueType.FloatList, new List<float> { 0.25f, 0.50f, 0.75f });
    config.RegisterProperty("subConfig", "aStringList", ConfigValueType.StringList, new List<string> { "Bar", "Baz" });
    // Finalize registration to lock the schema.
    config.FinalizeRegistration();
}

public void Main(string args) {    
    // Re-parse in case the Custom Data has changed.
    if (!config.CheckAndReparse()) {
        Echo("Configuration parsing failed. Please fix the errors and run again.");
        return;
    }

    // Retrieve values from the configuration.
    int anInteger = config.Get<int>("anInteger");
    float aFloat = config.Get<float>("aFloat");
    List<int> anIntList = config.Get<List<int>>("anIntList");
    string aString = config.Get<string>("subConfig", "aString");
    List<float> aFloatList = config.Get<List<float>>("subConfig", "aFloatList");
    List<string> aStringList = config.Get<List<string>>("subConfig", "aStringList");

    // Output the values to the terminal.
    Echo($"anInteger: {anInteger}");
    Echo($"aFloat: {aFloat}");
    Echo($"anIntList: {string.Join(", ", anIntList)}");
    Echo("subConfig:");
    Echo($"  aString: {aString}");
    Echo($"  aFloatList: {string.Join(", ", aFloatList)}");
    Echo($"  aStringList: {string.Join(", ", aStringList)}");
}
```