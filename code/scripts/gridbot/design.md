
# Grid Bot Design

## Problem 1: Fixing the Drone's Orientation to the Reference Grid
In order to constrain the drones orientation so that its reference frame is always aligned with the grid's reference frame, we need a way to automatically set the drones orientation according to the coordinate frame of the reference grid. One way of doing this would be to 

1. get the axes of the reference grid's coordinate frame
2. convert them to the world coordinate frame
3. compute the corresponding Euler angles for the grid's world orientation
4. set the drone's orientation to the computed Euler angles using the "Pitch", "Roll" and "Yaw" properties of its gyroscope(s).

This should be fairly straightforward to implement, but first wee need to figure out how (and if it's possible) to perform each of these steps within the constraints of the SE script api. We'll tackle each step one at a time.
### Step 1: Get the Reference Grid's Orientation
The API provides the capability to retrieve a quaternion for a block that can be used to transform the local coordinate frame of the block to the world coordinate frame.  Therefore, we can easily get the orientation of the reference grid by getting the quaternion of a block on the grid and simply applying the quaterion to the unit vectors of the axes, e.g. [1, 0, 0] for the right axis and so on. First, however, we need to decide on a reliable way of choosing the block in the drone grid to use as its orientation reference. The most intuitive choice for this is the drone's `IMyRemoteControl` since this block's orientation is already used as the reference for local thrust directions, which we already intuitively perceive as the reference for orienting in flight.


# Useful Snippets and Utility Code

## Set Pitch, Roll or Yaw of a Gyro
```
gyroscope.SetValueFloat("Pitch", 42);
gyroscope.SetValueFloat("Roll", 3.14);
gyroscope.SetValueFloat("Yaw", 2.71);
```

## Get World Orientation of a Block




# Functional Components
## Get Grid Orientation
### Step 1: Get attached connector:
1. Find main docking connector on this grid.
2. Check if connector is connected to another grid.
    * If not, exit
3.  


# Useful Third-party Classes

## Reference Body Classes
These classes provide orientation vectors and position of a block in *world coordinates*.
### RefBody
This is the base class for the other two classes. It holds the reference to the grid and the block, and provides a general method to get transformed local direction vectors in world coordinates. 
```csharp
This is a base class holding 
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

### StaticRefBody and DynamicRefBody
The StaticRefBody class calculates the vectors once during initialization. This is useful for static grids like planetary or asteroid anchored bases.
```csharp
// Static Reference Body : Reference vectors remain the same for the life of the instance
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

### DynamicRefBody
This class calculates the vectors each time they're requested. This is useful for moving grids like ships.
```csharp
// Dynamic Reference Body : Reference vectors are calculated when requested
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