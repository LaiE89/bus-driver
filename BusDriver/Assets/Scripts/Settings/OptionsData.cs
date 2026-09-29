using UnityEngine;
using System.Runtime.Serialization;

[DataContract]
public class OptionsData {

    [DataMember]
    public float sens;
    [DataMember]
    public int qualityIndex;
    [DataMember]
    public float volume;
    [DataMember]
    public int resolutionIndex;
    [DataMember]
    public bool isFullscreen;
    [DataMember]
    public float brightness;
    [DataMember]
    public int targetFPSIndex;
    [DataMember]
    public KeyCode switchCameraKey;
    [DataMember]
    public KeyCode handbrakeKey;
    [DataMember]
    public KeyCode doorsKey;
    [DataMember]
    public KeyCode leaveSeatKey;
    [DataMember]
    public KeyCode interactKey;

    public OptionsData() {
        sens = OptionsMenu.sens;
        qualityIndex = OptionsMenu.qualityIndex;
        volume = OptionsMenu.volume;
        resolutionIndex = OptionsMenu.resolutionIndex;
        isFullscreen = OptionsMenu.isFullscreen;
        brightness = OptionsMenu.brightness;
        targetFPSIndex = OptionsMenu.targetFPSIndex;
        switchCameraKey = ControlsMenu.switchCameraKey;
        handbrakeKey = GameKeys.handbrake;
        doorsKey = GameKeys.doors;
        leaveSeatKey = GameKeys.leaveSeat;
        interactKey = GameKeys.interact;
    }
}
