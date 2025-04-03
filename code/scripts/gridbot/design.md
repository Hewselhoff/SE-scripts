
# Grid Bot Design

## Problem 1: Fixing the Drone's Orientation to the Reference Grid
In order to constrain the drones orientation so that its reference frame is always aligned with the grid's reference frame, we need a way to automatically set the drones orientation according to the coordinate frame of the reference grid. One way of doing this would be to 

1. get the axes of the reference grid's coordinate frame
2. convert them to the world coordinate frame
3. compute the corresponding Euler angles for the grid's world orientation
4. set the drone's orientation to the computed Euler angles using the "Pitch", "Roll" and "Yaw" properties of its gyroscope(s).

This should be fairly straightforward to implement, but first wee need to figure out how (and if it's possible) to perform each of these steps within the constraints of the SE script api. We'll tackle each step one at a time.
### Step 1: Get the Reference Grid's Orientation
The API provides the capability to retrieve a quaternion for a block that can be used to transform the local coordinate frame of the block to the world coordinate frame.  Therefore, we can easily get the orientation of the reference grid by getting the quaternion of a block on the grid and simply applying the quaterion to the unit vectors of the axes, e.g. [1, 0, 0] for the right axis and so on. First, however, we need to decide on a reliable way of choosing the block in the reference grid to use as its orientation reference. The simplest choice for this would be to use a connector attached to the target grid. This will also serve as an easy way of identifying the target grid for automating the process of retrieving and storing the orientation info.

### Step 2: Convert the Reference Grid's Orientation to World Coordinates
Here, much of the work is done for us. I found a nice set of little utility classes in a thread from a post on the Steam SE forum. The classes provide methods for easily retrieving a block's orientation vectors in world coordinates. The implementation provides are three classes:

1. `RefBody` - This is the base class for the other two classes. It holds the reference to the grid and the block, and provides a general method to get transformed local direction vectors in world coordinates using the quaternion of the block passed to the constructor.
2. `StaticRefBody` - This class calculates the vectors once during initialization. This is useful for static grids like planetary or asteroid anchored bases. This is the class we will use for the reference grid.
3. `DynamicRefBody` - This class calculates the vectors each time they're requested. This is useful for moving grids like ships. 

The implementation is provided below.

> Thanks to the Steam user *"FragMuffin"* for these handy classes. The original thread can be found [here](https://steamcommunity.com/app/244850/discussions/0/618459405716679388/).

#### RefBody
```csharp
public class RefBody {
    public IMyCubeGrid RefGrid;       // Reference to a "ship" as a single rigid body
    public Vector3I IndexOffset;      // Index of the block you're interested in
    public Quaternion OrientOffset;   // quaternion used to transform the ship's direction vectors, to the block
    // Reference Vectors : to be overridden
    internal Vector3D i_VectorRight, i_VectorLeft, i_VectorUp, i_VectorDown, i_VectorBackward, i_VectorForward, i_Position;
    public virtual Vector3D VectorRight     { get {return i_VectorRight;   } internal set {i_VectorRight    = value;}}
    public virtual Vector3D VectorLeft      { get {return i_VectorLeft;    } internal set {i_VectorLeft     = value;}}
    public virtual Vector3D VectorUp        { get {return i_VectorUp;      } internal set {i_VectorUp       = value;}}
    public virtual Vector3D VectorDown      { get {return i_VectorDown;    } internal set {i_VectorDown     = value;}}
    public virtual Vector3D VectorBackward  { get {return i_VectorBackward;} internal set {i_VectorBackward = value;}}
    public virtual Vector3D VectorForward   { get {return i_VectorForward; } internal set {i_VectorForward  = value;}}
    public virtual Vector3D Position        { get {return i_Position;      } internal set {i_Position       = value;}}

    // Constructor(s)
    public RefBody(IMyCubeGrid refGrid, Vector3I indexOffset, Quaternion orientOffset) {
        RefGrid = refGrid;
        IndexOffset = indexOffset;
        OrientOffset = orientOffset;
        Initialize();
    }
    public RefBody(IMyCubeGrid refGrid) : this(refGrid, Vector3I.Zero, Quaternion.Identity) {}
    public RefBody(IMyCubeGrid refGrid, Vector3I indexOffset) : this(refGrid, indexOffset, Quaternion.Identity) {}
    public RefBody(IMyCubeBlock block) {
        RefGrid = block.CubeGrid;
        IndexOffset = block.Position;
        block.Orientation.GetQuaternion(out OrientOffset);
        Initialize();
    }
    public RefBody(IMyTerminalBlock block) : this(block as IMyCubeBlock) {}

    // Direction Vectors
    internal Vector3D GetTransformedDirVector(Vector3I dirIndexVect) {
        Vector3D fromPoint = RefGrid.GridIntegerToWorld(IndexOffset);
        Vector3D toPoint   = RefGrid.GridIntegerToWorld(IndexOffset - Vector3I.Transform(dirIndexVect, OrientOffset));
        return fromPoint - toPoint;
    }

    // Abstract Functions
    internal virtual void Initialize() {}
}
```

#### StaticRefBody
```csharp
public class StaticRefBody : RefBody {

    // Constructors re-directed to base (why?, because C#, that's why)
    public StaticRefBody(IMyCubeGrid refGrid, Vector3I indexOffset, Quaternion orientOffset) : base(refGrid, indexOffset, orientOffset) {} 
    public StaticRefBody(IMyCubeGrid refGrid, Vector3I indexOffset) : base(refGrid, indexOffset) {} 
    public StaticRefBody(IMyCubeBlock block) : base(block) {} 
    public StaticRefBody(IMyTerminalBlock block) : base(block) {} 
    
    internal override void Initialize() {
        // Set Static Vectors (once on instanciation)
        i_VectorRight     = GetTransformedDirVector(new Vector3I( 1, 0, 0)); // Vector3I.Right
        i_VectorUp        = GetTransformedDirVector(new Vector3I( 0, 1, 0)); // Vector3I.Up
        i_VectorBackward  = GetTransformedDirVector(new Vector3I( 0, 0, 1)); // Vector3I.Backward
        i_VectorLeft      = -i_VectorRight;
        i_VectorDown      = -i_VectorUp;
        i_VectorForward   = -i_VectorBackward;
        i_Position = RefGrid.GridIntegerToWorld(IndexOffset);
    }
}
```

#### DynamicRefBody
```csharp
public class DynamicRefBody : RefBody {
    internal override void Initialize() {}
    
    // Constructors re-directed to base (why?, because C#, that's why)
    public DynamicRefBody(IMyCubeGrid refGrid, Vector3I indexOffset, Quaternion orientOffset) : base(refGrid, indexOffset, orientOffset) {} 
    public DynamicRefBody(IMyCubeGrid refGrid, Vector3I indexOffset) : base(refGrid, indexOffset) {} 
    public DynamicRefBody(IMyCubeBlock block) : base(block) {} 
    public DynamicRefBody(IMyTerminalBlock block) : base(block) {} 

    // Direction Vectors : calculated anew each time they're requested
    public override Vector3D VectorRight     { get { return GetTransformedDirVector(new Vector3I( 1, 0, 0)); }}
    public override Vector3D VectorUp        { get { return GetTransformedDirVector(new Vector3I( 0, 1, 0)); }}
    public override Vector3D VectorBackward  { get { return GetTransformedDirVector(new Vector3I( 0, 0, 1)); }}
    public override Vector3D VectorLeft      { get { return GetTransformedDirVector(new Vector3I(-1, 0, 0)); }}
    public override Vector3D VectorDown      { get { return GetTransformedDirVector(new Vector3I( 0,-1, 0)); }}
    public override Vector3D VectorForward   { get { return GetTransformedDirVector(new Vector3I( 0, 0,-1)); }}
    public override Vector3D Position { get { return RefGrid.GridIntegerToWorld(IndexOffset); }}
}
```

### Step 3: Compute the Euler Angles
We have to be careful here as there is a risk of running into "gimbal lock" if we compute the Euler angles directly from the world space "forward, "up" and "right" vectors. Instead, we can convert our world space orientation vectors to a quaternion, and then convert the quaternion to Euler angles. Since the quaternion represents the rotation in 4D space, it avoids the "gimbal lock" problem often encountered when using Euler angles. We can use the following process to get our Euler angles (Pitch, Roll and Yaw) needed for driving our gyro:

1. **Construct the Rotation Matrix**</br>
    Assume our three vectors are already orthonormal. A common convention is to assign them as the columns of a 3×3 rotation matrix. For example, if we let:
    - $\mathbf{r} = [r_x, r_y, r_z]^T$ be the right vector,
    - $\mathbf{u} = [u_x, u_y, u_z]^T$ be the up vector,
    - $\mathbf{f} = [f_x, f_y, f_z]^T$ be the forward vector,</br>
    then we can form the rotation matrix as:
```math
R = \begin{matrix} r_x & u_x & f_x & 0\\ r_y & u_y & f_y & 0 \\ r_z & u_z & f_z & 0 \\ 0 & 0 & 0 & 1 \end{matrix}
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

We can implement this in C# as a small set of functions::
```csharp
public static Quaternion GetQuaternion(Vector3D right, Vector3D up, Vector3D forward) {
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
    return quaternion;
}
public static void QuaternionToEuler(Quaternion q, out double pitch, out double roll, out double yaw) {
    // Convert quaternion to Euler angles
    roll = Math.Atan2(2 * (q.W * q.X + q.Y * q.Z), 1 - 2 * (q.X * q.X + q.Y * q.Y));
    pitch = Math.Asin(2 * (q.W * q.Y - q.Z * q.X));
    yaw = Math.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));
}
```
```csharp
// Example usage
Vector3D right = refBody.VectorRight;
Vector3D up = refBody.VectorUp;
Vector3D forward = refBody.VectorForward;
Quaternion quaternion = GetQuaternion(right, up, forward);
QuaternionToEuler(quaternion, out double pitch, out double roll, out double yaw);
```
> *Note:* The above code assumes that the vectors are already orthonormal. This is safe since we're using the creating our rotation matrix from orientation vectors.

### Step 4: Set the Gyro's Orientation
Once we have the Euler angles, we can set the orientation of the gyroscope(s) on the drone. The SE API provides a method to set the orientation of a gyroscope using the `SetValueFloat` method. We can use this method to set the Pitch, Roll and Yaw properties of the gyroscope(s) to the computed values:
```csharp
// ...get the drones gyros as a list from terminal system, then...
foreach (var gyro in gyros) {
    gyro.SetValueFloat("Pitch", pitch);
    gyro.SetValueFloat("Roll", roll);
    gyro.SetValueFloat("Yaw", yaw);
}
```