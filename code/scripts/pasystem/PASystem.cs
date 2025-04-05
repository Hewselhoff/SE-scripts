//TODO: Implement config-api for configuration of range finder.

private IMySoundBlock soundBlock;
private IMyTimerBlock playDelayTimer;

private string SOUND_BLOCK_NAME = "SoundBlockPA";
private string TIMER_BLOCK_NAME = "TimerBlockPA";

//private string soundCode = "A077CMBT";
private List<string> soundList = new List<string>();

readonly MyCommandLine _commandLine = new MyCommandLine();

public Program(){
    soundBlock = GridTerminalSystem.GetBlockWithName(SOUND_BLOCK_NAME) as IMySoundBlock;
    soundBlock.GetSounds(soundList);
    playDelayTimer = GridTerminalSystem.GetBlockWithName(TIMER_BLOCK_NAME) as IMyTimerBlock;
    foreach(string sound in soundList){
       Echo(sound);
    }
}

public void Main(string argument, UpdateType updateSource){

    if(_commandLine.TryParse(argument)){
        HandleArguments();
    }
}

void HandleArguments(){

    int argCount = _commandLine.ArgumentCount;

    if(argCount != 1){
        Echo("WARNING: No argument provided!");
        return;
    }
    
    PlaySound(_commandLine.Argument(0));

    return;
}

void SetSound(string soundCode){
   soundBlock.SelectedSound = soundCode;
}

void PlaySound(string soundCode){
   SetSound(soundCode);
   playDelayTimer.StartCountdown();
}

