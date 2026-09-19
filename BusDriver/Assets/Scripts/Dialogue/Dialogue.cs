using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Dialogue {
    public string name;
    public bool isCenter;
    public AudioSource audioSource;

    [TextArea(3, 10)]
    public string[] sentences;

    // Routes to the objectives controller instead of the dialogue controller
    public bool isObjective;
}
