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
    static string pendingCamera;
    static bool midCaptureDone;
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
            if (EditorApplication.timeSinceStartup - realStart > 180) {
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
            UnityEngine.Object.FindAnyObjectByType<PlayerModeController>().enabled = false;
            bus.GetComponent<BusInput>().enabled = false;
            bus.Park(false);
            startTime = Time.time;
            Log("started");
        }

        float t = Time.time - startTime;
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
                    Capture("1_driver_view", "DriverCamera");
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
                    Capture("2_driver_view_accelerating", "DriverCamera");
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
                    Capture("2_driver_view_at_speed", "DriverCamera");
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

            case 6: // one camera switch per 0.4 s so each capture sees a settled frame
                if (t < nextCycleTime) {
                    break;
                }
                nextCycleTime = t + 0.4f;
                if (pendingCapture != null) {
                    Capture(pendingCapture, pendingCamera);
                    pendingCapture = null;
                    break;
                }
                if (cyclesDone < CameraCount()) {
                    CycleOnce();
                    break;
                }
                Check(!cctv.IsViewingCCTV, "cycling through every camera should end on the driver view");
                Check(UnityEngine.Object.FindObjectsByType<AudioListener>().Length == 1, "there must be exactly one AudioListener");
                Capture("4_driver_view_end", "DriverCamera");
                Log($"errors logged during play: {errorCount}");
                Check(errorCount == 0, "errors or exceptions were logged during play");
                Finish();
                break;
        }
    }

    static int CameraCount() {
        return bus.GetComponentsInChildren<Camera>(true).Length;
    }

    static void CycleOnce() {
        cctv.Cycle();
        cyclesDone++;
        int enabled = 0;
        string active = "";
        foreach (Camera cam in bus.GetComponentsInChildren<Camera>(true)) {
            if (cam.enabled) {
                enabled++;
                active = cam.name;
            }
        }
        Log($"cycle {cyclesDone}: index {cctv.ActiveIndex}, label '{cctv.ActiveLabel}', enabled camera '{active}'");
        Check(enabled == 1, $"{enabled} cameras enabled after cycle {cyclesDone}");
        if (cctv.IsViewingCCTV) {
            pendingCapture = "3_cctv_" + (cctv.ActiveIndex + 1);
            pendingCamera = active;
        }
    }

    static void Capture(string fileName, string cameraName) {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) {
            return;
        }
        Camera target = null;
        foreach (Camera cam in bus.GetComponentsInChildren<Camera>(true)) {
            if (cam.name == cameraName) {
                target = cam;
            }
        }
        if (target == null) {
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
