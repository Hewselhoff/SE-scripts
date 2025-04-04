/* v ---------------------------------------------------------------------- v */
/* v RefBody Classes                                                        v */
/* v ---------------------------------------------------------------------- v */

/// <summary>
/// RefBody is a base class for creating reference bodies that can be used to calculate direction vectors
/// and positions in a the world coordinate frame.
/// </summary>
public class RefBody {
    /// <summary>
    /// The reference grid associated with this reference body.
    /// </summary>
    public IMyCubeGrid RefGrid;

    /// <summary>
    /// The index offset of the reference body within the grid.
    /// </summary>
    public Vector3I IndexOffset;

    /// <summary>
    /// The orientation offset of the reference body as a quaternion.
    /// </summary>
    public Quaternion OrientOffset; 

    // Reference Vectors : to be overridden
    internal Vector3D i_VectorRight, i_VectorLeft, i_VectorUp, i_VectorDown, i_VectorBackward, i_VectorForward, i_Position;
    /// <summary> Unit vector normal to the right face of the reference body in world coordinates </summary>
    public virtual Vector3D VectorRight     { get {return i_VectorRight;   } internal set {i_VectorRight    = value;}}
    /// <summary> Unit vector normal to the left face of the reference body in world coordinates </summary>
    public virtual Vector3D VectorLeft      { get {return i_VectorLeft;    } internal set {i_VectorLeft     = value;}}
    /// <summary> Unit vector normal to the top face of the reference body in world coordinates </summary>
    public virtual Vector3D VectorUp        { get {return i_VectorUp;      } internal set {i_VectorUp       = value;}}
    /// <summary> Unit vector normal to the bottom face of the reference body in world coordinates </summary>
    public virtual Vector3D VectorDown      { get {return i_VectorDown;    } internal set {i_VectorDown     = value;}}
    /// <summary> Unit vector normal to the back face of the reference body in world coordinates </summary>
    public virtual Vector3D VectorBackward  { get {return i_VectorBackward;} internal set {i_VectorBackward = value;}}
    /// <summary> Unit vector normal to the front face of the reference body in world coordinates </summary>
    public virtual Vector3D VectorForward   { get {return i_VectorForward; } internal set {i_VectorForward  = value;}}
    /// <summary> The position of the reference body in world coordinates</summary>
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

/// <summary>
/// StaticRefBody is a RefBody that calculates its direction vectors once, when it's instantiated.
/// This is useful for static grids, such as planet or asteroid anchored bases.
/// </summary>
public class StaticRefBody : RefBody {

    // Constructors re-directed to base (why?, because C#, that's why)
    public StaticRefBody(IMyCubeGrid refGrid, Vector3I indexOffset, Quaternion orientOffset) : base(refGrid, indexOffset, orientOffset) {} 
    public StaticRefBody(IMyCubeGrid refGrid, Vector3I indexOffset) : base(refGrid, indexOffset) {} 
    public StaticRefBody(IMyCubeBlock block) : base(block) {} 
    public StaticRefBody(IMyTerminalBlock block) : base(block) {} 
    
    internal override void Initialize() {
        // Set Static Vectors (once on instantiation)
        i_VectorRight     = GetTransformedDirVector(new Vector3I( 1, 0, 0));
        i_VectorUp        = GetTransformedDirVector(new Vector3I( 0, 1, 0));
        i_VectorBackward  = GetTransformedDirVector(new Vector3I( 0, 0, 1));
        i_VectorLeft      = -i_VectorRight;
        i_VectorDown      = -i_VectorUp;
        i_VectorForward   = -i_VectorBackward;
        i_Position = RefGrid.GridIntegerToWorld(IndexOffset);
    }
}

/// <summary>
/// DynamicRefBody is a RefBody that recalculates its direction vectors each time they're requested.
/// This is useful for grids or sub-grids that are mobile, such as ships or rovers.
/// </summary>
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

/* ^ ---------------------------------------------------------------------- ^ */
/* ^ RefBody Classes                                                        ^ */
/* ^ ---------------------------------------------------------------------- ^ */