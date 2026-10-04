using System;

namespace BusDriver.Core.Data {
    // The shared enums of ROADMAP §4.8. Values are explicit and append-only: data assets store the
    // number, and save files store the name, so neither may ever be reused or renamed.
    public enum TellId : int {
        None = 0, HeadTrack = 10, Stillness = 11, EyesWide = 12, WhisperLean = 20, MouthWhisper = 21, JawStretch = 22,
        MimicFlicker = 30, MimicReveal = 31, TelegraphStand = 40, AngelWeep = 60, AngelReach = 61,
        DecoyNodOff = 50, DecoyPhoneGlow = 51, DecoyMutter = 52, DecoyFacingBackwards = 53, DecoyHoodUp = 54
    }
    public enum ReactionId : int { None = 0, Flinch = 1, LookAround = 2, TurnToCamera = 3 }
    public enum PassengerPose : int { Standing = 0, Seated = 1 }
    public enum DecoyKind : int { None = 0, NodOff = 1, PhoneGlow = 2, Mutter = 3, FacingBackwards = 4, HoodUp = 5 }
    public enum ThreatStage : int { Dormant = 0, Unsettled = 1, Aggressive = 2, Lethal = 3 }
    public enum ThreatCondition : int { Always = 0, Observed = 1, ObservedByCctv = 2, AttentionOnRoad = 3, PlayerOnFoot = 4 }
    [Flags] public enum ObserverKinds : int { None = 0, Cctv = 1, Driver = 2, OnFoot = 4, Mirror = 8 }
    public enum DeathCause : int { None = 0, MonsterKill = 1, SanityZero = 2, Fall = 3, Abandoned = 4 }
    public enum SanityTier : int { T0 = 0, T1 = 1, T2 = 2, T3 = 3, T4 = 4 }
    public enum ScareTier : int { Ambient = 0, Startle = 1, Monster = 2, Kill = 3 }
    public enum ScareStepKind : int {
        PlaySound = 1, ShowOverlay = 2, CameraShake = 3, FlickerCabinLights = 4, CctvStatic = 5, LockInput = 6,
        ForceHomeView = 7, CutToCctv = 8, ShowScareHead = 9, AllPassengersReact = 10, CabinLightsOff = 11,
        Blackout = 12, Wait = 13, HandsOverCamera = 14
    }
    public enum StopKind : int { Depot = 0, FarmGate = 1, GasStation = 2, Campground = 3, Church = 4, Clinic = 5, Trailhead = 6, Terminus = 7 }
    public enum SideProfile : int { Forest = 0, Rockface = 1, Drop = 2, CliffDrop = 3, Water = 4, TunnelWall = 5 }
    public enum BlockerKind : int { FallenTree = 0, FenceRoadClosed = 1, ConcreteBarriers = 2, CollapsedBridge = 3, Gate = 4 }
    public enum AudioGroup : int { Music = 0, Ambience = 1, SfxBus = 2, SfxCabin = 3, SfxWorld = 4, Voice = 5, Scares = 6, Ui = 7 }
    public enum AudioSnapshot : int { Default = 0, Earplugs = 1, Tunnel = 2, Blackout = 3 }
    public enum InputContext : int { None = 0, Menu = 1, Driving = 2, OnFoot = 3, Screen = 4, Cinematic = 5 }

    // Added with the save models (T-M1-03, D54)
    public enum ArrivalRating : int { None = 0, Early = 1, OnTime = 2, Late = 3, Missed = 4 }
    public enum ScareIntensity : int { Full = 0, Reduced = 1 }
    // A SoundDefinition's spatial blend (T-M1-09, D60)
    public enum SoundSpatial : int { TwoD = 0, ThreeD = 1 }
    // The dash's world-space screens (§4.14 BusViewBase.DashAnchor, D38; T-M1-14)
    public enum DashScreen : int { Clock = 0, FareBox = 1, Gps = 2, Mirror = 3 }
    // Mirrors UnityEngine.FullScreenMode's values; save models hold no Unity types (§4.9)
    public enum WindowMode : int { ExclusiveFullScreen = 0, FullScreenWindow = 1, MaximizedWindow = 2, Windowed = 3 }
    // A stop's record in the night (§2.4, T-M2-11). Served and Missed are final.
    public enum StopState : int { Pending = 0, Served = 1, Missed = 2 }

    // Route geometry (§3, RouteDefinition; T-M2-01, D71)
    public enum SegmentKind : int { Straight = 0, Arc = 1 }
    public enum RouteSide : int { Left = 0, Right = 1 }
    public enum RouteZoneKind : int { Tunnel = 0, Bridge = 1, Cliff = 2, RumbleStrip = 3 }
    // Where a zone lies across the road: the full width, or a strip along one edge
    public enum ZoneSpan : int { Across = 0, LeftEdge = 1, RightEdge = 2 }
    public enum SignKind : int { BridgeAhead = 0, NoGuardrailAhead = 1, SharpCurveRight = 2, Chevron = 3, TunnelAhead = 4, StopSign = 5, RoadClosed = 6 }
    public enum BlockerVariant : int { A = 0, B = 1 }

    // A look's greybox accessory (§4.8 PassengerLookDefinition; T-M3-01)
    public enum LookAccessory : int { None = 0, Cap = 1, Scarf = 2, Backpack = 3, Glasses = 4, LongCoat = 5 }
    // Seat zones (§2.6): Front = rows R1–R3, Mid = R4–R6, Rear = R7–R9; Any = no preference (T-M3-02)
    public enum SeatZone : int { Any = 0, Front = 1, Mid = 2, Rear = 3 }
    // A rider's place in the night (T-M3-02). Lost = walked away from a missed stop (T-M3-04).
    // Expelled = a monster the Salt charm threw out at the end of its telegraph (§2.14, T-M4-07).
    // Refused: turned away on the step, so they never paid a fare and never left a review (§2.4)
    public enum RiderStatus : int { Waiting = 0, Aboard = 1, Delivered = 2, Kicked = 3, Died = 4, Lost = 5, Expelled = 6, Refused = 7 }
    // A money event of the night (§2.7, T-M3-05). Lost = the refund when a rider is killed.
    public enum LedgerKind : int { Fare = 0, Tip = 1, Refund = 2, Lost = 3, Bounty = 4 }
    // How a kill sequence's telegraph is escaped (§2.14, D31, D47; MonsterDefinition.escape, T-M4-03)
    public enum EscapeKind : int { None = 0, ObserveFor = 1, UnobservedFor = 2 }
}
