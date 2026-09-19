using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Play mode smoke test for the MVP scene, meant for batch mode:
//   Unity -batchmode -projectPath BusDriver -executeMethod BusSmokeTest.Run -logFile -
// (no -quit, the test exits the editor itself). Drives the bus through BusController.SetInput,
// logs "[SMOKE]" lines, writes camera captures to Logs/smoke and exits 0 on pass, 1 on fail.
[InitializeOnLoad]
public static class BusSmokeTest {
    const string RunningKey = "BusSmokeTest.Running";
    const string ShotFolder = "Logs/smoke";

    static BusController bus;
    static CCTVSystem cctv;
    static Rigidbody rb;
    static float startTime;
    static double realStart;
    static int phase;
    static float startHeading;
    static float timeTo50 = -1f;
    static float stoppedAt = -1f;
    static int nextSpeedLog;
    static int errorCount;
    static int cyclesDone;
    static float nextCycleTime;
    static string pendingCapture;
    static Camera pendingCamera;
    static bool midCaptureDone;

    // Passenger phases
    static PlayerModeController mode;
    static BusCabin cabin;
    static BusDoors doors;
    static OnFootController onFoot;
    static BusStop stop;
    static StaringMonster monster;
    static float phaseStart;
    static bool stepDone;
    static bool stepDone2;
    static int ridersBefore;
    static int waitingBefore;
    static int freeSeatsBefore;
    static int boardedEvents;
    static int seatedEvents;
    static int leftEvents;
    static bool kickedWasMonster;
    static bool leftWasKicked;
    static Vector3 markPosition;
    static Quaternion markRotation;
    static Vector3 markTensor;
    static float peakSpeed;
    static readonly List<string> failures = new List<string>();

    // Entering play mode reloads the domain, so the hooks are re-attached from here
    static BusSmokeTest() {
        if (SessionState.GetBool(RunningKey, false)) {
            Attach();
        }
    }

    [MenuItem("Tools/Bus Driver/Run Smoke Test")]
    public static void Run() {
        SessionState.SetBool(RunningKey, true);
        EditorSceneManager.OpenScene(BusDriverSceneBuilder.ScenePath);
        Attach();
        EditorApplication.EnterPlaymode();
    }

    static void Attach() {
        realStart = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    static void OnLog(string message, string stackTrace, LogType type) {
        if (!EditorApplication.isPlaying || stackTrace.Contains("UnityEditor.Search")) {
            return;
        }
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) {
            errorCount++;
        }
    }

    static void Tick() {
        try {
            if (EditorApplication.timeSinceStartup - realStart > 400) {
                failures.Add("timed out");
                Finish();
                return;
            }
            if (!EditorApplication.isPlaying) {
                return;
            }
            Step();
        }catch (Exception e) {
            Debug.LogException(e);
            failures.Add("exception in test: " + e.Message);
            Finish();
        }
    }

