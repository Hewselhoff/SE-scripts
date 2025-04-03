#region Prelude
using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using VRageMath;
using VRage.Game;
using VRage.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.CommonLibs;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;

// Change this namespace for each script you create.
namespace SpaceEngineers.UWBlockPrograms.GridBot {
    public sealed class Program : MyGridProgram {
    // Your code goes between the next #endregion and #region
#endregion

        /* v ---------------------------------------------------------------------- v */
        /* v RefBody Classes                                                        v */
        /* v ---------------------------------------------------------------------- v */
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
        /* ^ ---------------------------------------------------------------------- ^ */
        /* ^ RefBody Classes                                                        ^ */
        /* ^ ---------------------------------------------------------------------- ^ */

        /* v ---------------------------------------------------------------------- v */
        /* v Transformations                                                        v */
        /* v ---------------------------------------------------------------------- v */
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
        /* ^ ---------------------------------------------------------------------- ^ */
        /* ^ Transformations                                                        ^ */
        /* ^ ---------------------------------------------------------------------- ^ */

        // Variables
        public double pitch, roll, yaw;
        public Program() {
            // initialize variables
            pitch = 0;
            roll = 0;
            yaw = 0;
           
        }

        public void Main(string args) {
           
        }

#region PreludeFooter
    }
}
#endregion