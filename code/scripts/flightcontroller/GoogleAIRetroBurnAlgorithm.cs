// Program
public class DescentScript : MyGridProgram
{
    // --- User-defined variables ---
    const double DesiredDescentSpeed = 5.0; // Target descent speed in m/s
    const double LandingAltitude = 100.0; // Altitude to initiate landing procedures
    const double DesiredDeceleration = 5.0; // Desired deceleration during descent in m/s^2

    // --- References to blocks ---
    IMyShipController shipController;
    List<IMyThrust> downwardThrusters = new List<IMyThrust>();

    // --- State variables ---
    double shipMass = 0;
    Vector3D currentVelocity;
    Vector3D currentPosition;
    double currentAltitude;

    // --- Initialization ---
    public Program()
    {
        // Get references to blocks
        shipController = GridTerminalSystem.GetBlockAs<IMyShipController>("Your Cockpit Name"); // Replace with your cockpit name
        GridTerminalSystem.GetBlocksOfType(downwardThrusters, block => block.Orientation.Forward == shipController.Orientation.Down);

        // Get initial state information
        shipMass = shipController.CalculateShipMass().TotalMass;
        currentVelocity = shipController.GetShipVelocities().LinearVelocity;
        currentPosition = shipController.GetPosition();
        shipController.TryGetAltitude(MyAltitudeType.SeaLevel, out currentAltitude);

        // Set the runtime to update the script frequently (e.g., every 10 ticks)
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    }

    // --- Main loop ---
    public void Main(string argument, UpdateType updateSource)
    {
        // Update ship's state
        shipMass = shipController.CalculateShipMass().TotalMass;
        currentVelocity = shipController.GetShipVelocities().LinearVelocity;
        currentPosition = shipController.GetPosition();
        shipController.TryGetAltitude(MyAltitudeType.SeaLevel, out currentAltitude);

        // Get natural gravity vector
        Vector3D naturalGravity = shipController.GetNaturalGravity();

        // Calculate required thrust
        Vector3D requiredThrust = CalculateRequiredThrust(naturalGravity);

        // Apply thrust overrides
        SetThrustOverrides(requiredThrust);

        // Landing logic (if needed)
        if (currentAltitude <= LandingAltitude)
        {
            // Initiate landing procedures (e.g., adjust desired descent speed)
            // ...
        }

        // Display information on a text panel (optional)
        // ...
    }

    // --- Helper function to calculate required thrust ---
    Vector3D CalculateRequiredThrust(Vector3D naturalGravity)
    {
        // Calculate gravitational force
        Vector3D gravitationalForce = shipMass * naturalGravity;

        // Calculate desired net force for deceleration
        Vector3D desiredNetForce = shipMass * (-DesiredDeceleration * Vector3D.Normalize(currentVelocity)); // Assuming deceleration against current velocity

        // Calculate required thrust
        Vector3D requiredThrust = desiredNetForce + gravitationalForce;

        return requiredThrust;
    }

    // --- Helper function to set thrust overrides ---
    void SetThrustOverrides(Vector3D requiredThrust)
    {
        // Calculate the magnitude of the required downward thrust
        double downwardThrustMagnitude = Math.Max(0, Vector3D.Dot(requiredThrust, shipController.Orientation.Down));

        // Calculate the fraction of total downward thrust needed
        double totalDownwardThrustCapacity = 0;
        foreach (var thruster in downwardThrusters)
        {
            totalDownwardThrustCapacity += thruster.MaxEffectiveThrust;
        }

        double overrideValue = downwardThrustMagnitude / totalDownwardThrustCapacity;

        // Set thrust overrides on downward thrusters
        foreach (var thruster in downwardThrusters)
        {
            thruster.ThrustOverridePercentage = (float)(overrideValue * 100);
        }
    }
}
