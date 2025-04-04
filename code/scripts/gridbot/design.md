
# Grid Bot Design

## Problem 1: Fixing the Drone's Orientation to a Reference Grid
In order to constrain the drones orientation so that its reference frame is always aligned with the grid's reference frame, we need a way to automatically set the drones orientation according to the coordinate frame of the reference grid. One way of doing this would be to 

1. identify a reference grid
2. get the reference grid orientation in world coordinates
3. compute the corresponding Euler angles for the grid's world orientation
4. set the drone's orientation to the computed Euler angles using the "Pitch", "Roll" and "Yaw" properties of its gyroscope(s).

This should be fairly straightforward to implement, but first wee need to figure out how (and if it's possible) to perform each of these steps within the constraints of the SE script api. We'll tackle each step one at a time.

### Step 1: Identify a Reference Grid
First, we need to decide on a reliable way of specifying the grid to use as our orientation reference. The simplest choice for this would be to use a connector attached to the target grid. To automate this step, we need a way for our script to:

1. find a connector on its own grid that is currently locked to another connector.
2. verify that the grid the other connector belongs to is static.

If both of these conditions are met, we can use the grid of the other connector as our reference. Since it is Highly unlikely that a drone will have multiple connectors simultaneously locked to moe than one *static* grid, it is safe to assume that the first connector we find that meets the criteria is attached to the intended target grid.

We can implement this step as its own C# function for reusability. The function will take a `MyGridProgram` object as an argument and return the reference grid as an `IMyCubeGrid` object. The function will look something like this:

```csharp
public IMyCubeGrid FindReferenceGrid(MyGridProgram program) {
    // Get all connectors on the grid
    List<IMyShipConnector> connectors = new List<IMyShipConnector>();
    program.GridTerminalSystem.GetBlocksOfType(connectors);

    // Loop through the connectors to find one that is locked to a static grid
    foreach (var connector in connectors) {
        if (connector.Status == MyShipConnectorStatus.Locked) {
            var otherConnector = connector.OtherConnector;
            if (otherConnector != null && otherConnector.CubeGrid.IsStatic) {
                return otherConnector.CubeGrid;
            }
        }
    }

    // If no suitable connector was found, return null
    return null;
}
```

### Step 2: Get the Reference Grid's Orientation in World Coordinates
Here, much of the work is done for us. We can leverage the `StaticRefBody` provided in the [world-coords](../../utils/world-coords/README.md) utilities. This class provides a convenient way of retrieving the orientation and position of any static grid in world coordinates.

```csharp
public static void SetReferenceGrid(MyGridProgram program, out StaticRefBody refBody) {
    // Get the reference grid
    IMyCubeGrid referenceGrid = FindReferenceGrid(program);
    if (referenceGrid == null) {
        return; // No reference grid found
    }
    // Get the reference grid's orientation in world coordinates
    refBody = new StaticRefBody(referenceGrid);

    return;
}
```

### Step 3: Compute the Euler Angles
We have to be careful here as there is a risk of running into "gimbal lock" if we compute the Euler angles directly from the world space "forward, "up" and "right" vectors. Instead, we can convert our world space orientation vectors to a quaternion, which doesn't suffer from this issue. We can use the following process to get our Euler angles (Pitch, Roll and Yaw) needed for driving our gyro:

1. **Construct the Rotation Matrix**</br>
    Assume our three vectors are already orthonormal. A common convention is to assign them as the columns of a 3×3 rotation matrix. For example, if we let:
    - $\mathbf{r} = [r_x, r_y, r_z]^T$ be the right vector,
    - $\mathbf{u} = [u_x, u_y, u_z]^T$ be the up vector,
    - $\mathbf{f} = [f_x, f_y, f_z]^T$ be the forward vector,</br>
    then we can form the rotation matrix as:
```math
R = \begin{matrix} r_x & u_x & f_x \\ r_y & u_y & f_y \\ r_z & u_z & f_z \end{matrix}
```
> *Note:* Depending on our coordinate system and application (for instance, if our “forward” axis is actually defined as $-z$ rather than $+z$), we may need to adjust the ordering or sign conventions.

2. **Convert the Rotation Matrix to a Quaternion**</br>>
    Once we have the rotation matrix $R$, we can convert it to a quaternion. We can use the `CreateFromRotationMatrix` method of the Quaternion class for this.
3. **Convert the Quaternion to Euler Angles:**  
    Once we have the quaternion $q = (w, x, y, z)$, we can convert it to Euler angles (Pitch, Roll, Yaw) using the following:
- **Roll ($\phi$, rotation about the x-axis):**  
```math
\phi = \arctan2\left(2\,(w\,x + y\,z),\, 1 - 2\,(x^2 + y^2)\right)
```        
- **Pitch ($\theta$, rotation about the y-axis):**  
```math
\theta = \arcsin\left(2\,(w\,y - z\,x)\right)
```
- **Yaw ($\psi$, rotation about the z-axis):**  
```math
\psi = \arctan2\left(2\,(w\,z + x\,y),\, 1 - 2\,(y^2 + z^2)\right)
```    
    These formulas assume that the rotation order is roll (x-axis), then pitch (y-axis), then yaw (z-axis).

We can implement this in C# with the following function:
```csharp
public static void GetEulerAngles(StaticRefBody refBody, out double pitch, out double roll, out double yaw) {
    // Get the right, up and forward vectors from the reference grid
    Vector3D right = refBody.Right;
    Vector3D up = refBody.Up;
    Vector3D forward = refBody.Forward;

    // Create the rotation matrix
    MatrixD rotationMatrix = new MatrixD(
        right.X, up.X, forward.X, 0,
        right.Y, up.Y, forward.Y, 0,
        right.Z, up.Z, forward.Z, 0,
        0, 0, 0, 1
    );

    // Convert to quaternion
    Quaternion quaternion;
    Quaternion.CreateFromRotationMatrix(ref rotationMatrix, out quaternion);

    // Convert quaternion to Euler angles
    roll = Math.Atan2(2 * (q.W * q.X + q.Y * q.Z), 1 - 2 * (q.X * q.X + q.Y * q.Y));
    pitch = Math.Asin(2 * (q.W * q.Y - q.Z * q.X));
    yaw = Math.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));

    return;
}
```

> *Note:* Above, we are using a 4x4 matrix to represent the rotation matrix since SE uses a 4D system for transformations. This is common in 3D graphics programming, where the last row and column are used for translation and homogeneous coordinates. This approach offers a more general representation of transformations with some valuable benefits, such as being able to combine translation and rotation in a single matrix.

### Step 4: Set the Gyro's Orientation
Once we have the Euler angles, we can set the orientation of the gyroscope(s) on the drone. The SE API provides an undocumented way to set the orientation of a gyroscope using the `SetValueFloat` method. First, though, we need a way to get a list of all the gyroscopes on the drone. We can use the `GetBlocksOfType` method of the `IMyGridTerminalSystem` as to do this. We'll implement this as a separate function:

```csharp
public static void GetMyGyros(MyGridProgram program, out List<IMyGyro> gyros) {
    // Get all gyros on the grid
    List<IMyGyro> allGyros = new List<IMyGyro>();
    program.GridTerminalSystem.GetBlocksOfType(allGyros);

    // Filter the gyros to only include those that are on my grid
    gyros = allGyros.Where(g => g.CubeGrid == program.Me.CubeGrid).ToList();
    if (gyros.Count == 0) {
        return;
    }
    return;
}
```

New, we can use the gyros' `SetValueFloat` method to update the Pitch, Roll and Yaw of the drone:

```csharp
public void OrientGyrosToGrid(List<IMyGyro> gyros, double pitch, double roll, double yaw) {
    // Loop through each gyro and set its orientation
    foreach (var gyro in gyros) {
        gyro.SetValueFloat("Pitch", pitch);
        gyro.SetValueFloat("Roll", roll);
        gyro.SetValueFloat("Yaw", yaw);
    }
    return;
}
```