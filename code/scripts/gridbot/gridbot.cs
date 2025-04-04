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
            public IMyCubeGrid RefGrid;
            public Vector3I IndexOffset;
            public Quaternion OrientOffset;
            internal Vector3D i_VectorRight, i_VectorLeft, i_VectorUp, i_VectorDown, i_VectorBackward, i_VectorForward, i_Position;
            public virtual Vector3D VectorRight     { get {return i_VectorRight;   } internal set {i_VectorRight    = value;}}
            public virtual Vector3D VectorLeft      { get {return i_VectorLeft;    } internal set {i_VectorLeft     = value;}}
            public virtual Vector3D VectorUp        { get {return i_VectorUp;      } internal set {i_VectorUp       = value;}}
            public virtual Vector3D VectorDown      { get {return i_VectorDown;    } internal set {i_VectorDown     = value;}}
            public virtual Vector3D VectorBackward  { get {return i_VectorBackward;} internal set {i_VectorBackward = value;}}
            public virtual Vector3D VectorForward   { get {return i_VectorForward; } internal set {i_VectorForward  = value;}}
            public virtual Vector3D Position        { get {return i_Position;      } internal set {i_Position       = value;}}
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
        /* ^ ---------------------------------------------------------------------- ^ */
        /* ^ Transformations                                                        ^ */
        /* ^ ---------------------------------------------------------------------- ^ */

        /* v ---------------------------------------------------------------------- v */
        /* v Grid Inspection Utilities                                              v */
        /* v ---------------------------------------------------------------------- v */
        public static IMyCubeGrid FindReferenceGrid(MyGridProgram program) {
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
        /* ^ ---------------------------------------------------------------------- ^ */
        /* ^ Grid Inspection Utilities                                              ^ */
        /* ^ ---------------------------------------------------------------------- ^ */

        /* v ---------------------------------------------------------------------- v */
        /* v Autopilot Utilities                                                    v */
        /* v ---------------------------------------------------------------------- v */
        public void OrientGyrosToGrid(List<IMyGyro> gyros, double pitch, double roll, double yaw) {
            // Loop through each gyro and set its orientation
            foreach (var gyro in gyros) {
                gyro.SetValueFloat("Pitch", pitch);
                gyro.SetValueFloat("Roll", roll);
                gyro.SetValueFloat("Yaw", yaw);
            }
            return;
        }
        /* ^ ---------------------------------------------------------------------- ^ */
        /* ^ Autopilot Utilities                                                    ^ */
        /* ^ ---------------------------------------------------------------------- ^ */

        // Variables
        public double pitch, roll, yaw;
        public StaticRefBody refBody;
        // List of gyros
        public List<IMyGyro>;
        
        public Program() {
            // initialize variables
            pitch = 0;
            roll = 0;
            yaw = 0;

            // Set the reference grid
            SetReferenceGrid(this, out refBody);
            if (refBody == null) {
                Echo("No reference grid found.");
                return;
            }
            // Get Euler angles
            GetEulerAngles(refBody, out pitch, out roll, out yaw);

            // Get the gyros
            GetMyGyros(this, out gyros);
            if (gyros == null || gyros.Count == 0) {
                Echo("No gyros found.");
                return;
            }
        }

        public void Main(string args) {
            // Orient Gyros to reference grid
            OrientGyrosToGrid(gyros, pitch, roll, yaw);           
        }

#region PreludeFooter
    }
}
#endregion