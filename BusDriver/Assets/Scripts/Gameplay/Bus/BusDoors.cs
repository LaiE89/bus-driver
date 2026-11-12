using System;
using System.Collections.Generic;
using UnityEngine;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Views;

namespace BusDriver.Gameplay.Bus {
    // The bus will not drive while the doors are open. They open for the driver (Q) or
    // for anyone holding them (a passenger who was told to leave), and only ever while
    // the bus is at a complete stop. One passenger at a time gets the doorway.
    public class BusDoors : MonoBehaviour {
        [SerializeField] BusController bus;
        [Tooltip("The door panel moves through the view (§4.14)")]
        [SerializeField] BusViewBase view;
        [SerializeField] float slideTime = 0.6f;

        public bool IsOpenWanted { get { return driverOpen || holds.Count > 0; } }
        // The driver asked for them. A passenger holding them (a kick) does not count.
        public bool OpenedByDriver { get { return driverOpen; } }
        public bool IsFullyOpen { get { return openAmount >= 1f; } }
        public bool IsClosed { get { return openAmount <= 0f && !lockHeld; } }
        public bool CanToggle { get { return bus != null && bus.IsStopped; } }
        // 0 closed … 1 open, linear in time
        public float OpenAmount { get { return openAmount; } }
        // true when they start to open, false once fully closed
        public event Action<bool> OnChanged;
        // The driver asked for the doors, including when a passenger was already holding them open
        public event Action OnDriverOpened;
        // Every time the panels reach fully open (a stop is Served on the first, §2.4)
        public event Action OnFullyOpened;

        readonly HashSet<object> holds = new HashSet<object>();
        IAudioService audio;
        bool driverOpen;
        bool lockHeld;
        float openAmount;
        // 1 while the panels are travelling open, -1 while they are travelling shut
        int motion;
        Passenger doorwayUser;

        public void Init(ShiftServices shift) {
            audio = shift != null && shift.Game != null ? shift.Game.Audio : null;
        }

        public bool TryOpen() {
            if (!bus.IsStopped) {
                return false;
            }
            bool already = driverOpen;
            driverOpen = true;
            if (!already && OnDriverOpened != null) {
                OnDriverOpened();
            }
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
            bool wasFullyOpen = openAmount >= 1f;
            float before = openAmount;
            openAmount = Mathf.MoveTowards(openAmount, open ? 1f : 0f, Time.deltaTime / slideTime);
            PlayDoorCue(before);
            if (!wasFullyOpen && openAmount >= 1f && OnFullyOpened != null) {
                OnFullyOpened();
            }
            if (view != null) {
                view.SetDoorOpen(openAmount);
            }

            if (lockHeld && !open && openAmount <= 0f) {
                lockHeld = false;
                bus.SetDriveLock(DriveLock.DoorsOpen, false);
                OnChanged?.Invoke(false);
            }
        }

        // One shot when the panels start opening, and one when they start closing, including a
        // reverse part-way through. A door that is already fully open or shut stays quiet.
        void PlayDoorCue(float before) {
            if (openAmount > before) {
                if (motion == 1) {
                    return;
                }
                motion = 1;
                PlayDoor(SoundIds.BusDoorOpen);
            }else if (openAmount < before) {
                if (motion == -1) {
                    return;
                }
                motion = -1;
                PlayDoor(SoundIds.BusDoorClose);
            }
        }

        void PlayDoor(string id) {
            if (audio != null) {
                audio.PlayAttached(id, transform);
            }
        }
    }
}