    static void Step() {
        if (bus == null) {
            // Let PlayerModeController.Start run before taking the controls away from it
            if (Time.timeSinceLevelLoad < 0.3f) {
                return;
            }
            bus = UnityEngine.Object.FindAnyObjectByType<BusController>();
            cctv = UnityEngine.Object.FindAnyObjectByType<CCTVSystem>();
            rb = bus.GetComponent<Rigidbody>();
            // The mode controller stays live so seat and door transitions run the real code.
            // Only the keyboard is taken away from the bus.
            bus.GetComponent<BusInput>().ExternalControl = true;
            mode = UnityEngine.Object.FindAnyObjectByType<PlayerModeController>();
            cabin = bus.GetComponent<BusCabin>();
            doors = bus.GetComponent<BusDoors>();
            monster = UnityEngine.Object.FindAnyObjectByType<StaringMonster>();
            foreach (BusStop candidate in UnityEngine.Object.FindObjectsByType<BusStop>()) {
                if (stop == null || candidate.WaitingCount > stop.WaitingCount) {
                    stop = candidate;
                }
            }
            cabin.OnPassengerBoarded += p => boardedEvents++;
            cabin.OnPassengerSeated += p => seatedEvents++;
            cabin.OnPassengerKicked += p => kickedWasMonster = p is Monster;
            cabin.OnPassengerLeft += p => { leftEvents++; leftWasKicked = p.WasKicked; };
            startTime = Time.time;
            Log("started");
        }

        float t = Time.time - startTime;
        // Seconds into the current phase, the passenger phases are timed relative to their start
        float pt = Time.time - phaseStart;
        switch (phase) {
            case 0: // settle
                bus.SetInput(0f, 0f, false);
                if (t >= 3f) {
                    float upDot = Vector3.Dot(bus.transform.up, Vector3.up);
                    Log($"settled: y={bus.transform.position.y:F3} upDot={upDot:F4} speed={rb.linearVelocity.magnitude:F3} m/s");
                    foreach (WheelCollider wheel in bus.GetComponentsInChildren<WheelCollider>()) {
                        bool grounded = wheel.GetGroundHit(out WheelHit hit);
                        float travel = grounded ? (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius) / wheel.suspensionDistance : -1f;
                        Log($"  {wheel.name}: grounded={grounded} extension={travel:F2} (0 compressed, 1 extended) load={hit.force:F0} N");
                        Check(grounded, wheel.name + " is not grounded after settling");
                    }
                    Check(upDot > 0.98f, "bus is not upright after settling");
                    Check(rb.linearVelocity.magnitude < 0.2f, "bus is moving with no input");
                    Check(Mathf.Abs(bus.transform.position.y) < 0.15f, "bus ride height is off");
                    Capture("1_driver_view", BusCamera("DriverCamera"));
                    Next();
                }
                break;

            case 1: // full throttle
                bus.SetInput(0f, 1f, false);
                if (timeTo50 < 0f && bus.SpeedKmh >= 50f) {
                    timeTo50 = t - 3f;
                }
                if (!midCaptureDone && t >= 9f) {
                    midCaptureDone = true;
                    Capture("2_driver_view_accelerating", BusCamera("DriverCamera"));
                    Check(!mode.TryLeaveSeat(), "left the seat while the bus was moving");
                    Check(!doors.TryOpen(), "opened the doors while the bus was moving");
                }
                if (t - 3f >= nextSpeedLog) {
                    Log($"  throttle t={nextSpeedLog}s speed={bus.SpeedKmh:F1} km/h");
                    nextSpeedLog += 2;
                }
                if (t >= 19f) {
                    Log($"after 16 s of throttle: {bus.SpeedKmh:F1} km/h, 0-50 in {(timeTo50 < 0f ? "n/a" : timeTo50.ToString("F1") + " s")}, gear {bus.CurrentGear}");
                    Check(bus.SpeedKmh > 35f, "bus is too slow under full throttle");
                    Check(bus.SpeedKmh < 90f, "bus exceeded its speed cap");
                    Check(bus.CurrentGear == BusController.Gear.Drive, "gear should be Drive");
                    startHeading = bus.transform.eulerAngles.y;
                    Capture("2_driver_view_at_speed", BusCamera("DriverCamera"));
                    Next();
                }
                break;

            case 2: // hard right at speed
                bus.SetInput(1f, 0.3f, false);
                if (t >= 22f) {
                    float turned = Mathf.DeltaAngle(startHeading, bus.transform.eulerAngles.y);
                    float upDot = Vector3.Dot(bus.transform.up, Vector3.up);
                    Log($"after 3 s of full right lock: turned {turned:F1} deg, upDot={upDot:F3}, steer={bus.SteerAngle:F1}, speed={bus.SpeedKmh:F1} km/h");
                    Check(turned > 8f, "bus did not turn right");
                    Check(upDot > 0.9f, "bus rolled over while steering");
                    Next();
                }
                break;

            case 3: // brake to a stop, then reverse
                bus.SetInput(0f, -1f, false);
                if (stoppedAt < 0f && bus.SpeedKmh < 1f) {
                    stoppedAt = t - 22f;
                    Log($"stopped after {stoppedAt:F1} s of braking");
                }
                if (t >= 34f) {
                    Log($"holding S: gear {bus.CurrentGear}, forward speed {bus.ForwardSpeed:F2} m/s");
                    Check(stoppedAt > 0f, "bus never stopped under braking");
                    Check(bus.CurrentGear == BusController.Gear.Reverse, "gear should be Reverse after holding brake at a standstill");
                    Check(bus.ForwardSpeed < -0.5f, "bus is not reversing");
                    Check(bus.SpeedKmh < 20f, "reverse speed cap exceeded");
                    Next();
                }
                break;

            case 4: // W while reversing brakes first, then drives
                bus.SetInput(0f, 1f, false);
                if (t >= 38f) {
                    Log($"W after reversing: gear {bus.CurrentGear}, forward speed {bus.ForwardSpeed:F2} m/s");
                    Check(bus.CurrentGear == BusController.Gear.Drive, "gear should return to Drive");
                    Check(bus.ForwardSpeed > 0.5f, "bus is not moving forward again");
                    Next();
                }
                break;

            case 5: // release, auto-hold
                bus.SetInput(0f, 0f, false);
                if (t >= 40f) {
                    Log($"released: speed {bus.SpeedKmh:F2} km/h");
                    Next();
                }
                break;

            case 6: // park at the stop with the door lined up
                if (!stepDone) {
                    stepDone = true;
                    Rigidbody body = rb;
                    Vector3 pose = stop.transform.TransformPoint(new Vector3(2f, 0.05f, -4.8f));
                    bus.transform.SetPositionAndRotation(pose, stop.transform.rotation);
                    body.position = pose;
                    body.rotation = stop.transform.rotation;
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                }
                bus.SetInput(0f, 0f, false);
                if (pt >= 2f && bus.IsStopped) {
                    ridersBefore = cabin.Passengers.Count;
                    waitingBefore = stop.WaitingCount;
                    freeSeatsBefore = cabin.FreeSeatCount;
                    Log($"at the stop: {ridersBefore} aboard, {waitingBefore} waiting, {freeSeatsBefore} free seats, in zone {stop.Contains(cabin)}");
                    Check(stop.Contains(cabin), "bus door is not inside the stop zone");
                    Check(cabin.CurrentStop == stop, "cabin does not report the stop it is parked at");
                    Check(monster != null && monster.State == PassengerState.Waiting, "test monster is not waiting at the stop");
                    Check(doors.TryOpen(), "doors refused to open at a complete stop");
                    Next();
                }
                break;

            case 7: // closing the doors mid-boarding sends the stragglers back to the stop
                bus.SetInput(0f, 0f, false);
                if (!stepDone && pt >= 0.9f) {
                    stepDone = true;
                    int walking = 0;
                    foreach (Passenger waiter in stop.GetComponentsInChildren<Passenger>()) {
                        if (waiter.State == PassengerState.Boarding) {
                            walking++;
                        }
                    }
                    Log($"closing the doors with {walking} passenger(s) on their way over");
                    Check(walking > 0, "nobody had started boarding yet");
                    Check(doors.TryClose(), "doors refused to close");
                }
                if (stepDone && ((doors.IsClosed && stop.WaitingCount == waitingBefore) || pt >= 12f)) {
                    Log($"after the abort: {stop.WaitingCount} waiting again, {cabin.Passengers.Count} aboard, {cabin.FreeSeatCount} free seats, parked {bus.IsParked}");
                    Check(stop.WaitingCount == waitingBefore, "stragglers did not go back to waiting");
                    Check(cabin.Passengers.Count == ridersBefore && cabin.FreeSeatCount == freeSeatsBefore, "an aborted boarder kept a seat or a place on the bus");
                    Check(doors.IsClosed && !bus.IsParked, "doors did not finish closing");
                    Check(doors.TryOpen(), "doors refused to reopen");
                    Next();
                }
                break;

            case 8: // boarding, and the bus must not move with the doors open
                if (pt >= 1f && pt < 3f) {
                    if (!stepDone) {
                        stepDone = true;
                        markPosition = bus.transform.position;
                    }
                    bus.SetInput(0f, 1f, false);
                }else {
                    bus.SetInput(0f, 0f, false);
                }
                if (pt >= 3f && !stepDone2) {
                    stepDone2 = true;
                    float moved = (bus.transform.position - markPosition).magnitude;
                    Log($"full throttle with doors open: moved {moved:F3} m, gear {bus.CurrentGear}, parked {bus.IsParked}");
                    Check(moved < 0.05f, "bus drove off with the doors open");
                    Check(bus.CurrentGear == BusController.Gear.Neutral, "gear should be Neutral while the doors are open");
                }
                bool everyoneSeated = cabin.Passengers.Count == ridersBefore + waitingBefore;
                foreach (Passenger rider in cabin.Passengers) {
                    everyoneSeated &= rider.State == PassengerState.Seated;
                }
                if (pt >= 3f && (everyoneSeated || pt >= 30f)) {
                    Log($"boarding took {pt:F1} s: {cabin.Passengers.Count} aboard, {stop.WaitingCount} still waiting, events boarded {boardedEvents} seated {seatedEvents}");
                    Check(everyoneSeated, "not everyone boarded and sat down in time");
                    Check(stop.WaitingCount == 0, "passengers left behind at the stop");
                    Check(boardedEvents == waitingBefore && seatedEvents == waitingBefore, "boarded/seated events do not match the number of boarders");
                    Check(cabin.FreeSeatCount == freeSeatsBefore - waitingBefore, "free seat count is wrong after boarding");
                    foreach (Passenger rider in cabin.Passengers) {
                        Check(rider.transform.IsChildOf(bus.transform), rider.name + " is not parented to the bus");
                        Check(rider.Seat != null && rider.Seat.Occupant == rider, rider.name + " does not own its seat");
                    }
                    Check(monster.IsAboard && monster is Monster && monster is Passenger, "monster is not aboard as a passenger");
                    // Front camera: every seat is ahead of it, inside the monster's neck limits
                    cctv.Cycle();
                    Next();
                }
                break;

            case 9: // the test monster stares at the active camera
                bus.SetInput(0f, 0f, false);
                if (pt >= 5f) {
                    Transform head = monster.transform.Find("Head");
                    Camera watcher = cctv.ActiveCamera;
                    float angle = Vector3.Angle(head.forward, watcher.transform.position - head.position);
                    Log($"monster head is {angle:F1} deg off '{watcher.name}'");
                    Check(angle < 15f, "monster is not staring at the active camera");
                    Capture("5_cctv_monster_staring", watcher);
                    cctv.ShowHome();
                    Check(doors.TryClose(), "doors refused to close");
                    Next();
                }
                break;

            case 10: // leave the seat and walk
                if (!stepDone) {
                    if (!doors.IsClosed && pt < 10f) {
                        break;
                    }
                    stepDone = true;
                    Check(doors.IsClosed, "doors never closed");
                    Check(!bus.IsParked, "drive lock still held after the doors closed");
                    markPosition = bus.transform.position;
                    markRotation = bus.transform.rotation;
                    markTensor = rb.inertiaTensor;
                    Check(mode.TryLeaveSeat(), "could not leave the seat at a complete stop");
                    onFoot = UnityEngine.Object.FindAnyObjectByType<OnFootController>();
                    onFoot.ExternalControl = true;
                    Check(mode.Mode == PlayerMode.OnFoot, "mode is not OnFoot");
                    Check(bus.IsFrozen && rb.isKinematic && bus.IsParked, "bus is not frozen and parked while on foot");
                    Check(cabin.IsWalkable, "cabin is not walkable");
                    Check(EnabledCameras() == 1 && mode.OnFootCamera.enabled, "on-foot camera is not the only enabled camera");
                    Check(UnityEngine.Object.FindObjectsByType<AudioListener>().Length == 1, "there must be exactly one AudioListener on foot");
                    phaseStart = Time.time;
                    break;
                }
                // The rig faces the back of the bus, so its left is the door side
                if (pt < 1f) {
                    onFoot.ExternalMove = new Vector2(-1f, 0f);
                }else if (pt < 1.4f) {
                    if (!stepDone2) {
                        stepDone2 = true;
                        Vector3 atDoor = bus.transform.InverseTransformPoint(onFoot.transform.position);
                        Log($"walked into the doorway: bus-local {atDoor}");
                        Check(atDoor.x < 1f, "player walked out through the doorway");
                        Check(atDoor.x > 0.5f, "player could not move sideways, the hull is probably pushing them");
                        Check(Mathf.Abs(atDoor.y - 0.55f) < 0.15f, "player is not standing on the bus floor");
                    }
                    onFoot.ExternalMove = new Vector2(1f, 0f);
                }else if (pt < 4.4f) {
                    onFoot.ExternalMove = new Vector2(0f, 1f);
                }else {
                    onFoot.ExternalMove = Vector2.zero;
                    Vector3 inAisle = bus.transform.InverseTransformPoint(onFoot.transform.position);
                    Log($"walked down the aisle: bus-local {inAisle}");
                    Check(inAisle.z < 2.5f, "player did not get down the aisle");
                    Check(Mathf.Abs(inAisle.x) < 0.45f && Mathf.Abs(inAisle.y - 0.55f) < 0.15f, "player left the aisle or the floor");
                    Capture("6_on_foot_aisle", mode.OnFootCamera);
                    Next();
                }
                break;

            case 11: // kick the monster off, through the same call the interactor makes
                if (!stepDone) {
                    // Stand in the aisle a step ahead of the monster's row, facing it
                    if (!stepDone2) {
                        stepDone2 = true;
                        Vector3 seatLocal = bus.transform.InverseTransformPoint(monster.transform.position);
                        Vector3 standLocal = new Vector3(0f, 0.6f, seatLocal.z + 0.9f);
                        Vector3 toMonster = bus.transform.TransformDirection(new Vector3(seatLocal.x, 0f, -0.9f));
                        onFoot.gameObject.SetActive(false);
                        onFoot.Place(bus.transform.TransformPoint(standLocal), Quaternion.LookRotation(toMonster).eulerAngles.y);
                        onFoot.gameObject.SetActive(true);
                        phaseStart = Time.time;
                        break;
                    }
                    if (pt < 0.3f) {
                        break;
                    }
                    stepDone = true;
                    stepDone2 = false;
                    phaseStart = Time.time;
                    PlayerInteractor interactor = onFoot.GetComponent<PlayerInteractor>();
                    IInteractable target = interactor.Current;
                    Log($"looking at: {(target as Component != null ? ((Component)target).name : "nothing")}, prompt '{interactor.CurrentPrompt}'");
                    Check(ReferenceEquals(target, monster), "the interaction ray did not pick up the passenger being looked at");
                    Check(interactor.CurrentPrompt == "Kick out", "seated passenger is not offering to be kicked");
                    target = monster;
                    target.Interact();
                    Check(monster.State == PassengerState.Leaving && monster.WasKicked, "monster did not start leaving");
                    Check(kickedWasMonster, "kicked event did not carry a Monster");
                }
                if (pt >= 2f && !stepDone2) {
                    stepDone2 = true;
                    Check(doors.IsOpenWanted, "doors did not open by themselves for the kicked passenger");
                    Capture("7_on_foot_kicked_walking", mode.OnFootCamera);
                }
                if ((monster == null && doors.IsClosed) || pt >= 30f) {
                    Log($"kicked passenger gone after {pt:F1} s: {cabin.Passengers.Count} aboard, left events {leftEvents}");
                    Check(monster == null, "kicked passenger never left");
                    Check(leftEvents == 1 && leftWasKicked, "left event missing or not flagged as kicked");
                    Check(cabin.Passengers.Count == ridersBefore + waitingBefore - 1, "passenger list did not shrink");
                    Check(cabin.FreeSeatCount == freeSeatsBefore - waitingBefore + 1, "seat was not freed");
                    Check(doors.IsClosed, "doors did not close again after the passenger left");
                    Next();
                }
                break;

            case 12: // back in the seat, the bus must come back to life without a jolt
                if (!stepDone) {
                    stepDone = true;
                    Check(mode.TrySitDown(), "could not sit back down");
                    Check(mode.Mode == PlayerMode.Driving && !bus.IsFrozen && !rb.isKinematic, "bus did not unfreeze");
                    Check(!cabin.IsWalkable && !bus.IsParked, "cabin still walkable or bus still locked after sitting down");
                    Check(EnabledCameras() == 1 && BusCamera("DriverCamera").enabled, "driver camera is not the only enabled camera");
                    float tensorDrift = (rb.inertiaTensor - markTensor).magnitude / markTensor.magnitude;
                    Log($"inertia tensor drift after walking: {tensorDrift:P3}");
                    Check(tensorDrift < 0.001f, "inertia tensor changed across the on-foot period");
                    peakSpeed = 0f;
                }
                if (pt < 1f) {
                    bus.SetInput(0f, 0f, false);
                    peakSpeed = Mathf.Max(peakSpeed, rb.linearVelocity.magnitude);
                }else if (!stepDone2) {
                    stepDone2 = true;
                    float moved = (bus.transform.position - markPosition).magnitude;
                    float turned = Quaternion.Angle(bus.transform.rotation, markRotation);
                    Log($"after unfreezing: moved {moved * 100f:F2} cm, turned {turned:F3} deg, peak speed {peakSpeed:F3} m/s");
                    Check(moved < 0.01f && turned < 0.1f, "bus pose shifted across the on-foot period");
                    Check(peakSpeed < 0.1f, "bus jolted when it unfroze");
                }else if (pt < 5f) {
                    bus.SetInput(0f, 1f, false);
                }else {
                    float upDot = Vector3.Dot(bus.transform.up, Vector3.up);
                    Log($"driving off: {bus.SpeedKmh:F1} km/h, gear {bus.CurrentGear}, upDot {upDot:F3}");
                    Check(bus.SpeedKmh > 10f && bus.CurrentGear == BusController.Gear.Drive && upDot > 0.98f, "bus does not drive normally after the on-foot period");
                    bus.SetInput(0f, 0f, false);
                    Next();
                }
                break;

            case 13: // one camera switch per 0.4 s so each capture sees a settled frame
                if (t < nextCycleTime) {
                    break;
                }
                nextCycleTime = t + 0.4f;
                if (pendingCapture != null) {
                    Capture(pendingCapture, pendingCamera);
                    pendingCapture = null;
                    break;
                }
                if (cyclesDone < cctv.ViewCount) {
                    CycleOnce();
                    break;
                }
                Check(!cctv.IsViewingCCTV, "cycling through every camera should end on the driver view");
                Check(UnityEngine.Object.FindObjectsByType<AudioListener>().Length == 1, "there must be exactly one AudioListener");
                Capture("4_driver_view_end", BusCamera("DriverCamera"));
                Log($"errors logged during play: {errorCount}");
                Check(errorCount == 0, "errors or exceptions were logged during play");
                Finish();
                break;
        }
    }

