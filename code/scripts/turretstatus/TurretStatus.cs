/**
 * This script is used to monitor defensive turrets connected to the grid and
 * report their statuses to an LCD. In addition to this, it is used to trigger
 * the ship-wide alert when threats are detected.
 */
private List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();
private List<IMyInteriorLight> whiteLights = new List<IMyInteriorLight>();
private List<IMyInteriorLight> redOutLights = new List<IMyInteriorLight>();
private IMySoundBlock warningSoundBlock;
private IMySoundBlock alarmSoundBlock;
private IMyTimerBlock warningTimer;

private string TURRET_GROUP_NAME = "Turrets(LOLCOW)";
private string DISPLAY_NAME = "TurretStatusMain(C2Center)LCD";
private string WHITE_LIGHT_GROUP_NAME = "InteriorLights";
private string RED_LIGHT_GROUP_NAME = "RedOutLights";
private string WARNING_SOUND_BLOCK_NAME = "SoundBlockWarning";
private string ALARM_SOUND_BLOCK_NAME = "SoundBlockAlarm";
private string WARNING_TIMER_BLOCK_NAME = "TimerBlockWarning";
private string VOICE_WARNING = "A077CMBT";
private string VOICE_WEAPONS_READY = "A079CMBT";

private bool alarmTriggered = false;
private bool manuallyTriggered = false;

readonly MyCommandLine _commandLine = new MyCommandLine();

private StringBuilder sb = new StringBuilder(); // StringBuilder object for output.
IMyTextPanel textPanel;

public Program(){
    // Register this script's Main method to be called (updated) every 10 game ticks.
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    // Grab the Block Group that includes all accessible turrets. Do all of this here in the ctor
    // so we don't allocate new memory every time the main script executes.
    ((IMyBlockGroup)GridTerminalSystem.GetBlockGroupWithName(TURRET_GROUP_NAME)).GetBlocksOfType<IMyLargeTurretBase>(turrets);

    // Get Block Groups for the red and white interior lights.
    ((IMyBlockGroup)GridTerminalSystem.GetBlockGroupWithName(WHITE_LIGHT_GROUP_NAME)).GetBlocksOfType<IMyInteriorLight>(whiteLights);
    ((IMyBlockGroup)GridTerminalSystem.GetBlockGroupWithName(RED_LIGHT_GROUP_NAME)).GetBlocksOfType<IMyInteriorLight>(redOutLights);

    warningSoundBlock = GridTerminalSystem.GetBlockWithName(WARNING_SOUND_BLOCK_NAME) as IMySoundBlock;
    alarmSoundBlock = GridTerminalSystem.GetBlockWithName(ALARM_SOUND_BLOCK_NAME) as IMySoundBlock;

    warningSoundBlock.SelectedSound = VOICE_WARNING;
    
    warningTimer = GridTerminalSystem.GetBlockWithName(WARNING_TIMER_BLOCK_NAME) as IMyTimerBlock;
    // Grab text panel that output will be directed to.
    textPanel = (IMyTextPanel)GridTerminalSystem.GetBlockWithName(DISPLAY_NAME);
}

public void Main(string argument, UpdateType updateSource){

    if(_commandLine.TryParse(argument)){
        HandleArguments();
    }

    sb.Clear();
    sb.Append("                        Turret Status").AppendLine();

    // Loop through the list of turrets and obtain the desired status info.
    int i=1;
    bool threatDetected = false;
    foreach(IMyLargeTurretBase turret in turrets){
       // Obtain turret status and add to string builder for output to LCD.
       sb.Append("[" + i++ + ".] " + turret.CustomName + " :").AppendLine();
       sb.Append("  Active:             " + HandleActiveStatus(turret.Enabled)).AppendLine();
       sb.Append("  AI Targeting:       " + HandleActiveStatus(turret.AIEnabled)).AppendLine();
       sb.Append("  Has Target:         " + HandleTargetStatus(turret.HasTarget)).AppendLine();
       sb.Append("  Manually Overriden: " + HandleTargetStatus(turret.IsUnderControl)).AppendLine();
       if(turret.HasTarget && turret.GetTargetedEntity().Relationship == MyRelationsBetweenPlayerAndBlock.Enemies && !threatDetected){
          threatDetected = true;
       }
    }

    textPanel.WriteText(sb.ToString());
    Echo("Threat Detected:      " + threatDetected);
    Echo("Alarm Triggered:      " + alarmTriggered);
    Echo("Manually Triggered: " + manuallyTriggered);
    Echo("Warning Code: " + warningSoundBlock.SelectedSound);
    
    // Threat has been detected and alarm is off, then turn it on.
    if(threatDetected && !alarmTriggered){
       ActivateAlarm();
       alarmTriggered = true;
    // No threat detected, but alarm is on. 
    }else if(!threatDetected && alarmTriggered){
       // If it was not manually triggered, then turn it off.
       // If it was manually triggered, let it stay on.
       if(!manuallyTriggered){
          DeactivateAlarm();
          alarmTriggered = false;
       }
    }

}

private string HandleActiveStatus(bool status){
   switch(status){
      case true:
          return "ACTIVE";
      default:
      case false:
          return "INACTIVE";
   }
}

private string HandleTargetStatus(bool status){
   switch(status){
      case true:
          return "YES";
      default:
      case false:
          return "NO";
   }
}

void ActivateAlarm(){
   alarmSoundBlock.Play();
   warningSoundBlock.SelectedSound = VOICE_WARNING;
   warningSoundBlock.Play();
   warningTimer.StartCountdown();
   warningSoundBlock.SelectedSound = VOICE_WEAPONS_READY;
   
   foreach(IMyInteriorLight light in whiteLights){
      light.Enabled = false;
   }
   foreach(IMyInteriorLight light in redOutLights){
      light.Enabled = true;
   }
}

void DeactivateAlarm(){
   foreach(IMyInteriorLight light in whiteLights){
      light.Enabled = true;
   }
   foreach(IMyInteriorLight light in redOutLights){
      light.Enabled = false;
   }
}

void HandleArguments(){
    
    int argCount = _commandLine.ArgumentCount;

    if(argCount != 1){
        return;
    }

    // ToLowerInvariant() converts string to lower case.
    if(_commandLine.Argument(0).ToLowerInvariant() == "toggle"){
        if(manuallyTriggered && alarmTriggered){
           Echo("Deactivating Alarm.");
           DeactivateAlarm();
           alarmTriggered = false;
           manuallyTriggered = false;
           warningSoundBlock.SelectedSound = VOICE_WARNING;
        }else if(!manuallyTriggered && !alarmTriggered){
           Echo("Activating Alarm.");
           ActivateAlarm();
           alarmTriggered = true;
           manuallyTriggered = true;
        }
    }else if(_commandLine.Argument(0).ToLowerInvariant() == "trigger"){
        Echo("Activating Alarm.");
        ActivateAlarm();
        alarmTriggered = true;
        manuallyTriggered = true;
    }else if(_commandLine.Argument(0).ToLowerInvariant() == "reset"){
        Echo("Deactivating Alarm.");
        DeactivateAlarm();
        alarmTriggered = false;
        manuallyTriggered = false;
    }else{
        Echo("WARNING: Argument [" + _commandLine.Argument(0).ToLowerInvariant() + "] not recognized!");
    }

    return;
}

