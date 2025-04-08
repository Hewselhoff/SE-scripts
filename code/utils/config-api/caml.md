# Caml: A Simplified YAML-like Configuration Language

Caml is a lightweight configuration language designed for human readability. It uses a simplified, YAML-like syntax to define settings with a limited set of data types.

## Formatting Rules

- **Key-Value Pair:**  
  A valid configuration entry follows the format:
  ```yaml
  key: value
  ```
  There must be a colon (`:`) immediately following the key, optionally followed by a space before the value.

- **Comments:**  
  Any line beginning with `#` is ignored:
  ```yaml
  # This is a comment
  ```

- **Ordering:**  
  Keys can appear in any order. Each key should only appear once in a configuration file.

- **Lists:**  
  Lists must be fully enclosed in square brackets. The format should be:
  ```yaml
  key: [item1, item2, item3]
  ```

## Supported Data Types

Caml supports the following data types:
- **Integers:** `10`, `-5`.
- **Floats:** `3.14`, `-0.5`
- **Strings:** `foo`, `bar` (do not need to be enclosed in quotes).
- **Lists:** Ordered collections of values of the above types. List items must be of uniform type.

### Lists
Caml supports lists of all above types except lists (nested lists are not supported). Items in a list must be of uniform type, e.g. all integers, all floats, or all strings.
- **Syntax:**  
  - A list must start with `[` and end with `]`.
  - Items within a list are separated by commas.
  - Whitespace around items is ignored.
- **Examples:**  
  - **List of Integers:**  
    ```yaml
    anIntList: [100, 200, 300]
    ```
  - **List of Floats:**  
    ```yaml
    aFloatList: [0.25, 0.50, 0.75]
    ```
  - **List of Strings:**  
    ```yaml
    aStringList: [Foo, Bar]
    ```

## Sub-Configs
Caml supports one-level sub-configurations, which are configurations nested within another configuration. Sub-configs are defined using the same syntax as top-level configs but are indented to indicate their hierarchy, e.g.:
```yaml
subConfig:
  key: value
  ...
```

>**NOTE:** CAML only supports one level of sub-configs. You cannot nest sub-configs within other sub-configs.

### Indentation Rules
1. Sub configs must be indented with spaces (not tabs).
2. Each sub config can be indented with any number of spaces, so long as it is consistent within that sub config.

## Example Caml Configuration

### A Root-Level Only Config
Below is an example of a configuration file that contains only root-level settings:

```yaml
# Numerical settings
anInteger: 42
afloat: 3.14
# String setting
aString: Foo
# List settings
anIntList: [1, 1, 2, 3, 5, 8, 13]
aFloatList: [0.25, 0.50, 0.75]
aStringList: [Bar, Baz]
```

### Config Split into Two Sub-Config
This example shows how to split a configuration into two sub-configurations, `subConfigA` and `subConfigB`, each containing different settings. This can be useful if your script is notianally partitioned into multiple components or modules, and you want to keep the settings for each component separate.
```yaml
# Numerical settings
subConfigA:
  anInteger: 42
  afloat: 3.14
  anIntList: [1, 1, 2, 3, 5, 8, 13]
subConfigB:
  aString: Foo
  aFloatList: [0.25, 0.50, 0.75]
  aStringList: [Bar, Baz]
```

### Mixture of Root-Level and Sub-Config
This example shows a configuration that contains both root-level settings and a sub-configuration. This is useful when you want to keep some settings at the root level for easy access while also grouping related settings into a sub-configuration.
```yaml
# Root-level settings
anInteger: 42
afloat: 3.14
anIntList: [1, 1, 2, 3, 5, 8, 13]
# Sub-configuration
subConfig:
  aString: Bar
  aFloatList: [0.25, 0.50, 0.75]
  aStringList: [Bar, Baz]
```