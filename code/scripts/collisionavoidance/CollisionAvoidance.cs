//TODO: Implement config-api for configuration of range finder.

private string CockpitName = "ControlSeatBridge(LOLCOW)Radar Reference";
private string THRUSTER_GROUP_NAME = "FullThrusters";
private IMyBlockGroup caCamsGroup;
private List<IMyCameraBlock> caCams;
private double SCAN_DIST = 4000; // Set your desired scan distance (detection range) for your camera in meters.
private StringBuilder sb = new StringBuilder(); // StringBuilder object for output.
private const double sideCamBias = 56.25; // Distance from side cameras to foward most extent of ship.
private const double forwardCamBias = 6.09; // Distance from forward camera to forward most extent of ship.
private const double MAX_DIST = 100000f; // Max scan distance.
private List<IMyThrust> thrusters = new List<IMyThrust>();
private List<IMyThrust> brakingThrusters = new List<IMyThrust>(); 
double shipMass = 0;
double speed = 0;
private float maxBrakingThrust = 0;
private float currentBrakingThrust = 0;
private string brakingThrusterStatus = "DISENGAGED";
IMyShipController controller;

public Program()
{
    // Register this script's Main method to be called (updated) every 100 game ticks.
    Runtime.UpdateFrequency = UpdateFrequency.Update100;

    // Grab the Block Group that includes all cameras used for CA. Do all of this here in the ctor
    // so we don't allocate new memory every time the main script executes.
    caCamsGroup = (IMyBlockGroup)GridTerminalSystem.GetBlockGroupWithName("CollisionAvoidanceCameras");
    caCams = new List<IMyCameraBlock>();
    caCamsGroup.GetBlocksOfType<IMyCameraBlock>(caCams);

    // Enable Raycasting for the CA cameras.
    foreach(IMyCameraBlock caCam in caCams){
        //Echo("Camera: " + caCam.CustomName);
        // Report status of camera's raycast ability. It should be enabled by default.
        caCam.EnableRaycast = true;
/*
        // Make sure your scan range is valid.
        if(!caCam.CanScan(SCAN_DIST)){
            Echo("ERROR: Unable to scan to "+SCAN_DIST+" meters");
        }
*/
    }

    // Ship Controller will provide properties like ship mass, etc.
    controller = GridTerminalSystem.GetBlockWithName(CockpitName) as IMyShipController;

    // Only get thrusters that are part of the ships thruster group.
    ((IMyBlockGroup)GridTerminalSystem.GetBlockGroupWithName(THRUSTER_GROUP_NAME)).GetBlocksOfType<IMyThrust>(thrusters);

    // Calculate max braking thrust. 
    foreach (IMyThrust thruster in thrusters) {
        if (thruster.Orientation.Forward == Base6Directions.Direction.Forward) {
            maxBrakingThrust += thruster.MaxEffectiveThrust;
            brakingThrusters.Add(thruster);
        }
    }
}

public void Main(string argument, UpdateType updateSource)
{
    shipMass = controller.CalculateShipMass().PhysicalMass;
    speed = controller.GetShipSpeed();
    // Command the cameras to perform raycasts of SCAN_DIST meters at an orientation
    // of 0 degrees elevation and 0 degrees azimuth relative to camera boresight (cross hairs).
    double minDist = SCAN_DIST;
    sb.Clear();
    string nearestObstacle = "NONE";
    string obstacleRange = "N/A";
    string trackingCam = "N/A";
    foreach(IMyCameraBlock caCam in caCams){
        
        // Perform Raycast.
        MyDetectedEntityInfo entityInfo = caCam.Raycast(SCAN_DIST,0,0);

        // If an object is detected by this camera, then execute the following.
        if(!entityInfo.IsEmpty()){
            
            // Calculate distance between raycast intersection point with object and camera location.
            double range = Vector3D.Distance((Vector3D)entityInfo.HitPosition,caCam.GetPosition());
            if(caCam.CustomName.Contains("Port") || caCam.CustomName.Contains("Strbrd")){
                range -= sideCamBias;
            }else{
                range -= forwardCamBias;
            }

            // Track min distance to identify nearest obstacle.
            if(range < minDist){
                minDist = range;
                nearestObstacle = entityInfo.Type.ToString();
                obstacleRange = Convert.ToInt32(range).ToString();
                trackingCam = caCam.CustomName;
            }
            

            // Grab block object of LCD we want to write to.
            //IMyTextPanel textPanel = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("CollisionAvoidanceDisplay[LCD]");
            //textPanel.WriteText(sb.ToString());
            //textPanel.ShowPrivateTextOnScreen();
            //textPanel.ShowPublicTextOnScreen();
        }
    }
    
    
    sb.Append("Nearest Obstacle: ").AppendLine();
    sb.Append("Type: " + nearestObstacle).AppendLine();
    sb.Append("Detected By: " + trackingCam).AppendLine();
    sb.Append("At Range Of: " + obstacleRange + " m").AppendLine();
    sb.Append("Inertial Damping Engaged: " + controller.DampenersOverride).AppendLine();
    sb.Append("Stopping Distance: " + CalculateStopDistance(speed) + " m").AppendLine();
    sb.Append("Time to Stop: " + CalculateStopTime(speed) + " s").AppendLine();
    sb.Append("Max Braking Thrust: " + maxBrakingThrust + " N").AppendLine();

    // TODO: Need more logic to ensure that thrusters are enabled/disabled properly.
    // If obstacle is within danger zone, then we need to brake ASAP.
    if(minDist < SCAN_DIST && speed > 0.0){
        controller.DampenersOverride = true;
        currentBrakingThrust = 0;
        brakingThrusterStatus = "ENGAGED";
        foreach(IMyThrust thruster in thrusters){
           thruster.Enabled = true;
           if(thruster.Orientation.Forward == Base6Directions.Direction.Forward){
              currentBrakingThrust += thruster.CurrentThrust;
           }
           //thruster.SetValueFloat("Override", 0); //Accelerate. TODO: Increase to max after testing!!
        }

        sb.Append("Closing Speed: " + speed + " m/s").AppendLine();
        sb.Append("Braking Thrusters: " + brakingThrusterStatus);
        sb.Append("Current Braking Thrust: " + currentBrakingThrust + " N").AppendLine();
    }
    sb.Append("Scan Distance: " + SCAN_DIST + " m").AppendLine();
    // Report nearest obstacle if present.
    if(sb.Length != 0){
        Echo(sb.ToString());
    }
}

private double CalculateStopDistance(double speed){

    if(shipMass <= 0.0f){
       Echo("ERROR: Ship mass is reported as 0kg.");
       return MAX_DIST;
    }else if(maxBrakingThrust <= 0.0){
       Echo("ERROR: Max thrust is 0.0.");
       return MAX_DIST;
    }
   
    double deceleration = (maxBrakingThrust/shipMass);
    float decelerationRounded = (float)Math.Round(deceleration * 100f) / 100f;

    return (speed*speed)/(2*decelerationRounded);
}

private double CalculateStopTime(double speed){
    if(shipMass <= 0.0f){
       Echo("ERROR: Ship mass is reported as 0kg.");
       return MAX_DIST;
    }else if(maxBrakingThrust <= 0.0){
       Echo("ERROR: Max thrust is 0.0.");
       return MAX_DIST;
    }

    double deceleration = (maxBrakingThrust/shipMass);
    float decelerationRounded = (float)Math.Round(deceleration * 100f) / 100f;
    double stopDistance = (speed*speed)/(2*decelerationRounded);
    return Math.Sqrt((stopDistance*2)/decelerationRounded);
}

