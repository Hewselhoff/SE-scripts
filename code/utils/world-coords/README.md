# Reference Body Classes
The **RefBody** classes provide a simple and convenient way to retrieve the orientation and position of a reference block or grid in world coordinates. They are designed to be used in scripts to simplify the process of working with grid orientations and positions.

> Credit for the basic implementation provided in [refbody.cs](./refbody.cs) goes to the Steam user [*FragMuffin*](https://steamcommunity.com/profiles/76561197996892763). Modifications to the code are minimal and consist primarily of expanded comments and documentation. The original code can found in [this thread](https://forums.keenswh.com/threads/using-a-block-as-a-reference-point-for-ship-orientation.7391950/) on the Steam Space Engineers forum.

## Table of Contents

- [Overview](#overview)
- [RefBody (Base Class)](#refbody-base-class)
  - [Properties](#properties)
  - [Methods and Constructors](#methods-and-constructors)
  - [How It Works](#how-it-works)
  - [Typical Instantiation Methods](#typical-instantiation-methods)
- [StaticRefBody](#staticrefbody)
  - [Description and Use Cases](#description-and-use-cases)
  - [Initialization and Behavior](#initialization-and-behavior)
  - [Usage Example](#usage-example-static)
- [DynamicRefBody](#dynamicrefbody)
  - [Description and Use Cases](#description-and-use-cases-1)
  - [Initialization and Behavior](#initialization-and-behavior-1)
  - [Usage Example](#usage-example-dynamic)
- [Example Scenarios in Space Engineers](#example-scenarios-in-space-engineers)
- [Conclusion](#conclusion)

---

## Overview

The **RefBody** classes are designed to abstract the concept of a reference body on a grid in Space Engineers. A reference body is a conceptual point (or block) that defines a coordinate frame relative to the grid. By using these classes, you can easily calculate direction vectors (such as right, left, up, down, forward, and backward) in world space. This is particularly useful when aligning blocks, orienting ships, or calculating trajectories.

There are two specialized implementations:
- **StaticRefBody**: Calculates and stores the directional vectors at the time of instantiation, ideal for static or anchored grids (e.g., bases on planets or asteroids).
- **DynamicRefBody**: Recalculates the directional vectors every time they are requested, ideal for dynamic grids (e.g., moving ships or rovers) whose orientation might change over time.

---

## RefBody (Base Class)

### Properties

- **RefGrid**  
  _Type:_ `IMyCubeGrid`  
  _Description:_ The grid associated with this reference body. It represents the structure (ship, station, etc.) in Space Engineers.

- **IndexOffset**  
  _Type:_ `Vector3I`  
  _Description:_ The grid coordinate offset where the reference body is located. This is used to translate grid positions into world coordinates.

- **OrientOffset**  
  _Type:_ `Quaternion`  
  _Description:_ The orientation offset for the reference body. This quaternion adjusts the local coordinate frame to align correctly with the world coordinate frame.

- **Direction Vectors**  
  These properties represent unit vectors in the world coordinate system:
  - `VectorRight`
  - `VectorLeft`
  - `VectorUp`
  - `VectorDown`
  - `VectorBackward`
  - `VectorForward`
  - `Position` – The world coordinate position of the reference body.

  Internally, these vectors are stored in fields (e.g., `i_VectorRight`, `i_VectorLeft`, etc.) and are calculated using the helper method `GetTransformedDirVector`.

### Methods and Constructors

- **Constructors**  
  The base class provides several constructors allowing initialization using different types:
  - With an `IMyCubeGrid`, index offset, and orientation offset.
  - With just an `IMyCubeGrid` (defaults are applied).
  - With an `IMyCubeBlock` or `IMyTerminalBlock`, which extract the grid, position, and orientation from the block.

- **GetTransformedDirVector(Vector3I dirIndexVect)**  
  _Description:_ This internal method computes a transformed directional vector by:
  1. Converting the local grid index to a world position.
  2. Transforming the input direction vector by the `OrientOffset`.
  3. Calculating the difference between the reference position and the transformed point.  
  This method is central to obtaining correct world-space direction vectors based on the grid’s properties.

- **Initialize()**  
  _Description:_ An overridable method meant to be implemented by derived classes. For a static reference body, this method precomputes the vectors, whereas for a dynamic reference body, it remains empty to allow on-demand calculation.

### How It Works

The **RefBody** class abstracts the transformation from a local grid coordinate system to world space. By storing a grid reference, an index offset, and an orientation offset, the class provides methods to calculate which direction (right, left, etc.) corresponds to the grid's faces in world space. This abstraction is crucial in environments like Space Engineers where grids (such as ships and bases) need to interact with the environment accurately, respecting both position and orientation.

### Typical Instantiation Methods
The base class offers multiple constructors to allow sub-classes to provide different ways of instantiating reference bodies. The two most common and convenient methods are:
1. **Using a Block Reference**:  
   This method is useful when you have a specific block in mind (like a cockpit or control panel) and want to use its grid and orientation.
2. **Using a Grid and Index Offset**:  
   This method is useful when you want to create a reference body based on the grid itself, allowing for more flexibility in specifying the reference point. The index offset can be used to specify a point on the grid. Not that this vector is not in world distance units, but in integer grid coordinates, e.g. `Vector3I(0, 0, 0)` denotes the block at the origin of the grid, `Vector3I(1, 0, 0)` denotes the block to the immediate right of the origin, etc.

---

## StaticRefBody

### Description and Use Cases

**StaticRefBody** is designed for grids that do not move or change orientation over time—typically bases or installations anchored to planets or asteroids. Since the grid is static, the directional vectors are calculated once during initialization and then stored for later use, which improves performance by avoiding redundant calculations.

### Initialization and Behavior

- **Initialization:**  
  The `Initialize()` method in **StaticRefBody** is overridden to compute and store the following:
  - `i_VectorRight` (using local vector `[1, 0, 0]`)
  - `i_VectorUp` (using local vector `[0, 1, 0]`)
  - `i_VectorBackward` (using local vector `[0, 0, 1]`)
  - The opposite vectors (left, down, forward) are set as the negatives of the computed vectors.
  - The position (`i_Position`) is calculated using the grid's conversion function.

- **Performance:**  
  Since the values are static, they do not update if the grid's orientation changes (which is expected for an anchored structure).

### Usage Example (StaticRefBody)

```csharp
// Example usage of StaticRefBody for a static base or anchored structure
// Assume that 'staticBlock' is an IMyCubeBlock reference from a block on the base

// Create a StaticRefBody instance using the block as the reference point
StaticRefBody staticRefBody = new StaticRefBody(staticBlock);

// Retrieve the precomputed directional vectors
Vector3D rightVector = staticRefBody.VectorRight;
Vector3D upVector = staticRefBody.VectorUp;
Vector3D forwardVector = staticRefBody.VectorForward;

// Use these vectors for aligning new modules or calculating trajectories
Vector3D dockingPortPosition = staticRefBody.Position + (rightVector * 10);
```

---

## DynamicRefBody

### Description and Use Cases

**DynamicRefBody** is intended for grids that are mobile or have changing orientations—such as ships, rovers, or sub-grids attached to moving parts. Instead of caching the direction vectors at instantiation, **DynamicRefBody** recalculates them every time they are accessed. This ensures that the directional vectors always reflect the current state of the grid.

### Initialization and Behavior

- **Initialization:**  
  The `Initialize()` method in **DynamicRefBody** is left empty. This design choice means that no vector values are precomputed or cached at instantiation.
  
- **On-Demand Calculation:**  
  Every time a property (such as `VectorRight`, `VectorUp`, etc.) is accessed, it calls the `GetTransformedDirVector` method with the corresponding local direction vector. This ensures that any change in the grid's orientation is immediately reflected in the returned vector.

### Usage Example (DynamicRefBody)

```csharp
// Example usage of DynamicRefBody for a dynamic ship or rover
// Assume that 'dynamicGrid' is an IMyCubeGrid reference from a moving ship

Vector3I referenceIndex = new Vector3I(0, 0, 0);

// Create a DynamicRefBody instance (orientation is handled dynamically)
DynamicRefBody dynamicRefBody = new DynamicRefBody(dynamicGrid, referenceIndex);

// Retrieve the current right vector, which recalculates based on the current orientation
Vector3D currentRightVector = dynamicRefBody.VectorRight;

// Later in the code, if the ship rotates, the following call will return the updated vector
Vector3D currentForwardVector = dynamicRefBody.VectorForward;

// Use these vectors for real-time navigation, alignment, or control systems.
```

---

## Example Scenarios in Space Engineers

### Static Scenario: Planetary Base Orientation

Imagine you have constructed an anchored base on an asteroid. Using **StaticRefBody** allows you to compute and store the base’s directional vectors once. You might use these vectors to:

- Align new modules or docking ports.
- Calculate fixed trajectories for incoming supply ships.
- Set up coordinate systems for in-game navigational aids.

Since the base remains static relative to the asteroid, the precomputed vectors remain valid indefinitely.

### Dynamic Scenario: Spaceship Navigation

For a spaceship that rotates and maneuvers in space, **DynamicRefBody** ensures that every time you query the direction (e.g., `VectorRight` or `VectorForward`), you get an updated vector that reflects the ship's current orientation. This is essential for things like:

- Real-time steering and control algorithms.
- Dynamic alignment for targeting or docking maneuvers.
- Updating HUD elements to display the ship’s current facing direction.

Because the ship's orientation can change at any moment, recalculating the vectors on demand avoids errors that could arise from using stale data.