    static Camera BusCamera(string cameraName) {
        foreach (Camera cam in bus.GetComponentsInChildren<Camera>(true)) {
            if (cam.name == cameraName) {
                return cam;
            }
        }
        return null;
    }

    static int EnabledCameras() {
        int enabled = 0;
        foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>()) {
            if (cam.enabled) {
                enabled++;
            }
        }
        return enabled;
    }

    static void CycleOnce() {
        cctv.Cycle();
        cyclesDone++;
        int enabled = 0;
        string active = "";
        foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>()) {
            if (cam.enabled) {
                enabled++;
                active = cam.name;
            }
        }
        Log($"cycle {cyclesDone}: index {cctv.ActiveIndex}, label '{cctv.ActiveLabel}', enabled camera '{active}'");
        Check(enabled == 1, $"{enabled} cameras enabled after cycle {cyclesDone}");
        if (cctv.IsViewingCCTV) {
            pendingCapture = "3_cctv_" + (cctv.ActiveIndex + 1);
            pendingCamera = cctv.ActiveCamera;
        }
    }

    static void Capture(string fileName, Camera target) {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || target == null) {
            return;
        }
        Log($"capture {fileName}: cam pos {target.transform.position} forward {target.transform.forward} enabled {target.enabled}");
        RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        // StandardRequest goes through the full camera stack path. SingleCameraRequest skips the
        // volume framework update, so the CCTV grade would be missing from the capture.
        RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = rt };
        if (!RenderPipeline.SupportsRenderRequest(target, request)) {
            Log("render requests unsupported, skipping capture");
            return;
        }
        RenderPipeline.SubmitRenderRequest(target, request);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;

        Directory.CreateDirectory(ShotFolder);
        File.WriteAllBytes(Path.Combine(ShotFolder, fileName + ".png"), image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        rt.Release();
    }

    static void Next() {
        phase++;
        phaseStart = Time.time;
        stepDone = false;
        stepDone2 = false;
    }

    static void Check(bool condition, string failure) {
        if (!condition) {
            failures.Add(failure);
            Log("FAIL: " + failure);
        }
    }

    static void Log(string message) {
        Debug.Log("[SMOKE] " + message);
    }

    static void Finish() {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SessionState.SetBool(RunningKey, false);
        Log(failures.Count == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + string.Join("; ", failures) + ")");
        if (Application.isBatchMode) {
            EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
        }else {
            EditorApplication.ExitPlaymode();
        }
    }
}
