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
    public float maxMotorTorque = 5200f;
    // Torque multiplier over speed / maxSpeed — firm launch, then taper
    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(0f, 0.85f),
        new Keyframe(0.2f, 1f),
        new Keyframe(0.7f, 0.7f),
        new Keyframe(1f, 0.2f));
    public float maxSpeedKmh = 70f;
    public float reverseTorque = 2400f;
    public float reverseMaxSpeedKmh = 12f;
    // How fast motor torque can climb / fall (Nm per second, per wheel)
    public float motorTorqueRise = 9000f;
    public float motorTorqueFall = 7000f;

    [Header("Brakes (per wheel)")]
    public float frontBrakeTorque = 4500f;
    public float rearBrakeTorque = 3200f;
    public float handbrakeTorque = 12000f;
    // Gentle drag when rolling with no pedal — natural coast-down
    public float coastBrakeTorque = 450f;
    public float autoHoldTorque = 2500f;
    public float autoHoldSpeed = 0.4f;
    public float brakeTorqueRise = 9000f;
    public float brakeTorqueFall = 12000f;

    [Header("Input smoothing")]
    // How fast pedal input ramps 0→1 (units per second)
    public float accelRise = 2.2f;
    public float accelFall = 1.6f;
    public float brakeRise = 1.4f;
    public float brakeFall = 2.2f;

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
