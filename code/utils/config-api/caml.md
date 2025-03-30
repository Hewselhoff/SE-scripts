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

## Example Caml Configuration

Below is an example of a complete Caml configuration file:

```yaml
# Caml configuration example

# Numerical settings
MaxSpeed: 10.5
MinSpeed: 1.0

# String setting
ShipName: Enterprise

# List settings
anIntList: [100, 200, 300]
aFloatList: [0.25, 0.50, 0.75]
aStringList: [Foo, Bar]
```