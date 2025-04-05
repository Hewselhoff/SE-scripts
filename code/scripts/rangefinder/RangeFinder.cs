//TODO: Implement config-api for configuration of range finder.

// Range Finder Display should be an inverted holo lcd placed two small blocks infront of the camera.
private double SCAN_DIST = 100000; // Set your desired scan distance (detection range) for your camera in meters.
private string RANGE_FINDER_CAM_NAME = "RngFinderCamera(LOLCOW)";
private string OUTPUT_DISPLAY_NAME = "RngFinderDisplay(LOLCOW)";
private IMyCameraBlock rangeFinderCam;
private IMyTextPanel rangeDataDisplay;
private StringBuilder sb = new StringBuilder(); // StringBuilder object for output.

public Program(){
    // Register this script's Main method to be Once.
    Runtime.UpdateFrequency = UpdateFrequency.Once;
    rangeFinderCam = (IMyCameraBlock)GridTerminalSystem.GetBlockWithName(RANGE_FINDER_CAM_NAME);
    rangeDataDisplay = (IMyTextPanel)GridTerminalSystem.GetBlockWithName(OUTPUT_DISPLAY_NAME);
    rangeFinderCam.EnableRaycast = true;
    // Report max scan distance in details section of Programmable Block terminal.
    Echo("Max Scan Range: " + SCAN_DIST + " meters");
}

public void Main(string argument, UpdateType updateSource){
    // Perform Raycast.
    MyDetectedEntityInfo entityInfo = rangeFinderCam.Raycast(SCAN_DIST,0,0);
    string targetRange = "N/A";
    string target = "NONE";
    // If an object is detected by this camera, then execute the following.
    if(!entityInfo.IsEmpty()){
        // Calculate distance between raycast intersection point with object and camera location.
        targetRange = Convert.ToInt32(Vector3D.Distance((Vector3D)entityInfo.HitPosition,rangeFinderCam.GetPosition())).ToString();
        target = entityInfo.Type.ToString();
    }
    PadText(6, ref sb);
    sb.Append("Target Type: " + target).AppendLine();
    sb.Append("At Range Of: " + targetRange + " meters").AppendLine();
    AddReticle(ref sb);

    DisplayRangeData(sb);
    sb.Clear();
}

// Display range data on the display.
public void DisplayRangeData(StringBuilder sbout){
    rangeDataDisplay.WriteText("");
    rangeDataDisplay.WriteText(sbout.ToString());
}

// Add specified number of newlines to string builder text.
public void PadText(int numLines, ref StringBuilder sb){
    for(int i=0; i <= numLines; i++){
        sb.Append("").AppendLine();
    }
}

// Make a camera reticle on the holo display since the actual
// camera cross hairs don't appear for some reason.
public void AddReticle(ref StringBuilder sb){
    PadText(1, ref sb);
    sb.Append("                                                 |").AppendLine();
    sb.Append("                                                 |").AppendLine();
}    

