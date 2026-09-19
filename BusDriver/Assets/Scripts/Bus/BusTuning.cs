using UnityEngine;

// Every handling number for the bus lives here rather than on the scene object,
// because the MVP scene is regenerated wholesale by BusDriverSceneBuilder.
// Edits made to this asset during Play mode persist.
[CreateAssetMenu(fileName = "BusTuning", menuName = "Bus Driver/Bus Tuning")]
public class BusTuning : ScriptableObject {
    [Header("Rigidbody")]
    public float mass = 11000f;
    public float linearDamping = 0.03f;
    public float angularDamping = 0.3f;
    public int solverIterations = 12;

    [Header("Wheels")]
    public float wheelRadius = 0.5f;
    public float wheelMass = 120f;
    public float wheelDampingRate = 1f;
    public float forceAppPointDistance = 0.4f;

    [Header("Suspension")]
    public float suspensionDistance = 0.3f;
    public float suspensionSpring = 200000f;
    public float suspensionDamper = 20000f;
    [Range(0f, 1f)] public float suspensionTarget = 0.5f;

    [Header("Friction")]
    public float frontForwardStiffness = 1.2f;
    public float rearForwardStiffness = 1.4f;
    public float frontSidewaysStiffness = 1.3f;
    // Grippier rear keeps the bus understeering instead of spinning
    public float rearSidewaysStiffness = 1.6f;

    [Header("Drive (per rear wheel)")]
    public float maxMotorTorque = 4500f;
    // Torque multiplier over speed / maxSpeed
    public AnimationCurve torqueCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.8f), new Keyframe(1f, 0.2f));
    public float maxSpeedKmh = 80f;
    public float reverseTorque = 2500f;
    public float reverseMaxSpeedKmh = 15f;

    [Header("Brakes (per wheel)")]
    public float frontBrakeTorque = 7000f;
    public float rearBrakeTorque = 5000f;
    public float handbrakeTorque = 15000f;
    public float coastBrakeTorque = 150f;
    public float autoHoldTorque = 3000f;
    public float autoHoldSpeed = 0.3f;

    [Header("Gears")]
    // m/s under which the bus counts as stopped
    public float standstillSpeed = 0.5f;
    // Seconds stopped with brake held before reverse engages
    public float reverseDelay = 0.3f;

    [Header("Steering")]
    public float lowSpeedSteerAngle = 38f;
    public float highSpeedSteerAngle = 8f;
    public float steerLowKmh = 5f;
    public float steerHighKmh = 60f;
    public float steerRate = 35f;
    public float steerReturnRate = 60f;

    [Header("Anti-roll")]
    public float frontAntiRoll = 40000f;
    public float rearAntiRoll = 60000f;

    [Header("Substeps")]
    public float substepSpeedThreshold = 5f;
    public int substepsBelowThreshold = 12;
    public int substepsAboveThreshold = 15;
}
