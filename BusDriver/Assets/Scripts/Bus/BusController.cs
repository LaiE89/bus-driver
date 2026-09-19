using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BusController : MonoBehaviour {
    public enum Gear { Reverse = -1, Neutral = 0, Drive = 1 }

    [SerializeField] BusTuning tuning;
    // Left, right order per axle
    [SerializeField] WheelCollider[] frontWheels;
    [SerializeField] WheelCollider[] rearWheels;
    [SerializeField] Transform[] frontWheelVisuals;
    [SerializeField] Transform[] rearWheelVisuals;
    [SerializeField] Transform centerOfMass;

    public float ForwardSpeed { get; private set; }
    public float SpeedKmh { get { return Mathf.Abs(ForwardSpeed) * 3.6f; } }
    public Gear CurrentGear { get; private set; }
    public float SteerAngle { get; private set; }
    public bool IsParked { get; private set; }
    public bool IsGrounded { get; private set; }

    Rigidbody rb;
    float steerInput;
    float accelInput;
    bool handbrakeInput;
    float standstillTimer;
    Vector3[] frontLocalPos, rearLocalPos;
    Quaternion[] frontLocalRot, rearLocalRot;

    void Awake() {
        rb = GetComponent<Rigidbody>();
        CurrentGear = Gear.Neutral;
        frontLocalPos = new Vector3[frontWheels.Length];
        frontLocalRot = new Quaternion[frontWheels.Length];
        rearLocalPos = new Vector3[rearWheels.Length];
        rearLocalRot = new Quaternion[rearWheels.Length];
        ApplyTuning();
        CachePoses(frontWheels, frontLocalPos, frontLocalRot);
        CachePoses(rearWheels, rearLocalPos, rearLocalRot);
    }

    public void SetInput(float steer, float accel, bool handbrake) {
        steerInput = Mathf.Clamp(steer, -1f, 1f);
        accelInput = Mathf.Clamp(accel, -1f, 1f);
        handbrakeInput = handbrake;
    }

    // Full handbrake and no drive, for when the driver leaves the seat
    public void Park(bool parked) {
        IsParked = parked;
        if (parked) {
            CurrentGear = Gear.Neutral;
        }
    }

    public void ResetUpright() {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) {
            forward = Vector3.forward;
        }
        rb.position = rb.position + Vector3.up * 1f;
        rb.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        SteerAngle = 0f;
    }

    public void ApplyTuning() {
        rb.mass = tuning.mass;
        rb.linearDamping = tuning.linearDamping;
        rb.angularDamping = tuning.angularDamping;
        rb.solverIterations = tuning.solverIterations;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        // A sleeping body ignores motor torque changes
        rb.sleepThreshold = 0f;
        if (centerOfMass != null) {
            rb.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);
        }
        foreach (WheelCollider wheel in frontWheels) {
            ApplyWheelTuning(wheel, tuning.frontForwardStiffness, tuning.frontSidewaysStiffness);
        }
        foreach (WheelCollider wheel in rearWheels) {
            ApplyWheelTuning(wheel, tuning.rearForwardStiffness, tuning.rearSidewaysStiffness);
        }
    }

    void ApplyWheelTuning(WheelCollider wheel, float forwardStiffness, float sidewaysStiffness) {
        wheel.radius = tuning.wheelRadius;
        wheel.mass = tuning.wheelMass;
        wheel.wheelDampingRate = tuning.wheelDampingRate;
        wheel.suspensionDistance = tuning.suspensionDistance;
        wheel.forceAppPointDistance = tuning.forceAppPointDistance;

        JointSpring spring = wheel.suspensionSpring;
        spring.spring = tuning.suspensionSpring;
        spring.damper = tuning.suspensionDamper;
        spring.targetPosition = tuning.suspensionTarget;
        wheel.suspensionSpring = spring;

        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.extremumSlip = 0.4f;
        forward.extremumValue = 1f;
        forward.asymptoteSlip = 0.8f;
        forward.asymptoteValue = 0.6f;
        forward.stiffness = forwardStiffness;
        wheel.forwardFriction = forward;

        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.extremumSlip = 0.25f;
        sideways.extremumValue = 1f;
        sideways.asymptoteSlip = 0.5f;
        sideways.asymptoteValue = 0.75f;
        sideways.stiffness = sidewaysStiffness;
        wheel.sidewaysFriction = sideways;

        wheel.ConfigureVehicleSubsteps(tuning.substepSpeedThreshold, tuning.substepsBelowThreshold, tuning.substepsAboveThreshold);
    }

    void FixedUpdate() {
        ForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float absSpeed = Mathf.Abs(ForwardSpeed);
        if (absSpeed < tuning.standstillSpeed) {
            standstillTimer += Time.fixedDeltaTime;
        }else {
            standstillTimer = 0f;
        }

        float throttle = 0f;
        float brake = 0f;
        float accel = IsParked ? 0f : accelInput;

        // Automatic gears: S brakes to a stop and then reverses, W while reversing brakes first
        if (accel > 0.01f) {
            if (CurrentGear == Gear.Reverse && ForwardSpeed < -tuning.standstillSpeed) {
                brake = accel;
            }else {
                CurrentGear = Gear.Drive;
                throttle = accel;
            }
        }else if (accel < -0.01f) {
            if (CurrentGear == Gear.Reverse) {
                throttle = -accel;
            }else if (ForwardSpeed <= tuning.standstillSpeed && standstillTimer >= tuning.reverseDelay) {
                CurrentGear = Gear.Reverse;
                throttle = -accel;
            }else {
                brake = -accel;
            }
        }

        ApplyDrive(throttle, absSpeed);
        ApplyBrakes(throttle, brake, absSpeed);
        ApplySteering();
        IsGrounded = false;
        ApplyAntiRoll(frontWheels, tuning.frontAntiRoll);
        ApplyAntiRoll(rearWheels, tuning.rearAntiRoll);

        CachePoses(frontWheels, frontLocalPos, frontLocalRot);
        CachePoses(rearWheels, rearLocalPos, rearLocalRot);
    }

    void ApplyDrive(float throttle, float absSpeed) {
        float motor = 0f;
        if (throttle > 0f) {
            if (CurrentGear == Gear.Drive) {
                float maxSpeed = tuning.maxSpeedKmh / 3.6f;
                motor = throttle * tuning.maxMotorTorque * tuning.torqueCurve.Evaluate(Mathf.Clamp01(absSpeed / maxSpeed));
                if (ForwardSpeed >= maxSpeed) {
                    motor = 0f;
                }
            }else {
                float maxSpeed = tuning.reverseMaxSpeedKmh / 3.6f;
                motor = -throttle * tuning.reverseTorque * (1f - Mathf.Clamp01(absSpeed / maxSpeed));
            }
        }
        // Wheels can stick when motor and brake torque are both exactly zero
        if (motor == 0f) {
            motor = 0.0001f;
        }
        foreach (WheelCollider wheel in rearWheels) {
            wheel.motorTorque = motor;
        }
    }

    void ApplyBrakes(float throttle, float brake, float absSpeed) {
        float front = brake * tuning.frontBrakeTorque;
        float rear = brake * tuning.rearBrakeTorque;
        if (throttle <= 0f && brake <= 0f) {
            // Engine braking while rolling, auto-hold at a standstill so the bus never creeps
            float hold = absSpeed < tuning.autoHoldSpeed ? tuning.autoHoldTorque : tuning.coastBrakeTorque;
            front = hold;
            rear = hold;
        }
        if (handbrakeInput || IsParked) {
            rear = Mathf.Max(rear, tuning.handbrakeTorque);
        }
        foreach (WheelCollider wheel in frontWheels) {
            wheel.brakeTorque = front;
        }
        foreach (WheelCollider wheel in rearWheels) {
            wheel.brakeTorque = rear;
        }
    }

    void ApplySteering() {
        float speedFactor = Mathf.InverseLerp(tuning.steerLowKmh, tuning.steerHighKmh, SpeedKmh);
        float maxAngle = Mathf.Lerp(tuning.lowSpeedSteerAngle, tuning.highSpeedSteerAngle, speedFactor);
        float target = (IsParked ? 0f : steerInput) * maxAngle;
        bool returning = Mathf.Abs(target) < Mathf.Abs(SteerAngle) || Mathf.Sign(target) != Mathf.Sign(SteerAngle);
        float rate = returning ? tuning.steerReturnRate : tuning.steerRate;
        SteerAngle = Mathf.MoveTowards(SteerAngle, target, rate * Time.fixedDeltaTime);
        foreach (WheelCollider wheel in frontWheels) {
            wheel.steerAngle = SteerAngle;
        }
    }

    // Transfers load from the compressed side of an axle to the extended side to limit body roll
    void ApplyAntiRoll(WheelCollider[] axle, float antiRoll) {
        if (axle.Length < 2) {
            return;
        }
        WheelCollider left = axle[0];
        WheelCollider right = axle[1];
        bool groundedLeft = left.GetGroundHit(out WheelHit hitLeft);
        bool groundedRight = right.GetGroundHit(out WheelHit hitRight);
        IsGrounded |= groundedLeft || groundedRight;

        float travelLeft = groundedLeft ? SuspensionTravel(left, hitLeft) : 1f;
        float travelRight = groundedRight ? SuspensionTravel(right, hitRight) : 1f;
        float force = (travelLeft - travelRight) * antiRoll;
        if (groundedLeft) {
            rb.AddForceAtPosition(left.transform.up * -force, left.transform.position);
        }
        if (groundedRight) {
            rb.AddForceAtPosition(right.transform.up * force, right.transform.position);
        }
    }

    // 0 = fully compressed, 1 = fully extended
    float SuspensionTravel(WheelCollider wheel, WheelHit hit) {
        return (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius) / wheel.suspensionDistance;
    }

    void CachePoses(WheelCollider[] wheels, Vector3[] localPos, Quaternion[] localRot) {
        for (int i = 0; i < wheels.Length; i++) {
            wheels[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
            localPos[i] = transform.InverseTransformPoint(pos);
            localRot[i] = Quaternion.Inverse(transform.rotation) * rot;
        }
    }

    // Poses are cached relative to the body in FixedUpdate and applied against the
    // interpolated transform here, otherwise the wheels jitter against the bus
    void Update() {
        ApplyPoses(frontWheelVisuals, frontLocalPos, frontLocalRot);
        ApplyPoses(rearWheelVisuals, rearLocalPos, rearLocalRot);
    }

    void ApplyPoses(Transform[] visuals, Vector3[] localPos, Quaternion[] localRot) {
        for (int i = 0; i < visuals.Length && i < localPos.Length; i++) {
            if (visuals[i] == null) {
                continue;
            }
            visuals[i].SetPositionAndRotation(transform.TransformPoint(localPos[i]), transform.rotation * localRot[i]);
        }
    }
}
