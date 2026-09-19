using System;
using System.Collections.Generic;
using UnityEngine;

// The bus will not drive while the doors are open. They open for the driver (F) or
// for anyone holding them (a passenger who was told to leave), and only ever while
// the bus is at a complete stop. One passenger at a time gets the doorway.
public class BusDoors : MonoBehaviour {
    [SerializeField] BusController bus;
    [SerializeField] Transform panel;
    [SerializeField] Vector3 closedLocalPos;
    [SerializeField] Vector3 openLocalPos;
    [SerializeField] float slideTime = 0.6f;

    public bool IsOpenWanted { get { return driverOpen || holds.Count > 0; } }
    public bool IsFullyOpen { get { return openAmount >= 1f; } }
    public bool IsClosed { get { return openAmount <= 0f && !lockHeld; } }
    public bool CanToggle { get { return bus.IsStopped; } }
    // true when they start to open, false once fully closed
    public event Action<bool> OnChanged;

    readonly HashSet<object> holds = new HashSet<object>();
    bool driverOpen;
    bool lockHeld;
    float openAmount;
    Passenger doorwayUser;

    public bool TryOpen() {
        if (!bus.IsStopped) {
            return false;
        }
        driverOpen = true;
        return true;
    }

    public bool TryClose() {
        driverOpen = false;
        return true;
    }

    public bool TryToggle() {
        return driverOpen ? TryClose() : TryOpen();
    }

    // Keeps the doors open (opening them once the bus has stopped) until released
    public void Hold(object who) {
        holds.Add(who);
    }

    public void Release(object who) {
        holds.Remove(who);
    }

    // The doorway token. Refused while the doors are moving or about to close.
    public bool TryEnter(Passenger passenger) {
        if (!IsFullyOpen || !IsOpenWanted) {
            return false;
        }
        if (doorwayUser != null && doorwayUser != passenger) {
            return false;
        }
        doorwayUser = passenger;
        return true;
    }

    public void Exit(Passenger passenger) {
        if (doorwayUser == passenger) {
            doorwayUser = null;
        }
    }

    void Update() {
        if (IsOpenWanted && !lockHeld && bus.IsStopped) {
            lockHeld = true;
            bus.SetDriveLock(DriveLock.DoorsOpen, true);
            OnChanged?.Invoke(true);
        }

        // Closing is deferred rather than refused, whoever is in the doorway finishes first
        bool open = lockHeld && (IsOpenWanted || doorwayUser != null);
        openAmount = Mathf.MoveTowards(openAmount, open ? 1f : 0f, Time.deltaTime / slideTime);
        if (panel != null) {
            panel.localPosition = Vector3.Lerp(closedLocalPos, openLocalPos, Mathf.SmoothStep(0f, 1f, openAmount));
        }

        if (lockHeld && !open && openAmount <= 0f) {
            lockHeld = false;
            bus.SetDriveLock(DriveLock.DoorsOpen, false);
            OnChanged?.Invoke(false);
        }
    }
}
